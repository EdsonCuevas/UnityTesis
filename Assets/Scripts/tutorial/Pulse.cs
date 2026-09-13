using UnityEngine;

public class Pulse : MonoBehaviour
{
    public float scaleAmount = 0.15f;
    public float bobHeight = 0f;
    public float speed = 3f;

    Vector3 baseScale;
    Vector3 basePosition;

    void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.localPosition;
    }

    void Update()
    {
        float wave = Mathf.Sin(Time.time * speed);
        transform.localScale = baseScale * (1f + wave * scaleAmount);
        transform.localPosition = basePosition + Vector3.up * (wave * bobHeight);
    }
}
