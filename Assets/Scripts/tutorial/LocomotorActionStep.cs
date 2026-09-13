using UnityEngine;

public enum LocomotorAction
{
    Crouch,
    Jump
}

public class LocomotorActionStep : TutorialStep
{
    public LocomotorAction action;
    [Tooltip("Segundos que debe permanecer agachado antes de levantarse.")]
    public float crouchSeconds = 1f;

    float crouchedFor;
    bool jumped;
    bool wasGrounded;

    bool CrouchDone => crouchedFor >= crouchSeconds && !Context.Locomotor.IsCrouching;

    public override float Progress
    {
        get
        {
            if (action == LocomotorAction.Jump) return jumped ? 1f : 0f;
            return CrouchDone ? 1f : 0.8f * Mathf.Clamp01(crouchedFor / crouchSeconds);
        }
    }

    public override bool IsComplete => action == LocomotorAction.Jump ? jumped : CrouchDone;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        crouchedFor = 0f;
        jumped = false;
        wasGrounded = context.Locomotor.IsGrounded;
    }

    public override void Tick()
    {
        var locomotor = Context.Locomotor;

        if (action == LocomotorAction.Jump)
        {
            if (wasGrounded && !locomotor.IsGrounded && locomotor.Velocity.y > 0.1f)
                jumped = true;
            wasGrounded = locomotor.IsGrounded;
        }
        else if (locomotor.IsCrouching)
        {
            crouchedFor += Time.deltaTime;
        }
    }
}
