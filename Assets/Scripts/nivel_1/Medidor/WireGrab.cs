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

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// <summary>El cable pulsa porque es el del paso.</summary>
    public bool Highlighted { get; private set; }
    public bool Locked { get; private set; }
    /// <summary>Una mano está sobre el cable o lo sostiene.</summary>
    public bool Hovered { get; private set; }

    readonly List<GameObject> grabObjects = new List<GameObject>();
    readonly List<IInteractableView> views = new List<IInteractableView>();
    Renderer tube;
    Color baseColor;
    MaterialPropertyBlock block;
    float shown = -1f;

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
            wire.SetLocked(!grabbable.Contains(wire));
        }
    }

    /// <summary>Todos los cables se pueden agarrar y ninguno pulsa.</summary>
    public static void Free()
    {
        foreach (var wire in All)
        {
            wire.Highlighted = false;
            wire.SetLocked(false);
        }
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
