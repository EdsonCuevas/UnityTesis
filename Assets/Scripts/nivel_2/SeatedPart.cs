using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Pieza que se monta a mano en su lugar (la pastilla, la tapa de la caja). Si se suelta cerca de su asiento
/// mientras el paso la acepta, se acomoda sola y queda fija.
/// Con <see cref="hinge"/> asignada, el montaje es en dos tiempos, como una pastilla real: al soltarla queda
/// enganchada de un lado e inclinada, y hay que empujar el otro lado con la mano hasta que truena el clic.
/// </summary>
[RequireComponent(typeof(Grabbable), typeof(Rigidbody))]
public class SeatedPart : MonoBehaviour
{
    [Tooltip("Pose de la pieza ya montada.")]
    public Transform seat;
    [Tooltip("Objeto con el agarre de Meta; se apaga mientras el paso no use la pieza.")]
    public GameObject grabObject;
    public bool startsSeated;
    [Tooltip("Distancia al asiento dentro de la cual la pieza se acomoda al soltarla.")]
    public float snapRadius = 0.08f;
    [Tooltip("Si se suelta más lejos que el radio pero a menos de esta distancia, cuenta como un intento fallido.")]
    public float missRadius = 0.3f;
    public float snapDuration = 0.2f;
    [Tooltip("Apaga su collider ya montada, para que no empuje los cables y herramientas. Una pieza que se vuelve a agarrar lo necesita prendido.")]
    public bool noCollisionWhenSeated;

    [Header("Enganchar y presionar (opcional)")]
    [Tooltip("Bisagra del enganche: su posición es el borde que se engancha y su eje X el eje de giro. Vacío = se acomoda de una vez.")]
    public Transform hinge;
    [Tooltip("Inclinación con la que queda enganchada, en grados alrededor del eje X de la bisagra.")]
    public float hookAngle = 25f;
    [Tooltip("Punto de la pieza que se empuja con la mano (se mueve con ella).")]
    public Transform pressPoint;
    [Tooltip("Distancia de la mano al punto de empuje para que cuente como tocarlo.")]
    public float pressRadius = 0.07f;
    [Tooltip("Inclinación restante a la que la pieza entra sola y truena el clic.")]
    public float clickAngle = 3f;
    public float clickVolume = 0.8f;

    /// <summary>Lo activa el paso de montarla, para que no se acomode antes de tiempo.</summary>
    public bool Accepting { get; set; }
    public bool IsSeated { get; private set; }
    public bool IsHooked => hookState == HookState.Hooked;
    public bool IsHeld => grabbable.SelectingPointsCount > 0;
    /// <summary>Qué tanto se ha empujado ya enganchada: 0 recién enganchada, 1 montada.</summary>
    public float PressProgress => IsSeated ? 1f : IsHooked ? 1f - angle / Mathf.Max(hookAngle, 0.01f) : 0f;
    public bool UsesHook => hinge != null;
    /// <summary>Distancia a la pose donde se suelta: la enganchada si hay bisagra, el asiento si no.</summary>
    public float DistanceToSeat => Vector3.Distance(transform.position, UsesHook ? HookedPosition(hookAngle) : seat.position);
    public float LastMissTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Última vez que una mano la empujó estando enganchada.</summary>
    public float LastPushTime { get; private set; } = float.NegativeInfinity;

    /// <summary>Manos que pueden empujarla; las asigna el paso.</summary>
    public Transform LeftHand { get; set; }
    public Transform RightHand { get; set; }

    enum HookState { None, Hooked }

