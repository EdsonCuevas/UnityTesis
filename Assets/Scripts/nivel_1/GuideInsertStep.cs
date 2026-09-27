using UnityEngine;

public class GuideInsertStep : TutorialStep
{
    public PullGuide guide;

    float nextHintTime;

    public override float Progress => guide.InsertProgress01;
    public override bool IsComplete => guide.State >= PullGuide.GuideState.AtEntry;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        if (guide.State != PullGuide.GuideState.Carried || Time.time < nextHintTime) return;
        Context.ShowHint("Suelta la punta de la guía junto a la salida del tubo, dentro del registro");
        nextHintTime = Time.time + 6f;
    }
}
