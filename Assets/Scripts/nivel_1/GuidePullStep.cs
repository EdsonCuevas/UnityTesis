using UnityEngine;

public class GuidePullStep : TutorialStep
{
    static readonly Color JamColor = new Color(1f, 0.55f, 0.2f);
    static readonly Color UnjamColor = new Color(0.35f, 1f, 0.5f);

    public PullGuide guide;

    [Header("Atasco")]
    [Tooltip("Lugar del panel mientras la guía está atorada (el murete).")]
    public Transform jamPanelAnchor;
    [Tooltip("Destino de la línea guía mientras la guía está atorada (la entrada del medidor).")]
    public Transform jamTarget;
    public Transform[] jamPathPoints;
    [Tooltip("Objetos visibles solo mientras la guía está atorada (flecha en la entrada).")]
    public GameObject[] visibleWhileJammed;
    [Tooltip("Descripción del panel mientras la guía está atorada.")]
    [TextArea(2, 6)] public string jamBody =
        "La guía se atoró. Ve al murete, agarra un cable justo debajo de donde entra al medidor " +
        "y empújalo hacia arriba, hacia el medidor, con movimientos cortos. Los tres cables entran juntos.";
    [Tooltip("Narración de la descripción del atasco.")]
    public AudioClip jamNarration;
    [Tooltip("Narración al destrabarse la guía.")]
    public AudioClip unjamNarration;

    float nextHintTime;
    bool wasJammed;
    int shownUnjamPercent;

    public override float Progress => guide.PullProgress01;
    public override bool IsComplete => guide.State == PullGuide.GuideState.Done;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
        wasJammed = false;
        SetJamVisible(false);
    }

    public override void End()
    {
        base.End();
        SetJamVisible(false);
    }

    public override void Tick()
    {
        if (guide.IsJammed != wasJammed)
        {
            wasJammed = guide.IsJammed;
            OnJamChanged(wasJammed);
        }
        if (guide.IsJammed) ShowUnjamProgress();

        string hint =
            Time.time - guide.LastJammedPullTime < 1f ? "La guía está atorada: ve al murete y empuja los tres cables hacia el medidor" :
            guide.IsJammed && AnyCableNeedsCloserGrip() ? "Agarra el cable más arriba, justo debajo de donde entra al medidor" :
            Time.time - CableSlipTime() < 1f ? "El cable se resbaló: empújalo más despacio" :
            Time.time - guide.LastPushBlockedTime < 1f
                ? (guide.IsJammed ? "Un cable ya no alcanza: acerca su rollo al medidor" : "Ya empujaste suficiente cable: regresa al registro a jalar la guía") :
            Time.time - guide.LastSlipTime < 1f ? "La guía se resbaló: jala más despacio en las curvas" :
            Time.time - guide.LastBlockedTime < 1f ? "Engancha los tres cables a la guía antes de jalar" :
            Time.time - guide.LastUntapedTime < 1f ? "Encinta el amarre de los cables antes de jalar la guía" :
            Time.time - guide.LastOutOfSlackTime < 1f ? "Un cable ya no alcanza: acerca su rollo al medidor" :
            null;

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3f;
    }

    void OnJamChanged(bool jammed)
    {
        // Jamming is part of the job, not a mistake, so this notice isn't counted as a hint.
        Context.Panel.ShowFeedback(jammed ? "¡La guía se atoró!" : "¡Se destrabó! Regresa al registro y sigue jalando",
            jammed ? JamColor : UnjamColor, 3f);
        Context.Panel.MoveTo(jammed ? jamPanelAnchor : panelAnchor);
        shownUnjamPercent = 0;

        if (!Context.GuidesVisible) return;
        Context.Panel.SetBody(jammed ? jamBody : body);
        AudioClip narrationClip = jammed ? jamNarration : unjamNarration;
        if (narrationClip != null) Context.Narrate?.Invoke(narrationClip);
        SetVisible(!jammed);
        SetJamVisible(jammed);

        Transform target = jammed ? jamTarget : worldTarget;
        if (target != null) Context.GuideLine.Show(target, jammed ? jamPathPoints : pathPoints);
        else Context.GuideLine.Hide();
    }

    /// <summary>Muestra cuánto falta empujar, para que se note que el empuje sí entra.</summary>
    void ShowUnjamProgress()
    {
        int percent = Mathf.FloorToInt(guide.UnjamProgress01 * 10f) * 10;
        if (percent <= shownUnjamPercent) return;
        shownUnjamPercent = percent;
        Context.Panel.ShowFeedback($"Empujando los cables… {percent}%", UnjamColor, 2f);
    }

    bool AnyCableNeedsCloserGrip()
    {
        foreach (var cable in guide.cables)
            if (cable.Status == ConduitPathGuide.FeedStatus.NeedCloserGrip) return true;
        return false;
    }

    float CableSlipTime()
    {
        float latest = float.NegativeInfinity;
        foreach (var cable in guide.cables)
            latest = Mathf.Max(latest, cable.LastSlipTime);
        return latest;
    }

    void SetJamVisible(bool visible)
    {
        foreach (var go in visibleWhileJammed)
            if (go != null) go.SetActive(visible);
    }
}
