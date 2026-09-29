using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tirón de prueba: agarrar con el grip cada cable cerca de su terminal y jalarlo unos centímetros para
/// comprobar que quedó firme. El tramo acomodado está fijo y no se puede agarrar con las manos de Meta, así
/// que el agarre se detecta por la distancia del control al cable; el cable se estira unos milímetros
/// hacia la mano y vibra. Cada punta se prueba en la terminal donde haya quedado.
/// </summary>
public class TugTestStep : TutorialStep
{
    const string StartHint = "Agarra con el grip cada cable cerca de su terminal y jálalo hacia ti";
    const string NearHint = "Acerca la mano al cable que brilla, junto a su terminal";
    const string PullHint = "Jala el cable unos centímetros hacia ti";

    static readonly Color TestedColor = new Color(0.35f, 1f, 0.5f);

    [Tooltip("Puntas que se prueban.")]
    public WireStripper[] wires;
    [Tooltip("Flecha que señala el siguiente cable. Va también en 'visible durante el paso'.")]
    public FollowTarget marker;

    [Header("Agarre")]
    [Range(0f, 1f)] public float gripThreshold = 0.6f;
    [Tooltip("Distancia máxima del control al cable para agarrarlo.")]
    public float grabRadius = 0.06f;
    [Tooltip("Largo del cable, desde la boca de la terminal, donde cuenta agarrarlo.")]
    public float reach = 0.08f;

    [Header("Tirón")]
    [Tooltip("Cuánto hay que alejar la mano mientras se sostiene el cable.")]
    public float tugDistance = 0.03f;
    [Tooltip("Lo más que se estira el cable a la vista.")]
    public float maxStretch = 0.006f;
    [Tooltip("Eslabones después del agarrado que también se mueven con el tirón.")]
    public int falloffLinks = 4;
    [Tooltip("Distancia a lo largo del cable donde se pone la flecha.")]
    public float markerAlong = 0.03f;

    class Hand
    {
        public OVRInput.Controller controller;
        public Transform transform;
        public bool gripWas;
        public int target = -1;
        public int link;
        public Vector3 start;
        public float tugSince;
        public float strongUntil;
        public readonly List<Transform> nudged = new List<Transform>();
    }

    Hand[] hands;
    bool[] tested;
    WireGrab[] owners;
    float startedAt;
    float lastMissTime;
    float nextHintTime;

    public override float Progress
    {
        get
        {
            if (tested == null || tested.Length == 0) return 1f;
            int count = 0;
            foreach (bool done in tested)
                if (done) count++;
            return (float)count / tested.Length;
        }
    }

    public override bool IsComplete => tested != null && Progress >= 1f;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        hands = new[]
        {
            new Hand { controller = OVRInput.Controller.LTouch, transform = context.LeftHand },
            new Hand { controller = OVRInput.Controller.RTouch, transform = context.RightHand }
        };
        tested = new bool[wires.Length];
        owners = new WireGrab[wires.Length];
        for (int i = 0; i < wires.Length; i++) owners[i] = wires[i].GetComponentInParent<WireGrab>(true);

