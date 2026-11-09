using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Boca de una terminal del medidor (zapata o conector de neutro). Su posición es la boca y su eje Z la
/// dirección en que entra el cable. Acepta cualquier punta pelada que se le acerque sostenida; sin apretar
/// el tornillo el cable se sale al jalarlo, y ya apretado se queda acomodado.
/// Una terminal puede recibir varios cables bajo el mismo tornillo (el conector de neutro): para meter otro
/// hay que aflojar el tornillo, y los cables que ya estaban bien apretados se quedan en su lugar.
/// </summary>
public class MeterTerminal : MonoBehaviour
{
    public enum TerminalState
    {
        Empty,
        Inserted,
        Tightened
    }

    [System.Serializable]
    public class WireRoute
    {
        [Tooltip("Puntas que siguen esta ruta.")]
        public WireStripper[] wires = new WireStripper[0];
        [Tooltip("Lugar dentro de la boca, de 0 a capacidad − 1, repartidos a lo largo del eje X de la terminal.")]
        public int slot;
        [Tooltip("Puntos por donde se acomoda el cable al apretar el tornillo, en orden desde la boca.")]
        public Transform[] points = new Transform[0];

        public bool Uses(WireStripper wire) => wires != null && System.Array.IndexOf(wires, wire) >= 0;
    }

    /// <summary>Un cable metido en la boca.</summary>
    class Seat
    {
        public WireTip tip;
        public int slot;
        /// <summary>Apretado y correcto: ya no se sale ni se suelta al aflojar para meter otro cable.</summary>
        public bool secured;
        public int pullLink = -1;
        public float pullBaseline;
        public readonly List<Vector3> dressTargets = new List<Vector3>();
        public readonly List<Vector3> dressStarts = new List<Vector3>();
        public float dressStartTime;

        public WireStripper Wire => tip.Stripper;
    }

    public static readonly List<MeterTerminal> All = new List<MeterTerminal>();
    /// <summary>Se entró una punta a una terminal (correcta o no).</summary>
    public static event System.Action<MeterTerminal, WireStripper> WireInserted;
    /// <summary>Una punta sin apretar se salió al jalarla.</summary>
    public static event System.Action<MeterTerminal, WireStripper> WirePulledOut;

    [Tooltip("Nombre para los avisos, por ejemplo 'la terminal de línea'.")]
    public string label;
    public TerminalScrew screw;
    [Tooltip("Puntas que van en esta terminal. Cualquier otra también entra, pero es una conexión incorrecta.")]
    public WireStripper[] accepts;

    [Header("Entrada")]
    [Tooltip("Cuántos cables caben bajo el tornillo.")]
    [Min(1)] public int capacity = 1;
    [Tooltip("Separación entre los cables dentro de la boca, a lo largo del eje X de la terminal.")]
    public float slotSpacing = 0.005f;
    [Tooltip("Distancia máxima del extremo de la punta a la boca para meterla.")]
    public float acceptRadius = 0.03f;
    [Tooltip("Ángulo máximo entre la punta y la dirección de entrada.")]
    public float maxAngle = 60f;
    [Tooltip("Eslabones desde la punta que cuentan como sostener el cable cerca de ella.")]
    public int nearLinks = 12;
    [Tooltip("Vueltas del tornillo a partir de las cuales la boca está cerrada y el cable no entra.")]
    public float closedTurns = 0.5f;
    [Tooltip("Cuánto hay que estirar el cable al jalarlo para que se salga si el tornillo no está apretado.")]
    public float pullOutDistance = 0.04f;

    [Header("Acomodo")]
    [Tooltip("Ruta por defecto por donde se acomoda el cable al apretar el tornillo, en orden desde la boca.")]
    public Transform[] dressPoints;
    [Tooltip("Rutas propias de algunos cables; los demás usan la ruta por defecto.")]
    public WireRoute[] routes = new WireRoute[0];
    public float dressSeconds = 0.4f;
    [Tooltip("Eslabones máximos entre la punta y el resto fijo del cable para acomodar todo el tramo hasta ahí.")]
    public int maxDressLinks = 60;

