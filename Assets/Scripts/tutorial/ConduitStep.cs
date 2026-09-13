public class ConduitStep : TutorialStep
{
    public ConduitPathGuide conduit;

    public override float Progress => conduit.Progress01;
    public override bool IsComplete => conduit.IsComplete;
}
