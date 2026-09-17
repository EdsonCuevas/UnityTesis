using UnityEngine;

/// <summary>
/// Va en un hijo del ancla de un extremo del cable (por ejemplo, la punta pelable) y lo orienta
/// en la dirección en que termina el cable, porque el ancla puede girar distinto que los segmentos.
/// </summary>
public class WireEndAligner : MonoBehaviour
{
    [Tooltip("Segmento unido al ancla de este extremo.")]
    public Transform neighbor;

    void LateUpdate()
    {
        Vector3 direction = transform.parent.position - neighbor.position;
        if (direction.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}
