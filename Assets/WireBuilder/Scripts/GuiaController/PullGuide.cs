using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Guía jalacables. Se toma del carrete, se mete por la salida del ducto (registro) y se empuja
/// hasta la entrada (medidor). Ahí se enganchan los cables y, al jalarla desde el registro,
/// recorren el ducto juntos. Se dibuja siguiendo el recorrido, sin física de cuerda.
/// </summary>
public class PullGuide : MonoBehaviour
{
    public enum GuideState
    {
        OnSpool,
        Carried,
        Inserting,
        AtEntry,
        Pulling,
        Done
    }

    [Header("Recorrido")]
    [Tooltip("Ducto cuyo recorrido sigue la guía (entrada = medidor, salida = registro).")]
    public ConduitPathGuide path;
    [Tooltip("Cables que se enganchan a la cabeza de la guía. Deben tener desactivado handFeed.")]
    public ConduitPathGuide[] cables;

    [Header("Guía")]
    public Transform head;
    [Tooltip("Pose de la cabeza sobre el carrete.")]
    public Transform headRest;
    [Tooltip("Punto del carrete del que sale la guía.")]
    public Transform spoolExit;
    public LineRenderer line;
    [Tooltip("Punto por donde la guía sube desde la salida del ducto hacia el carrete (por ejemplo, el borde de la fosa). Vacío = 20 cm arriba de la salida.")]
    public Transform outsideBend;
    [Tooltip("Desplazamiento de la cabeza respecto a la entrada mientras espera los cables, para que se vea.")]
    public Vector3 entryHeadOffset = new Vector3(0f, 0f, 0.06f);
    public float lineSampleSpacing = 0.05f;

    [Header("Manos")]
    public Transform leftHand;
    public Transform rightHand;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;
    [Tooltip("Distancia a la cabeza, o al tramo de guía fuera del ducto, para agarrarla.")]
    public float grabRadius = 0.12f;
    [Tooltip("Largo del tramo de guía, desde la salida del ducto, que se puede agarrar para empujar o jalar.")]
    public float grabbableLength = 0.6f;
    [Tooltip("Distancia de la cabeza a la salida del ducto para meterla al soltarla.")]
    public float mouthRadius = 0.2f;
    [Tooltip("Distancia máxima de la mano a la salida del ducto mientras empuja o jala.")]
    public float maxReach = 1.0f;
    [Tooltip("Qué tan cerca de la salida puede llegar la mano antes de dejar de empujar.")]
    public float handStopDistance = 0.05f;
    [Tooltip("Velocidad mínima de la mano (suavizada); filtra la vibración del tracking.")]
    public float minSpeed = 0.02f;
    public float speedSmoothing = 0.08f;

    [Header("Enganche")]
    [Tooltip("Distancia de la punta de un cable a la cabeza de la guía para engancharse.")]
    public float hookRadius = 0.15f;

    [Header("Velocidad")]
    [Tooltip("Velocidad máxima al meter la guía (m/s).")]
    public float insertSpeed = 1.5f;
    [Tooltip("Metros que avanza la guía por cada metro que empuja la mano al meterla.")]
    public float insertGain = 1.5f;
    [Tooltip("Resistencia por cada 90° de curva al meter la guía; es rígida y se desliza mejor que los cables.")]
    public float insertBendResistance = 0.2f;
    public float pullSpeed = 0.6f;
    [Tooltip("Resistencia extra por cada 90° de curva recorrida: divide la velocidad máxima.")]
    public float bendResistance = 0.5f;
    [Tooltip("Cuánto puede pasarse la mano de la velocidad máxima al jalar antes de que la guía se le resbale.")]
    public float slipTolerance = 1.4f;

    [Header("Eventos")]
    public UnityEvent OnInserted;
    public UnityEvent OnPulled;
    public UnityEvent OnSlipped;

