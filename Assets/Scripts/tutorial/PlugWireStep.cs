using UnityEngine;

public class PlugWireStep : TutorialStep
{
    public PlugController plug;

    float startDistance;

    public override float Progress =>
        plug.isConected ? 1f : 0.9f * Mathf.Clamp01(1f - Distance() / Mathf.Max(startDistance, 0.01f));

    public override bool IsComplete => plug.isConected;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        startDistance = Distance();
    }

    float Distance() => Vector3.Distance(plug.endAnchor.position, plug.plugPosition.position);
}
