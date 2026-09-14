using UnityEngine;

public class StripWireStep : TutorialStep
{
    public WireStripper stripper;

    static readonly Color HintColor = new Color(1f, 0.8f, 0.3f);

    float nextHintTime;

    public override float Progress => stripper.Progress01;
    public override bool IsComplete => stripper.IsStripped;

    void Awake() => stripper.enabled = false;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
        stripper.enabled = true;
    }

    public override void Tick()
    {
        if (stripper.WaitingForCableHold && Time.time > nextHintTime)
        {
            Context.Panel.ShowFeedback("Sostén el cable blanco con la otra mano para poder jalar", HintColor, 2.5f);
            nextHintTime = Time.time + 3f;
        }
    }

    public override void End()
    {
        stripper.enabled = false;
        base.End();
    }
}
