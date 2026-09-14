using UnityEngine;

public abstract class TutorialStep : MonoBehaviour
{
    [Header("Contenido")]
    public string title;
    [TextArea(2, 6)] public string body;
    public AudioClip narration;

    [Header("Guías")]
    [Tooltip("Lugar donde se muestra el panel en este paso. Vacío = se queda donde estaba.")]
    public Transform panelAnchor;
    public ControllerPart[] highlightParts;
    [Tooltip("Destino al que apunta la línea guía del piso.")]
    public Transform worldTarget;
    [Tooltip("Puntos intermedios de la línea guía, en orden (por ejemplo, una puerta).")]
    public Transform[] pathPoints;
    [Tooltip("Objetos visibles solo durante este paso (marcadores, flechas).")]
    public GameObject[] visibleDuringStep;

    protected TutorialContext Context { get; private set; }

    public abstract float Progress { get; }
    public abstract bool IsComplete { get; }

    public virtual void Begin(TutorialContext context)
    {
        Context = context;
        SetVisible(true);
    }

    public virtual void Tick() { }

    public virtual void End() => SetVisible(false);

    public void SetVisible(bool visible)
    {
        foreach (var go in visibleDuringStep)
            if (go != null) go.SetActive(visible);
    }
}