    [Header("Eventos")]
    public UnityEvent OnInserted;
    public UnityEvent OnTightened;
    public UnityEvent OnLoosened;
    public UnityEvent OnRemoved;
    public UnityEvent OnWrongWire;

    /// <summary>Lo activa el primer paso de conexión, para que no entren cables antes.</summary>
    public bool Accepting { get; set; }
    public TerminalState State { get; private set; }
    public int Count => seats.Count;
    public bool HasRoom => seats.Count < capacity;
    /// <summary>La boca está cerrada por el tornillo y no entra otro cable.</summary>
    public bool IsClosed => screw.Turns >= closedTurns;
    public float LastUnstrippedTime { get; private set; } = float.NegativeInfinity;
    public float LastClosedTime { get; private set; } = float.NegativeInfinity;

    static WireStripper[] tips;

    readonly List<Seat> seats = new List<Seat>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (tips == null || tips.Length == 0 || tips[0] == null)
            tips = FindObjectsByType<WireStripper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    public bool Accepts(WireStripper wire) => System.Array.IndexOf(accepts, wire) >= 0;

    /// <summary>Cables metidos en esta terminal.</summary>
    public IEnumerable<WireStripper> Wires
    {
        get
        {
            foreach (var seat in seats) yield return seat.Wire;
        }
    }

    /// <summary>Cables metidos que todavía se pueden sacar (no quedaron apretados en su lugar correcto).</summary>
    public IEnumerable<WireStripper> LooseWires
    {
        get
        {
            foreach (var seat in seats)
                if (!seat.secured) yield return seat.Wire;
        }
    }

    public bool Contains(WireStripper wire) => FindSeat(wire) != null;

    /// <summary>Terminal donde está metida esa punta, o null.</summary>
    public static MeterTerminal Holding(WireStripper wire)
    {
        foreach (var terminal in All)
            if (terminal.Contains(wire)) return terminal;
        return null;
    }

    void Update()
    {
        if (Accepting && HasRoom) TryInsert();
        if (seats.Count == 0) return;

        if (State == TerminalState.Inserted && screw.IsTight) Tighten();
        else if (State == TerminalState.Tightened && !screw.IsTight) Loosen();

        if (State != TerminalState.Inserted) return;
        for (int i = seats.Count - 1; i >= 0; i--)
            if (!seats[i].secured && IsPulledOut(seats[i])) Remove(seats[i], true);
    }

    void FixedUpdate()
    {
        foreach (var seat in seats) HoldInPlace(seat);
    }

    void LateUpdate()
    {
        // The aligner is off while seated, so the tip points into the terminal.
        foreach (var seat in seats) seat.Wire.transform.rotation = Quaternion.LookRotation(transform.forward);
    }

    void TryInsert()
    {
        foreach (var stripper in tips)
        {
            if (stripper == null || !stripper.gameObject.activeInHierarchy || Holding(stripper) != null) continue;

            var tip = WireTip.For(stripper);
            if (tip.HeldLink(nearLinks) < 0) continue;
            if (Vector3.Distance(tip.End, transform.position) > acceptRadius) continue;
            if (Vector3.Angle(tip.Forward, transform.forward) > maxAngle) continue;
            if (CloserTerminal(tip.End)) continue;

            if (!stripper.IsStripped)
            {
                LastUnstrippedTime = Time.time;
                continue;
            }
            if (IsClosed)
            {
                LastClosedTime = Time.time;
                continue;
            }

            Insert(tip);
            return;
        }
    }

    // Terminal mouths can sit close together; the tip goes into the nearest one with room.
    bool CloserTerminal(Vector3 point)
    {
        float distance = Vector3.Distance(point, transform.position);
        foreach (var other in All)
            if (other != this && other.Accepting && other.HasRoom &&
                Vector3.Distance(point, other.transform.position) < distance)
                return true;
        return false;
    }

