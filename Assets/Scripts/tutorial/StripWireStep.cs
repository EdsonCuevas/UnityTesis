using UnityEngine;

public class StripWireStep : TutorialStep
{
    public WireStripper stripper;
    [Tooltip("Rigidbody de la punta del cable: queda fijo en la prensa durante el paso.")]
    public Rigidbody cableTip;
    [Tooltip("Agarres de la punta del cable que se desactivan mientras está en la prensa.")]
    public GameObject[] disabledDuringStep;

    Vector3 clampPosition;
    Quaternion clampRotation;
    bool wasKinematic;

    public override float Progress => stripper.Progress01;
    public override bool IsComplete => stripper.IsStripped;

    void Awake()
    {
        clampPosition = cableTip.position;
        clampRotation = cableTip.rotation;
        stripper.enabled = false;
    }

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        wasKinematic = cableTip.isKinematic;
        cableTip.isKinematic = true;
        cableTip.transform.SetPositionAndRotation(clampPosition, clampRotation);
        foreach (var go in disabledDuringStep)
            go.SetActive(false);
        stripper.enabled = true;
    }

    public override void End()
    {
        stripper.enabled = false;
        cableTip.isKinematic = wasKinematic;
        foreach (var go in disabledDuringStep)
            go.SetActive(true);
        base.End();
    }
}
