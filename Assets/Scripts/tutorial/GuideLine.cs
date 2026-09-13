using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GuideLine : MonoBehaviour
{
    public Transform head;
    [Tooltip("Raíz del jugador: sus colliders se ignoran al buscar el piso.")]
    public Transform ignoreRoot;
    public int pointCount = 20;
    public float startAhead = 0.4f;
    public float hideWithin = 0.8f;
    public float floorOffset = 0.03f;
    public float arcHeight = 0.08f;
    public float scrollSpeed = 1.5f;

    readonly RaycastHit[] hits = new RaycastHit[8];
    LineRenderer line;
    Transform target;
    float scroll;
    float floorY;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = pointCount;
        line.enabled = false;
    }

    public void Show(Transform destination)
    {
        target = destination;
        floorY = head.position.y - 1.6f;
    }

    public void Hide()
    {
        target = null;
        line.enabled = false;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 forward = head.forward;
        forward.y = 0f;
        Vector3 start = head.position + (forward.sqrMagnitude > 1e-4f ? forward.normalized * startAhead : Vector3.zero);
        Vector3 end = target.position;

        floorY = FindFloorY(floorY);
        start.y = floorY + floorOffset;
        end.y = floorY + floorOffset;

        line.enabled = Vector3.Distance(start, end) > hideWithin;
        if (!line.enabled) return;

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            point.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            line.SetPosition(i, point);
        }

        scroll = Mathf.Repeat(scroll - scrollSpeed * Time.deltaTime, 1f);
        line.material.mainTextureOffset = new Vector2(scroll, 0f);
    }

    float FindFloorY(float fallback)
    {
        int count = Physics.RaycastNonAlloc(head.position, Vector3.down, hits, 5f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        float result = fallback;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(ignoreRoot) || hits[i].distance >= nearest) continue;
            nearest = hits[i].distance;
            result = hits[i].point.y;
        }
        return result;
    }
}
