using UnityEngine;

public class ConduitStep : TutorialStep
{
    public ConduitPathGuide conduit;

    float nextHintTime;

    public override float Progress => conduit.Progress01;
    public override bool IsComplete => conduit.IsComplete;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        string hint = conduit.Status switch
        {
            ConduitPathGuide.FeedStatus.TipMisaligned => "Apunta la punta del cable hacia la entrada del tubo",
            ConduitPathGuide.FeedStatus.NeedCloserGrip => "Sujeta el cable más cerca de la entrada para empujarlo",
            ConduitPathGuide.FeedStatus.OutOfSlack => "Ya no alcanza el cable: acerca el resto al tubo",
            _ => null
        };

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3f;
    }
}
