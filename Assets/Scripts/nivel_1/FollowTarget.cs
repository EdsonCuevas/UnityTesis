using UnityEngine;

/// <summary>Mantiene este objeto sobre un objetivo que se mueve (por ejemplo, una flecha sobre la punta de un cable).</summary>
public class FollowTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 0.12f, 0f);

    void LateUpdate()
    {
        if (target != null)
            transform.position = target.position + worldOffset;
    }
}
