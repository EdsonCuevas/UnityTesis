using UnityEngine;

/// <summary>
/// Componente opcional para el startAnchor de cualquier cable.
/// Puede usarse para marcar el extremo de origen y exponer
/// eventos cuando el cable empieza a ser movido.
///
/// ─── Sistema de conexiones ────────────────────────────────────────────
///
///  CABLE DE LUZ (cable que será jalado por el ducto):
///    startAnchor  ──── segments[] ──── endAnchor [WireTipReceiver]
///                                           │
///                                    Es enganchado por ↓
///
///  GUÍA JALACABLES (wire que arrastra al cable de luz):
///    startAnchor  ──── segments[] ──── endAnchor [WireTipConnector]
///                                           │
///                                    Al llegar al destino:
///                                    endAnchor del cable de luz →  PlugController (socket fijo)
///
///  TIPOS DE CONEXIÓN (WireConnectorType enum):
///    • Guide  → WireTipConnector agarra WireTipReceiver  (cable-a-cable, dinámico)
///    • Plug   → PlugController   agarra el endAnchor     (socket fijo en pared/tablero)
///
/// ─── Setup en Unity ──────────────────────────────────────────────────
///  1. Cable de luz:
///     - Construir con WireBuilder normalmente.
///     - Agregar WireTipReceiver al endAnchorPoint prefab (o manualmente al instanciado).
///     - Asignar lightCableController en el inspector de WireTipReceiver.
///
///  2. Guía jalacables:
///     - Construir con WireBuilder normalmente.
///     - Agregar WireTipConnector al endAnchorPoint prefab (o manualmente al instanciado).
///     - Agregar SphereCollider con isTrigger=true al mismo GameObject.
///     - Asignar guideWireController en el inspector de WireTipConnector.
///
///  3. Socket destino (PlugController):
///     - Sin cambios en el sistema existente.
///     - El cable de luz llega al socket después de ser jalado por la guía.
/// </summary>
public class WireStartConnector : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("Tipo de cable al que pertenece este startAnchor.")]
    public WireConnectorType cableType;

    [Tooltip("Referencia al WireController de este cable.")]
    public WireController wireController;
}
