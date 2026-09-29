using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Extremo de un cable con punta pelable: su ancla y los eslabones ordenados desde la punta.
/// Permite fijar eslabones (cinemáticos y sin poder agarrarse) y saber cuál sostiene una mano.
/// </summary>
public class WireTip
{
    static readonly Dictionary<WireStripper, WireTip> Cache = new Dictionary<WireStripper, WireTip>();

    public readonly WireStripper Stripper;
    public readonly WireEndAligner Aligner;
    /// <summary>El 0 es el ancla; los demás van de la punta hacia el resto del cable.</summary>
    public readonly Transform[] Links;
    public readonly float Spacing;

    readonly Rigidbody[] bodies;
    readonly Grabbable[] grabbables;
    readonly Behaviour[][] interactables;

    public Transform Anchor => Links[0];
    /// <summary>Dirección hacia la que apunta la punta.</summary>
    public Vector3 Forward => Stripper.transform.forward;
    /// <summary>Extremo del cobre (o del forro, si no está pelada).</summary>
    public Vector3 End => Stripper.transform.position + Forward * Stripper.stripLength;

    public static WireTip For(WireStripper stripper)
    {
        if (!Cache.TryGetValue(stripper, out var tip))
        {
            // Entries of a previous scene load point to destroyed strippers.
            var stale = new List<WireStripper>();
            foreach (var key in Cache.Keys)
                if (key == null) stale.Add(key);
            foreach (var key in stale) Cache.Remove(key);

            tip = new WireTip(stripper);
            Cache[stripper] = tip;
        }
        return tip;
    }

    /// <summary>Descarta los datos guardados de esa punta (por ejemplo, después de cortar el cable).</summary>
    public static void Forget(WireStripper stripper) => Cache.Remove(stripper);

    WireTip(WireStripper stripper)
    {
        Stripper = stripper;
        Aligner = stripper.GetComponent<WireEndAligner>();

        Transform anchor = stripper.transform.parent;
        var wire = anchor.GetComponentInParent<WireController>();
        var segments = wire.segments;
        bool fromStart = wire.starAnchorTemp == anchor;

        Links = new Transform[segments.Count + 1];
        Links[0] = anchor;
        for (int i = 0; i < segments.Count; i++)
            Links[i + 1] = fromStart ? segments[i] : segments[segments.Count - 1 - i];

        bodies = new Rigidbody[Links.Length];
        grabbables = new Grabbable[Links.Length];
        interactables = new Behaviour[Links.Length][];
        for (int i = 0; i < Links.Length; i++)
        {
            bodies[i] = Links[i].GetComponent<Rigidbody>();
            grabbables[i] = Links[i].GetComponent<Grabbable>();
            var found = new List<Behaviour>();
            foreach (var interactable in Links[i].GetComponentsInChildren<IInteractable>(true))
                if (interactable is Behaviour behaviour) found.Add(behaviour);
            interactables[i] = found.ToArray();
        }
        Spacing = Mathf.Max(wire.segmentsSeparation, 0.005f);
    }

    /// <summary>Primer eslabón (desde la punta, hasta maxLinks) que sostiene una mano, o -1.</summary>
    public int HeldLink(int maxLinks)
    {
        int last = Mathf.Min(maxLinks, Links.Length - 1);
        for (int i = 0; i <= last; i++)
            if (IsHeld(i)) return i;
        return -1;
    }

    public bool IsHeld(int index) => grabbables[index] != null && grabbables[index].SelectingPointsCount > 0;

    public Rigidbody Body(int index) => bodies[index];
    public Grabbable GrabbableAt(int index) => grabbables[index];

    public void Pin(int index, Vector3 position, Quaternion rotation)
    {
        // Disabling the interactables releases the hand; Grabbable then restores isKinematic,
        // so the body is locked again on every call.
        SetInteractable(index, false);
        var body = bodies[index];
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        body.position = position;
        body.rotation = rotation;
        Links[index].SetPositionAndRotation(position, rotation);
    }

    public void Unpin(int index)
    {
        bodies[index].isKinematic = false;
        SetInteractable(index, true);
    }

    void SetInteractable(int index, bool enabled)
    {
        foreach (var interactable in interactables[index])
            interactable.enabled = enabled;
    }
}
