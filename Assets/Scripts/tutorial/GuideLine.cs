using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GuideLine : MonoBehaviour
{
    public Transform head;
    [Tooltip("Raíz del jugador: sus colliders se ignoran al buscar el piso.")]
    public Transform ignoreRoot;
    public float startAhead = 0.4f;
    [Tooltip("Distancia a la que un punto de paso se da por alcanzado.")]
    public float waypointReachRadius = 0.8f;
    public float hideWithin = 0.8f;
    public float floorOffset = 0.05f;
    public float scrollSpeed = 1.5f;

    static readonly Transform[] NoWaypoints = new Transform[0];

    readonly RaycastHit[] hits = new RaycastHit[8];
    readonly List<Vector3> points = new List<Vector3>();
    LineRenderer line;
    Transform target;
    Transform[] waypoints = NoWaypoints;
    int nextWaypoint;
    float scroll;
    float floorY;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.enabled = false;
    }

    public void Show(Transform destination, Transform[] pathPoints)
    {
        target = destination;
        waypoints = pathPoints ?? NoWaypoints;
        nextWaypoint = 0;
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

        Vector3 player = Flat(head.position);
        while (nextWaypoint < waypoints.Length &&
               Vector3.Distance(player, Flat(waypoints[nextWaypoint].position)) <= waypointReachRadius)
            nextWaypoint++;

        floorY = FindFloorY(floorY);

        points.Clear();
        Vector3 firstTarget = nextWaypoint < waypoints.Length ? waypoints[nextWaypoint].position : target.position;
        points.Add(OnFloor(player + Vector3.ClampMagnitude(Flat(firstTarget) - player, startAhead)));
        for (int i = nextWaypoint; i < waypoints.Length; i++)
            points.Add(OnFloor(waypoints[i].position));
        points.Add(OnFloor(target.position));

        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);

        line.enabled = length > hideWithin;
        if (!line.enabled) return;

        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
            line.SetPosition(i, points[i]);

        scroll = Mathf.Repeat(scroll - scrollSpeed * Time.deltaTime, 1f);
        line.material.mainTextureOffset = new Vector2(scroll, 0f);
    }

    Vector3 OnFloor(Vector3 position)
    {
        position.y = floorY + floorOffset;
        return position;
    }

    static Vector3 Flat(Vector3 position)
    {
        position.y = 0f;
        return position;
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
