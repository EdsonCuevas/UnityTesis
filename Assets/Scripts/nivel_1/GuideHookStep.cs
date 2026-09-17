using UnityEngine;

public class GuideHookStep : TutorialStep
{
    public PullGuide guide;
    public ConduitPathGuide cable;

    float startDistance;

    public override float Progress
    {
        get
        {
            if (IsComplete) return 1f;
            float remaining = Distance() - guide.hookRadius;
            return 0.9f * Mathf.Clamp01(1f - remaining / Mathf.Max(startDistance - guide.hookRadius, 0.01f));
        }
    }

    public override bool IsComplete => cable.IsEngaged || cable.IsComplete;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        startDistance = Distance();
    }

    float Distance() => cable.Tip != null ? Vector3.Distance(cable.Tip.position, guide.HookPoint) : 0f;
}
