using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Regresa el objeto a su pose inicial si cae por debajo de cierta distancia (por ejemplo, fuera del mapa).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ResetIfFallen : MonoBehaviour
{
    [Tooltip("Metros por debajo de la posición inicial a partir de los cuales el objeto regresa.")]
    public float fallDistance = 2f;

    Rigidbody body;
    Vector3 startPosition;
    Quaternion startRotation;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        if (transform.position.y > startPosition.y - fallDistance || body.IsLocked()) return;

        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        body.position = startPosition;
        body.rotation = startRotation;
    }
}
