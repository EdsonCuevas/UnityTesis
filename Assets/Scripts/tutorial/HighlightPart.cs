using UnityEngine;

public class HighlightPart : MonoBehaviour
{
    public Renderer rend;
    public Color highlightColor = Color.cyan;
    public float intensity = 5f;

    private Material mat;

    void Start()
    {
        // Crea instancia para no afectar material original
        mat = rend.material;
    }

    public void Highlight(bool state)
    {
        if (state)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor",
                highlightColor * intensity);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
    }
}