using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Coloca este componente en el endAnchor de la GUÍA JALACABLES.
/// 
/// Detecta cuando su punta entra en contacto con un WireTipReceiver
/// (la punta del cable de luz) y los une. Al jalar la guía, arrastra
/// el cable de luz. Se desconecta automáticamente si se separan más
/// de disconnectDistance.
/// 
/// Requiere: SphereCollider con isTrigger = true en este GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WireTipConnector : MonoBehaviour
{
    // ─── Identificador de tipo ───────────────────────────────────────────────
    /// <summary>
    /// Tipo de conector. Siempre Guide para esta clase.
    /// Permite distinguirlo de PlugController en checks externos.
    /// </summary>
    public WireConnectorType connectorType => WireConnectorType.Guide;

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Configuración")]
    [Tooltip("Distancia máxima entre la punta de la guía y el receiver antes de desconectarse.")]
    public float disconnectDistance = 0.4f;

    [Tooltip("Offset local de la posición exacta donde se ancla el receiver (punta del conector).")]
    public Vector3 tipOffset = Vector3.zero;

    [Header("Wire de la guía (para validar distancia de desconexión)")]
    [Tooltip("WireController de la guía jalacables. Asignar en el inspector.")]
    public WireController guideWireController;

    [Header("Eventos")]
    public UnityEvent OnConnected;
    public UnityEvent OnDisconnected;

    // ─── Estado ──────────────────────────────────────────────────────────────
    /// <summary>¿Hay un receiver conectado actualmente?</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Receiver actualmente conectado. Null si no hay conexión.</summary>
    public WireTipReceiver ConnectedReceiver { get; private set; }

    // ─── Posición de la punta (world space) ──────────────────────────────────
    private Vector3 TipPosition => transform.TransformPoint(tipOffset);

    // ─── Trigger: detectar receiver ──────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (IsConnected) return;

        WireTipReceiver receiver = other.GetComponent<WireTipReceiver>();
        if (receiver == null) return;

        // Evitar conectar a un receiver que ya esté conectado a otro conector
        if (receiver.IsConnected) return;

        Connect(receiver);
    }

    // ─── Conexión ─────────────────────────────────────────────────────────────
    private void Connect(WireTipReceiver receiver)
    {
        IsConnected = true;
        ConnectedReceiver = receiver;

        receiver.AttachTo(this);

        OnConnected.Invoke();
        Debug.Log($"[WireTipConnector] Conectado a '{receiver.gameObject.name}' (cable de luz).");
    }

    // ─── Desconexión ─────────────────────────────────────────────────────────
    public void Disconnect()
    {
        if (!IsConnected) return;

        ConnectedReceiver?.Detach();
        ConnectedReceiver = null;
        IsConnected = false;

        OnDisconnected.Invoke();
        Debug.Log("[WireTipConnector] Desconectado.");
    }

    // ─── Update: mover receiver y vigilar desconexión ────────────────────────
    private void Update()
    {
        if (!IsConnected || ConnectedReceiver == null)
            return;

        // Mover el endAnchor del cable de luz a la punta de la guía
        ConnectedReceiver.FollowTip(TipPosition, transform.rotation);

        // Auto-desconectar si el último segmento de la guía se aleja demasiado
        if (ShouldAutoDisconnect())
        {
            Disconnect();
        }
    }

    // ─── Lógica de desconexión por distancia ─────────────────────────────────
    private bool ShouldAutoDisconnect()
    {
        if (guideWireController == null) return false;
        if (guideWireController.segments == null || guideWireController.segments.Count == 0) return false;

        Transform lastSeg = guideWireController.segments[guideWireController.segments.Count - 1];
        return Vector3.Distance(lastSeg.position, TipPosition) > disconnectDistance;
    }

    // ─── Gizmo para ver la punta en editor ───────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsConnected ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(TipPosition, 0.05f);
        Gizmos.DrawLine(transform.position, TipPosition);
    }
}
