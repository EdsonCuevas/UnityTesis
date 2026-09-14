using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

public class WireStripper : MonoBehaviour
{
    [Header("Pinzas")]
    public Grabbable pliers;
    [Tooltip("Punto entre las quijadas de las pinzas.")]
    public Transform pliersJaw;
    public Transform leftHand;
    public Transform rightHand;
    [Range(0f, 1f)] public float triggerThreshold = 0.6f;

    [Header("Cable")]
    [Tooltip("Forro que se desliza. Es hijo de este objeto, cuyo eje Z apunta hacia la punta del cable.")]
    public Transform insulation;
    public float stripLength = 0.025f;
    [Tooltip("Distancia máxima entre las quijadas y el forro para poder apretarlo.")]
    public float gripRadius = 0.04f;
    [Tooltip("Agarres del tramo final del cable. Si hay alguno, solo se pela mientras una mano sostiene el cable.")]
    public Grabbable[] cableHandles;

    public UnityEvent OnStripped;

    public bool IsStripped { get; private set; }
    public bool WaitingForCableHold { get; private set; }
    public float Progress01 => IsStripped ? 1f : Mathf.Clamp01(slide / stripLength);

    Vector3 insulationStart;
    float slide;
    bool clamping;
    float clampStartAlong;
    float slideAtClamp;
    OVRInput.Controller clampHand;

    void Awake() => insulationStart = insulation.localPosition;

    void OnDisable() => StopClamp();

    void Update()
    {
        if (IsStripped) return;

        bool held = pliers.SelectingPointsCount > 0;
        OVRInput.Controller hand = held ? NearestHand() : OVRInput.Controller.None;
        bool squeezing = held && OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, hand) >= triggerThreshold;
        bool jawOnInsulation = Vector3.Distance(pliersJaw.position, insulation.position) <= gripRadius;
        float along = Vector3.Dot(pliersJaw.position - transform.position, transform.forward);

        if (!clamping && squeezing && jawOnInsulation)
        {
            clamping = true;
            clampHand = hand;
            clampStartAlong = along;
            slideAtClamp = slide;
        }
        else if (clamping && (!squeezing || !jawOnInsulation))
        {
            StopClamp();
        }

        if (!clamping) return;

        OVRInput.SetControllerVibration(0.3f, 0.25f, clampHand);

        WaitingForCableHold = !IsCableHeld();
        if (WaitingForCableHold)
        {
            clampStartAlong = along;
            slideAtClamp = slide;
            return;
        }

        slide = Mathf.Max(slide, slideAtClamp + along - clampStartAlong);
        insulation.localPosition = insulationStart + Vector3.forward * Mathf.Min(slide, stripLength);

        if (slide >= stripLength)
            ReleaseInsulation();
    }

    void ReleaseInsulation()
    {
        StopClamp();
        IsStripped = true;
        insulation.SetParent(null, true);
        insulation.gameObject.AddComponent<CapsuleCollider>();
        insulation.gameObject.AddComponent<Rigidbody>().mass = 0.01f;
        OnStripped.Invoke();
    }

    void StopClamp()
    {
        if (clamping)
            OVRInput.SetControllerVibration(0f, 0f, clampHand);
        clamping = false;
        WaitingForCableHold = false;
    }

    bool IsCableHeld()
    {
        if (cableHandles.Length == 0) return true;
        foreach (var handle in cableHandles)
            if (handle.SelectingPointsCount > 0) return true;
        return false;
    }

    OVRInput.Controller NearestHand()
    {
        Vector3 position = pliers.transform.position;
        return Vector3.Distance(position, leftHand.position) < Vector3.Distance(position, rightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
    }
}
