using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Coloca este componente en el endAnchor del CABLE DE LUZ.
/// 
/// Es el extremo que será capturado por WireTipConnector (la guía jalacables)
/// y arrastrado a través del ducto / canaleta.
/// 
/// También puede conectarse a un PlugController (socket fijo) como destino final,
/// usando el sistema existente — estos dos tipos de conexión son independientes
/// y se diferencian por WireConnectorType.
/// 
/// Requiere: Rigidbody en este GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WireTipReceiver : MonoBehaviour
{
    // ─── Identificador de tipo ───────────────────────────────────────────────
    /// <summary>
    /// Este extremo acepta conexiones de tipo Guide (guía jalacables).
    /// El PlugController (tipo Plug) lo maneja por separado.
    /// </summary>
    public WireConnectorType acceptedConnectorType => WireConnectorType.Guide;

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Referencias del cable de luz")]
    [Tooltip("WireController del cable de luz. Necesario para referencia y debug.")]
    public WireController lightCableController;

    [Header("Evento al ser conectado/desconectado por la guía")]
    public UnityEvent OnAttachedToGuide;
    public UnityEvent OnDetachedFromGuide;

    // ─── Estado ──────────────────────────────────────────────────────────────
    /// <summary>¿Está siendo jalado por una WireTipConnector ahora mismo?</summary>
    public bool IsConnected { get; private set; }

    /// <summary>El conector de guía que está jalando este receiver.</summary>
    public WireTipConnector CurrentConnector { get; private set; }

    // ─── Componentes ─────────────────────────────────────────────────────────
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // ─── API llamada desde WireTipConnector ──────────────────────────────────

    /// <summary>
    /// Llamado por WireTipConnector cuando engancha este receiver.
    /// Hace el Rigidbody kinematic para que sea movido por la guía.
    /// </summary>
    public void AttachTo(WireTipConnector connector)
    {
        IsConnected = true;
        CurrentConnector = connector;
        _rb.isKinematic = true;

        OnAttachedToGuide.Invoke();
    }

    /// <summary>
    /// Llamado por WireTipConnector cada frame para mover este receiver
    /// a la posición de la punta de la guía.
    /// </summary>
    public void FollowTip(Vector3 worldPosition, Quaternion rotation)
    {
        transform.position = worldPosition;
        transform.rotation = rotation;
    }

    /// <summary>
    /// Llamado por WireTipConnector al desconectarse (por distancia o manual).
    /// Devuelve la física al Rigidbody.
    /// </summary>
    public void Detach()
    {
        IsConnected = false;
        CurrentConnector = null;
        _rb.isKinematic = false;

        OnDetachedFromGuide.Invoke();
    }

    // ─── Desconexión manual desde este lado (p.ej. botón en UI) ─────────────
    /// <summary>
    /// Desconecta activamente desde el lado del cable de luz.
    /// Útil para botones de UI o eventos externos.
    /// </summary>
    public void ForceDetach()
    {
        if (CurrentConnector != null)
            CurrentConnector.Disconnect();
        else
            Detach();
    }

    // ─── Diferenciación en runtime ───────────────────────────────────────────
    /// <summary>
    /// Verifica si el objeto dado es un conector compatible (tipo Guide).
    /// Útil para checks externos sin hard-casting.
    /// </summary>
    public static bool IsGuideConnector(GameObject obj)
    {
        WireTipConnector c = obj.GetComponent<WireTipConnector>();
        return c != null; // WireTipConnector siempre es Guide por diseño
    }

    /// <summary>
    /// Verifica si el objeto dado es un socket fijo (tipo Plug).
    /// </summary>
    public static bool IsPlugConnector(GameObject obj)
    {
        PlugController p = obj.GetComponent<PlugController>();
        return p != null;
    }

    // ─── Gizmo ───────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsConnected ? Color.cyan : Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.07f);
    }
}
