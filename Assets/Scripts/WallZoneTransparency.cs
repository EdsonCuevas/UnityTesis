using UnityEngine;

public class WallZoneTransparency : MonoBehaviour
{
    [Header("Referencias")]
    public Renderer zonaTransparente;
    public Renderer mureteRenderer;
    public Renderer banquetaRenderer;

    [Header("Configuración")]
    [Range(0f, 1f)] public float alphaVisible = 1f;
    [Range(0f, 1f)] public float alphaTransparente = 0.15f;
    public float velocidad = 3f;

    private Material mat;
    private Material matMurete;
    private Material matBanqueta;
    private float alphaObjetivo;

    void Start()
    {
        if (zonaTransparente == null || mureteRenderer == null || banquetaRenderer == null)
        {
            Debug.LogError("Asigna los 3 renderers en el Inspector");
            enabled = false;
            return;
        }

        mat = zonaTransparente.material;
        matMurete = mureteRenderer.material;
        matBanqueta = banquetaRenderer.material;
        alphaObjetivo = alphaVisible;
    }

    void Update()
    {
        if (mat == null) return;

        Color c = mat.GetColor("_BaseColor");
        c.a = Mathf.Lerp(c.a, alphaObjetivo, Time.deltaTime * velocidad);
        mat.SetColor("_BaseColor", c);

        Color c2 = matMurete.GetColor("_BaseColor");
        c2.a = c.a;
        matMurete.SetColor("_BaseColor", c2);

        Color c3 = matBanqueta.GetColor("_BaseColor");
        c3.a = c.a;
        matBanqueta.SetColor("_BaseColor", c3);
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