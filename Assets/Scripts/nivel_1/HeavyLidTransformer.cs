using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Hace que la tapa del registro se sienta pesada.
/// Con una mano solo se levanta un poco el lado agarrado: la tapa gira sobre el borde contrario, que sigue apoyado.
/// Con las dos manos se levanta completa, pero sigue a las manos con retraso y sube despacio.
/// Si se queda en el aire con una sola mano, se resbala y cae. Vibra los controles y suena al caer.
/// Se asigna como transformador de una y de dos manos en el Grabbable.
/// </summary>
[RequireComponent(typeof(Grabbable), typeof(BoxCollider))]
public class HeavyLidTransformer : MonoBehaviour, ITransformer
{
    [Header("Una mano")]
    [Tooltip("Lo más que sube el lado agarrado con una sola mano.")]
    public float maxEdgeLift = 0.06f;
    [Tooltip("Si la mano sube esto por encima de lo que permite la tapa, cuenta como forcejeo (muestra el aviso).")]
    public float strainExtra = 0.04f;
    [Tooltip("Distancia libre bajo la tapa a partir de la cual se considera que está en el aire.")]
    public float supportGap = 0.05f;

    [Header("Dos manos")]
    [Tooltip("Separación mínima entre las manos; más juntas (en la misma agarradera) no la levantan.")]
    public float minHandSpread = 0.4f;
    [Tooltip("Rapidez máxima con la que la tapa sigue a las manos (m/s).")]
    public float maxSpeed = 1.2f;
    [Tooltip("Rapidez máxima de subida (m/s): por el peso sube más lento de lo que baja.")]
    public float maxLiftSpeed = 0.45f;
    [Tooltip("Qué tan rápido alcanza la pose de las manos (más alto = menos retraso).")]
    public float followSharpness = 6f;
    [Tooltip("Giro máximo (grados/s).")]
    public float maxTurnSpeed = 90f;
    [Tooltip("Cuánto se cuelga la tapa por debajo de las manos.")]
    public float sag = 0.025f;

    [Header("Vibración")]
    [Range(0f, 1f)] public float grabPulse = 0.5f;
    [Range(0f, 1f)] public float liftHum = 0.25f;
    [Range(0f, 1f)] public float strainPulse = 0.8f;

    [Header("Golpe al caer")]
    [Tooltip("Velocidad de impacto mínima para el golpe.")]
    public float thudSpeed = 0.6f;
    [Range(0f, 1f)] public float thudVolume = 0.8f;
    [Tooltip("Distancia máxima de una mano a la tapa para que vibre con el golpe.")]
    public float thudHapticRange = 1.5f;

    /// <summary>Último momento en que se intentó levantar la tapa con una sola mano.</summary>
    public float LastStrainTime { get; private set; } = float.NegativeInfinity;

    IGrabbable grabbable;
    BoxCollider box;
    Rigidbody body;
    AudioSource thudSource;
    Transform leftAnchor, rightAnchor;
    readonly List<HandGrabInteractable> handGrabs = new List<HandGrabInteractable>();
    readonly List<GrabInteractable> grabs = new List<GrabInteractable>();

    enum Mode { None, OneHand, TwoHands }
    Mode mode;

    // Una mano
    Vector3 pivot, pivotAxis, startPosition;
    Quaternion startRotation;
    float startHandY, armLength;

    // Dos manos
    Vector3 relPosition;
    Quaternion relRotation;
    float lastUpdateTime, sagIn;

    float hapticUntil;
    float lastThudTime;

    public void Initialize(IGrabbable owner)
    {
        grabbable = owner;
        box = GetComponent<BoxCollider>();
        body = GetComponent<Rigidbody>();
        GetComponentsInChildren(true, handGrabs);
        GetComponentsInChildren(true, grabs);
    }

    void Awake()
    {
        thudSource = gameObject.AddComponent<AudioSource>();
        thudSource.playOnAwake = false;
        thudSource.spatialBlend = 1f;
        thudSource.minDistance = 0.5f;
        thudSource.maxDistance = 10f;
        thudSource.clip = MakeThudClip();
    }

