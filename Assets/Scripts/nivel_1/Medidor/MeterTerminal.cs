using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Boca de una terminal del medidor (zapata o entrada del conector de neutro). Su posición es la boca y su
/// eje Z la dirección en que entra el cable. Acepta cualquier punta pelada que se le acerque sostenida;
/// sin apretar el tornillo el cable se sale al jalarlo, y ya apretado se queda acomodado.
/// </summary>
public class MeterTerminal : MonoBehaviour
{
    public enum TerminalState
    {
        Empty,
        Inserted,
        Tightened
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
    [Tooltip("Puntos por donde se acomoda el cable al apretar el tornillo, en orden desde la boca.")]
    public Transform[] dressPoints;
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
    public WireStripper Wire { get; private set; }
    public bool HasCorrectWire => Wire != null && Accepts(Wire);
    public float LastUnstrippedTime { get; private set; } = float.NegativeInfinity;
    public float LastClosedTime { get; private set; } = float.NegativeInfinity;

    static WireStripper[] tips;

    WireTip seated;
    int pullLink = -1;
    float pullBaseline;
    readonly List<Vector3> dressTargets = new List<Vector3>();
    readonly List<Vector3> dressStarts = new List<Vector3>();
    float dressStartTime;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        if (tips == null || tips.Length == 0 || tips[0] == null)
            tips = FindObjectsByType<WireStripper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    public bool Accepts(WireStripper wire) => System.Array.IndexOf(accepts, wire) >= 0;

    /// <summary>Terminal donde está metida esa punta, o null.</summary>
    public static MeterTerminal Holding(WireStripper wire)
    {
        foreach (var terminal in All)
            if (terminal.Wire == wire) return terminal;
        return null;
    }

    void Update()
    {
        if (State == TerminalState.Empty)
        {
            if (Accepting) TryInsert();
            return;
        }

        if (State == TerminalState.Inserted && screw.IsTight) Tighten();
        else if (State == TerminalState.Tightened && !screw.IsTight) Loosen();

        if (State == TerminalState.Inserted && IsPulledOut()) Remove(true);
    }

    void FixedUpdate()
    {
        if (seated != null) HoldInPlace();
    }

    void LateUpdate()
    {
        // The aligner is off while seated, so the tip points into the terminal.
        if (seated != null) seated.Stripper.transform.rotation = Quaternion.LookRotation(transform.forward);
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
            if (screw.Turns >= closedTurns)
            {
                LastClosedTime = Time.time;
                continue;
            }

            Insert(tip);
            return;
        }
    }

    // The neutral connector entries sit close together; the tip goes into the nearest free one.
    bool CloserTerminal(Vector3 point)
    {
        float distance = Vector3.Distance(point, transform.position);
        foreach (var other in All)
            if (other != this && other.Accepting && other.State == TerminalState.Empty &&
                Vector3.Distance(point, other.transform.position) < distance)
                return true;
        return false;
    }

    void Insert(WireTip tip)
    {
        seated = tip;
        Wire = tip.Stripper;
        State = TerminalState.Inserted;
        pullLink = -1;
        if (tip.Aligner != null) tip.Aligner.enabled = false;
        dressTargets.Clear();
        HoldInPlace();

        OnInserted.Invoke();
        if (!HasCorrectWire) OnWrongWire.Invoke();
        WireInserted?.Invoke(this, Wire);
    }

    void Tighten()
    {
        State = TerminalState.Tightened;
        BuildDress();
        OnTightened.Invoke();
    }

    void Loosen()
    {
        State = TerminalState.Inserted;
        for (int i = 1; i <= dressTargets.Count; i++) seated.Unpin(i);
        dressTargets.Clear();
        pullLink = -1;
        OnLoosened.Invoke();
    }

