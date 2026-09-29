using UnityEngine;

/// <summary>
/// Conectar una o más puntas peladas en las terminales del medidor: meterlas en su boca y apretar el
/// tornillo con el destornillador. En práctica cada punta debe quedar en su lugar; en evaluación cuenta
/// donde quede apretada y los lugares equivocados se registran como errores.
/// </summary>
public class MeterTerminalStep : TutorialStep
{
    const string UnstrippedHint = "Pela la punta antes de conectarla";
    const string ClosedHint = "Afloja el tornillo de la terminal para poder meter el cable";
    const string PulledOutHint = "El cable se salió: vuelve a meterlo y aprieta el tornillo";
    const string TakeScrewdriverHint = "Toma el destornillador y aprieta el tornillo de la terminal";
    const string AlignHint = "Apoya la punta del destornillador en el tornillo, de frente";
    const string TurnHint = "Mantén el gatillo y gira la muñeca a la derecha para apretar";
    const string ReleaseTriggerHint = "Suelta el gatillo para regresar la muñeca";
    const string OtherWrongHint = "Ese no es su lugar: saca el cable y revisa a dónde va";

    [System.Serializable]
    public class Connection
    {
        [Tooltip("Punta pelable del cable que se conecta.")]
        public WireStripper wire;
        [Tooltip("Terminales donde va; cualquiera de ellas cuenta.")]
        public MeterTerminal[] terminals;
        [Tooltip("Aviso si la punta entra en otra terminal.")]
        public string wrongPlaceHint;
    }

    public Connection[] connections;
    public Screwdriver screwdriver;
    [Tooltip("Flecha que señala la terminal y después su tornillo. Va también en 'visible durante el paso'.")]
    public FollowTarget marker;

    /// <summary>Puntas que entraron en una terminal equivocada durante el paso.</summary>
    public int WrongConnections { get; private set; }

    float[] startDistances;
    float seatedSince;
    WireStripper seatedWire;
    float heldSince;
    float lastPulledOutTime;
    float nextHintTime;

    // In evaluation there are no hints to fix a wrong place, so any tightened terminal counts.
    bool Strict => Context == null || Context.HintsEnabled;

