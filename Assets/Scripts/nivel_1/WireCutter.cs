using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Corte del sobrante de un cable con las pinzas: se aprietan sobre el cable cerca de la marca, entre la
/// entrada del ducto y la punta. El tramo cortado se queda en el rollo y la punta pelable pasa al corte.
/// </summary>
public class WireCutter : MonoBehaviour
{
    [Header("Pinzas")]
    public Grabbable pliers;
    [Tooltip("Punto entre las quijadas de las pinzas.")]
    public Transform pliersJaw;
    public Transform leftHand;
    public Transform rightHand;
    [Range(0f, 1f)] public float triggerThreshold = 0.6f;
    [Tooltip("Distancia máxima entre las quijadas y el cable para cortarlo.")]
    public float gripRadius = 0.03f;

    [Header("Cable")]
    [Tooltip("Punta pelable del extremo que sobra; después del corte queda en el corte.")]
    public WireStripper tip;
    [Tooltip("Ducto por donde entró el cable; el largo que queda se mide desde su entrada.")]
    public ConduitPathGuide conduit;
    [Tooltip("Cable libre que queda entre la entrada del ducto y la marca.")]
    public float keepLength = 0.5f;
    [Tooltip("Qué tanto puede alejarse el corte de la marca, hacia cualquier lado.")]
    public float tolerance = 0.15f;
    [Tooltip("Anillo que marca dónde cortar; se acomoda sobre el cable.")]
    public Transform mark;
    [Tooltip("Agarres del cable que usa el pelado, contados desde la punta nueva.")]
    public int stripHandles = 16;

    public UnityEvent OnCut;

    public bool IsCut { get; private set; }
    public bool IsPliersHeld => pliers.SelectingPointsCount > 0;
    /// <summary>Último intento de cortar fuera del rango de la marca.</summary>
    public float LastOutOfRangeTime { get; private set; } = float.NegativeInfinity;
    public Vector3 MarkPosition => markLink >= 0 ? WireTip.For(tip).Links[markLink].position : tip.transform.position;

    int markLink = -1;
    int minLink;
    int maxLink;
    int freeLinks;
    bool wasSqueezing;

    void Awake() => enabled = false;

    /// <summary>Ubica la marca. Se llama cuando el cable ya está dentro del ducto.</summary>
    public void Prepare()
    {
        var wire = WireTip.For(tip);
        freeLinks = 0;
        while (freeLinks < wire.Links.Length && !conduit.IsInside(wire.Links[freeLinks])) freeLinks++;

        // Links are counted from the tip, so the mark sits keepLength back from the conduit entry.
        int keep = Mathf.RoundToInt(keepLength / wire.Spacing);
        int slack = Mathf.RoundToInt(tolerance / wire.Spacing);
        markLink = Mathf.Clamp(freeLinks - keep, 2, Mathf.Max(2, freeLinks - 3));
        minLink = Mathf.Max(2, markLink - slack);
        maxLink = Mathf.Max(minLink, Mathf.Min(freeLinks - 3, markLink + slack));
    }

    void Update()
    {
        if (IsCut || markLink < 0) return;

        OVRInput.Controller hand = IsPliersHeld ? NearestHand() : OVRInput.Controller.None;
        bool squeezing = IsPliersHeld && OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, hand) >= triggerThreshold;
        if (squeezing && !wasSqueezing) TryCut(hand);
        wasSqueezing = squeezing;
    }

    void LateUpdate()
    {
        if (IsCut || markLink < 0 || mark == null) return;

        var links = WireTip.For(tip).Links;
        Vector3 along = links[markLink - 1].position - links[markLink + 1].position;
        mark.position = links[markLink].position;
        if (along.sqrMagnitude > 1e-8f)
            mark.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(90f, 0f, 0f);
    }

    void TryCut(OVRInput.Controller hand)
    {
        var links = WireTip.For(tip).Links;
        int nearest = -1;
        float nearestDistance = gripRadius;
        for (int i = 1; i < freeLinks; i++)
        {
            float distance = Vector3.Distance(pliersJaw.position, links[i].position);
            if (distance > nearestDistance) continue;
            nearest = i;
            nearestDistance = distance;
        }
        if (nearest < 0) return;

        if (nearest < minLink || nearest > maxLink)
        {
            LastOutOfRangeTime = Time.time;
            return;
        }
        Cut(nearest);
        OVRInput.SetControllerVibration(0.5f, 0.7f, hand);
        Invoke(nameof(StopVibration), 0.12f);
    }

    /// <summary>Quita los eslabones entre la punta y el corte, y une la punta al eslabón del corte.</summary>
    void Cut(int link)
    {
        var old = WireTip.For(tip);
        var links = old.Links;
        var wire = links[0].GetComponentInParent<WireController>();
        Rigidbody anchorBody = old.Body(0);
        Rigidbody keptBody = old.Body(link);

        // Move the anchor first: joints configure their anchors from the current poses.
        Vector3 outward = links[link].position - links[link + 1].position;
        outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector3.up;
        Vector3 position = links[link].position + outward * old.Spacing;
        anchorBody.linearVelocity = Vector3.zero;
        anchorBody.angularVelocity = Vector3.zero;
        anchorBody.position = position;
        links[0].SetPositionAndRotation(position, Quaternion.LookRotation(outward));

        foreach (var joint in links[link].GetComponents<Joint>())
            if (joint.connectedBody == old.Body(link - 1)) joint.connectedBody = anchorBody;
        foreach (var joint in links[0].GetComponents<Joint>())
            if (joint.connectedBody == old.Body(1)) joint.connectedBody = keptBody;

        for (int i = 1; i < link; i++)
        {
            wire.segments.Remove(links[i]);
            links[i].gameObject.SetActive(false);
        }

        var aligner = tip.GetComponent<WireEndAligner>();
        if (aligner != null) aligner.neighbor = links[link];

        WireTip.Forget(tip);
        var fresh = WireTip.For(tip);
        var handles = new Grabbable[Mathf.Min(stripHandles, fresh.Links.Length)];
        for (int i = 0; i < handles.Length; i++) handles[i] = fresh.GrabbableAt(i);
        tip.cableHandles = handles;

        IsCut = true;
        if (mark != null) mark.gameObject.SetActive(false);
        OnCut.Invoke();
    }

    void StopVibration()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    OVRInput.Controller NearestHand()
    {
        Vector3 position = pliers.transform.position;
        return Vector3.Distance(position, leftHand.position) < Vector3.Distance(position, rightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
    }
}
