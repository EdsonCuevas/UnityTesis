using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Va en la raíz de un cable con punta pelable. Permite que solo los cables del paso actual se puedan
/// agarrar, hace pulsar el cable del paso y lo ilumina fijo cuando una mano está a punto de agarrarlo.
/// </summary>
public class WireGrab : MonoBehaviour
{
    public static readonly List<WireGrab> All = new List<WireGrab>();

    [Tooltip("Color hacia el que se ilumina el cable.")]
    public Color highlightColor = new Color(1f, 0.75f, 0.1f);
    public float pulseSpeed = 4f;
    [Range(0f, 1f)] public float pulseMin = 0.1f;
    [Range(0f, 1f)] public float pulseMax = 0.55f;
    [Range(0f, 1f)] public float hoverAmount = 0.9f;

    [Header("Guardado")]
    [Tooltip("Punta del cable que se guarda mientras no le toca. Vacío = el cable no se guarda.")]
    public WireStripper parkTip;
    [Tooltip("Ruta donde espera el cable, desde su salida del tubo. Debe ser más larga que el cable libre.")]
    public Transform[] parkRoute = new Transform[0];
    public float parkSeconds = 0.5f;

    [Header("En espera")]
    [Tooltip("El cable espera fijo y sin poder agarrarse hasta el primer paso que lo use. Sus eslabones deben estar cinemáticos en la escena.")]
    public bool startDormant;
    [Tooltip("Eslabones que siguen fijos al despertar el cable, por ejemplo los que quedan dentro del tubo.")]
    public Rigidbody[] stayFixed = new Rigidbody[0];

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>El cable pulsa porque es el del paso.</summary>
    public bool Highlighted { get; private set; }
    public bool Locked { get; private set; }
    /// <summary>Una mano está sobre el cable o lo sostiene.</summary>
    public bool Hovered { get; private set; }
    public bool Dormant { get; private set; }

    readonly List<GameObject> grabObjects = new List<GameObject>();
    readonly List<IInteractableView> views = new List<IInteractableView>();
    Renderer tube;
    Color baseColor;
    MaterialPropertyBlock block;
    float shown = -1f;
    WireTip parked;
    readonly List<Vector3> parkStarts = new List<Vector3>();
    readonly List<Vector3> parkTargets = new List<Vector3>();
    readonly List<Quaternion> parkRotations = new List<Quaternion>();
    float parkStartTime;
    bool parkMoving;