    void Start()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null) return;
        leftAnchor = rig.leftHandAnchor;
        rightAnchor = rig.rightHandAnchor;
    }

    void OnDisable() => StopHaptics();

    public void BeginTransform()
    {
        lastUpdateTime = Time.time;
        var points = grabbable.GrabPoints;

        if (points.Count >= 2)
        {
            mode = Mode.TwoHands;
            sagIn = 0f;
            startPosition = transform.position;
            GetHandFrame(points[0].position, points[1].position, out Vector3 origin, out Quaternion frame);
            Quaternion inverse = Quaternion.Inverse(frame);
            relPosition = inverse * (transform.position - origin);
            relRotation = inverse * transform.rotation;
            Pulse(grabPulse, 0.12f, both: true);
            return;
        }

        mode = Mode.OneHand;
        Vector3 hand = points[0].position;

        if (!IsSupported())
        {
            // Una mano no aguanta la tapa en el aire: se resbala.
            mode = Mode.None;
            LastStrainTime = Time.time;
            Pulse(strainPulse, 0.2f, both: false, at: hand);
            ReleaseAll();
            return;
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
        startHandY = hand.y;
        SetUpPivot(hand);
        Pulse(grabPulse, 0.12f, both: false, at: hand);
    }

    public void UpdateTransform()
    {
        float dt = Mathf.Max(0f, Time.time - lastUpdateTime);
        lastUpdateTime = Time.time;
        var points = grabbable.GrabPoints;

        if (mode == Mode.OneHand && points.Count >= 1) UpdateOneHand(points[0].position);
        else if (mode == Mode.TwoHands && points.Count >= 2) UpdateTwoHands(points[0].position, points[1].position, dt);
    }

    public void EndTransform()
    {
        mode = Mode.None;
        StopHaptics();
    }

    void Update()
    {
        if (hapticUntil > 0f && Time.time >= hapticUntil) StopHaptics();
        // La tapa sigue acercándose a las manos aunque estén quietas (sin eventos de movimiento).
        if (mode == Mode.TwoHands) UpdateTransform();
    }

    // ---------- Una mano: gira sobre el borde contrario ----------

    void SetUpPivot(Vector3 hand)
    {
        Vector3 c = box.center, half = box.size * 0.5f;
        Vector3 g = transform.InverseTransformPoint(hand) - c;
        bool alongX = Mathf.Abs(g.x) / half.x >= Mathf.Abs(g.z) / half.z;

        Vector3 edgeLocal = c + new Vector3(0f, -half.y, 0f);
        Vector3 axisLocal;
        if (alongX)
        {
            edgeLocal.x -= Mathf.Sign(g.x) * half.x;
            axisLocal = Vector3.forward;
        }
        else
        {
            edgeLocal.z -= Mathf.Sign(g.z) * half.z;
            axisLocal = Vector3.right;
        }

        pivot = transform.TransformPoint(edgeLocal);
        pivotAxis = transform.TransformDirection(axisLocal).normalized;

        Vector3 arm = Vector3.ProjectOnPlane(hand - pivot, pivotAxis);
        armLength = Mathf.Max(0.1f, arm.magnitude);
        // Gira en el sentido que sube el lado agarrado.
        if ((Quaternion.AngleAxis(1f, pivotAxis) * arm).y < arm.y) pivotAxis = -pivotAxis;
    }

    void UpdateOneHand(Vector3 hand)
    {
        float wanted = hand.y - startHandY;
        float lift = Mathf.Clamp(wanted, 0f, maxEdgeLift);
        float angle = Mathf.Asin(Mathf.Clamp(lift / armLength, -1f, 1f)) * Mathf.Rad2Deg;

        Quaternion turn = Quaternion.AngleAxis(angle, pivotAxis);
        transform.SetPositionAndRotation(pivot + turn * (startPosition - pivot), turn * startRotation);

        bool straining = wanted > maxEdgeLift + strainExtra;
        if (straining)
        {
            LastStrainTime = Time.time;
            Vibrate(strainPulse, NearestController(hand));
        }
        else
        {
            Vibrate(lift > 0.005f ? liftHum * (0.5f + 0.5f * lift / maxEdgeLift) : 0f, NearestController(hand));
        }
    }

    // ---------- Dos manos: la sigue con retraso ----------

    void UpdateTwoHands(Vector3 a, Vector3 b, float dt)
    {
        if (Vector3.Distance(a, b) < minHandSpread)
        {
            // Las dos manos en la misma agarradera: no hay forma de cargarla así.
            LastStrainTime = Time.time;
            GetHandFrame(a, b, out Vector3 o, out Quaternion f);
            Quaternion inv = Quaternion.Inverse(f);
            relPosition = inv * (transform.position - o);
            relRotation = inv * transform.rotation;
            return;
        }

        GetHandFrame(a, b, out Vector3 origin, out Quaternion frame);
        // Solo se cuelga ya levantada, para no hundirse en el registro al agarrarla.
        float raised = Mathf.Clamp01((transform.position.y - startPosition.y) / 0.1f);
        sagIn = Mathf.MoveTowards(sagIn, raised, dt / 0.4f);
        Vector3 targetPosition = origin + frame * relPosition + Vector3.down * (sag * sagIn);
        Quaternion targetRotation = frame * relRotation;

        Vector3 current = transform.position;
        Vector3 step = (targetPosition - current) * (1f - Mathf.Exp(-followSharpness * dt));
        step = Vector3.ClampMagnitude(step, maxSpeed * dt);
        if (step.y > 0f) step.y = Mathf.Min(step.y, maxLiftSpeed * dt);
        Vector3 next = current + step;

        Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation,
            Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-followSharpness * dt)),
            maxTurnSpeed * dt);
        transform.SetPositionAndRotation(next, nextRotation);

        float moving = dt > 0f ? (step.magnitude / dt) / maxSpeed : 0f;
        float hum = moving > 0.1f ? liftHum * Mathf.Clamp01(0.4f + moving) : 0f;
        Vibrate(hum, OVRInput.Controller.LTouch);
        Vibrate(hum, OVRInput.Controller.RTouch);
    }

    static void GetHandFrame(Vector3 a, Vector3 b, out Vector3 origin, out Quaternion frame)
    {
        origin = (a + b) * 0.5f;
        Vector3 right = b - a;
        Vector3 forward = Vector3.Cross(right, Vector3.up);
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        forward.Normalize();
        frame = Quaternion.LookRotation(forward, Vector3.Cross(forward, right.normalized));
    }

    // ---------- Apoyo, soltar ----------

    bool IsSupported()
    {
        Vector3 c = box.center, half = box.size * 0.5f;
        Vector3[] corners =
        {
            new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
            new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z), new Vector3(0f, -half.y, 0f)
        };
        foreach (var corner in corners)
        {
            Vector3 from = transform.TransformPoint(c + corner) + Vector3.up * 0.02f;
            var hits = Physics.RaycastAll(from, Vector3.down, supportGap + 0.02f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
                if (hit.rigidbody != body) return true;
        }
        return false;
    }

    void ReleaseAll()
    {
        foreach (var interactable in handGrabs)
            foreach (var interactor in new List<HandGrabInteractor>(interactable.SelectingInteractors))
                interactor.ForceRelease();
        foreach (var interactable in grabs)
            foreach (var interactor in new List<GrabInteractor>(interactable.SelectingInteractors))
                interactor.ForceRelease();
    }

    // ---------- Golpe ----------

    void OnCollisionEnter(Collision collision)
    {
        if (grabbable == null || grabbable.GrabPoints.Count > 0) return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < thudSpeed || Time.time - lastThudTime < 0.25f) return;
        lastThudTime = Time.time;

        float strength = Mathf.Clamp01(speed / 3f);
        thudSource.pitch = Random.Range(0.9f, 1.05f);
        thudSource.PlayOneShot(thudSource.clip, thudVolume * (0.4f + 0.6f * strength));

        foreach (var anchor in new[] { leftAnchor, rightAnchor })
        {
            if (anchor == null || Vector3.Distance(anchor.position, transform.position) > thudHapticRange) continue;
            OVRInput.SetControllerVibration(0.3f, 0.4f + 0.5f * strength,
                anchor == leftAnchor ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
        }
        hapticUntil = Time.time + 0.15f;
    }

    static AudioClip MakeThudClip()
    {
        const int rate = 22050;
        int samples = (int)(rate * 0.35f);
        var data = new float[samples];
        var random = new System.Random(7);
        float low = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (noise - low) * 0.08f;
            float body = Mathf.Sin(2f * Mathf.PI * (55f + 40f * Mathf.Exp(-t * 30f)) * t);
            data[i] = (0.7f * body * Mathf.Exp(-t * 14f) + 1.6f * low * Mathf.Exp(-t * 25f)) * 0.9f;
        }
        var clip = AudioClip.Create("GolpeTapa", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ---------- Vibración ----------

    OVRInput.Controller NearestController(Vector3 point)
    {
        if (leftAnchor == null || rightAnchor == null) return OVRInput.Controller.RTouch;
        return Vector3.Distance(point, leftAnchor.position) < Vector3.Distance(point, rightAnchor.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
    }

    void Pulse(float amplitude, float seconds, bool both, Vector3 at = default)
    {
        if (both)
        {
            OVRInput.SetControllerVibration(0.4f, amplitude, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0.4f, amplitude, OVRInput.Controller.RTouch);
        }
        else
        {
            OVRInput.SetControllerVibration(0.4f, amplitude, NearestController(at));
        }
        hapticUntil = Time.time + seconds;
    }

    void Vibrate(float amplitude, OVRInput.Controller controller)
    {
        if (Time.time < hapticUntil) return; // deja terminar un pulso
        OVRInput.SetControllerVibration(amplitude > 0f ? 0.3f : 0f, amplitude, controller);
    }

    void StopHaptics()
    {
        hapticUntil = 0f;
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}
