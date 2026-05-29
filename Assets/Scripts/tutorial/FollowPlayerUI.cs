using UnityEngine;

public class FollowPlayerUI : MonoBehaviour
{
    public Transform playerHead;
    public float followSpeed = 5f;
    public float distance = 1.2f;
    public float heightOffset = -0.1f;

    void LateUpdate()
    {
        Vector3 forward = playerHead.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 targetPos = playerHead.position
            + forward * distance
            + Vector3.up * heightOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * followSpeed
        );

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            Quaternion.LookRotation(
                transform.position - playerHead.position
            ),
            Time.deltaTime * followSpeed
        );
    }
}