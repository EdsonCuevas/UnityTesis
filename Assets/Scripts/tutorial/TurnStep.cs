using UnityEngine;

public class TurnStep : TutorialStep
{
    public float requiredDegrees = 90f;
    [Tooltip("Si hay worldTarget, además hay que terminar mirándolo con esta tolerancia.")]
    public float facingTolerance = 30f;

    float accumulated;
    float lastYaw;

    public override float Progress => Mathf.Clamp01(accumulated / requiredDegrees) * (IsFacingTarget() ? 1f : 0.9f);
    public override bool IsComplete => accumulated >= requiredDegrees && IsFacingTarget();

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        accumulated = 0f;
        lastYaw = context.RigRoot.eulerAngles.y;
    }

    public override void Tick()
    {
        float yaw = Context.RigRoot.eulerAngles.y;
        accumulated += Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw));
        lastYaw = yaw;
    }

    bool IsFacingTarget()
    {
        if (worldTarget == null) return true;

        Vector3 toTarget = worldTarget.position - Context.Head.position;
        toTarget.y = 0f;
        Vector3 forward = Context.Head.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, toTarget) <= facingTolerance;
    }
}
