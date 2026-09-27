using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class PlugController : MonoBehaviour
{
    public bool isConected = false;
    public UnityEvent OnWirePlugged;
    public UnityEvent OnWireUnplugged;
    public Transform plugPosition;

    [Tooltip("Distance the last wire segment must be from the plug before the wire auto-disconnects.")]
    public float disconnectDistance = 0.5f;

    [HideInInspector]
    public Transform endAnchor;
    [HideInInspector]
    public Rigidbody endAnchorRB;
    [HideInInspector]
    public WireController wireController;

    // Segment joined to endAnchor; endAnchor may be either end of the wire.
    Transform adjacentSegment;

    public void OnPlugged()
    {
        OnWirePlugged.Invoke();
    }

    public void Disconnect()
    {
        isConected = false;
        if (endAnchorRB != null)
            endAnchorRB.isKinematic = false;
        OnWireUnplugged.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.name);
        if (endAnchor != null && other.gameObject == endAnchor.gameObject)
        {
            isConected = true;
            adjacentSegment = FindAdjacentSegment();
            endAnchorRB.isKinematic = true;
            endAnchor.transform.position = plugPosition.position;
            endAnchor.transform.rotation = transform.rotation;

            OnPlugged();
        }
    }

    private void Update()
    {
        if (!isConected)
            return;

        // Auto-disconnect when the wire is pulled far enough from the plug
        if (adjacentSegment != null)
        {
            if (Vector3.Distance(adjacentSegment.position, plugPosition.position) > disconnectDistance)
            {
                Disconnect();
                return;
            }
        }

        endAnchorRB.isKinematic = true;
        endAnchor.transform.position = plugPosition.position;
        Vector3 eulerRotation = new Vector3(this.transform.eulerAngles.x + 90, this.transform.eulerAngles.y, this.transform.eulerAngles.z);
        endAnchor.transform.rotation = Quaternion.Euler(eulerRotation);
    }

    Transform FindAdjacentSegment()
    {
        if (wireController == null || wireController.segments == null || wireController.segments.Count == 0)
            return null;

        Transform first = wireController.segments[0];
        Transform last = wireController.segments[wireController.segments.Count - 1];
        return Vector3.Distance(first.position, endAnchor.position) < Vector3.Distance(last.position, endAnchor.position)
            ? first
            : last;
    }
}
