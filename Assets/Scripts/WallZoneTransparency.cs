using UnityEngine;

/// Cuando el jugador entra a la zona, el murete y la banqueta se vuelven
/// transparentes para dejar ver el tubo de la acometida bajo el piso.
///
/// Cada renderer tiene dos materiales:
///  - opaco: se usa mientras el alfa es 1 (escribe profundidad, sombras normales,
///    sin problemas de orden de dibujo).
///  - transparente: solo se usa mientras se desvanece.
/// El tubo también se desvanece (para ver los cables dentro), pero con
/// ordenExtra = 1 para que su versión transparente siempre se dibuje después
/// del piso y del murete, sin depender de la posición de la cámara.
public class WallZoneTransparency : MonoBehaviour
{
    [System.Serializable]
    public class Entrada
    {
        public Renderer renderer;
        [Tooltip("Material normal. Si se deja vacío se usa el que tenga el renderer.")]
        public Material opaco;
        [Tooltip("Material transparente (Surface Type = Transparent) que se usa al desvanecer.")]
        public Material transparente;
        [Tooltip("Alfa mínimo de este renderer al desvanecer. -1 = usar el general.")]
        public float alphaPropio = -1f;
        [Tooltip("Suma a la cola de render del material transparente. Ej: 1 en el tubo para que siempre se dibuje después del piso y del murete.")]
        public int ordenExtra = 0;

        [System.NonSerialized] public Material opacoReal;
        [System.NonSerialized] public Material fadeInstancia;
        [System.NonSerialized] public Color colorBase;
        [System.NonSerialized] public bool usandoFade;
    }

    [Header("Renderers que se desvanecen")]
    public Entrada[] renderers;

    [Header("Configuración")]
    [Range(0f, 1f)] public float alphaVisible = 1f;
    [Range(0f, 1f)] public float alphaTransparente = 0.15f;
    public float velocidad = 3f;

    private float alphaActual;
    private float alphaObjetivo;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        alphaActual = alphaObjetivo = alphaVisible;

        if (renderers == null) return;
        foreach (var e in renderers)
        {
            if (e == null || e.renderer == null) continue;
            e.opacoReal = e.opaco != null ? e.opaco : e.renderer.sharedMaterial;
            if (e.transparente == null)
            {
                Debug.LogWarning($"[WallZoneTransparency] {e.renderer.name} no tiene material transparente.", this);
                continue;
            }
            e.fadeInstancia = new Material(e.transparente);
            if (e.ordenExtra != 0) e.fadeInstancia.renderQueue = e.transparente.renderQueue + e.ordenExtra;
            e.colorBase = e.fadeInstancia.GetColor(BaseColorId);
            e.renderer.sharedMaterial = e.opacoReal;
            e.usandoFade = false;
        }
    }

    void Update()
    {
        alphaActual = Mathf.Lerp(alphaActual, alphaObjetivo, Time.deltaTime * velocidad);
        if (Mathf.Abs(alphaActual - alphaObjetivo) < 0.002f) alphaActual = alphaObjetivo;

        // Opaco solo cuando ya terminó de volver a ser visible por completo.
        bool opaco = alphaActual >= 0.999f;

        if (renderers == null) return;
        foreach (var e in renderers)
        {
            if (e == null || e.renderer == null || e.fadeInstancia == null) continue;

            if (opaco)
            {
                if (e.usandoFade)
                {
                    e.renderer.sharedMaterial = e.opacoReal;
                    e.usandoFade = false;
                }
                continue;
            }

            // t: 0 = visible, 1 = totalmente desvanecido (según el alfa general)
            float rango = alphaVisible - alphaTransparente;
            float t = rango > 0.0001f ? Mathf.Clamp01((alphaVisible - alphaActual) / rango) : 1f;
            float minimo = e.alphaPropio >= 0f ? e.alphaPropio : alphaTransparente;
            Color c = e.colorBase;
            c.a = e.colorBase.a * Mathf.Lerp(alphaVisible, minimo, t);
            e.fadeInstancia.SetColor(BaseColorId, c);
            if (!e.usandoFade)
            {
                e.renderer.sharedMaterial = e.fadeInstancia;
                e.usandoFade = true;
            }
        }
    }

    void OnDestroy()
    {
        if (renderers == null) return;
        foreach (var e in renderers)
            if (e != null && e.fadeInstancia != null) Destroy(e.fadeInstancia);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
            alphaObjetivo = alphaTransparente;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.transform.root.CompareTag("Player"))
            alphaObjetivo = alphaVisible;
    }
}
