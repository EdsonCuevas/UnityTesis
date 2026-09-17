using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Tapa del registro. Al soltarla cerca de su asiento se acomoda en él;
/// en otro lugar queda plana sobre el piso que tenga debajo.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class RegistroLid : MonoBehaviour
{
    [Tooltip("Pose de la tapa cerrada.")]
    public Transform seat;
    [Tooltip("Distancia horizontal al asiento a partir de la cual el registro cuenta como abierto.")]
    public float openDistance = 0.5f;
    [Tooltip("Distancia horizontal al asiento dentro de la cual la tapa se acomoda sola al soltarla.")]
    public float snapRadius = 0.25f;
    [Tooltip("Capas que se ignoran al buscar el piso (por ejemplo, los cables).")]
    public LayerMask ignoreLayers;

    readonly RaycastHit[] hits = new RaycastHit[16];
    Grabbable grabbable;
    Renderer[] renderers;
    bool wasHeld;

    public bool IsHeld => grabbable.SelectingPointsCount > 0;
    public bool IsSeated { get; private set; } = true;
    public bool IsOpen => FlatDistanceToSeat() >= openDistance;
    public float OpenAmount01 => Mathf.Clamp01(FlatDistanceToSeat() / openDistance);

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        bool held = IsHeld;
        if (held) IsSeated = false;
        else if (wasHeld) Settle();
        wasHeld = held;
    }

    void Settle()
    {
        if (FlatDistanceToSeat() <= snapRadius)
        {
            transform.SetPositionAndRotation(seat.position, seat.rotation);
            IsSeated = true;
            return;
        }

        transform.rotation = Quaternion.Euler(seat.eulerAngles.x, transform.eulerAngles.y, seat.eulerAngles.z);
        if (TryFindGround(out float groundY))
            transform.position += Vector3.up * (groundY - Bounds().min.y);
    }

    bool TryFindGround(out float groundY)
    {
        Bounds bounds = Bounds();
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, 5f, ~ignoreLayers.value, QueryTriggerInteraction.Ignore);

        groundY = 0f;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.distance >= nearest || hit.transform.IsChildOf(transform)) continue;
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
            nearest = hit.distance;
            groundY = hit.point.y;
        }
        return nearest < float.MaxValue;
    }

    Bounds Bounds()
    {
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    float FlatDistanceToSeat()
    {
        Vector3 delta = transform.position - seat.position;
        delta.y = 0f;
        return delta.magnitude;
    }
}
