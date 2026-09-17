using Oculus.Interaction;
using UnityEngine;

public class GrabStep : TutorialStep
{
    public Grabbable target;
    public float holdSeconds = 1.5f;
    [Tooltip("Distancia mínima entre la mano y el objeto al agarrarlo (0 = cualquier agarre). Sirve para exigir agarre a distancia.")]
    public float minGrabDistance = 0f;

    float heldFor;
    bool validGrab;

    public override float Progress => Mathf.Clamp01(heldFor / holdSeconds);
    public override bool IsComplete => heldFor >= holdSeconds;

    public override void Begin(TutorialContext context)
    {
        base.Begin(context);
        heldFor = 0f;
        validGrab = false;
        target.WhenPointerEventRaised += HandlePointerEvent;
    }

    public override void End()
    {
        target.WhenPointerEventRaised -= HandlePointerEvent;
        base.End();
    }

    public override void Tick()
    {
        if (validGrab && target.SelectingPointsCount > 0)
            heldFor += Time.deltaTime;
    }

    void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            validGrab = minGrabDistance <= 0f || NearestHandDistance() >= minGrabDistance;
            if (!validGrab)
                Context.ShowHint("Aléjate un poco y agárralo desde lejos", 3f);
        }
        else if (evt.Type == PointerEventType.Unselect && !IsComplete)
        {
            validGrab = false;
            heldFor = 0f;
        }
    }

    float NearestHandDistance()
    {
        Vector3 position = target.transform.position;
        return Mathf.Min(
            Vector3.Distance(position, Context.LeftHand.position),
            Vector3.Distance(position, Context.RightHand.position));
    }
}
