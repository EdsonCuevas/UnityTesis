using UnityEngine;

public class PlugWireStep : TutorialStep
{
    public PlugController plug;

    Collider plugTrigger;
    float startDistance;

    public override float Progress =>
        plug.isConected ? 1f : 0.9f * Mathf.Clamp01(1f - Distance() / Mathf.Max(startDistance, 0.01f));

    public override bool IsComplete => plug.isConected;

    // The socket only accepts the wire once its step starts, so earlier steps keep their order.
    void Awake()
    {
        plugTrigger = plug.GetComponent<Collider>();
        plugTrigger.enabled = false;
    }

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        plugTrigger.enabled = true;
        startDistance = Distance();
    }

    float Distance() => Vector3.Distance(plug.endAnchor.position, plug.plugPosition.position);
}
