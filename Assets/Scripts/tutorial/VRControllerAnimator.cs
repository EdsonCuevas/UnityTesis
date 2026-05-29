using UnityEngine;
using UnityEngine.XR;

public class VRControllerAnimator : MonoBehaviour
{
    [Header("Partes del control")]
    public Transform trigger;
    public Transform grip;
    public Transform thumbstick;
    public Transform buttonA;
    public Transform buttonB;

    private Vector3 triggerStartPos;
    private Vector3 gripStartPos;
    private Vector3 buttonAStartPos;
    private Vector3 buttonBStartPos;
    private Quaternion thumbstickStartRot;

    void Start()
    {
        triggerStartPos = trigger.localPosition;
        gripStartPos = grip.localPosition;
        buttonAStartPos = buttonA.localPosition;
        buttonBStartPos = buttonB.localPosition;
        thumbstickStartRot = thumbstick.localRotation;
    }

    void Update()
    {
        InputDevice right = InputDevices.GetDeviceAtXRNode(
            XRNode.RightHand);

        AnimateTrigger(right);
        AnimateGrip(right);
        AnimateThumbstick(right);
        AnimateButtonA(right);
        AnimateButtonB(right);
    }

    void AnimateTrigger(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.trigger, out float val))
        {
            trigger.localPosition = triggerStartPos
                + new Vector3(0, 0, -val * 0.008f);
        }
    }

    void AnimateGrip(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.grip, out float val))
        {
            grip.localRotation =
                Quaternion.Euler(val * 15f, 0, 0);
        }
    }

    void AnimateThumbstick(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.primary2DAxis, out Vector2 axis))
        {
            thumbstick.localRotation = thumbstickStartRot *
                Quaternion.Euler(
                    axis.y * 15f,
                    0,
                    -axis.x * 15f
                );
        }
    }

    void AnimateButtonA(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.primaryButton, out bool pressed))
        {
            buttonA.localPosition = buttonAStartPos
                + (pressed
                    ? new Vector3(0, -0.003f, 0)
                    : Vector3.zero);
        }
    }

    void AnimateButtonB(InputDevice device)
    {
        if (device.TryGetFeatureValue(
            CommonUsages.secondaryButton, out bool pressed))
        {
            buttonB.localPosition = buttonBStartPos
                + (pressed
                    ? new Vector3(0, -0.003f, 0)
                    : Vector3.zero);
        }
    }
}