    Grabbable grabbable;
    Rigidbody body;
    Collider solid;
    AudioSource clickSource;
    AudioClip hookClip, clickClip;
    bool wasHeld;
    float snapT = -1f;
    Vector3 snapFromPosition;
    Quaternion snapFromRotation;
    float snapToAngle;
    HookState hookState;
    float angle;
    Vector3 lastLeft, lastRight;
    bool leftTouching, rightTouching;
    float hapticUntil;
    OVRInput.Controller vibrating = OVRInput.Controller.None;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        body = GetComponent<Rigidbody>();
        solid = GetComponent<Collider>();
        if (UsesHook)
        {
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 1f;
            clickSource.minDistance = 0.4f;
            clickSource.maxDistance = 8f;
            hookClip = MakeClickClip("EnganchePieza", 0.6f, 2600f);
            clickClip = MakeClickClip("ClicPieza", 1f, 4200f);
        }
        SetGrabbable(false);
        if (startsSeated) Settle();
    }

    void OnDisable() => StopHaptics();

    public void SetGrabbable(bool value)
    {
        if (grabObject != null) grabObject.SetActive(value);
    }

    void FixedUpdate()
    {
        if (IsHeld)
        {
            wasHeld = true;
            snapT = -1f;
            IsSeated = false;
            hookState = HookState.None;
            if (solid != null) solid.enabled = true;
            return;
        }

        if (snapT >= 0f)
        {
            StepSnap();
            return;
        }

        if (wasHeld)
        {
            wasHeld = false;
            if (Accepting && DistanceToSeat <= snapRadius)
            {
                BeginSnap(UsesHook ? hookAngle : 0f);
                return;
            }
            if (Accepting && DistanceToSeat <= missRadius) LastMissTime = Time.time;
        }

        if (IsHooked)
        {
            StepPress();
            return;
        }

        // Grabbable restores the kinematic state it saved on grab, so a part taken off its seat would stay frozen.
        if (!IsSeated && body.isKinematic && !body.IsLocked()) body.isKinematic = false;
    }

    void Update()
    {
        if (hapticUntil > 0f && Time.time >= hapticUntil) StopHaptics();
    }

    // ---------- Acomodo ----------

    void BeginSnap(float toAngle)
    {
        snapT = 0f;
        snapToAngle = toAngle;
        snapFromPosition = transform.position;
        snapFromRotation = transform.rotation;
        body.isKinematic = true;
        // Ya no la sostiene nadie: que no empuje los cables al entrar.
        if (solid != null && UsesHook) solid.enabled = false;
    }

    void StepSnap()
    {
        snapT = Mathf.Min(1f, snapT + Time.fixedDeltaTime / snapDuration);
        float eased = Mathf.SmoothStep(0f, 1f, snapT);
        body.MovePosition(Vector3.Lerp(snapFromPosition, HookedPosition(snapToAngle), eased));
        body.MoveRotation(Quaternion.Slerp(snapFromRotation, HookedRotation(snapToAngle), eased));
        if (snapT < 1f) return;

        snapT = -1f;
        if (snapToAngle > 0f) Hook();
        else Settle();
    }

    void Hook()
    {
        hookState = HookState.Hooked;
        angle = hookAngle;
        transform.SetPositionAndRotation(HookedPosition(angle), HookedRotation(angle));
        leftTouching = rightTouching = false;
        PlayClick(hookClip, 0.6f);
    }

    void Settle()
    {
        bool clicked = IsHooked;
        hookState = HookState.None;
        transform.SetPositionAndRotation(seat.position, seat.rotation);
        body.isKinematic = true;
        if (solid != null && noCollisionWhenSeated) solid.enabled = false;
        IsSeated = true;
        if (clicked)
        {
            PlayClick(clickClip, 1f);
            Pulse(0.9f, 0.12f, NearestController(transform.position));
        }
    }

    // ---------- Empujar ----------

    void StepPress()
    {
        // La mano que toca el punto de empuje y se mueve hacia adentro cierra la pieza tanto como avanzó.
        Vector3 press = pressPoint != null ? pressPoint.position : transform.position;
        float radius = Vector3.Distance(press, hinge.position);
        Vector3 inward = (PressPointAt(angle - 1f, press) - press).normalized;

        float pushed = Mathf.Max(Push(LeftHand, ref lastLeft, ref leftTouching, press, inward),
                                 Push(RightHand, ref lastRight, ref rightTouching, press, inward));
        if (pushed <= 0f) return;

        LastPushTime = Time.time;
        angle = Mathf.Max(0f, angle - pushed / Mathf.Max(radius, 0.01f) * Mathf.Rad2Deg);
        if (angle <= clickAngle)
        {
            BeginSnap(0f);
            return;
        }
        body.MovePosition(HookedPosition(angle));
        body.MoveRotation(HookedRotation(angle));
        Vibrate(0.15f + 0.25f * PressProgress, NearestController(press));
    }

    float Push(Transform hand, ref Vector3 last, ref bool touching, Vector3 press, Vector3 inward)
    {
        if (hand == null) return 0f;
        Vector3 now = hand.position;
        bool touch = Vector3.Distance(now, press) <= pressRadius;
        float moved = touching && touch ? Vector3.Dot(now - last, inward) : 0f;
        touching = touch;
        last = now;
        return moved;
    }

    Vector3 PressPointAt(float atAngle, Vector3 currentPress)
    {
        Quaternion delta = Quaternion.AngleAxis(atAngle - angle, hinge.right);
        return hinge.position + delta * (currentPress - hinge.position);
    }

    Vector3 HookedPosition(float atAngle) =>
        hinge == null ? seat.position : hinge.position + Quaternion.AngleAxis(atAngle, hinge.right) * (seat.position - hinge.position);

    Quaternion HookedRotation(float atAngle) =>
        hinge == null ? seat.rotation : Quaternion.AngleAxis(atAngle, hinge.right) * seat.rotation;

    // ---------- Sonido y vibración ----------

    void PlayClick(AudioClip clip, float volume)
    {
        if (clickSource != null && clip != null) clickSource.PlayOneShot(clip, clickVolume * volume);
    }

    OVRInput.Controller NearestController(Vector3 point)
    {
        if (LeftHand == null || RightHand == null) return OVRInput.Controller.RTouch;
        return Vector3.Distance(point, LeftHand.position) < Vector3.Distance(point, RightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
    }

    void Pulse(float amplitude, float seconds, OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0.5f, amplitude, controller);
        vibrating = controller;
        hapticUntil = Time.time + seconds;
    }

    void Vibrate(float amplitude, OVRInput.Controller controller)
    {
        if (vibrating != OVRInput.Controller.None && vibrating != controller) StopHaptics();
        OVRInput.SetControllerVibration(0.3f, amplitude, controller);
        vibrating = controller;
        hapticUntil = Time.time + 0.06f; // se apaga sola si la mano deja de empujar
    }

    void StopHaptics()
    {
        hapticUntil = 0f;
        if (vibrating == OVRInput.Controller.None) return;
        OVRInput.SetControllerVibration(0f, 0f, vibrating);
        vibrating = OVRInput.Controller.None;
    }

    static AudioClip MakeClickClip(string name, float strength, float pitch)
    {
        const int rate = 22050;
        int samples = (int)(rate * 0.09f);
        var data = new float[samples];
        var random = new System.Random(11);
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float snap = Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-t * 160f);
            float body = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 60f);
            data[i] = (0.6f * snap + 0.35f * body + 0.5f * noise * Mathf.Exp(-t * 300f)) * strength;
        }
        var clip = AudioClip.Create(name, samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
