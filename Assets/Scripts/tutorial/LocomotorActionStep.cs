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
    [Tooltip("Altura mínima que debe subir la cabeza después de presionar A para contar el salto.")]
    public float minJumpRise = 0.08f;
    [Tooltip("Segundos después de presionar A en los que se espera ver la subida.")]
    public float jumpWindowSeconds = 1f;

    float crouchedFor;
    bool jumped;
    float pressTime = float.NegativeInfinity;
    float pressHeadY;

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
        pressTime = float.NegativeInfinity;
    }

    public override void Tick()
    {
        if (action == LocomotorAction.Jump)
        {
            float headY = Context.Head.position.y;
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                pressTime = Time.time;
                pressHeadY = headY;
            }

            if (Time.time - pressTime <= jumpWindowSeconds && headY - pressHeadY >= minJumpRise)
                jumped = true;
        }
        else if (Context.Locomotor.IsCrouching)
        {
            crouchedFor += Time.deltaTime;
        }
    }
}