    void Insert(WireTip tip)
    {
        var seat = new Seat { tip = tip, slot = FreeSlot(tip.Stripper) };
        seats.Add(seat);
        State = TerminalState.Inserted;
        if (tip.Aligner != null) tip.Aligner.enabled = false;
        HoldInPlace(seat);

        OnInserted.Invoke();
        if (!Accepts(seat.Wire)) OnWrongWire.Invoke();
        WireInserted?.Invoke(this, seat.Wire);
    }

    /// <summary>El lugar de la ruta del cable si está libre; si no, el primero libre.</summary>
    int FreeSlot(WireStripper wire)
    {
        var route = RouteFor(wire);
        if (route != null && route.slot >= 0 && route.slot < capacity && !SlotTaken(route.slot)) return route.slot;
        for (int slot = 0; slot < capacity; slot++)
            if (!SlotTaken(slot)) return slot;
        return 0;
    }

    bool SlotTaken(int slot)
    {
        foreach (var seat in seats)
            if (seat.slot == slot) return true;
        return false;
    }

    WireRoute RouteFor(WireStripper wire)
    {
        if (routes == null) return null;
        foreach (var route in routes)
            if (route != null && route.Uses(wire)) return route;
        return null;
    }

    Vector3 SlotPosition(int slot) => transform.position + transform.right * ((slot - (capacity - 1) * 0.5f) * slotSpacing);

    void Tighten()
    {
        State = TerminalState.Tightened;
        bool allCorrect = true;
        foreach (var seat in seats)
        {
            bool correct = Accepts(seat.Wire);
            allCorrect &= correct;
            if (seat.secured) continue;
            seat.secured = correct;
            BuildDress(seat);
        }
        // A full, correct terminal can't be loosened by accident; otherwise it must open again for the next
        // wire, or to fix a wrong one.
        screw.Locked = allCorrect && seats.Count >= capacity;
        OnTightened.Invoke();
    }

    void Loosen()
    {
        State = TerminalState.Inserted;
        foreach (var seat in seats)
        {
            if (seat.secured) continue;
            for (int i = 1; i <= seat.dressTargets.Count; i++) seat.tip.Unpin(i);
            seat.dressTargets.Clear();
            seat.pullLink = -1;
        }
        OnLoosened.Invoke();
    }

    void Remove(Seat seat, bool pulled)
    {
        var tip = seat.tip;
        for (int i = 1; i <= seat.dressTargets.Count; i++) tip.Unpin(i);
        seat.dressTargets.Clear();
        tip.Unpin(0);
        if (tip.Aligner != null) tip.Aligner.enabled = true;

        seats.Remove(seat);
        if (seats.Count == 0) State = TerminalState.Empty;
        OnRemoved.Invoke();
        if (pulled) WirePulledOut?.Invoke(this, tip.Stripper);
    }

    Seat FindSeat(WireStripper wire)
    {
        foreach (var seat in seats)
            if (seat.Wire == wire) return seat;
        return null;
    }

    /// <summary>
    /// Sin apretar, la punta se sale si una mano estira el cable cerca de la terminal. El estiramiento se
    /// mide contra el mínimo desde que se agarró, porque al asentarse la punta el cable ya queda algo tenso.
    /// </summary>
    bool IsPulledOut(Seat seat)
    {
        int held = seat.tip.HeldLink(nearLinks);
        if (held < 1)
        {
            seat.pullLink = -1;
            return false;
        }

        float stretch = Vector3.Distance(seat.tip.Links[held].position, SlotPosition(seat.slot)) - held * seat.tip.Spacing;
        if (held != seat.pullLink)
        {
            seat.pullLink = held;
            seat.pullBaseline = stretch;
        }
        seat.pullBaseline = Mathf.Min(seat.pullBaseline, stretch);
        return stretch - seat.pullBaseline > pullOutDistance;
    }

    void HoldInPlace(Seat seat)
    {
        Vector3 mouth = SlotPosition(seat.slot);
        seat.tip.Pin(0, mouth, transform.rotation);

        float t = dressSeconds > 0f ? Mathf.Clamp01((Time.time - seat.dressStartTime) / dressSeconds) : 1f;
        for (int i = 0; i < seat.dressTargets.Count; i++)
        {
            Vector3 position = Vector3.Lerp(seat.dressStarts[i], seat.dressTargets[i], Mathf.SmoothStep(0f, 1f, t));
            Vector3 previous = i == 0 ? mouth : seat.dressTargets[i - 1];
            Vector3 direction = seat.dressTargets[i] - previous;
            seat.tip.Pin(i + 1, position, direction.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(direction) : Quaternion.identity);
        }
    }