    public override float Progress
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < connections.Length; i++) sum += ConnectionProgress(i);
            return connections.Length > 0 ? sum / connections.Length : 1f;
        }
    }

    public override bool IsComplete
    {
        get
        {
            foreach (var connection in connections)
                if (!IsDone(connection)) return false;
            return true;
        }
    }

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        foreach (var terminal in MeterTerminal.All) terminal.Accepting = true;
        MeterTerminal.WireInserted += OnWireInserted;
        MeterTerminal.WirePulledOut += OnWirePulledOut;

        startDistances = new float[connections.Length];
        for (int i = 0; i < connections.Length; i++)
            startDistances[i] = Mathf.Max(DistanceToTerminal(connections[i]), 0.05f);

        WrongConnections = 0;
        seatedWire = null;
        heldSince = float.PositiveInfinity;
        lastPulledOutTime = float.NegativeInfinity;
        nextHintTime = 0f;
    }

    public override void End()
    {
        MeterTerminal.WireInserted -= OnWireInserted;
        MeterTerminal.WirePulledOut -= OnWirePulledOut;
        base.End();
    }

    public override void Tick()
    {
        var pending = Pending();
        UpdateMarker(pending);

        MeterTerminal seatedIn = pending != null ? PlacedIn(pending) : null;
        var wire = seatedIn != null ? pending.wire : null;
        if (wire != seatedWire)
        {
            seatedWire = wire;
            seatedSince = Time.time;
        }

        if (!screwdriver.IsHeld) heldSince = float.PositiveInfinity;
        else if (float.IsPositiveInfinity(heldSince)) heldSince = Time.time;

        string hint = null;
        if (RecentTerminalTime(t => t.LastUnstrippedTime)) hint = UnstrippedHint;
        else if (RecentTerminalTime(t => t.LastClosedTime)) hint = ClosedHint;
        else if (Time.time - lastPulledOutTime < 1f) hint = PulledOutHint;
        else if (seatedIn != null) hint = ScrewHint(seatedIn);

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3.5f;
    }

    string ScrewHint(MeterTerminal terminal)
    {
        if (Time.time - screwdriver.LastLoosenTime < 0.5f) return ReleaseTriggerHint;
        if (!screwdriver.IsHeld) return Time.time - seatedSince > 5f ? TakeScrewdriverHint : null;
        if (screwdriver.CurrentScrew != terminal.screw)
        {
            if (Time.time - screwdriver.LastMisalignedTime < 1f) return AlignHint;
            return Time.time - heldSince > 8f ? AlignHint : null;
        }
        float idleSince = Mathf.Max(screwdriver.EngagedSince, screwdriver.LastTurnTime);
        return Time.time - idleSince > 4f ? TurnHint : null;
    }

    void OnWireInserted(MeterTerminal terminal, WireStripper wire)
    {
        if (terminal.Accepts(wire)) return;
        WrongConnections++;

        var connection = Find(wire);
        string hint = connection != null && !string.IsNullOrEmpty(connection.wrongPlaceHint)
            ? connection.wrongPlaceHint
            : OtherWrongHint;
        Context.ShowHint(hint, 3.5f);
        nextHintTime = Time.time + 3.5f;
    }

    void OnWirePulledOut(MeterTerminal terminal, WireStripper wire)
    {
        // Pulling a wrong wire out is the fix, not a mistake.
        var connection = Find(wire);
        if (connection != null && System.Array.IndexOf(connection.terminals, terminal) >= 0)
            lastPulledOutTime = Time.time;
    }

    /// <summary>Terminal donde está la punta y que cuenta para el paso, o null.</summary>
    MeterTerminal PlacedIn(Connection connection)
    {
        var terminal = MeterTerminal.Holding(connection.wire);
        if (terminal == null) return null;
        return !Strict || System.Array.IndexOf(connection.terminals, terminal) >= 0 ? terminal : null;
    }

    bool IsDone(Connection connection)
    {
        var terminal = PlacedIn(connection);
        return terminal != null && terminal.State == MeterTerminal.TerminalState.Tightened;
    }

    float ConnectionProgress(int index)
    {
        var connection = connections[index];
        var terminal = PlacedIn(connection);
        if (terminal != null)
            return terminal.State == MeterTerminal.TerminalState.Tightened ? 1f : 0.5f + 0.45f * terminal.screw.Progress01;

        float distance = DistanceToTerminal(connection);
        return 0.4f * Mathf.Clamp01(1f - distance / startDistances[index]);
    }

    float DistanceToTerminal(Connection connection)
    {
        var target = FreeTerminal(connection);
        return target != null ? Vector3.Distance(WireTip.For(connection.wire).End, target.transform.position) : 0f;
    }

    /// <summary>Primera terminal libre de la conexión (la primera, si todas están ocupadas).</summary>
    static MeterTerminal FreeTerminal(Connection connection)
    {
        foreach (var terminal in connection.terminals)
            if (terminal.State == MeterTerminal.TerminalState.Empty) return terminal;
        return connection.terminals.Length > 0 ? connection.terminals[0] : null;
    }

    Connection Pending()
    {
        foreach (var connection in connections)
            if (!IsDone(connection)) return connection;
        return null;
    }

    Connection Find(WireStripper wire)
    {
        foreach (var connection in connections)
            if (connection.wire == wire) return connection;
        return null;
    }

    void UpdateMarker(Connection pending)
    {
        if (marker == null) return;
        if (pending == null)
        {
            marker.target = null;
            return;
        }

        var terminal = PlacedIn(pending);
        marker.target = terminal != null ? terminal.screw.head : FreeTerminal(pending).transform;
    }

    static bool RecentTerminalTime(System.Func<MeterTerminal, float> time)
    {
        foreach (var terminal in MeterTerminal.All)
            if (Time.time - time(terminal) < 1f) return true;
        return false;
    }
}
