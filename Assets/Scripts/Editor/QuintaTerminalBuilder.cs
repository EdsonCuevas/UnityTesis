using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera la quinta terminal de la base de 220 V a partir de la foto de referencia: placa de lámina
/// galvanizada doblada (el protector), mordaza de dos dedos para la quinta cuchilla del medidor,
/// abrazadera y tornillo de cabeza de cruz con arandela. Guarda mallas, materiales y un prefab.
/// </summary>
public static class QuintaTerminalBuilder
{
    const string Folder = "Assets/Models/QuintaTerminal";
    public const string PrefabPath = Folder + "/Quinta_Terminal_Real.prefab";

    /// <summary>Centro de la cabeza del tornillo en el mundo; es el pivote del prefab.</summary>
    public static readonly Vector3 Origin = new Vector3(7.586f, 1.342f, 0.230f);

    class Builder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<List<int>> subs = new List<List<int>>();
        readonly Vector3 offset;

        public Builder(Vector3 offset, int subMeshes)
        {
            this.offset = offset;
            for (int i = 0; i < subMeshes; i++) subs.Add(new List<int>());
        }

        int V(Vector3 p, Vector3 n)
        {
            verts.Add(p - offset);
            normals.Add(n);
            return verts.Count - 1;
        }

        /// <summary>Triángulo con cara hacia <paramref name="n"/> (lo voltea si hace falta).</summary>
        void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 facing)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                (b, c) = (c, b);
                (nb, nc) = (nc, nb);
            }
            var t = subs[sub];
            t.Add(V(a, na)); t.Add(V(b, nb)); t.Add(V(c, nc));
        }

        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            Tri(sub, a, b, c, n, n, n, n);
            Tri(sub, a, c, d, n, n, n, n);
        }

        /// <summary>Hexaedro: 0-3 un anillo, 4-7 el anillo opuesto en el mismo orden. Caras planas hacia afuera.</summary>
        public void Hex(int sub, Vector3[] c)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in c) center += p;
            center /= 8f;
            int[][] faces =
            {
                new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };
            foreach (var f in faces)
            {
                Vector3 a = c[f[0]], b = c[f[1]], cc = c[f[2]], d = c[f[3]];
                Vector3 n = Vector3.Cross(cc - a, d - b).normalized;
                if (Vector3.Dot(n, (a + b + cc + d) * 0.25f - center) < 0f) n = -n;
                Quad(sub, a, b, cc, d, n);
            }
        }

        public void Box(int sub, Vector3 min, Vector3 max)
        {
            Hex(sub, new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            });
        }

        /// <summary>
        /// Sólido de revolución alrededor del eje Z que pasa por <paramref name="center"/>.
        /// El perfil (radio, z) va del eje abajo, hacia afuera, sube y regresa al eje arriba.
        /// </summary>
        public void Lathe(int sub, Vector3 center, Vector2[] profile, int segments)
        {
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                Vector2 d = (p1 - p0).normalized;
                Vector2 nrz = new Vector2(d.y, -d.x); // hacia afuera con este orden de perfil
                for (int s = 0; s < segments; s++)
                {
                    float a0 = s * Mathf.PI * 2f / segments, a1 = (s + 1) * Mathf.PI * 2f / segments;
                    Vector3 r0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    Vector3 r1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    Vector3 v00 = center + r0 * p0.x + Vector3.forward * p0.y;
                    Vector3 v01 = center + r1 * p0.x + Vector3.forward * p0.y;
                    Vector3 v10 = center + r0 * p1.x + Vector3.forward * p1.y;
                    Vector3 v11 = center + r1 * p1.x + Vector3.forward * p1.y;
                    Vector3 n0 = (r0 * nrz.x + Vector3.forward * nrz.y).normalized;
                    Vector3 n1 = (r1 * nrz.x + Vector3.forward * nrz.y).normalized;
                    Vector3 facing = (n0 + n1).normalized;
                    if (p0.x > 1e-6f) Tri(sub, v00, v01, v11, n0, n1, n1, facing);
                    if (p1.x > 1e-6f) Tri(sub, v00, v11, v10, n0, n1, n0, facing);
                }
            }
        }

        /// <summary>
        /// Lámina doblada: el perfil va en XZ y cada punto tiene su rango de alto (Y).
        /// El grosor crece hacia -Z (hacia el fondo de la base).
        /// </summary>
        public void BentSheet(int sub, Vector2[] xz, Vector2[] yRange, float thickness)
        {
            int n = xz.Length;
            var back = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 acc = Vector2.zero;
                if (i > 0) acc += SegBack(xz[i - 1], xz[i]);
                if (i < n - 1) acc += SegBack(xz[i], xz[i + 1]);
                back[i] = acc.normalized * thickness;
            }
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 F(int k, bool top, bool rear)
                {
                    Vector2 p = xz[k] + (rear ? back[k] : Vector2.zero);
                    return new Vector3(p.x, top ? yRange[k].y : yRange[k].x, p.y);
                }
                Hex(sub, new[]
                {
                    F(i, false, false), F(i + 1, false, false), F(i + 1, false, true), F(i, false, true),
                    F(i, true, false), F(i + 1, true, false), F(i + 1, true, true), F(i, true, true),
                });
            }
        }

        /// <summary>
        /// Lámina curva con grosor a partir de una malla de puntos de su cara de enfrente.
        /// La cara de enfrente mira hacia <paramref name="frontRef"/> (su X se toma de cada punto).
        /// Normales suaves para que la curva no se vea facetada.
        /// </summary>
        public void CurvedSheet(int sub, Vector3[,] p, float thickness, Vector3 frontRef)
        {
            int ni = p.GetLength(0), nk = p.GetLength(1);
            var n = new Vector3[ni, nk];
            var q = new Vector3[ni, nk];
            for (int i = 0; i < ni; i++)
                for (int k = 0; k < nk; k++)
                {
                    Vector3 du = p[Mathf.Min(i + 1, ni - 1), k] - p[Mathf.Max(i - 1, 0), k];
                    Vector3 dv = p[i, Mathf.Min(k + 1, nk - 1)] - p[i, Mathf.Max(k - 1, 0)];
                    Vector3 nn = Vector3.Cross(du, dv).normalized;
                    Vector3 toFront = new Vector3(p[i, k].x, frontRef.y, frontRef.z) - p[i, k];
                    if (Vector3.Dot(nn, toFront) < 0f) nn = -nn;
                    n[i, k] = nn;
                    q[i, k] = p[i, k] - nn * thickness;
                }

            for (int i = 0; i < ni - 1; i++)
                for (int k = 0; k < nk - 1; k++)
                {
                    Vector3 f = (n[i, k] + n[i + 1, k] + n[i + 1, k + 1] + n[i, k + 1]).normalized;
                    Tri(sub, p[i, k], p[i + 1, k], p[i + 1, k + 1], n[i, k], n[i + 1, k], n[i + 1, k + 1], f);
                    Tri(sub, p[i, k], p[i + 1, k + 1], p[i, k + 1], n[i, k], n[i + 1, k + 1], n[i, k + 1], f);
                    Tri(sub, q[i, k], q[i + 1, k], q[i + 1, k + 1], -n[i, k], -n[i + 1, k], -n[i + 1, k + 1], -f);
                    Tri(sub, q[i, k], q[i + 1, k + 1], q[i, k + 1], -n[i, k], -n[i + 1, k + 1], -n[i, k + 1], -f);
                }

            // Cantos: las cuatro orillas de la lámina.
            void Edge(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 outward)
            {
                Vector3 en = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                if (Vector3.Dot(en, outward) < 0f) en = -en;
                Quad(sub, a0, a1, b1, b0, en);
            }
            for (int i = 0; i < ni - 1; i++)
            {
                Edge(p[i, 0], p[i + 1, 0], q[i, 0], q[i + 1, 0], p[i, 0] - p[i, 1]);
                Edge(p[i, nk - 1], p[i + 1, nk - 1], q[i, nk - 1], q[i + 1, nk - 1], p[i, nk - 1] - p[i, nk - 2]);
            }
            for (int k = 0; k < nk - 1; k++)
            {
                Edge(p[0, k], p[0, k + 1], q[0, k], q[0, k + 1], p[0, k] - p[1, k]);
                Edge(p[ni - 1, k], p[ni - 1, k + 1], q[ni - 1, k], q[ni - 1, k + 1], p[ni - 1, k] - p[ni - 2, k]);
            }
        }

        static Vector2 SegBack(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x);
            return n.y > 0f ? -n : n;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    [MenuItem("Tesis/Construir quinta terminal (220 V)")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Models", "QuintaTerminal");

        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/CFE/ASSETS/Metal.mat");
        var galv = LitMaterial("Lamina_Galvanizada", new Color(0.47f, 0.48f, 0.47f), 0.65f, 0.42f);
        var slot = LitMaterial("Tornillo_Ranura", new Color(0.06f, 0.06f, 0.06f), 0f, 0.2f);

        var o = Origin;

        // Protector: canal de lámina en U a lo largo de la mordaza. El fondo queda detrás de la mordaza
        // y del tornillo; las orillas de arriba y abajo se curvan hacia el frente y se abren hacia el
        // centro de la base. La orilla de abajo llega más lejos que la de arriba.
        var plate = new Builder(o, 1);
        const float chYc = 1.342f, xOut = 7.6275f, xIn = 7.5640f;
        const float halfFlat = 0.0105f, zBack = 0.2160f;
        const int stations = 14, wallPts = 7;
        int cols = wallPts * 2;
        var grid = new Vector3[stations, cols];
        for (int i = 0; i < stations; i++)
        {
            float s = i / (float)(stations - 1);
            for (int k = 0; k < cols; k++)
            {
                bool top = k < wallPts;
                float t = top ? (wallPts - 1 - k) / (float)(wallPts - 1) : (k - wallPts) / (float)(wallPts - 1);
                float sgn = top ? 1f : -1f;
                float xEnd = xIn - (top ? 0.0020f : 0.0075f) * t * t;
                float x = Mathf.Lerp(xOut, xEnd, s);
                float h = Mathf.Lerp(0.0125f, top ? 0.0200f : 0.0225f, Mathf.Pow(s, 1.3f));
                float zFront = Mathf.Lerp(0.2275f, 0.2315f, s);
                // Codo redondo: sigue al fondo plano y sube hacia el frente (cóncavo hacia el frente).
                const float maxA = 80f * Mathf.Deg2Rad;
                float a = t * maxA;
                float y = chYc + sgn * (halfFlat + (h - halfFlat) * Mathf.Sin(a) / Mathf.Sin(maxA));
                float z = zBack + (zFront - zBack) * (1f - Mathf.Cos(a)) / (1f - Mathf.Cos(maxA));
                grid[i, k] = new Vector3(x, y, z);
            }
        }
        plate.CurvedSheet(0, grid, 0.0010f, new Vector3(0f, chYc, 0.27f));

        // Mordaza: base, dos dedos con labio abierto, resortes de respaldo y tope.
        var jaw = new Builder(o, 1);
        const float yc = 1.342f, gap = 0.0009f, ft = 0.0020f;
        jaw.Box(0, new Vector3(7.5990f, 1.3320f, 0.2160f), new Vector3(7.6240f, 1.3520f, 0.2180f));
        foreach (float sgn in new[] { 1f, -1f })
        {
            float yIn = yc + sgn * gap, yOut = yc + sgn * (gap + ft);
            var fingerXZ = new[] { new Vector2(0.2180f, 0f), new Vector2(0.2395f, 0f), new Vector2(0.2440f, sgn * 0.0028f) };
            for (int i = 0; i < fingerXZ.Length - 1; i++)
            {
                Vector2 a = fingerXZ[i], b = fingerXZ[i + 1];
                jaw.Hex(0, new[]
                {
                    new Vector3(7.6010f, yIn + a.y, a.x), new Vector3(7.6010f, yIn + b.y, b.x),
                    new Vector3(7.6010f, yOut + b.y, b.x), new Vector3(7.6010f, yOut + a.y, a.x),
                    new Vector3(7.6220f, yIn + a.y, a.x), new Vector3(7.6220f, yIn + b.y, b.x),
                    new Vector3(7.6220f, yOut + b.y, b.x), new Vector3(7.6220f, yOut + a.y, a.x),
                });
            }
            float ys0 = yc + sgn * (gap + ft + 0.0003f), ys1 = ys0 + sgn * 0.0015f;
            jaw.Box(0, new Vector3(7.6040f, Mathf.Min(ys0, ys1), 0.2180f), new Vector3(7.6190f, Mathf.Max(ys0, ys1), 0.2345f));
        }
        jaw.Box(0, new Vector3(7.6220f, 1.3375f, 0.2180f), new Vector3(7.6255f, 1.3465f, 0.2315f));

        // Abrazadera bajo el tornillo: bloque roscado con paredes a los lados del cable.
        var clamp = new Builder(o, 1);
        clamp.Box(0, new Vector3(7.5765f, 1.3335f, 0.2160f), new Vector3(7.5955f, 1.3505f, 0.2228f));
        clamp.Box(0, new Vector3(7.5765f, 1.3335f, 0.2228f), new Vector3(7.5955f, 1.3350f, 0.2275f));
        clamp.Box(0, new Vector3(7.5765f, 1.3490f, 0.2228f), new Vector3(7.5955f, 1.3505f, 0.2275f));
        for (int i = 0; i < 4; i++) // estrías de agarre
        {
            float x = 7.5790f + i * 0.0045f;
            clamp.Box(0, new Vector3(x, 1.3350f, 0.2228f), new Vector3(x + 0.0012f, 1.3490f, 0.2236f));
        }

        // Tornillo de cabeza redonda con arandela y cruz. Pivote en el centro de la cabeza, Z hacia afuera.
        var screw = new Builder(o, 2);
        screw.Lathe(0, o, new[]
        {
            new Vector2(0f, -0.0100f), new Vector2(0.0021f, -0.0100f), new Vector2(0.0021f, -0.0025f),
            new Vector2(0.0068f, -0.0025f), new Vector2(0.0068f, -0.0017f), new Vector2(0.0056f, -0.0017f),
            new Vector2(0.0056f, -0.0002f), new Vector2(0.0053f, 0.0007f), new Vector2(0.0047f, 0.0013f),
            new Vector2(0.0038f, 0.0016f), new Vector2(0.0025f, 0.0017f), new Vector2(0f, 0.0017f),
        }, 32);
        screw.Box(1, o + new Vector3(-0.0029f, -0.00045f, 0.0011f), o + new Vector3(0.0029f, 0.00045f, 0.00178f));
        screw.Box(1, o + new Vector3(-0.00045f, -0.0029f, 0.0011f), o + new Vector3(0.00045f, 0.0029f, 0.00178f));

        var meshes = new Dictionary<string, Mesh>
        {
            { "Quinta_Placa", plate.ToMesh("Quinta_Placa") },
            { "Quinta_Mordaza", jaw.ToMesh("Quinta_Mordaza") },
            { "Quinta_Abrazadera", clamp.ToMesh("Quinta_Abrazadera") },
            { "Quinta_Tornillo", screw.ToMesh("Quinta_Tornillo") },
        };
        foreach (var key in new List<string>(meshes.Keys)) meshes[key] = SaveMesh(meshes[key]);

        var root = new GameObject("Quinta_Terminal_Real");
        Part(root, "Quinta_Placa", meshes["Quinta_Placa"], galv);
        Part(root, "Quinta_Mordaza", meshes["Quinta_Mordaza"], metal);
        Part(root, "Quinta_Abrazadera", meshes["Quinta_Abrazadera"], metal);
        Part(root, "Quinta_Tornillo", meshes["Quinta_Tornillo"], metal, slot);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Quinta terminal generada en " + PrefabPath);
    }

    static Vector2[] Dome(float r, float h) => new[]
    {
        new Vector2(0f, -0.0003f), new Vector2(r, -0.0003f), new Vector2(r, 0f),
        new Vector2(r * 0.8f, h * 0.6f), new Vector2(r * 0.45f, h * 0.95f), new Vector2(0f, h),
    };

    static void Part(GameObject root, string name, Mesh mesh, params Material[] mats)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
    }

    static Mesh SaveMesh(Mesh mesh)
    {
        string path = Folder + "/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material LitMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
