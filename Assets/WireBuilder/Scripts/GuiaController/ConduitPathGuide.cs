using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

public class ConduitPathGuide : MonoBehaviour
{
    public enum FeedStatus
    {
        WaitingForTip,
        TipMisaligned,
        Feeding,
        NeedCloserGrip,
        OutOfSlack,
        Slipped,
        Complete
    }

    [Header("Trayectoria del ducto")]
    [Tooltip("Puntos del recorrido en orden desde la entrada. Se unen con una curva suave.")]
    public Transform[] waypoints;
    [Tooltip("Distancia entre muestras de la curva (metros).")]
    public float sampleSpacing = 0.01f;

    [Header("Cable")]
    public WireController wireController;
    [Tooltip("Desactívalo cuando el cable avanza arrastrado por la guía jalacables en lugar de empujarse a mano.")]
    public bool handFeed = true;

    [Header("Entrada")]
    [Tooltip("Distancia máxima entre la punta del cable y la entrada para introducirla.")]
    public float entryRadius = 0.15f;
    [Tooltip("Ángulo máximo entre la dirección del cable y la del ducto para poder introducirlo.")]
    public float maxEntryAngle = 70f;
    [Tooltip("Eslabones desde la punta que cuentan como 'sostener la punta' para introducirla.")]
    public int tipGrabLinks = 8;

    [Header("Empuje")]
    [Tooltip("Distancia máxima entre la mano y la entrada del ducto para que el empuje se transmita.")]
    public float maxPushReach = 0.4f;
    [Tooltip("Velocidad mínima de la mano (suavizada) para contar como empuje; filtra la vibración del tracking.")]
    public float minPushSpeed = 0.02f;
    [Tooltip("Tiempo de suavizado de la velocidad de la mano (segundos).")]
    public float pushSmoothing = 0.08f;
    [Tooltip("Qué tan cerca de la entrada puede llegar la mano antes de dejar de empujar.")]
    public float handStopDistance = 0.04f;
    [Tooltip("Velocidad máxima de empuje en tramo recto (m/s).")]
    public float maxFeedSpeed = 0.6f;
    [Tooltip("Resistencia extra por cada 90° de curva que ya recorrió la punta: divide la velocidad máxima de empuje.")]
    public float bendResistance = 0.5f;
    [Tooltip("Cuánto puede pasarse la mano de la velocidad máxima antes de que el cable se le resbale.")]
    public float slipTolerance = 1.4f;
    [Tooltip("Segundos que el eslabón resbalado no se puede volver a agarrar.")]
    public float slipRegrabDelay = 0.35f;
    [Tooltip("Cable libre extra que debe quedar afuera además de la distancia recta al inicio del cable.")]
    public float slackMargin = 0.05f;

    [Header("Manos")]
    [Tooltip("LeftControllerAnchor: se mide su movimiento mientras empuja el cable.")]
    public Transform leftHand;
    [Tooltip("RightControllerAnchor: se mide su movimiento mientras empuja el cable.")]
    public Transform rightHand;
    [Range(0f, 1f)] public float gripThreshold = 0.5f;

    [Header("Eventos")]
    public UnityEvent OnEngaged;
    public UnityEvent OnDisengaged;
    public UnityEvent OnCompleted;
    public UnityEvent OnSlipped;

    public bool IsEngaged { get; private set; }
    public bool IsComplete { get; private set; }
    public FeedStatus Status { get; private set; }
    public float LastSlipTime { get; private set; } = float.NegativeInfinity;
    public float Progress01 => _pathLength > 0f ? _progress / _pathLength : 0f;
    public float PathLength { get { EnsurePath(); return _pathLength; } }
    /// <summary>Punta del cable (eslabón que entra primero). Null antes de Start.</summary>
    public Transform Tip => _chain != null ? _chain[0] : null;

    readonly List<Vector3> _samples = new List<Vector3>();
    readonly List<float> _cumLength = new List<float>();
    readonly List<float> _cumBend = new List<float>();
    float _pathLength;

    // Index 0 is the cable tip; higher indices go back toward the start anchor.
    Transform[] _chain;
    Rigidbody[] _bodies;
    Grabbable[] _grabbables;
    Behaviour[][] _interactables;
    float _spacing;
    int _inside;
    float _progress;

