using UnityEngine;

public class StripWireStep : TutorialStep
{
    public WireStripper stripper;
    [Tooltip("Otras puntas que se pelan en el mismo paso, por ejemplo el otro extremo del puente.")]
    public WireStripper[] otherStrippers = new WireStripper[0];
    public string holdCableHint = "Sostén el cable blanco con la otra mano para poder jalar";

    float nextHintTime;

    public override float Progress
    {
        get
        {
            float sum = stripper.Progress01;
            foreach (var other in otherStrippers) sum += other.Progress01;
            return sum / (1 + otherStrippers.Length);
        }
    }

    public override bool IsComplete
    {
        get
        {
            if (!stripper.IsStripped) return false;
            foreach (var other in otherStrippers)
                if (!other.IsStripped) return false;
            return true;
        }
    }

    void Awake() => SetEnabled(false);

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        nextHintTime = 0f;
        SetEnabled(true);
        WireGrab.Only(Strippers());
    }

    public override void Tick()
    {
        HideStrippedMarks();

        bool waiting = stripper.WaitingForCableHold;
        foreach (var other in otherStrippers) waiting |= other.WaitingForCableHold;
        if (waiting && Time.time > nextHintTime)
        {
            Context.ShowHint(holdCableHint);
            nextHintTime = Time.time + 3f;
        }
    }

    public override void End()
    {
        SetEnabled(false);
        WireGrab.Free();
        base.End();
    }

    void SetEnabled(bool enabled)
    {
        stripper.enabled = enabled;
        foreach (var other in otherStrippers) other.enabled = enabled;
    }

    System.Collections.Generic.IEnumerable<WireStripper> Strippers()
    {
        yield return stripper;
        foreach (var other in otherStrippers) yield return other;
    }

    // With several tips, the strip mark of a finished one would still show until the step ends.
    void HideStrippedMarks()
    {
        if (otherStrippers.Length == 0) return;
        foreach (var go in visibleDuringStep)
        {
            if (go == null || !go.activeSelf) continue;
            if (Stripped(stripper, go)) go.SetActive(false);
            foreach (var other in otherStrippers)
                if (Stripped(other, go)) go.SetActive(false);
        }
    }

    static bool Stripped(WireStripper tip, GameObject go) => tip.IsStripped && go.transform.IsChildOf(tip.transform);
}
