using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zapata de tornillo con gancho: al meter el cable, el cobre pelado baja por detrás de la cabeza del
/// tornillo y da vuelta alrededor del vástago, en el sentido en que se aprieta, como se hace en una
/// instalación real. El tornillo, al apretarse, prensa el gancho contra la placa.
/// Es solo visual: la lógica de meter, apretar y jalar sigue en <see cref="MeterTerminal"/>.
/// </summary>
[RequireComponent(typeof(MeterTerminal))]
public class TerminalWireHook : MonoBehaviour
{
    [Tooltip("Radio del vástago del tornillo.")]
    public float shankRadius = 0.0035f;
    [Tooltip("Radio del cobre del gancho.")]
    public float copperRadius = 0.0018f;
    [Tooltip("Cara de la placa, medida sobre el eje del tornillo desde el pivote de la cabeza floja (negativo = hacia adentro).")]
    public float plateDepth = -0.0056f;
    [Tooltip("Grados que da la vuelta el cobre alrededor del vástago.")]
    public float wrapDegrees = 290f;
    [Tooltip("Tiempo en que el cobre recto toma la forma del gancho.")]
    public float bendSeconds = 0.35f;
    [Tooltip("Nombre del cilindro de cobre recto de la punta, que se oculta mientras se ve el gancho.")]
    public string copperName = "Cobre";
    public int ringSides = 10;
    public float sampleStep = 0.001f;

    class Hook
    {
        public GameObject go;
        public Mesh mesh;
        public Renderer straightCopper;
        public float startTime;
        public bool settled;
    }

    MeterTerminal terminal;
    readonly Dictionary<WireStripper, Hook> hooks = new Dictionary<WireStripper, Hook>();
    readonly List<WireStripper> stale = new List<WireStripper>();
    readonly HashSet<WireStripper> current = new HashSet<WireStripper>();

    // Path in terminal-local space, resampled evenly from the mouth to the end of the hook.
    Vector3[] hookPath;
    float pathLength;

    void Awake()
    {
        terminal = GetComponent<MeterTerminal>();
        hookPath = BuildPath(out pathLength);
    }

    void OnDisable()
    {
        foreach (var pair in hooks) Release(pair.Key, pair.Value);
        hooks.Clear();
    }

    void LateUpdate()
    {
        current.Clear();
        foreach (var wire in terminal.Wires) current.Add(wire);

        stale.Clear();
        foreach (var pair in hooks)
            if (!current.Contains(pair.Key)) stale.Add(pair.Key);
        foreach (var wire in stale)
        {
            Release(wire, hooks[wire]);
            hooks.Remove(wire);
        }

        foreach (var wire in current)
        {
            if (!hooks.TryGetValue(wire, out var hook))
            {
                hook = Create(wire);
                hooks.Add(wire, hook);
            }
            if (!hook.settled) Animate(hook);
        }
    }

    Hook Create(WireStripper wire)
    {
        var hook = new Hook { startTime = Time.time };
        var copper = wire.transform.Find(copperName);
        hook.straightCopper = copper != null ? copper.GetComponent<Renderer>() : null;

        hook.go = new GameObject("Gancho_" + wire.transform.parent.parent.name);
        hook.go.transform.SetParent(transform, false);
        hook.go.AddComponent<MeshFilter>().sharedMesh = hook.mesh = new Mesh { name = "Gancho" };
        var renderer = hook.go.AddComponent<MeshRenderer>();
        if (hook.straightCopper != null)
        {
            renderer.sharedMaterial = hook.straightCopper.sharedMaterial;
            hook.straightCopper.enabled = false;
        }
        return hook;
    }

    void Release(WireStripper wire, Hook hook)
    {
        if (hook.straightCopper != null) hook.straightCopper.enabled = true;
        if (hook.go != null) Destroy(hook.go);
        if (hook.mesh != null) Destroy(hook.mesh);
    }

