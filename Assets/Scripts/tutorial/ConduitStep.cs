using UnityEngine;

public class ConduitStep : TutorialStep
{
    public ConduitPathGuide conduit;
    public string misalignedHint = "Apunta la punta del cable hacia la entrada del tubo";
    [Tooltip("Aviso cuando una mano frena el resto del cable, que se arrastraría solo.")]
    public string heldBackHint = "Deja libre el resto del cable: se arrastra solo al empujar";

    protected float nextHintTime;

    public override float Progress => conduit.Progress01;
    public override bool IsComplete => conduit.IsComplete;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        string hint = Time.time - conduit.LastSlipTime < 1f
            ? "El cable se resbaló: empuja más despacio en las curvas"
            : conduit.Status switch
            {
                ConduitPathGuide.FeedStatus.TipMisaligned => misalignedHint,
                ConduitPathGuide.FeedStatus.NeedCloserGrip => "Sujeta el cable más cerca de la entrada para empujarlo",
                ConduitPathGuide.FeedStatus.OutOfSlack => conduit.StartEndIsFixed
                    ? "Ya no alcanza el cable: acerca el resto al tubo"
                    : heldBackHint,
                _ => null
            };

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3f;
    }
}
