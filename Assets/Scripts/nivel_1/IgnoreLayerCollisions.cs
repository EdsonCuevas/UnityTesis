using UnityEngine;

/// <summary>
/// Hace que los colliders de este objeto no choquen con los de ciertas capas
/// (por ejemplo, que una herramienta no empuje los cables).
/// </summary>
public class IgnoreLayerCollisions : MonoBehaviour
{
    public LayerMask layers;

    void Start()
    {
        var own = GetComponentsInChildren<Collider>(true);
        foreach (var other in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((layers.value & (1 << other.gameObject.layer)) == 0) continue;
            foreach (var mine in own)
                Physics.IgnoreCollision(mine, other, true);
        }
    }
}
