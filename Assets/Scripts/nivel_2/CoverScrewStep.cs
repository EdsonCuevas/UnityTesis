using UnityEngine;

/// <summary>
/// Aflojar o apretar con el destornillador los tornillos de una tapa (la de la caja de la pastilla).
/// Los tornillos solo se pueden girar durante el paso; cada uno se bloquea al terminarlo, así el
/// destornillador lo suelta y pasa al siguiente.
/// </summary>
public class CoverScrewStep : TutorialStep
{
    const string AlignHint = "Apoya la punta del destornillador en el tornillo, de frente";
    const string TightenTurnHint = "Mantén el gatillo y gira la muñeca a la derecha para apretar";
    const string LoosenTurnHint = "Mantén el gatillo y gira la muñeca a la izquierda para aflojar";
    const string ReleaseTriggerHint = "Suelta el gatillo para regresar la muñeca";

    public enum Goal
    {
        Loosen,
        Tighten
    }

    public TerminalScrew[] screws;
    public Goal goal;
    public Screwdriver screwdriver;
    [Tooltip("Flecha que señala el siguiente tornillo. Va también en 'visible durante el paso'.")]
    public FollowTarget marker;
    [Tooltip("Aviso si no ha tomado el destornillador.")]
    public string takeScrewdriverHint = "Toma el destornillador y afloja los tornillos de la tapa";

    float startedAt;
    float heldSince;
    float nextHintTime;

    bool Loosening => goal == Goal.Loosen;

    public override float Progress
    {
        get
        {
            if (screws.Length == 0) return 1f;
            float sum = 0f;
            foreach (var screw in screws) sum += ScrewProgress(screw);
            return sum / screws.Length;
        }
    }

    public override bool IsComplete
    {
        get
        {
            foreach (var screw in screws)
                if (!IsDone(screw)) return false;
            return true;
        }
    }

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        foreach (var screw in screws) screw.Locked = IsDone(screw);
        startedAt = Time.time;
        heldSince = float.PositiveInfinity;
        nextHintTime = 0f;
    }

    public override void End()
    {
        foreach (var screw in screws) screw.Locked = true;
        if (marker != null) marker.target = null;
        base.End();
    }

    public override void Tick()
    {
        // Each finished screw locks, so the screwdriver lets go of it.
        foreach (var screw in screws)
            if (!screw.Locked && IsDone(screw)) screw.Locked = true;

        var pending = Pending();
        if (marker != null) marker.target = pending != null ? pending.head : null;

        if (!screwdriver.IsHeld) heldSince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(heldSince)) heldSince = Time.time;

        string hint = Hint();
        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3.5f;
    }

    string Hint()
    {
        // Twisting the wrong way: the ratchet only works one way per step.
        if (!Loosening && Time.time - screwdriver.LastLoosenTime < 0.5f) return ReleaseTriggerHint;
        if (!screwdriver.IsHeld) return Time.time - startedAt > 6f ? takeScrewdriverHint : null;

        var current = screwdriver.CurrentScrew;
        if (current == null || System.Array.IndexOf(screws, current) < 0)
        {
            if (Time.time - screwdriver.LastMisalignedTime < 1f) return AlignHint;
            return Time.time - heldSince > 8f ? AlignHint : null;
        }

        float idleSince = Mathf.Max(screwdriver.EngagedSince, screwdriver.LastTurnTime);
        if (Time.time - idleSince <= 4f) return null;
        return Loosening ? LoosenTurnHint : TightenTurnHint;
    }

    bool IsDone(TerminalScrew screw) => Loosening ? screw.Turns <= 0.001f : screw.IsTight;

    float ScrewProgress(TerminalScrew screw) => Loosening ? 1f - screw.Progress01 : screw.Progress01;

    TerminalScrew Pending()
    {
        // The one being turned first, then the first unfinished.
        var current = screwdriver.CurrentScrew;
        if (current != null && System.Array.IndexOf(screws, current) >= 0 && !IsDone(current)) return current;
        foreach (var screw in screws)
            if (!IsDone(screw)) return screw;
        return null;
    }
}
