public class LidStep : TutorialStep
{
    public enum Goal
    {
        Open,
        Close
    }

    public RegistroLid lid;
    public Goal goal;

    public override float Progress => goal == Goal.Open
        ? (IsComplete ? 1f : 0.9f * lid.OpenAmount01)
        : (lid.IsSeated ? 1f : 0.9f * (1f - lid.OpenAmount01));

    public override bool IsComplete => goal == Goal.Open
        ? lid.IsOpen && !lid.IsHeld
        : lid.IsSeated;
}
