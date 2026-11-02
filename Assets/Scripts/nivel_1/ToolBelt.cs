using UnityEngine;

/// <summary>
/// Cinto de herramientas a la altura de la cadera. El rig solo sigue cabeza y manos, así que la cadera se
/// estima desde la cabeza: no se mueve al inclinar la cabeza y solo gira cuando el cuerpo ya giró.
/// </summary>
[DefaultExecutionOrder(100)]
public class ToolBelt : MonoBehaviour
{
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Altura")]
    [Tooltip("Metros de los ojos a la cadera.")]
    public float hipDrop = 0.62f;
    [Tooltip("Punto del cuello alrededor del cual gira la cabeza, medido desde los ojos.")]
    public Vector3 neckOffset = new Vector3(0f, -0.1f, -0.09f);

    [Header("Giro")]
    [Tooltip("Grados que puede girar la cabeza antes de que el cinto la siga.")]
    public float yawDeadZone = 35f;
    [Tooltip("Qué tan rápido alcanza el cinto el giro del cuerpo.")]
    public float yawFollowSpeed = 4f;
    [Tooltip("Al mirar más abajo que este ángulo el cinto deja de girar: la vista ya no indica hacia dónde está el cuerpo.")]
    public float maxPitchForYaw = 55f;

    float yaw;
    bool hasYaw;
    OVRInput.Controller vibratingHand = OVRInput.Controller.None;

    void LateUpdate() => FollowHead();

    /// <summary>Lleva el cinto a la cadera. Las fundas lo llaman antes de colocar su herramienta.</summary>
    public void FollowHead()
    {
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        bool canReadYaw = forward.sqrMagnitude > 1e-4f && Vector3.Angle(head.forward, forward) < maxPitchForYaw;
        if (canReadYaw)
        {
            float headYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!hasYaw)
            {
                yaw = headYaw;
                hasYaw = true;
            }

            float delta = Mathf.DeltaAngle(yaw, headYaw);
            float excess = Mathf.Abs(delta) - yawDeadZone;
            if (excess > 0f)
                yaw += Mathf.Sign(delta) * excess * Mathf.Clamp01(yawFollowSpeed * Time.deltaTime);
        }

        Vector3 neck = head.TransformPoint(neckOffset);
        Vector3 position = new Vector3(neck.x, head.position.y - hipDrop, neck.z);
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    public void Vibrate(Vector3 near, float amplitude, float seconds)
    {
        StopVibration();
        vibratingHand = Vector3.Distance(near, leftHand.position) < Vector3.Distance(near, rightHand.position)
            ? OVRInput.Controller.LTouch
            : OVRInput.Controller.RTouch;
        OVRInput.SetControllerVibration(0.5f, amplitude, vibratingHand);
        CancelInvoke(nameof(StopVibration));
        Invoke(nameof(StopVibration), seconds);
    }

    void StopVibration()
    {
        if (vibratingHand == OVRInput.Controller.None) return;
        OVRInput.SetControllerVibration(0f, 0f, vibratingHand);
        vibratingHand = OVRInput.Controller.None;
    }
}