    /// <summary>El cobre sale recto de la boca y se dobla hasta quedar en gancho.</summary>
    void Animate(Hook hook)
    {
        float t = bendSeconds > 0f ? Mathf.Clamp01((Time.time - hook.startTime) / bendSeconds) : 1f;
        float k = Mathf.SmoothStep(0f, 1f, t);

        var points = new Vector3[hookPath.Length];
        Vector3 down = Vector3.forward; // The terminal's Z axis is the direction the wire enters.
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 straight = hookPath[0] + down * (pathLength * i / (points.Length - 1));
            points[i] = Vector3.Lerp(straight, hookPath[i], k);
        }
        Tube(hook.mesh, points, copperRadius, ringSides);
        hook.settled = t >= 1f;
    }

    /// <summary>
    /// Ruta del cobre: baja de la boca hacia el lado derecho del vástago (visto de frente), se mete detrás
    /// de la cabeza hasta la placa y da vuelta en sentido horario, el mismo en que se aprieta el tornillo.
    /// </summary>
    Vector3[] BuildPath(out float length)
    {
        var head = terminal.screw.head;
        Vector3 axis = head.forward;
        Vector3 mouth = transform.position;
        Vector3 up = Vector3.ProjectOnPlane(-transform.forward, axis).normalized;
        Vector3 right = Vector3.Cross(up, -axis).normalized;
        float radius = shankRadius + copperRadius + 0.0003f;
        Vector3 center = head.position + axis * (plateDepth + copperRadius);

        var raw = new List<Vector3>();
        // Drop: from the mouth down to the right side of the shank, stepping back behind the head on the way.
        Vector3 start = center + right * radius;
        float mouthDepth = Vector3.Dot(mouth - start, axis);
        Vector3 mouthFlat = mouth - axis * mouthDepth;
        int dropSteps = Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(mouth, start) / sampleStep));
        for (int i = 0; i < dropSteps; i++)
        {
            float s = i / (float)dropSteps;
            float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.7f));
            raw.Add(Vector3.Lerp(mouthFlat, start, s) + axis * (mouthDepth * (1f - back)));
        }
        // Wrap: clockwise seen from the front, from 3 o'clock on.
        int wrapSteps = Mathf.Max(8, Mathf.CeilToInt(wrapDegrees * Mathf.Deg2Rad * radius / sampleStep));
        for (int i = 0; i <= wrapSteps; i++)
        {
            float angle = (90f + wrapDegrees * i / wrapSteps) * Mathf.Deg2Rad;
            raw.Add(center + radius * (Mathf.Sin(angle) * right + Mathf.Cos(angle) * up));
        }

        length = 0f;
        for (int i = 1; i < raw.Count; i++) length += Vector3.Distance(raw[i - 1], raw[i]);

        var local = new Vector3[raw.Count];
        for (int i = 0; i < raw.Count; i++) local[i] = transform.InverseTransformPoint(raw[i]);
        return Resample(local, Mathf.Max(8, Mathf.CeilToInt(length / sampleStep) + 1));
    }

    /// <summary>Largo del cobre que hace falta para el gancho, para ajustar cuánto se pela.</summary>
    public float CopperLength()
    {
        terminal = GetComponent<MeterTerminal>();
        BuildPath(out float length);
        return length;
    }

    static Vector3[] Resample(Vector3[] points, int count)
    {
        var lengths = new float[points.Length];
        for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        float total = lengths[lengths.Length - 1];

        var result = new Vector3[count];
        int segment = 1;
        for (int i = 0; i < count; i++)
        {
            float target = total * i / (count - 1);
            while (segment < points.Length - 1 && lengths[segment] < target) segment++;
            float span = lengths[segment] - lengths[segment - 1];
            float t = span > 1e-6f ? (target - lengths[segment - 1]) / span : 0f;
            result[i] = Vector3.Lerp(points[segment - 1], points[segment], t);
        }
        return result;
    }

    /// <summary>Tubo redondo a lo largo de los puntos, con tapa en la punta.</summary>
    static void Tube(Mesh mesh, Vector3[] points, float radius, int sides)
    {
        int rings = points.Length;
        var vertices = new Vector3[rings * sides + 2];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new List<int>((rings - 1) * sides * 6 + sides * 6);

        Vector3 tangent = (points[1] - points[0]).normalized;
        Vector3 normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        for (int r = 0; r < rings; r++)
        {
            Vector3 next = r < rings - 1 ? points[r + 1] - points[r] : points[r] - points[r - 1];
            Vector3 newTangent = next.sqrMagnitude > 1e-12f ? next.normalized : tangent;
            // Parallel transport keeps the ring from twisting along the bends.
            normal = (Quaternion.FromToRotation(tangent, newTangent) * normal).normalized;
            tangent = newTangent;
            Vector3 binormal = Vector3.Cross(tangent, normal);
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                Vector3 offset = Mathf.Cos(a) * normal + Mathf.Sin(a) * binormal;
                vertices[r * sides + s] = points[r] + offset * radius;
                normals[r * sides + s] = offset;
                uvs[r * sides + s] = new Vector2(s / (float)sides, r / (float)(rings - 1));
            }
        }
        for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = r * sides + s, b = r * sides + (s + 1) % sides;
                int c = a + sides, d = b + sides;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(b); triangles.Add(d); triangles.Add(c);
            }

        int startCap = rings * sides, endCap = startCap + 1;
        vertices[startCap] = points[0];
        normals[startCap] = (points[0] - points[1]).normalized;
        vertices[endCap] = points[rings - 1];
        normals[endCap] = tangent;
        int last = (rings - 1) * sides;
        for (int s = 0; s < sides; s++)
        {
            int n = (s + 1) % sides;
            triangles.Add(startCap); triangles.Add(n); triangles.Add(s);
            triangles.Add(endCap); triangles.Add(last + s); triangles.Add(last + n);
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    void OnDrawGizmosSelected()
    {
        terminal = GetComponent<MeterTerminal>();
        if (terminal == null || terminal.screw == null || terminal.screw.head == null) return;
        var path = BuildPath(out _);
        Gizmos.color = new Color(0.85f, 0.5f, 0.2f);
        for (int i = 1; i < path.Length; i++)
            Gizmos.DrawLine(transform.TransformPoint(path[i - 1]), transform.TransformPoint(path[i]));
    }
}