    void Remove(bool pulled)
    {
        var tip = seated;
        var wire = Wire;
        for (int i = 1; i <= dressTargets.Count; i++) tip.Unpin(i);
        dressTargets.Clear();
        tip.Unpin(0);
        if (tip.Aligner != null) tip.Aligner.enabled = true;

        seated = null;
        Wire = null;
        State = TerminalState.Empty;
        OnRemoved.Invoke();
        if (pulled) WirePulledOut?.Invoke(this, wire);
    }

    /// <summary>
    /// Sin apretar, la punta se sale si una mano estira el cable cerca de la terminal. El estiramiento se
    /// mide contra el mínimo desde que se agarró, porque al asentarse la punta el cable ya queda algo tenso.
    /// </summary>
    bool IsPulledOut()
    {
        int held = seated.HeldLink(nearLinks);
        if (held < 1)
        {
            pullLink = -1;
            return false;
        }

        float stretch = Vector3.Distance(seated.Links[held].position, transform.position) - held * seated.Spacing;
        if (held != pullLink)
        {
            pullLink = held;
            pullBaseline = stretch;
        }
        pullBaseline = Mathf.Min(pullBaseline, stretch);
        return stretch - pullBaseline > pullOutDistance;
    }

    void HoldInPlace()
    {
        seated.Pin(0, transform.position, transform.rotation);

        float t = dressSeconds > 0f ? Mathf.Clamp01((Time.time - dressStartTime) / dressSeconds) : 1f;
        for (int i = 0; i < dressTargets.Count; i++)
        {
            Vector3 position = Vector3.Lerp(dressStarts[i], dressTargets[i], Mathf.SmoothStep(0f, 1f, t));
            Vector3 previous = i == 0 ? transform.position : dressTargets[i - 1];
            Vector3 direction = dressTargets[i] - previous;
            seated.Pin(i + 1, position, direction.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(direction) : Quaternion.identity);
        }
    }

    /// <summary>
    /// Reparte los eslabones cercanos a la punta sobre la ruta de acomodo. Si el resto del cable está fijo
    /// cerca (en el ducto o en otra terminal), todo el tramo libre se reparte hasta ese punto, para que no
    /// quede un bucle colgando aunque sobre cable.
    /// </summary>
    void BuildDress()
    {
        dressTargets.Clear();
        dressStarts.Clear();
        dressStartTime = Time.time;
        if (dressPoints == null || dressPoints.Length == 0) return;

        var route = new List<Vector3> { transform.position };
        foreach (var point in dressPoints)
            if (point != null) route.Add(point.position);

        int count = seated.Links.Length - 1;
        float spacing = seated.Spacing;
        int fixedLink = FixedLink();
        if (fixedLink > 0)
        {
            route.Add(seated.Links[fixedLink].position);
            count = fixedLink - 1;
            spacing = RouteLength(route) / fixedLink;
        }

        float along = spacing;
        for (int i = 1; i < route.Count && dressTargets.Count < count; )
        {
            Vector3 from = route[i - 1];
            float length = Vector3.Distance(from, route[i]);
            if (along > length)
            {
                along -= length;
                route[i - 1] = route[i];
                i++;
                continue;
            }

            Vector3 target = Vector3.MoveTowards(from, route[i], along);
            dressTargets.Add(target);
            dressStarts.Add(seated.Links[dressTargets.Count].position);
            route[i - 1] = target;
            along = spacing;
        }
    }

    /// <summary>Primer eslabón desde la punta que otro sistema mantiene fijo, o -1 si no hay uno cerca.</summary>
    int FixedLink()
    {
        int last = Mathf.Min(maxDressLinks, seated.Links.Length - 1);
        for (int i = 1; i <= last; i++)
            // A held link is kinematic only because the hand locks it.
            if (seated.Body(i).isKinematic && !seated.IsHeld(i)) return i;
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
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.02f);

        if (dressPoints == null) return;
        Vector3 previous = transform.position;
        foreach (var point in dressPoints)
        {
            if (point == null) continue;
            Gizmos.DrawLine(previous, point.position);
            previous = point.position;
        }
    }
}
