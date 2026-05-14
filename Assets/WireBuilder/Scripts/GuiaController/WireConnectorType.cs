/// <summary>
/// Identifica el tipo de conector en el sistema de cables.
/// Permite diferenciar en runtime si un objeto es un socket fijo (Plug)
/// o una conexión dinámica cable-a-cable (Guide).
/// </summary>
public enum WireConnectorType
{
    /// <summary>
    /// Socket fijo en la pared / tablero. Usa PlugController.
    /// El endAnchor del cable se ancla a una posición estática en la escena.
    /// </summary>
    Plug,

    /// <summary>
    /// Punta de la guía jalacables. Usa WireTipConnector.
    /// El endAnchor del cable de luz sigue dinámicamente a la punta de la guía.
    /// </summary>
    Guide
}