    Transform _pushHand;
    int _pushLink = -1;
    OVRInput.Controller _pushController = OVRInput.Controller.None;
    Vector3 _pushPrevPos;
    float _pushVelocity;
    OVRInput.Controller _vibratingHand = OVRInput.Controller.None;
    int _slippedLink = -1;
    float _slipRegrabTime;

    void Start()
    {
        if (waypoints == null || waypoints.Length < 2)
        {
            Debug.LogError("[ConduitPathGuide] Necesitas al menos 2 waypoints.", this);
            enabled = false;
            return;
        }

        EnsurePath();
        if (wireController == null) return;

        if (leftHand == null || rightHand == null)
        {
            Debug.LogError("[ConduitPathGuide] Asigna leftHand y rightHand (anclas de los controles).", this);
            enabled = false;
            return;
        }

        BuildChain();
    }

    void OnDisable() => UpdateHaptics(0f);

    void FixedUpdate()
    {
        if (_chain == null || IsComplete) return;

        if (_slippedLink >= 0 && Time.time >= _slipRegrabTime)
        {
            if (_slippedLink >= _inside) SetInteractable(_slippedLink, true);
            _slippedLink = -1;
        }

        if (!IsEngaged)
        {
            if (handFeed) TryEngage();
            return;
        }

        if (handFeed) ApplyFeed(ReadFeed());
        if (!IsEngaged) return;

        PlaceInsideLinks();
        if (_progress >= _pathLength)
            Complete();
    }

    void TryEngage()
    {
        bool near = Vector3.Distance(_chain[0].position, _samples[0]) <= entryRadius;
        if (!near || !IsHeldNearTip())
        {
            Status = FeedStatus.WaitingForTip;
            return;
        }

        if (Vector3.Angle(CableDirection(), TangentAt(0f)) > maxEntryAngle)
        {
            Status = FeedStatus.TipMisaligned;
            return;
        }

        Engage();
    }

    /// <summary>Engancha la punta a la guía jalacables: entra al ducto sin revisar mano ni orientación.</summary>
    public bool HookToGuide()
    {
        if (_chain == null || IsEngaged || IsComplete) return false;
        Engage();
        return true;
    }

    /// <summary>Avance máximo posible sin que el cable que queda afuera deje de alcanzar su inicio.</summary>
    public float MaxReachableProgress()
    {
        // A free start end just gets dragged along, so only the cable's own length limits the feed.
        if (StartEndIsFree())
            return Mathf.Clamp((_chain.Length - 1) * _spacing - 0.0001f, 0f, _pathLength);

        float needed = Vector3.Distance(wireController.starAnchorTemp.position, _samples[0]) + slackMargin;
        int allowedInside = _chain.Length - Mathf.CeilToInt(needed / _spacing);
        return Mathf.Clamp(allowedInside * _spacing - 0.0001f, 0f, _pathLength);
    }

    bool StartEndIsFree()
    {
        var start = wireController.starAnchorTemp;
        var body = start.GetComponent<Rigidbody>();
        if (body == null || body.isKinematic) return false;

        var grabbable = start.GetComponent<Grabbable>();
        if (grabbable != null && grabbable.SelectingPointsCount > 0) return false;

        int last = _chain.Length - 1;
        for (int i = Mathf.Max(_inside, last - tipGrabLinks * 2); i <= last; i++)
            if (_grabbables[i] != null && _grabbables[i].SelectingPointsCount > 0)
                return false;
        return true;
    }

    /// <summary>Lleva la punta hasta esa distancia del recorrido (solo avanza). Lo usa la guía jalacables.</summary>
    public void FeedTo(float distance)
    {
        if (!IsEngaged || IsComplete) return;

        float reachable = MaxReachableProgress();
        Status = distance > reachable ? FeedStatus.OutOfSlack : FeedStatus.Feeding;
        float feed = Mathf.Min(distance, reachable) - _progress;
        if (feed > 0f) Advance(feed);
    }

    void Engage()
    {
        IsEngaged = true;
        _progress = 0f;
        _inside = 0;
        DetachHand();
        CaptureLink(0);
        Status = FeedStatus.Feeding;
        OnEngaged.Invoke();
    }

