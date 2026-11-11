using UnityEngine;

/// <summary>
/// Servicio elegido en el menú: 127 V (monofásico) o 220 V (bifásico). Cada nivel de 220 V es una escena
/// aparte con el sufijo "_220"; se guarda en PlayerPrefs para que se conserve entre sesiones.
/// </summary>
public static class VoltageMode
{
    const string Key = "modo_220";
    const string Suffix = "_220";

    public static bool Is220
    {
        get => PlayerPrefs.GetInt(Key, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static string Label => Is220 ? "220 V" : "127 V";

    /// <summary>Escena del nivel en el modo elegido. El nivel 1 de 127 V se llama SampleScene.</summary>
    public static string SceneFor(string sceneName)
    {
        if (!Is220 || string.IsNullOrEmpty(sceneName) || sceneName.EndsWith(Suffix)) return sceneName;
        return sceneName switch
        {
            "SampleScene" => "Nivel1" + Suffix,
            "Nivel2" => "Nivel2" + Suffix,
            _ => sceneName
        };
    }

    /// <summary>Id de resultados del nivel en el modo elegido.</summary>
    public static string LevelIdFor(string levelId) =>
        Is220 && !string.IsNullOrEmpty(levelId) && !levelId.EndsWith(Suffix) ? levelId + Suffix : levelId;
}
