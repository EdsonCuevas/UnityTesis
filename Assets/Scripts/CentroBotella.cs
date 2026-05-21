using UnityEngine;

public class EnderezarBotella : MonoBehaviour
{
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Si está casi quieta
        if (rb.linearVelocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.05f)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.fixedDeltaTime * 5f
            );

            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}