using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Encintado del amarre entre los cables y la cabeza de la guía jalacables. Se pega la punta de la
/// cinta acercando el rollo al amarre y luego se dan vueltas con el rollo alrededor de él.
/// </summary>
public class GuideTape : MonoBehaviour
{
    public PullGuide guide;

    [Header("Cinta")]
    public Grabbable roll;
    [Tooltip("Centro del rollo, de donde sale la cinta.")]
    public Transform rollCenter;
    [Tooltip("Tira de cinta entre el amarre y el rollo mientras se encinta.")]
    public LineRenderer strip;
    [Tooltip("Capas de cinta sobre el amarre: cilindro hijo de la cabeza con su eje Y a lo largo de la guía.")]
    public Transform wrap;

    [Header("Manos")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Encintado")]
    public float requiredTurns = 3f;
    [Tooltip("Distancia del rollo al amarre para pegar la punta de la cinta.")]
    public float stickRadius = 0.1f;
    [Tooltip("Distancia máxima del rollo al amarre mientras se dan vueltas.")]
    public float maxWrapDistance = 0.3f;
    [Tooltip("Distancia mínima del rollo al eje del amarre para medir el giro.")]
    public float minOrbitRadius = 0.02f;
    public float wrapLength = 0.05f;
    public float wrapBaseRadius = 0.012f;
    public float wrapRadiusPerTurn = 0.002f;

    public UnityEvent OnTaped;

    public bool IsStuck { get; private set; }
    public bool IsTaped { get; private set; }
    public bool IsRollHeld => roll.SelectingPointsCount > 0;
    public float Turns => Mathf.Abs(wrapAngle) / 360f;
    public float Progress01 => IsTaped ? 1f : Mathf.Clamp01(Turns / requiredTurns);
    /// <summary>Último momento en que avanzó el encintado (o se pegó la cinta).</summary>
    public float LastTurnTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Último intento de encintar antes de enganchar los tres cables.</summary>
    public float LastNotHookedTime { get; private set; } = float.NegativeInfinity;

    float wrapAngle;
    Vector3 prevRadial;
    int quarterTurns;
    OVRInput.Controller vibratingHand = OVRInput.Controller.None;
    float vibrateUntil;

    void Awake()
    {
        strip.enabled = false;
        wrap.gameObject.SetActive(false);
    }

    void OnDisable() => StopVibration();

    void Update()
    {
        if (vibratingHand != OVRInput.Controller.None && Time.time >= vibrateUntil) StopVibration();

        // The tape goes into the conduit with the head; once the guide is back on the spool it's gone.
        wrap.gameObject.SetActive(Turns > 0.05f && guide.State != PullGuide.GuideState.Done);
        if (IsTaped) return;

        Vector3 offset = rollCenter.position - guide.head.position;
        if (guide.State != PullGuide.GuideState.AtEntry || !guide.AllHooked)
        {
            if (IsRollHeld && offset.magnitude <= stickRadius) LastNotHookedTime = Time.time;
            strip.enabled = false;
            return;
        }

        if (!IsStuck)
        {
            if (!IsRollHeld || offset.magnitude > stickRadius) return;
            IsStuck = true;
            LastTurnTime = Time.time;
            Vibrate(0.4f, 0.08f);
        }

        // Dropping the roll or walking away leaves the tape end stuck; wrapping resumes on return.
        bool wrapping = IsRollHeld && offset.magnitude <= maxWrapDistance;
        strip.enabled = wrapping;
        if (!wrapping)
        {
            prevRadial = Vector3.zero;
            return;
        }

        Vector3 axis = guide.head.forward;
        Vector3 radial = Vector3.ProjectOnPlane(offset, axis);
        if (radial.magnitude >= minOrbitRadius)
        {
            if (prevRadial != Vector3.zero)
            {
                float before = Turns;
                wrapAngle += Vector3.SignedAngle(prevRadial, radial, axis);
                if (Turns > before) LastTurnTime = Time.time;
            }
            prevRadial = radial;
        }

        UpdateVisuals(radial.magnitude >= minOrbitRadius ? radial.normalized : guide.head.up);

        // A tick every quarter turn lets the player feel the wrapping without looking.
        int quarters = Mathf.FloorToInt(Turns * 4f);
        if (quarters > quarterTurns) Vibrate(0.25f, 0.04f);
        quarterTurns = quarters;

        if (Turns >= requiredTurns) Finish();
    }

    void UpdateVisuals(Vector3 radialDirection)
    {
        float radius = wrapBaseRadius + wrapRadiusPerTurn * Mathf.Min(Turns, requiredTurns * 2f);
        wrap.localScale = new Vector3(radius * 2f, wrapLength * 0.5f, radius * 2f);
        strip.SetPosition(0, guide.head.position + radialDirection * radius);
        strip.SetPosition(1, rollCenter.position);
    }

    void Finish()
    {
        IsTaped = true;
        strip.enabled = false;
        Vibrate(0.6f, 0.15f);
        OnTaped.Invoke();
    }

    void Vibrate(float amplitude, float seconds)
    {
        var hand = NearestHand();
        if (vibratingHand != OVRInput.Controller.None && vibratingHand != hand) StopVibration();
        OVRInput.SetControllerVibration(0.5f, amplitude, hand);
        vibratingHand = hand;
        vibrateUntil = Time.time + seconds;
    }

    void StopVibration()
    {
        if (vibratingHand == OVRInput.Controller.None) return;
        OVRInput.SetControllerVibration(0f, 0f, vibratingHand);
        vibratingHand = OVRInput.Controller.None;
    }

    OVRInput.Controller NearestHand() =>
        Vector3.Distance(rollCenter.position, leftHand.position) < Vector3.Distance(rollCenter.position, rightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
}
