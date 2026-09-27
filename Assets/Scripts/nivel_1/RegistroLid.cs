using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Tapa del registro con física. Si queda en reposo cerca de su asiento, se acomoda en él.
/// </summary>
[RequireComponent(typeof(Grabbable), typeof(Rigidbody))]
public class RegistroLid : MonoBehaviour
{
    [Tooltip("Pose de la tapa cerrada.")]
    public Transform seat;
    [Tooltip("Distancia horizontal al asiento a partir de la cual el registro cuenta como abierto.")]
    public float openDistance = 0.5f;
    [Tooltip("Distancia horizontal al asiento dentro de la cual la tapa se acomoda sola.")]
    public float snapRadius = 0.25f;
    [Tooltip("Diferencia de altura máxima con el asiento para acomodarse.")]
    public float snapHeight = 0.15f;
    [Tooltip("Segundos quieta antes de acomodarse.")]
    public float restSeconds = 0.3f;
    public float snapDuration = 0.25f;
    [Tooltip("Si cae por debajo del asiento más de esta distancia, vuelve junto al registro.")]
    public float fallLimit = 2f;

    Grabbable grabbable;
    Rigidbody body;
    float restingFor;
    float snapT = -1f;
    Vector3 snapFromPosition;
    Quaternion snapFromRotation;

    public bool IsHeld => grabbable.SelectingPointsCount > 0;
    public bool IsSeated { get; private set; } = true;
    public bool IsOpen => FlatDistanceToSeat() >= openDistance;
    public float OpenAmount01 => Mathf.Clamp01(FlatDistanceToSeat() / openDistance);

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        body = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (IsHeld)
        {
            snapT = -1f;
            restingFor = 0f;
            IsSeated = false;
            return;
        }

        if (snapT >= 0f)
        {
            StepSnap();
            return;
        }

        // Grabbable restores the kinematic state it saved on grab; a lid released mid-snap would stay frozen.
        if (body.isKinematic && !body.IsLocked())
            body.isKinematic = false;

        if (transform.position.y < seat.position.y - fallLimit)
        {
            PlaceBesideSeat();
            return;
        }

        if (IsSeated) return;

        bool resting = body.linearVelocity.sqrMagnitude < 0.0025f && body.angularVelocity.sqrMagnitude < 0.01f;
        restingFor = resting ? restingFor + Time.fixedDeltaTime : 0f;

        if (restingFor >= restSeconds && FlatDistanceToSeat() <= snapRadius
            && Mathf.Abs(transform.position.y - seat.position.y) <= snapHeight)
            BeginSnap();
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
        if (snapT < 1f) return;

        snapT = -1f;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        IsSeated = true;
    }

    void PlaceBesideSeat()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = seat.position + seat.right * (openDistance + 0.3f) + Vector3.up * 0.3f;
        body.rotation = seat.rotation;
    }

    float FlatDistanceToSeat()
    {
        Vector3 delta = transform.position - seat.position;
        delta.y = 0f;
        return delta.magnitude;
    }
}