    float ReadFeed()
    {
        if (_pushHand == null && !TryAttachHand())
            return 0f;

        bool gripping = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, _pushController) >= gripThreshold;
        if (!gripping)
        {
            DetachHand();
            Status = FeedStatus.Feeding;
            return 0f;
        }

        Vector3 entry = _samples[0];
        Vector3 hand = _pushHand.position;
        if (Vector3.Distance(hand, entry) > maxPushReach)
        {
            DetachHand();
            Status = FeedStatus.NeedCloserGrip;
            return 0f;
        }

        Vector3 toEntry = entry - hand;
        Vector3 axis = toEntry.magnitude > 0.06f ? toEntry.normalized : TangentAt(0f);
        float along = Vector3.Dot(hand - _pushPrevPos, axis);
        _pushPrevPos = hand;

        // Smoothed speed rejects tracking jitter (zero-mean) without discarding real motion.
        float dt = Time.fixedDeltaTime;
        _pushVelocity = Mathf.Lerp(_pushVelocity, along / dt, 1f - Mathf.Exp(-dt / pushSmoothing));
        Status = FeedStatus.Feeding;
        if (Mathf.Abs(_pushVelocity) < minPushSpeed) return 0f;

        bool handAtEntry = Vector3.Dot(hand - entry, TangentAt(0f)) > -handStopDistance;
        if (along > 0f && handAtEntry) return 0f;

        // The cable only enters as fast as the bends allow. Pushing faster makes it slip out of
        // the hand instead of feeding less than the hand moved, which would pile it up outside.
        if (_pushVelocity > SpeedLimit() * slipTolerance)
        {
            Slip();
            return 0f;
        }