    public GuideState State { get; private set; }
    public float LastSlipTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Último intento de jalar sin tener todos los cables enganchados.</summary>
    public float LastBlockedTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Último momento en que un cable ya no alcanzó y frenó la guía.</summary>
    public float LastOutOfSlackTime { get; private set; } = float.NegativeInfinity;

    public Vector3 HookPoint => path.PositionAt(0f) + entryHeadOffset;

    public int HookedCount
    {
        get
        {
            int count = 0;
            foreach (var cable in cables)
                if (cable.IsEngaged || cable.IsComplete) count++;
            return count;
        }
    }

    public bool AllHooked => HookedCount == cables.Length;

    public float InsertProgress01 => State switch
    {
        GuideState.OnSpool => 0f,
        GuideState.Carried => 0.1f,
        GuideState.Inserting => 0.15f + 0.85f * (1f - headDistance / Length),
        _ => 1f
    };

    public float PullProgress01 => State switch
    {
        GuideState.Done => 1f,
        GuideState.Pulling => headDistance / Length,
        _ => 0f
    };

    float Length => path.PathLength;
    Vector3 Mouth => path.PositionAt(Length);
    Vector3 Outward => path.TangentAt(Length);

    // Distance along the path from the entry to the head; the guide fills head..exit.
    float headDistance;
    Transform driveHand;
    OVRInput.Controller driveController = OVRInput.Controller.None;
    Vector3 drivePrevPos;
    float driveVelocity;
    float hapticAmplitude;
    OVRInput.Controller vibratingHand = OVRInput.Controller.None;
    bool leftWasGripping;
    bool rightWasGripping;
    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> outsidePoints = new List<Vector3>();
    Vector3[] pointBuffer = new Vector3[64];

    void OnDisable()
    {
        hapticAmplitude = 0f;
        UpdateHaptics();
    }

    void FixedUpdate()
    {
        bool leftGripping = IsGripping(OVRInput.Controller.LTouch);
        bool rightGripping = IsGripping(OVRInput.Controller.RTouch);
        bool leftPressed = leftGripping && !leftWasGripping;
        bool rightPressed = rightGripping && !rightWasGripping;
        leftWasGripping = leftGripping;
        rightWasGripping = rightGripping;

        hapticAmplitude = 0f;
        switch (State)
        {
            case GuideState.OnSpool:
                if (TryAttach(leftPressed, rightPressed, head.position, head.position))
                    State = GuideState.Carried;
                break;

            case GuideState.Carried:
                if (!IsGripping(driveController)) ReleaseHead();
                break;

            case GuideState.Inserting:
            case GuideState.AtEntry:
            case GuideState.Pulling:
                if (State == GuideState.AtEntry) HookCables();
                if (State == GuideState.Pulling && AllCablesComplete()) Finish();
                else DriveGuide(leftPressed, rightPressed);
                break;
        }
        UpdateHaptics();
    }

    void DriveGuide(bool leftPressed, bool rightPressed)
    {
        if (driveHand == null && !TryAttachToOutsideGuide(leftPressed, rightPressed)) return;

        Vector3 mouth = Mouth;
        if (!IsGripping(driveController) || Vector3.Distance(driveHand.position, mouth) > maxReach)
        {
            DetachHand();
            return;
        }

        // The guide runs from the hand into the mouth, so moving toward the mouth feeds it in and
        // moving away pulls it out, whichever side of the pit the hand is on.
        Vector3 hand = driveHand.position;
        Vector3 toMouth = mouth - hand;
        Vector3 inwardAxis = toMouth.magnitude > 0.06f ? toMouth.normalized : -Outward;
        float outward = -Vector3.Dot(hand - drivePrevPos, inwardAxis);
        drivePrevPos = hand;

        float dt = Time.fixedDeltaTime;
        driveVelocity = Mathf.Lerp(driveVelocity, outward / dt, 1f - Mathf.Exp(-dt / speedSmoothing));
        if (Mathf.Abs(driveVelocity) < minSpeed) return;

        if (State == GuideState.Inserting) Insert(outward, toMouth.magnitude);
        else Pull(outward);
    }