    void Awake()
    {
        var wire = GetComponent<WireController>();
        tube = wire.ropeMesh.GetComponent<Renderer>();
        baseColor = tube.sharedMaterial.GetColor(BaseColorId);
        block = new MaterialPropertyBlock();

        foreach (var interactable in GetComponentsInChildren<IInteractable>(true))
        {
            if (!(interactable is Behaviour behaviour)) continue;
            if (interactable is IInteractableView view) views.Add(view);

            // Links hidden inside the wall start with their grab turned off and stay that way.
            var go = behaviour.gameObject;
            if (go != gameObject && go.activeSelf && go.GetComponent<Rigidbody>() == null && !grabObjects.Contains(go))
                grabObjects.Add(go);
        }

        if (startDormant)
        {
            Dormant = true;
            SetLocked(true);
        }
    }

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        SetLocked(false);
        Highlighted = false;
        Hovered = false;
        Paint(0f);
    }

    /// <summary>Solo esas puntas (y las de alsoGrabbable) se pueden agarrar; las primeras pulsan.</summary>
    public static void Only(IEnumerable<WireStripper> tips, IEnumerable<WireStripper> alsoGrabbable = null)
    {
        var highlighted = Owners(tips);
        var grabbable = Owners(alsoGrabbable);
        grabbable.UnionWith(highlighted);

        foreach (var wire in All)
        {
            wire.Highlighted = highlighted.Contains(wire);
            if (grabbable.Contains(wire)) wire.Wake();
            wire.SetLocked(!grabbable.Contains(wire));
            if (grabbable.Contains(wire)) wire.Unpark();
            else wire.Park();
        }
    }

    /// <summary>Todos los cables se pueden agarrar y ninguno pulsa.</summary>
    public static void Free()
    {
        foreach (var wire in All)
        {
            wire.Highlighted = false;
            wire.SetLocked(wire.Dormant);
        }
    }

    void Wake()
    {
        if (!Dormant) return;
        Dormant = false;

        var wire = GetComponent<WireController>();
        foreach (var segment in wire.segments) Release(segment);
        Release(wire.starAnchorTemp);
        Release(wire.endAnchorTemp);
    }

    void Release(Transform link)
    {
        var body = link.GetComponent<Rigidbody>();
        if (System.Array.IndexOf(stayFixed, body) < 0) body.isKinematic = false;
    }

    static HashSet<WireGrab> Owners(IEnumerable<WireStripper> tips)
    {
        var owners = new HashSet<WireGrab>();
        if (tips == null) return owners;
        foreach (var tip in tips)
        {
            var owner = tip != null ? tip.GetComponentInParent<WireGrab>(true) : null;
            if (owner != null) owners.Add(owner);
        }
        return owners;
    }

    void SetLocked(bool locked)
    {
        if (Locked == locked) return;
        Locked = locked;
        // Deactivating the grab objects is separate from the enabled flag that pinning and the conduit use.
        foreach (var go in grabObjects)
            if (go != null) go.SetActive(!locked);
    }

    /// <summary>Lleva el cable libre a su ruta de espera, si tiene una y todavía no está conectado.</summary>
    void Park()
    {
        if (parked != null || parkTip == null || parkRoute.Length == 0 || MeterTerminal.Holding(parkTip) != null) return;

        var tip = WireTip.For(parkTip);
        int fixedLink = -1;
        for (int i = 1; i < tip.Links.Length && fixedLink < 0; i++)
            if (tip.Body(i).isKinematic && !tip.IsHeld(i)) fixedLink = i;
        if (fixedLink < 0) return;

        var route = new List<Vector3> { tip.Links[fixedLink].position };
        foreach (var point in parkRoute)
            if (point != null) route.Add(point.position);

        parkStarts.Clear();
        parkTargets.Clear();
        parkRotations.Clear();

        // Walks from the fixed link to the tip, so index 0 of the lists is the link next to the fixed one.
        int segment = 1;
        Vector3 from = route[0];
        Vector3 previous = from;
        for (int link = fixedLink - 1; link >= 0; link--)
        {
            float left = tip.Spacing;
            while (segment < route.Count)
            {
                float length = Vector3.Distance(from, route[segment]);
                if (left <= length) break;
                left -= length;
                from = route[segment];
                segment++;
            }
            if (segment < route.Count) from = Vector3.MoveTowards(from, route[segment], left);

            Vector3 direction = previous - from;
            parkStarts.Add(tip.Links[link].position);
            parkTargets.Add(from);
            parkRotations.Add(direction.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            previous = from;
        }

        parked = tip;
        parkMoving = true;
        parkStartTime = Time.time;
    }

    void Unpark()
    {
        if (parked == null) return;
        for (int i = 0; i < parkTargets.Count; i++)
            parked.Unpin(parkTargets.Count - 1 - i);
        parked = null;
        parkMoving = false;
    }

    void FixedUpdate()
    {
        if (!parkMoving) return;

        float t = parkSeconds > 0f ? Mathf.Clamp01((Time.time - parkStartTime) / parkSeconds) : 1f;
        for (int i = 0; i < parkTargets.Count; i++)
        {
            Vector3 position = Vector3.Lerp(parkStarts[i], parkTargets[i], Mathf.SmoothStep(0f, 1f, t));
            parked.Pin(parkTargets.Count - 1 - i, position, parkRotations[i]);
        }
        if (t >= 1f) parkMoving = false;
    }

    void Update()
    {
        Hovered = false;
        if (!Locked)
            foreach (var view in views)
                if (view.State == InteractableState.Hover || view.State == InteractableState.Select)
                {
                    Hovered = true;
                    break;
                }

        float amount = 0f;
        if (Hovered) amount = hoverAmount;
        else if (Highlighted) amount = Mathf.Lerp(pulseMin, pulseMax, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        Paint(amount);
    }

    void Paint(float amount)
    {
        if (tube == null || Mathf.Approximately(amount, shown)) return;
        shown = amount;
        if (amount <= 0f)
        {
            tube.SetPropertyBlock(null);
            return;
        }
        tube.GetPropertyBlock(block);
        block.SetColor(BaseColorId, Color.Lerp(baseColor, highlightColor, amount));
        tube.SetPropertyBlock(block);
    }
}
