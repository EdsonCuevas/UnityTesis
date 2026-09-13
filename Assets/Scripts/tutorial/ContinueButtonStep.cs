public class ContinueButtonStep : TutorialStep
{
    public string buttonLabel = "Comenzar";

    bool pressed;

    public override float Progress => pressed ? 1f : 0f;
    public override bool IsComplete => pressed;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        pressed = false;
        context.Panel.SetButtons(new TutorialPanel.ButtonSpec(buttonLabel, () => pressed = true));
    }

    public override void End()
    {
        Context.Panel.ClearButtons();
        base.End();
    }
}
