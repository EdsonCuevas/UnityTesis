using UnityEngine;

public class ReachZoneStep : TutorialStep
{
    public float radius = 0.7f;
    [Tooltip("Segundos que el jugador debe correr durante el paso (0 = no se exige correr).")]
    public float requiredRunSeconds = 0f;

    float startDistance;
    float runSeconds;
    bool reached;
    float nextHintTime;

    public override float Progress
    {
        get
        {
            float approach = Mathf.Clamp01(1f - (Distance() - radius) / Mathf.Max(startDistance - radius, 0.01f));
            if (requiredRunSeconds <= 0f) return approach;
            return 0.5f * approach + 0.5f * Mathf.Clamp01(runSeconds / requiredRunSeconds);
        }
    }

    public override bool IsComplete => reached && runSeconds >= requiredRunSeconds;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        startDistance = Distance();
        runSeconds = 0f;
        reached = false;
        nextHintTime = 0f;
    }

    public override void Tick()
    {
        if (Context.Locomotor.IsRunning && FlatSpeed() > 0.5f)
            runSeconds += Time.deltaTime;

        if (Distance() <= radius)
            reached = true;

        if (reached && runSeconds < requiredRunSeconds && Time.time > nextHintTime)
        {
            Context.ShowHint("Mantén hundido el joystick izquierdo mientras caminas para correr", 3f);
            nextHintTime = Time.time + 4f;
        }
    }

    float Distance()
    {
        Vector3 delta = worldTarget.position - Context.Head.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    float FlatSpeed()
    {
        Vector3 velocity = Context.Locomotor.Velocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }
}
