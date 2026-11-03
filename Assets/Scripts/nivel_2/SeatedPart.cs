using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Pieza que se monta a mano en su lugar (la pastilla, la tapa de la caja). Si se suelta cerca de su asiento
/// mientras el paso la acepta, se acomoda sola y queda fija.
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

    /// <summary>Lo activa el paso de montarla, para que no se acomode antes de tiempo.</summary>
    public bool Accepting { get; set; }
    public bool IsSeated { get; private set; }
    public bool IsHeld => grabbable.SelectingPointsCount > 0;
    public float DistanceToSeat => Vector3.Distance(transform.position, seat.position);
    public float LastMissTime { get; private set; } = float.NegativeInfinity;

    Grabbable grabbable;
    Rigidbody body;
    Collider solid;
    bool wasHeld;
    float snapT = -1f;
    Vector3 snapFromPosition;
    Quaternion snapFromRotation;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        body = GetComponent<Rigidbody>();
        solid = GetComponent<Collider>();
        SetGrabbable(false);
        if (startsSeated) Settle();
    }

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
                BeginSnap();
                return;
            }
            if (Accepting && DistanceToSeat <= missRadius) LastMissTime = Time.time;
        }

        // Grabbable restores the kinematic state it saved on grab, so a part taken off its seat would stay frozen.
        if (!IsSeated && body.isKinematic && !body.IsLocked()) body.isKinematic = false;
    }

    void BeginSnap()
    {
        snapT = 0f;
        snapFromPosition = transform.position;
        snapFromRotation = transform.rotation;
        body.isKinematic = true;
    }

    void StepSnap()
    {
        snapT = Mathf.Min(1f, snapT + Time.fixedDeltaTime / snapDuration);
        float eased = Mathf.SmoothStep(0f, 1f, snapT);
        body.MovePosition(Vector3.Lerp(snapFromPosition, seat.position, eased));
        body.MoveRotation(Quaternion.Slerp(snapFromRotation, seat.rotation, eased));
        if (snapT >= 1f)
        {
            snapT = -1f;
            Settle();
        }
    }

    void Settle()
    {
        transform.SetPositionAndRotation(seat.position, seat.rotation);
        body.isKinematic = true;
        if (solid != null && noCollisionWhenSeated) solid.enabled = false;
        IsSeated = true;
    }
}
