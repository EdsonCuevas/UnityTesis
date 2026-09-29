using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Destornillador para los tornillos de las terminales. Al acercar la punta a un tornillo se acopla solo:
/// se muestra alineado sobre el tornillo y gira con él. Con el gatillo presionado, girar la muñeca a la
/// derecha aprieta y a la izquierda afloja; el giro se mide sobre el eje del control, sin importar cómo
/// se agarró. Sin gatillo la muñeca regresa sin mover el tornillo (como una matraca).
/// </summary>
public class Screwdriver : MonoBehaviour
{
    public Grabbable grabbable;
    [Tooltip("Punta del destornillador; su eje Z apunta hacia afuera de la punta, a lo largo del vástago. Hijo directo de este objeto.")]
    public Transform tip;
    [Tooltip("Malla del destornillador; se oculta mientras se muestra la copia acoplada al tornillo.")]
    public MeshRenderer body;

    [Header("Manos")]
    public Transform leftHand;
    public Transform rightHand;
    [Range(0f, 1f)] public float triggerThreshold = 0.6f;

    [Header("Acople")]
    [Tooltip("Distancia máxima de la punta a la cabeza del tornillo para acoplarse.")]
    public float engageRadius = 0.03f;
    [Tooltip("Ángulo máximo entre el vástago y el eje del tornillo para acoplarse.")]
    public float maxEngageAngle = 60f;
    [Tooltip("Cuánto puede alejarse el control, desde donde se acopló, antes de soltar el tornillo.")]
    public float releaseDistance = 0.08f;
    [Tooltip("Al terminar un tornillo, cuánto más allá del radio de acople hay que alejar la punta para acoplarse a otro.")]
    public float awayDistance = 0.02f;

    public bool IsHeld => grabbable.SelectingPointsCount > 0;
    public bool Engaged => CurrentScrew != null;
    public TerminalScrew CurrentScrew { get; private set; }
    public bool TriggerPressed { get; private set; }
    public float EngagedSince { get; private set; } = float.PositiveInfinity;
    /// <summary>Último momento en que el tornillo giró.</summary>
    public float LastTurnTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Último momento en que el tornillo se aflojó.</summary>
    public float LastLoosenTime { get; private set; } = float.NegativeInfinity;
    /// <summary>Último momento en que la punta estuvo en un tornillo pero chueca.</summary>
    public float LastMisalignedTime { get; private set; } = float.NegativeInfinity;

    Transform ghost;
    Transform hand;
    TerminalScrew finishedScrew;
    OVRInput.Controller controller;
    Vector3 handAtEngage;
    Quaternion ghostBase;
    float turnsAtEngage;
    Vector3 prevUp;
    int quarterTurns;
    bool atStop;
    OVRInput.Controller vibratingHand = OVRInput.Controller.None;
    float vibrateUntil;

    void Awake()
    {
        // Visual copy shown on the screw, so the grabbed object is never moved against the Grabbable.
        ghost = new GameObject(name + "_Acoplado").transform;
        ghost.localScale = transform.lossyScale;
        ghost.gameObject.AddComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
        ghost.gameObject.AddComponent<MeshRenderer>().sharedMaterials = body.sharedMaterials;
        ghost.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        Disengage();
        StopVibration();
    }

    void Update()
    {
        if (vibratingHand != OVRInput.Controller.None && Time.time >= vibrateUntil) StopVibration();

        if (!IsHeld)
        {
            TriggerPressed = false;
            Disengage();
            return;
        }

        if (!Engaged)
        {
            TriggerPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, NearestController()) >= triggerThreshold;
            TryEngage();
            return;
        }

        // Once the terminal locks, let go so the finished screw can't be worked any more.
        if (CurrentScrew.Locked || Vector3.Distance(hand.position, handAtEngage) > releaseDistance)
        {
            // Otherwise the next connector screw, 1.35 cm away, would couple while still twisting.
            if (CurrentScrew.Locked) finishedScrew = CurrentScrew;
            Disengage();
            return;
        }

        TriggerPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller) >= triggerThreshold;
        Vector3 up = Vector3.ProjectOnPlane(hand.up, hand.forward);
        if (!TriggerPressed || up.sqrMagnitude < 0.01f)
        {
            prevUp = Vector3.zero;
            return;
        }

        if (prevUp != Vector3.zero)
        {
            // Twisting right is clockwise seen from behind the hand: negative around its forward axis.
            float wanted = -Vector3.SignedAngle(prevUp, up, hand.forward);
            float turned = CurrentScrew.Turn(wanted);
            if (Mathf.Abs(turned) > 0.01f)
            {
                LastTurnTime = Time.time;
                if (turned < 0f) LastLoosenTime = Time.time;
            }
            UpdateHaptics(wanted);
        }
        prevUp = up;
    }

    void LateUpdate()
    {
        if (!Engaged) return;

        // Tightening is clockwise seen from the front: positive around the screw's outward axis.
        Vector3 axis = CurrentScrew.Axis;
        Quaternion rotation = Quaternion.AngleAxis((CurrentScrew.Turns - turnsAtEngage) * 360f, axis) * ghostBase;
        Vector3 tipOffset = Vector3.Scale(tip.localPosition, transform.lossyScale);
        ghost.SetPositionAndRotation(CurrentScrew.HeadPoint - rotation * tipOffset, rotation);
    }

    void TryEngage()
    {
        if (finishedScrew != null)
        {
            if (Vector3.Distance(tip.position, finishedScrew.HeadPoint) < engageRadius + awayDistance) return;
            finishedScrew = null;
        }

        // The neutral connector screws sit close together, so take the nearest one.
        TerminalScrew nearest = null;
        float nearestDistance = engageRadius;
        foreach (var screw in TerminalScrew.All)
        {
            if (screw.Locked) continue;
            float distance = Vector3.Distance(tip.position, screw.HeadPoint);
            if (distance > nearestDistance) continue;
            nearest = screw;
            nearestDistance = distance;
        }
        if (nearest == null) return;

        // The shaft points into the screw, against its outward axis.
        if (Vector3.Angle(tip.forward, -nearest.Axis) > maxEngageAngle)
        {
            LastMisalignedTime = Time.time;
            return;
        }

        controller = NearestController();
        hand = controller == OVRInput.Controller.LTouch ? leftHand : rightHand;
        handAtEngage = hand.position;
        CurrentScrew = nearest;
        EngagedSince = Time.time;
        turnsAtEngage = nearest.Turns;
        prevUp = Vector3.zero;
        quarterTurns = Mathf.FloorToInt(nearest.Turns * 4f);
        atStop = nearest.IsTight;

        // Keep the grip's roll so the copy doesn't jump, but line the shaft up with the screw.
        ghostBase = Quaternion.FromToRotation(-tip.forward, nearest.Axis) * transform.rotation;
        ghost.gameObject.SetActive(true);
        body.enabled = false;
        LateUpdate();
        Vibrate(0.4f, 0.06f);
    }

    void Disengage()
    {
        if (!Engaged) return;
        CurrentScrew = null;
        EngagedSince = float.PositiveInfinity;
        prevUp = Vector3.zero;
        ghost.gameObject.SetActive(false);
        body.enabled = true;
    }

    void UpdateHaptics(float wanted)
    {
        // A tick every quarter turn, and a firm knock when the screw bottoms out.
        int quarters = Mathf.FloorToInt(CurrentScrew.Turns * 4f);
        if (quarters != quarterTurns) Vibrate(0.35f, 0.05f);
        quarterTurns = quarters;

        if (CurrentScrew.IsTight && wanted > 0f && !atStop) Vibrate(0.8f, 0.2f);
        atStop = CurrentScrew.IsTight;
    }

    void Vibrate(float amplitude, float seconds)
    {
        var target = Engaged ? controller : NearestController();
        if (vibratingHand != OVRInput.Controller.None && vibratingHand != target) StopVibration();
        OVRInput.SetControllerVibration(0.5f, amplitude, target);
        vibratingHand = target;
        vibrateUntil = Time.time + seconds;
    }

    void StopVibration()
    {
        if (vibratingHand == OVRInput.Controller.None) return;
        OVRInput.SetControllerVibration(0f, 0f, vibratingHand);
        vibratingHand = OVRInput.Controller.None;
    }

    OVRInput.Controller NearestController() =>
        Vector3.Distance(transform.position, leftHand.position) < Vector3.Distance(transform.position, rightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
}
