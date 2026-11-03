using Oculus.Interaction;
using UnityEngine;

/// <summary>Mantiene este objeto sobre un objetivo que se mueve (por ejemplo, una flecha sobre la punta de un cable).</summary>
public class FollowTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 0.12f, 0f);
    [Tooltip("Oculta este objeto mientras se sostiene esta herramienta. Puede ir vacío.")]
    public Grabbable hideWhileHeld;

    Renderer[] renderers;

    void Awake() => renderers = GetComponentsInChildren<Renderer>(true);

    void LateUpdate()
    {
        if (target != null)
            transform.position = target.position + worldOffset;

        if (hideWhileHeld == null) return;
        bool visible = hideWhileHeld.SelectingPointsCount == 0;
        foreach (var part in renderers)
            part.enabled = visible;
    }
}
