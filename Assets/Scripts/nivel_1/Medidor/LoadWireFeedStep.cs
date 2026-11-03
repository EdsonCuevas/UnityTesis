using UnityEngine;

/// <summary>
/// El jugador mete un cable de carga por el hueco de atrás de la base y lo empuja hasta que pasa al otro
/// lado del murete. La punta que entra es la opuesta a la pelable, que se queda en la base.
/// </summary>
public class LoadWireFeedStep : ConduitStep
{
    [Tooltip("Punta pelable del cable: se queda de este lado.")]
    public WireStripper stripper;
    [Tooltip("Flecha que señala la punta que entra y después el hueco. Va también en 'visible durante el paso'.")]
    public FollowTarget marker;
    public string wrongEndHint = "Esa punta se queda de este lado: mete la otra por el hueco";

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        WireGrab.Only(new[] { stripper });
    }

    public override void Tick()
    {
        if (marker != null) marker.target = conduit.IsEngaged ? conduit.waypoints[0] : conduit.Tip;

        if (!conduit.IsEngaged && Time.time >= nextHintTime && WrongEndAtEntry())
        {
            Context.ShowHint(wrongEndHint);
            nextHintTime = Time.time + 3f;
        }
        base.Tick();
    }

    public override void End()
    {
        WireGrab.Free();
        base.End();
    }

    bool WrongEndAtEntry() =>
        Vector3.Distance(WireTip.For(stripper).End, conduit.waypoints[0].position) <= conduit.entryRadius;
}
