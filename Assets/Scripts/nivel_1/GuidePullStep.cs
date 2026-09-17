using UnityEngine;

public class GuidePullStep : TutorialStep
{
    public PullGuide guide;

    float nextHintTime;

    public override float Progress => guide.PullProgress01;
    public override bool IsComplete => guide.State == PullGuide.GuideState.Done;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        string hint =
            Time.time - guide.LastSlipTime < 1f ? "La guía se resbaló: jala más despacio en las curvas" :
            Time.time - guide.LastBlockedTime < 1f ? "Engancha los tres cables a la guía antes de jalar" :
            Time.time - guide.LastOutOfSlackTime < 1f ? "Un cable ya no alcanza: acerca su rollo al medidor" :
            null;

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3f;
    }
}