    void Insert(float outward, float handToMouth)
    {
        // Pushing (negative outward) moves the head deeper, toward the entry.
        if (outward < 0f && handToMouth < handStopDistance) return;

        float bendFromExit = path.BendAt(Length) - path.BendAt(headDistance);
        float resistance = 1f + insertBendResistance * bendFromExit / 90f;
        float maxStep = insertSpeed / resistance * Time.fixedDeltaTime;
        headDistance = Mathf.Clamp(headDistance + Mathf.Clamp(outward * insertGain, -maxStep, maxStep), 0f, Length);
        hapticAmplitude = Mathf.Clamp(0.1f + 0.2f * (resistance - 1f), 0.1f, 0.5f);

        if (headDistance > 0f) return;
        State = GuideState.AtEntry;
        DetachHand();
        OnInserted.Invoke();
    }

    void Pull(float outward)
    {
        if (outward <= 0f) return;
        if (!AllHooked)
        {
            LastBlockedTime = Time.time;
            return;
        }

        State = GuideState.Pulling;
        float resistance = Resistance(path.BendAt(headDistance));
        float speedLimit = pullSpeed / resistance;
        if (driveVelocity > speedLimit * slipTolerance)
        {
            Slip();
            return;
        }

        // The cable with the least slack left holds the whole guide back.
        float reachable = Length;
        foreach (var cable in cables)
            reachable = Mathf.Min(reachable, cable.MaxReachableProgress() / cable.PathLength * Length);

        float target = headDistance + Mathf.Min(outward, speedLimit * slipTolerance * Time.fixedDeltaTime);
        if (target > reachable)
        {
            target = reachable;
            LastOutOfSlackTime = Time.time;
        }
        if (target <= headDistance) return;

        headDistance = target;
        foreach (var cable in cables)
            cable.FeedTo(headDistance / Length * cable.PathLength);

        float nearSlip = Mathf.Clamp01(driveVelocity / (speedLimit * slipTolerance));
        hapticAmplitude = Mathf.Clamp(0.15f + 0.25f * (resistance - 1f) + 0.35f * nearSlip * nearSlip, 0.15f, 0.9f);
    }

    void HookCables()
    {
        Vector3 hook = HookPoint;
        foreach (var cable in cables)
            if (!cable.IsEngaged && !cable.IsComplete && cable.Tip != null
                && Vector3.Distance(cable.Tip.position, hook) <= hookRadius)
                cable.HookToGuide();
    }

    bool AllCablesComplete()
    {
        foreach (var cable in cables)
            if (!cable.IsComplete) return false;
        return true;
    }

    void Finish()
    {
        State = GuideState.Done;
        DetachHand();
        OnPulled.Invoke();
    }

    void ReleaseHead()
    {
        bool atMouth = Vector3.Distance(head.position, Mouth) <= mouthRadius;
        DetachHand();
        if (atMouth)
        {
            headDistance = Length;
            State = GuideState.Inserting;
        }
        else
        {
            State = GuideState.OnSpool;
        }
    }

    void Slip()
    {
        DetachHand();
        LastSlipTime = Time.time;
        OnSlipped.Invoke();
    }

    float Resistance(float bendDegrees) => 1f + bendResistance * bendDegrees / 90f;

