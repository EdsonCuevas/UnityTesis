using UnityEngine;

public class LidStep : TutorialStep
{
    public enum Goal
    {
        Open,
        Close
    }

    public RegistroLid lid;
    public Goal goal;
    [Tooltip("Aviso cuando se intenta levantar la tapa con una sola mano.")]
    public string twoHandsHint = "La tapa pesa mucho: usa las dos manos para levantarla";

    HeavyLidTransformer heavy;
    float nextHintTime;

    public override float Progress => goal == Goal.Open
        ? (IsComplete ? 1f : 0.9f * lid.OpenAmount01)
        : (lid.IsSeated ? 1f : 0.9f * (1f - lid.OpenAmount01));

    public override bool IsComplete => goal == Goal.Open
        ? lid.IsOpen && !lid.IsHeld
        : lid.IsSeated;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        heavy = lid.GetComponent<HeavyLidTransformer>();
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        if (heavy == null || Time.time - heavy.LastStrainTime > 0.5f || Time.time < nextHintTime) return;
        Context.ShowHint(twoHandsHint, 3f);
        nextHintTime = Time.time + 5f;
    }
}
