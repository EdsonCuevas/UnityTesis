using UnityEngine;

public class GuideTapeStep : TutorialStep
{
    public GuideTape tape;

    float heldSince;
    float nextHintTime;

    public override float Progress =>
        tape.IsTaped ? 1f :
        tape.IsStuck ? 0.2f + 0.8f * tape.Progress01 :
        tape.IsRollHeld ? 0.1f :
        0f;

    public override bool IsComplete => tape.IsTaped;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        heldSince = float.PositiveInfinity;
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        if (!tape.IsRollHeld) heldSince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(heldSince)) heldSince = Time.time;

        float heldFor = Time.time - heldSince;
        string hint =
            Time.time - tape.LastNotHookedTime < 1f ? "Engancha los tres cables a la guía antes de encintar" :
            !tape.IsStuck && heldFor > 5f ? "Acerca el rollo a la cabeza de la guía, donde están enganchados los cables" :
            tape.IsStuck && heldFor > 5f && Time.time - tape.LastTurnTime > 5f ? "Da vueltas con el rollo alrededor del amarre, siempre en el mismo sentido" :
            null;

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 4f;
    }
}