    bool IsGripping(OVRInput.Controller controller) =>
        controller != OVRInput.Controller.None
        && OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller) >= gripThreshold;

    /// <summary>Engancha la mano que acaba de apretar el grip cerca del tramo de guía que sale del ducto.</summary>
    bool TryAttachToOutsideGuide(bool leftPressed, bool rightPressed)
    {
        if (!leftPressed && !rightPressed) return false;

        outsidePoints.Clear();
        AddOutsideGuide(outsidePoints, BendPoint);

        // Only the first grabbableLength of the guide, measured from the mouth, can be held.
        float walked = 0f;
        for (int i = 1; i < outsidePoints.Count && walked < grabbableLength; i++)
        {
            Vector3 a = outsidePoints[i - 1];
            Vector3 b = outsidePoints[i];
            float segment = Vector3.Distance(a, b);
            if (walked + segment > grabbableLength)
                b = Vector3.Lerp(a, b, (grabbableLength - walked) / segment);
            walked += segment;
            if (TryAttach(leftPressed, rightPressed, a, b)) return true;
        }
        return false;
    }

    Vector3 BendPoint => outsideBend != null ? outsideBend.position : Mouth + Vector3.up * 0.2f;

    /// <summary>Tramo fuera del ducto: salida, punto intermedio y caída hasta el carrete.</summary>
    void AddOutsideGuide(List<Vector3> into, Vector3 middle)
    {
        into.Add(Mouth);
        into.Add(middle);
        AddSag(into, middle, spoolExit.position, false);
    }

    /// <summary>Engancha la mano que acaba de apretar el grip cerca del segmento a-b.</summary>
    bool TryAttach(bool leftPressed, bool rightPressed, Vector3 a, Vector3 b)
    {
        if (leftPressed && DistanceToSegment(leftHand.position, a, b) <= grabRadius)
            AttachHand(leftHand, OVRInput.Controller.LTouch);
        else if (rightPressed && DistanceToSegment(rightHand.position, a, b) <= grabRadius)
            AttachHand(rightHand, OVRInput.Controller.RTouch);
        return driveHand != null;
    }

    void AttachHand(Transform hand, OVRInput.Controller controller)
    {
        driveHand = hand;
        driveController = controller;
        drivePrevPos = hand.position;
        driveVelocity = 0f;
    }

    void DetachHand()
    {
        driveHand = null;
        driveController = OVRInput.Controller.None;
        driveVelocity = 0f;
    }

    void UpdateHaptics()
    {
        var hand = hapticAmplitude > 0f ? driveController : OVRInput.Controller.None;
        if (vibratingHand != OVRInput.Controller.None && vibratingHand != hand)
            OVRInput.SetControllerVibration(0f, 0f, vibratingHand);
        if (hand != OVRInput.Controller.None)
            OVRInput.SetControllerVibration(0.5f, hapticAmplitude, hand);
        vibratingHand = hand;
    }

    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(point, a + ab * t);
    }

    void LateUpdate()
    {
        points.Clear();
        switch (State)
        {
            case GuideState.OnSpool:
            case GuideState.Done:
                head.SetPositionAndRotation(headRest.position, headRest.rotation);
                AddSag(points, spoolExit.position, head.position, true);
                break;

            case GuideState.Carried:
                head.SetPositionAndRotation(driveHand.position, driveHand.rotation);
                AddSag(points, spoolExit.position, head.position, true);
                break;

            default:
                Vector3 headPosition = State == GuideState.AtEntry ? HookPoint : path.PositionAt(headDistance);
                head.SetPositionAndRotation(headPosition, Quaternion.LookRotation(-path.TangentAt(headDistance)));
                points.Add(headPosition);
                for (float d = headDistance + lineSampleSpacing; d < Length; d += lineSampleSpacing)
                    points.Add(path.PositionAt(d));
                AddOutsideGuide(points, driveHand != null ? driveHand.position : BendPoint);
                break;
        }

        if (pointBuffer.Length < points.Count) pointBuffer = new Vector3[points.Count * 2];
        points.CopyTo(pointBuffer);
        line.positionCount = points.Count;
        line.SetPositions(pointBuffer);
    }

    static void AddSag(List<Vector3> into, Vector3 from, Vector3 to, bool includeStart)
    {
        const int segments = 10;
        float distance = Vector3.Distance(from, to);
        Vector3 control = (from + to) * 0.5f + Vector3.down * Mathf.Min(0.15f * distance, 0.2f);
        for (int i = includeStart ? 0 : 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            into.Add(Vector3.Lerp(Vector3.Lerp(from, control, t), Vector3.Lerp(control, to, t), t));
        }
    }
}
