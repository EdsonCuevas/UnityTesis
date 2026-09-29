using UnityEngine;

public class CutWireStep : TutorialStep
{
    public WireCutter[] cutters;

    float heldSince;
    float nextHintTime;

    public override float Progress
    {
        get
        {
            int cut = 0;
            foreach (var cutter in cutters)
                if (cutter.IsCut) cut++;
            return cutters.Length > 0 ? (float)cut / cutters.Length : 1f;
        }
    }

    public override bool IsComplete
    {
        get
        {
            foreach (var cutter in cutters)
                if (!cutter.IsCut) return false;
            return true;
        }
    }

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        heldSince = float.PositiveInfinity;
        nextHintTime = 0f;
        foreach (var cutter in cutters)
        {
            cutter.Prepare();
            cutter.enabled = true;
        }
    }

    public override void End()
    {
        foreach (var cutter in cutters)
            cutter.enabled = false;
        base.End();
    }

    public override void Tick()
    {
        bool held = cutters.Length > 0 && cutters[0].IsPliersHeld;
        if (!held) heldSince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(heldSince)) heldSince = Time.time;

        float outOfRange = float.NegativeInfinity;
        foreach (var cutter in cutters)
            outOfRange = Mathf.Max(outOfRange, cutter.LastOutOfRangeTime);

        string hint =
            Time.time - outOfRange < 1f ? "Ahí no: corta sobre la marca, a unos 40 centímetros de donde sale el cable del tubo" :
            held && Time.time - heldSince > 10f ? "Pon las quijadas de las pinzas sobre la marca del cable y aprieta el gatillo" :
            null;

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3.5f;
    }
}
