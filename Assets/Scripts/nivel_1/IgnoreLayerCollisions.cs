using UnityEngine;

/// <summary>
/// Hace que los colliders de este objeto no choquen con los de ciertas capas
/// (por ejemplo, que una herramienta no empuje los cables).
/// </summary>
public class IgnoreLayerCollisions : MonoBehaviour
{
    public LayerMask layers;
    [Tooltip("Objetos cuyos colliders también se ignoran, sin importar su capa (por ejemplo, cables en la capa Default).")]
    public Transform[] alsoIgnore = new Transform[0];

    void Start()
    {
        var own = GetComponentsInChildren<Collider>(true);
        foreach (var root in alsoIgnore)
            foreach (var other in root.GetComponentsInChildren<Collider>(true))
                foreach (var mine in own)
                    Physics.IgnoreCollision(mine, other, true);

        foreach (var other in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((layers.value & (1 << other.gameObject.layer)) == 0) continue;
            foreach (var mine in own)
                Physics.IgnoreCollision(mine, other, true);
        }
    }
}