        startedAt = Time.time;
        lastMissTime = float.NegativeInfinity;
        nextHintTime = 0f;
        WireGrab.Only(Untested());
    }

    public override void End()
    {
        foreach (var hand in hands) Release(hand);
        foreach (var owner in owners)
            if (owner != null) owner.Emphasized = false;
        MeterTerminal.Nudges.Clear();
        WireGrab.Free();
        base.End();
    }

    IEnumerable<WireStripper> Untested()
    {
        for (int i = 0; i < wires.Length; i++)
            if (!tested[i]) yield return wires[i];
    }

    public override void Tick()
    {
        foreach (var owner in owners)
            if (owner != null) owner.Emphasized = false;
        foreach (var hand in hands) UpdateHand(hand);

        UpdateMarker();
        UpdateHints();
    }

    void UpdateHand(Hand hand)
    {
        bool grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, hand.controller) >= gripThreshold;
        Vector3 position = hand.transform.position;

        if (hand.target >= 0)
        {
            var terminal = TightTerminal(hand.target);
            if (!grip || terminal == null) Release(hand);
            else Pull(hand, terminal, position);
        }
        else
        {
            int target = Nearest(position, out int link);
            if (target >= 0 && owners[target] != null) owners[target].Emphasized = true;

            if (grip && !hand.gripWas)
            {
                if (target >= 0)
                {
                    hand.target = target;
                    hand.link = link;
                    hand.start = position;
                    hand.tugSince = Time.time;
                }
                else lastMissTime = Time.time;
            }
        }
        hand.gripWas = grip;
    }

    void Pull(Hand hand, MeterTerminal terminal, Vector3 position)
    {
        if (owners[hand.target] != null) owners[hand.target].Emphasized = true;

        Vector3 pull = position - hand.start;
        float amount = pull.magnitude;
        Nudge(hand, terminal, Vector3.ClampMagnitude(pull * 0.2f, maxStretch));

        if (!tested[hand.target] && amount >= tugDistance)
        {
            tested[hand.target] = true;
            hand.strongUntil = Time.time + 0.15f;
            OVRInput.SetControllerVibration(0.5f, 0.9f, hand.controller);
            WireGrab.Only(Untested());

            int count = Mathf.RoundToInt(Progress * tested.Length);
            Context.Panel.ShowFeedback($"Conexión firme ({count} de {tested.Length})", TestedColor, 1.5f);
            return;
        }
        if (Time.time >= hand.strongUntil)
            OVRInput.SetControllerVibration(0.3f, 0.3f * Mathf.Clamp01(amount / tugDistance), hand.controller);
    }

    /// <summary>Mueve el tramo agarrado hacia la mano: nada en la punta, todo en el eslabón agarrado y menos después.</summary>
    void Nudge(Hand hand, MeterTerminal terminal, Vector3 offset)
    {
        var links = terminal.Seated.Links;
        int last = Mathf.Min(terminal.DressedLinks, hand.link + falloffLinks);
        for (int j = 1; j <= last; j++)
        {
            float weight = j <= hand.link ? (float)j / hand.link : 1f - (float)(j - hand.link) / (falloffLinks + 1);
            MeterTerminal.Nudges[links[j]] = offset * weight;
            if (!hand.nudged.Contains(links[j])) hand.nudged.Add(links[j]);
        }
    }

    void Release(Hand hand)
    {
        foreach (var link in hand.nudged) MeterTerminal.Nudges.Remove(link);
        hand.nudged.Clear();
        if (hand.target >= 0) OVRInput.SetControllerVibration(0f, 0f, hand.controller);
        hand.target = -1;
    }

    /// <summary>
    /// Cable acomodado más cercano al punto, dentro del alcance desde su terminal. Los extremos del puente
    /// comparten eslabones, así que a igual distancia gana la terminal más cercana a lo largo del cable.
    /// </summary>
    int Nearest(Vector3 point, out int nearestLink)
    {
        int nearest = -1;
        nearestLink = -1;
        float best = float.PositiveInfinity;
        for (int i = 0; i < wires.Length; i++)
        {
            var terminal = TightTerminal(i);
            if (terminal == null) continue;

            var links = terminal.Seated.Links;
            Vector3 previous = terminal.transform.position;
            float along = 0f;
            for (int j = 1; j <= terminal.DressedLinks; j++)
            {
                Vector3 position = links[j].position;
                along += Vector3.Distance(previous, position);
                previous = position;
                if (along > reach) break;

                float distance = Vector3.Distance(point, position);
                if (distance > grabRadius) continue;
                float score = distance + 0.05f * along;
                if (score >= best) continue;
                best = score;
                nearest = i;
                nearestLink = j;
            }
        }
        return nearest;
    }

    MeterTerminal TightTerminal(int index)
    {
        var terminal = MeterTerminal.Holding(wires[index]);
        return terminal != null && terminal.State == MeterTerminal.TerminalState.Tightened && terminal.DressedLinks > 0
            ? terminal
            : null;
    }

    void UpdateMarker()
    {
        if (marker == null) return;
        marker.target = null;
        for (int i = 0; i < wires.Length; i++)
        {
            if (tested[i]) continue;
            var terminal = TightTerminal(i);
            if (terminal == null) continue;
            marker.target = LinkAt(terminal, markerAlong);
            return;
        }
    }

    static Transform LinkAt(MeterTerminal terminal, float distance)
    {
        var links = terminal.Seated.Links;
        Vector3 previous = terminal.transform.position;
        float along = 0f;
        for (int j = 1; j <= terminal.DressedLinks; j++)
        {
            along += Vector3.Distance(previous, links[j].position);
            previous = links[j].position;
            if (along >= distance) return links[j];
        }
        return links[terminal.DressedLinks];
    }

    void UpdateHints()
    {
        string hint = null;
        if (Time.time - lastMissTime < 1f) hint = NearHint;
        else
        {
            foreach (var hand in hands)
                if (hand.target >= 0 && !tested[hand.target] && Time.time - hand.tugSince > 3f)
                    hint = PullHint;
            if (hint == null && Progress <= 0f && Time.time - startedAt > 10f) hint = StartHint;
        }

        if (hint == null || Time.time < nextHintTime) return;
        Context.ShowHint(hint);
        nextHintTime = Time.time + 3.5f;
    }
}