        return along;
    }

    float Resistance() => 1f + bendResistance * BendAt(_progress) / 90f;

    float SpeedLimit() => maxFeedSpeed / Resistance();

    void Slip()
    {
        int link = _pushLink;
        DetachHand();
        if (link >= _inside)
        {
            SetInteractable(link, false);
            _slippedLink = link;
            _slipRegrabTime = Time.time + slipRegrabDelay;
        }
        Status = FeedStatus.Slipped;
        LastSlipTime = Time.time;
        UpdateHaptics(0f);
        OnSlipped.Invoke();
    }

    bool TryAttachHand()
    {
        int held = NearestHeldOutsideLink();
        if (held < 0)
        {
            Status = FeedStatus.Feeding;
            return false;
        }

        Vector3 linkPosition = _chain[held].position;
        if (Vector3.Distance(linkPosition, _samples[0]) > maxPushReach)
        {
            Status = FeedStatus.NeedCloserGrip;
            return false;
        }

        bool left = Vector3.Distance(linkPosition, leftHand.position) < Vector3.Distance(linkPosition, rightHand.position);
        _pushHand = left ? leftHand : rightHand;
        _pushLink = held;
        _pushController = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        _pushPrevPos = _pushHand.position;
        _pushVelocity = 0f;
        return true;
    }

    void DetachHand()
    {
        _pushHand = null;
        _pushLink = -1;
        _pushController = OVRInput.Controller.None;
        _pushVelocity = 0f;
    }

    void ApplyFeed(float feed)
    {
        float maxStep = maxFeedSpeed * Time.fixedDeltaTime;
        float resistance = Resistance();
        float pushStep = SpeedLimit() * slipTolerance * Time.fixedDeltaTime;

        if (feed > 0f)
        {
            feed = Mathf.Min(feed, pushStep);
            float outsideAfter = (_chain.Length - LinksInsideFor(_progress + feed)) * _spacing;
            float needed = Vector3.Distance(wireController.starAnchorTemp.position, _samples[0]) + slackMargin;
            if (outsideAfter < needed)
            {
                Status = FeedStatus.OutOfSlack;
                feed = 0f;
            }
        }
        else
        {
            feed = Mathf.Max(feed, -maxStep);
        }

        // Vibration grows with the bends and as the hand nears the slipping speed.
        float nearSlip = Mathf.Clamp01(_pushVelocity / (SpeedLimit() * slipTolerance));
        UpdateHaptics(feed > 0f
            ? Mathf.Clamp(0.15f + 0.25f * (resistance - 1f) + 0.35f * nearSlip * nearSlip, 0.15f, 0.9f)
            : 0f);

        Advance(feed);
    }

    void Advance(float feed)
    {
        _progress = Mathf.Clamp(_progress + feed, 0f, _pathLength);
        int target = LinksInsideFor(_progress);
        while (_inside < target) CaptureLink(_inside);
        while (_inside > target && _inside > 1) ReleaseLink(_inside - 1);

        if (_progress <= 0f && feed < 0f)
            Disengage();
    }

    bool IsHeldNearTip()
    {
        int last = Mathf.Min(tipGrabLinks, _chain.Length - 1);
        for (int i = 0; i <= last; i++)
            if (_grabbables[i] != null && _grabbables[i].SelectingPointsCount > 0)
                return true;
        return false;
    }

    Vector3 CableDirection()
    {
        Vector3 direction = _chain[0].position - _chain[Mathf.Min(3, _chain.Length - 1)].position;
        return direction.sqrMagnitude > 1e-6f ? direction.normalized : _chain[0].forward;
    }

    int LinksInsideFor(float progress) => Mathf.Min(_chain.Length, 1 + Mathf.FloorToInt(progress / _spacing));

    int NearestHeldOutsideLink()
    {
        for (int i = _inside; i < _chain.Length; i++)
            if (_grabbables[i] != null && _grabbables[i].SelectingPointsCount > 0)
                return i;
        return -1;
    }

    void CaptureLink(int index)
    {
        // Disabling the interactables releases the hand; Grabbable then restores
        // isKinematic, so the conduit re-locks it afterwards (and every physics step).
        SetInteractable(index, false);

        var body = _bodies[index];
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.isKinematic = true;
        _inside = index + 1;
    }

    void ReleaseLink(int index)
    {
        _bodies[index].isKinematic = false;
        if (index != _slippedLink) SetInteractable(index, true);
        _inside = index;
    }

    void SetInteractable(int index, bool enabled)
    {
        foreach (var interactable in _interactables[index])
            interactable.enabled = enabled;
    }

    void Disengage()
    {
        while (_inside > 0) ReleaseLink(_inside - 1);
        IsEngaged = false;
        DetachHand();
        Status = FeedStatus.WaitingForTip;
        UpdateHaptics(0f);
        OnDisengaged.Invoke();
    }

    void PlaceInsideLinks()
    {
        for (int k = 0; k < _inside; k++)
        {
            float distance = Mathf.Max(0f, _progress - k * _spacing);
            var body = _bodies[k];
            body.isKinematic = true;
            body.MovePosition(PositionAt(distance));
            body.MoveRotation(Quaternion.LookRotation(TangentAt(distance)));
        }
    }

    void Complete()
    {
        IsComplete = true;
        Status = FeedStatus.Complete;
        DetachHand();
        UpdateHaptics(0f);

        OnCompleted.Invoke();
    }

    void UpdateHaptics(float amplitude)
    {
        var hand = amplitude > 0f ? _pushController : OVRInput.Controller.None;

        if (_vibratingHand != OVRInput.Controller.None && _vibratingHand != hand)
            OVRInput.SetControllerVibration(0f, 0f, _vibratingHand);
        if (hand != OVRInput.Controller.None)
            OVRInput.SetControllerVibration(0.5f, amplitude, hand);
        _vibratingHand = hand;
    }

    void BuildChain()
    {
        var segments = wireController.segments;
        Transform tip = wireController.endAnchorTemp;
        bool firstIsNearTip = Vector3.Distance(segments[0].position, tip.position)
                              < Vector3.Distance(segments[segments.Count - 1].position, tip.position);

        int count = segments.Count + 1;
        _chain = new Transform[count];
        _bodies = new Rigidbody[count];
        _grabbables = new Grabbable[count];
        _interactables = new Behaviour[count][];

        _chain[0] = tip;
        for (int i = 0; i < segments.Count; i++)
            _chain[i + 1] = firstIsNearTip ? segments[i] : segments[segments.Count - 1 - i];

        float chainLength = 0f;
        for (int i = 0; i < count; i++)
        {
            _bodies[i] = _chain[i].GetComponent<Rigidbody>();
            _grabbables[i] = _chain[i].GetComponent<Grabbable>();
            _interactables[i] = CollectInteractables(_chain[i]);
            if (i >= 2) chainLength += Vector3.Distance(_chain[i - 1].position, _chain[i].position);
        }
        _spacing = Mathf.Max(chainLength / Mathf.Max(1, count - 2), 0.005f);
    }

    static Behaviour[] CollectInteractables(Transform link)
    {
        var found = new List<Behaviour>();
        foreach (var interactable in link.GetComponentsInChildren<IInteractable>(true))
            if (interactable is Behaviour behaviour)
                found.Add(behaviour);
        return found.ToArray();
    }

    void BuildPath()
    {
        _samples.Clear();
        _cumLength.Clear();
        _cumBend.Clear();

        int count = waypoints.Length;
        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = waypoints[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = waypoints[i].position;
            Vector3 p2 = waypoints[i + 1].position;
            Vector3 p3 = waypoints[Mathf.Min(i + 2, count - 1)].position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / sampleSpacing));
            for (int s = i == 0 ? 0 : 1; s <= steps; s++)
                _samples.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
        }

        _cumLength.Add(0f);
        _cumBend.Add(0f);
        for (int i = 1; i < _samples.Count; i++)
        {
            _cumLength.Add(_cumLength[i - 1] + Vector3.Distance(_samples[i - 1], _samples[i]));
            float bend = i >= 2 ? Vector3.Angle(_samples[i - 1] - _samples[i - 2], _samples[i] - _samples[i - 1]) : 0f;
            _cumBend.Add(_cumBend[i - 1] + bend);
        }
        _pathLength = _cumLength[_cumLength.Count - 1];
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Centripetal parameterization avoids loops and overshoot at tight corners.
        float t1 = Knot(p0, p1);
        float t2 = t1 + Knot(p1, p2);
        float t3 = t2 + Knot(p2, p3);
        float u = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = Blend(p0, p1, 0f, t1, u);
        Vector3 a2 = Blend(p1, p2, t1, t2, u);
        Vector3 a3 = Blend(p2, p3, t2, t3, u);
        Vector3 b1 = Blend(a1, a2, 0f, t2, u);
        Vector3 b2 = Blend(a2, a3, t1, t3, u);
        return Blend(b1, b2, t1, t2, u);
    }

    static float Knot(Vector3 a, Vector3 b) => Mathf.Max(Mathf.Sqrt(Vector3.Distance(a, b)), 1e-4f);

    static Vector3 Blend(Vector3 a, Vector3 b, float ta, float tb, float u) =>
        Vector3.LerpUnclamped(a, b, (u - ta) / (tb - ta));

    int SampleIndex(float distance)
    {
        int low = 0;
        int high = _cumLength.Count - 2;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (_cumLength[mid] <= distance) low = mid;
            else high = mid - 1;
        }
        return low;
    }

    void EnsurePath()
    {
        if (_samples.Count < 2) BuildPath();
    }

    public Vector3 PositionAt(float distance)
    {
        EnsurePath();
        distance = Mathf.Clamp(distance, 0f, _pathLength);
        int i = SampleIndex(distance);
        float span = _cumLength[i + 1] - _cumLength[i];
        float t = span > 0f ? (distance - _cumLength[i]) / span : 0f;
        return Vector3.Lerp(_samples[i], _samples[i + 1], t);
    }

    public Vector3 TangentAt(float distance)
    {
        EnsurePath();
        int i = SampleIndex(Mathf.Clamp(distance, 0f, _pathLength));
        return (_samples[i + 1] - _samples[i]).normalized;
    }

    /// <summary>Grados de curva acumulados desde la entrada hasta esa distancia.</summary>
    public float BendAt(float distance)
    {
        EnsurePath();
        return _cumBend[SampleIndex(Mathf.Clamp(distance, 0f, _pathLength))];
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        foreach (var waypoint in waypoints)
            if (waypoint == null) return;

        if (!Application.isPlaying) BuildPath();
        if (_samples.Count < 2) return;

        Gizmos.color = IsComplete ? Color.green : IsEngaged ? Color.cyan : Color.yellow;
        for (int i = 1; i < _samples.Count; i++)
            Gizmos.DrawLine(_samples[i - 1], _samples[i]);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_samples[0], entryRadius);

        if (IsEngaged)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(PositionAt(_progress), 0.02f);
        }
    }
}
