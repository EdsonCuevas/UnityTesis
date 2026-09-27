public class ComfortStep : TutorialStep
{
    bool done;

    public override float Progress => done ? 1f : 0f;
    public override bool IsComplete => done;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        done = false;
        RefreshButtons();
    }

    public override void End()
    {
        Context.Panel.ClearButtons();
        base.End();
    }

    void RefreshButtons()
    {
        Context.Panel.SetButtons(
            new TutorialPanel.ButtonSpec(ComfortSettings.SnapTurn ? "Giro: por pasos" : "Giro: suave", ToggleSnapTurn),
            new TutorialPanel.ButtonSpec(ComfortSettings.Vignette ? "Viñeta: activada" : "Viñeta: desactivada", ToggleVignette),
            new TutorialPanel.ButtonSpec("Listo", () => done = true));
    }

    void ToggleSnapTurn()
    {
        ComfortSettings.SnapTurn = !ComfortSettings.SnapTurn;
        ComfortSettings.ApplyToScene();
        RefreshButtons();
    }

    void ToggleVignette()
    {
        ComfortSettings.Vignette = !ComfortSettings.Vignette;
        ComfortSettings.ApplyToScene();
        RefreshButtons();
    }
}
