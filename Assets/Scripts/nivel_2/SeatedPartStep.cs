using UnityEngine;

/// <summary>Quitar una pieza de su asiento y dejarla aparte, o llevarla a su asiento y soltarla ahí.</summary>
public class SeatedPartStep : TutorialStep
{
    public enum Goal
    {
        Remove,
        Place
    }

    public SeatedPart part;
    public Goal goal;
    [Tooltip("Distancia al asiento a partir de la cual la pieza ya se quitó.")]
    public float removedDistance = 0.25f;
    [Tooltip("Aviso si se suelta cerca del asiento pero sin llegar.")]
    public string missHint = "Acércala más a su lugar antes de soltarla";
    [Tooltip("Aviso si la pieza quedó enganchada y nadie la empuja (solo piezas con bisagra).")]
    public string pressHint = "Empuja la parte de arriba con la mano hasta que haga clic";
    [Tooltip("Segundos enganchada sin empujarla antes del aviso.")]
    public float pressHintDelay = 5f;

    float startDistance;
    float nextHintTime;
    float hookedSince = -1f;

    public override float Progress
    {
        get
        {
            if (goal == Goal.Remove)
                return IsComplete ? 1f : 0.9f * Mathf.Clamp01(part.DistanceToSeat / removedDistance);
            if (part.IsSeated) return 1f;
            if (part.IsHooked) return 0.5f + 0.45f * part.PressProgress;
            float approach = Mathf.Clamp01(1f - part.DistanceToSeat / Mathf.Max(startDistance, 0.01f));
            return (part.UsesHook ? 0.45f : 0.9f) * approach;
        }
    }

    public override bool IsComplete => goal == Goal.Remove
        ? !part.IsHeld && part.DistanceToSeat >= removedDistance
        : part.IsSeated;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        startDistance = part.DistanceToSeat;
        nextHintTime = 0f;
        hookedSince = -1f;
        part.LeftHand = context.LeftHand;
        part.RightHand = context.RightHand;
        part.Accepting = goal == Goal.Place;
        part.SetGrabbable(true);
    }

    public override void Tick()
    {
        if (part.IsHooked)
        {
            if (hookedSince < 0f) hookedSince = Time.time;
            float idleSince = Mathf.Max(hookedSince, part.LastPushTime);
            if (Time.time - idleSince >= pressHintDelay && Time.time >= nextHintTime)
            {
                Context.ShowHint(pressHint);
                nextHintTime = Time.time + 6f;
            }
            return;
        }
        hookedSince = -1f;

        if (Time.time - part.LastMissTime > 1f || Time.time < nextHintTime) return;
        Context.ShowHint(missHint);
        nextHintTime = Time.time + 3f;
    }

    public override void End()
    {
        part.Accepting = false;
        part.SetGrabbable(false);
        base.End();
    }
}
