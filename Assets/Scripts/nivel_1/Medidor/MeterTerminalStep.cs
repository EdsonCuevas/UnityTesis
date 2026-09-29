using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conectar una o más puntas peladas en las terminales del medidor: meterlas en su boca y apretar el
/// tornillo con el destornillador. En práctica cada punta debe quedar en su lugar; en evaluación cuenta
/// donde quede apretada y los lugares equivocados se registran como errores. Una conexión puede aceptar
/// varias puntas (los dos extremos del puente); cada punta llena una sola conexión.
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
        [Tooltip("Otras puntas que también cuentan, por ejemplo el otro extremo del puente.")]
        public WireStripper[] otherWires = new WireStripper[0];
        [Tooltip("Terminales donde va; cualquiera de ellas cuenta.")]
        public MeterTerminal[] terminals;
        [Tooltip("Aviso si la punta entra en otra terminal.")]
        public string wrongPlaceHint;

        public IEnumerable<WireStripper> Wires
        {
            get
            {
                yield return wire;
                if (otherWires == null) yield break;
                foreach (var other in otherWires)
                    if (other != null) yield return other;
            }
        }

        public bool Uses(WireStripper tip) => tip == wire || (otherWires != null && System.Array.IndexOf(otherWires, tip) >= 0);
        public bool Lists(MeterTerminal terminal) => terminal != null && System.Array.IndexOf(terminals, terminal) >= 0;
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
            startDistances[i] = Mathf.Clamp(DistanceToTerminal(connections[i]), 0.05f, 10f);

        WireGrab.Only(StepWires(), Occupants());

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
        WireGrab.Free();
        base.End();
    }

    IEnumerable<WireStripper> StepWires()
    {
        foreach (var connection in connections)
            foreach (var wire in connection.Wires)
                yield return wire;
    }

    /// <summary>
    /// Cables que un paso anterior dejó en las terminales de este (en evaluación cuenta cualquier lugar):
    /// hay que poder sacarlos para liberar la terminal.
    /// </summary>
    IEnumerable<WireStripper> Occupants()
    {
        foreach (var connection in connections)
            foreach (var terminal in connection.terminals)
                if (terminal != null && terminal.Wire != null) yield return terminal.Wire;
    }

    public override void Tick()
    {
        var pending = Pending();
        UpdateMarker(pending);

        WireStripper wire = null;
        MeterTerminal seatedIn = pending != null ? PlacedIn(pending, out wire) : null;
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
        if (terminal.Accepts(wire) && !AlreadyFilled(terminal, wire)) return;
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
        foreach (var connection in connections)
            if (connection.Uses(wire) && connection.Lists(terminal))
                lastPulledOutTime = Time.time;
    }

    /// <summary>
    /// Los dos extremos del puente entran en el conector, pero solo uno va ahí: si la conexión de esa
    /// terminal ya tiene otra de sus puntas, esta es una conexión equivocada.
    /// </summary>
    bool AlreadyFilled(MeterTerminal terminal, WireStripper wire)
    {
        foreach (var connection in connections)
        {
            if (!connection.Uses(wire) || !connection.Lists(terminal)) continue;
            foreach (var other in connection.Wires)
                if (other != wire && connection.Lists(MeterTerminal.Holding(other))) return true;
        }
        return false;
    }

    /// <summary>Terminal donde está una punta de la conexión y que cuenta para el paso, o null.</summary>
    MeterTerminal PlacedIn(Connection connection, out WireStripper placed)
    {
        var terminal = ListedPlacement(connection, out placed);
        if (terminal != null || Strict) return terminal;

        // In evaluation any terminal counts, but a tip fills only one connection.
        foreach (var candidate in connection.Wires)
        {
            terminal = MeterTerminal.Holding(candidate);
            if (terminal != null && !ClaimedByOther(connection, candidate))
            {
                placed = candidate;
                return terminal;
            }
        }
        placed = null;
        return null;
    }

    /// <summary>Primera punta de la conexión que está en una de sus terminales.</summary>
    static MeterTerminal ListedPlacement(Connection connection, out WireStripper placed)
    {
        foreach (var candidate in connection.Wires)
        {
            var terminal = MeterTerminal.Holding(candidate);
            if (connection.Lists(terminal))
            {
                placed = candidate;
                return terminal;
            }
        }
        placed = null;
        return null;
    }

    bool ClaimedByOther(Connection connection, WireStripper wire)
    {
        foreach (var other in connections)
            if (other != connection && ListedPlacement(other, out var placed) != null && placed == wire) return true;
        return false;
    }

    bool IsDone(Connection connection)
    {
        var terminal = PlacedIn(connection, out _);
        return terminal != null && terminal.State == MeterTerminal.TerminalState.Tightened;
    }

    float ConnectionProgress(int index)
    {
        var connection = connections[index];
        var terminal = PlacedIn(connection, out _);
        if (terminal != null)
            return terminal.State == MeterTerminal.TerminalState.Tightened ? 1f : 0.5f + 0.45f * terminal.screw.Progress01;

        float distance = DistanceToTerminal(connection);
        return 0.4f * Mathf.Clamp01(1f - distance / startDistances[index]);
    }

    /// <summary>Distancia de la punta libre más cercana a la terminal libre de la conexión.</summary>
    float DistanceToTerminal(Connection connection)
    {
        var target = FreeTerminal(connection);
        if (target == null) return 0f;

        float distance = float.PositiveInfinity;
        foreach (var candidate in connection.Wires)
            if (MeterTerminal.Holding(candidate) == null)
                distance = Mathf.Min(distance, Vector3.Distance(WireTip.For(candidate).End, target.transform.position));
        return distance;
    }

    /// <summary>Primera terminal libre de la conexión (la primera, si todas están ocupadas).</summary>
    static MeterTerminal FreeTerminal(Connection connection)
    {
        foreach (var terminal in connection.terminals)
            if (terminal.State == MeterTerminal.TerminalState.Empty) return terminal;
        return connection.terminals.Length > 0 ? connection.terminals[0] : null;
    }

    /// <summary>La conexión con una punta metida sin apretar o, si no hay, la primera sin terminar.</summary>
    Connection Pending()
    {
        Connection first = null;
        foreach (var connection in connections)
        {
            if (IsDone(connection)) continue;
            if (PlacedIn(connection, out _) != null) return connection;
            if (first == null) first = connection;
        }
        return first;
    }

    Connection Find(WireStripper wire)
    {
        foreach (var connection in connections)
            if (connection.Uses(wire)) return connection;
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

        var terminal = PlacedIn(pending, out _);
        marker.target = terminal != null ? terminal.screw.head : FreeTerminal(pending).transform;
    }

    static bool RecentTerminalTime(System.Func<MeterTerminal, float> time)
    {
        foreach (var terminal in MeterTerminal.All)
            if (Time.time - time(terminal) < 1f) return true;
        return false;
    }
}