    /// <summary>
    /// Reparte los eslabones cercanos a la punta sobre la ruta de acomodo. Si el resto del cable está fijo
    /// cerca (en el ducto o en otra terminal), todo el tramo libre se reparte hasta ese punto, para que no
    /// quede un bucle colgando aunque sobre cable.
    /// </summary>
    void BuildDress(Seat seat)
    {
        seat.dressTargets.Clear();
        seat.dressStarts.Clear();
        seat.dressStartTime = Time.time;

        var route = RouteFor(seat.Wire);
        var points = route != null && route.points != null && route.points.Length > 0 ? route.points : dressPoints;
        if (points == null || points.Length == 0) return;

        var path = new List<Vector3> { SlotPosition(seat.slot) };
        foreach (var point in points)
            if (point != null) path.Add(point.position);

        var tip = seat.tip;
        int count = tip.Links.Length - 1;
        float spacing = tip.Spacing;
        int fixedLink = FixedLink(tip);
        if (fixedLink > 0)
        {
            path.Add(tip.Links[fixedLink].position);
            count = fixedLink - 1;
        }
        Smooth(path);
        if (fixedLink > 0) spacing = RouteLength(path) / fixedLink;

        float along = spacing;
        for (int i = 1; i < path.Count && seat.dressTargets.Count < count; )
        {
            Vector3 from = path[i - 1];
            float length = Vector3.Distance(from, path[i]);
            if (along > length)
            {
                along -= length;
                path[i - 1] = path[i];
                i++;
                continue;
            }

            Vector3 target = Vector3.MoveTowards(from, path[i], along);
            seat.dressTargets.Add(target);
            seat.dressStarts.Add(tip.Links[seat.dressTargets.Count].position);
            path[i - 1] = target;
            along = spacing;
        }
    }

    // Rounds the route's corners so the wire bends instead of kinking; its ends stay in place.
    static void Smooth(List<Vector3> route)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var smooth = new List<Vector3> { route[0] };
            for (int i = 1; i < route.Count; i++)
            {
                if (i > 1) smooth.Add(Vector3.Lerp(route[i - 1], route[i], 0.25f));
                if (i < route.Count - 1) smooth.Add(Vector3.Lerp(route[i - 1], route[i], 0.75f));
            }
            smooth.Add(route[route.Count - 1]);
            route.Clear();
            route.AddRange(smooth);
        }
    }

    /// <summary>Primer eslabón desde la punta que otro sistema mantiene fijo, o -1 si no hay uno cerca.</summary>
    int FixedLink(WireTip tip)
    {
        int last = Mathf.Min(maxDressLinks, tip.Links.Length - 1);
        for (int i = 1; i <= last; i++)
            // A held link is kinematic only because the hand locks it.
            if (tip.Body(i).isKinematic && !tip.IsHeld(i)) return i;
        return -1;
    }

    static float RouteLength(List<Vector3> route)
    {
        float length = 0f;
        for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i - 1], route[i]);
        return length;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = State == TerminalState.Tightened ? Color.green : State == TerminalState.Inserted ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, acceptRadius);
        for (int slot = 0; slot < capacity; slot++)
        {
            Vector3 mouth = SlotPosition(slot);
            Gizmos.DrawLine(mouth, mouth + transform.forward * 0.02f);
        }

        DrawRoute(dressPoints, SlotPosition(0));
        if (routes == null) return;
        foreach (var route in routes)
            if (route != null) DrawRoute(route.points, SlotPosition(Mathf.Clamp(route.slot, 0, capacity - 1)));
    }

    static void DrawRoute(Transform[] points, Vector3 previous)
    {
        if (points == null) return;
        foreach (var point in points)
        {
            if (point == null) continue;
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
}
