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

    float startDistance;
    float nextHintTime;

    public override float Progress => goal == Goal.Remove
        ? (IsComplete ? 1f : 0.9f * Mathf.Clamp01(part.DistanceToSeat / removedDistance))
        : (part.IsSeated ? 1f : 0.9f * Mathf.Clamp01(1f - part.DistanceToSeat / Mathf.Max(startDistance, 0.01f)));

    public override bool IsComplete => goal == Goal.Remove
        ? !part.IsHeld && part.DistanceToSeat >= removedDistance
        : part.IsSeated;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        startDistance = part.DistanceToSeat;
        nextHintTime = 0f;
        part.Accepting = goal == Goal.Place;
        part.SetGrabbable(true);
    }

    public override void Tick()
    {
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
