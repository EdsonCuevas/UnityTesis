using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selector 127 V / 220 V del menú principal. Cambia a qué escena llevan los botones de nivel
/// (MenuController.LoadLevel) y qué tiempos muestran.
/// </summary>
public class MenuVoltageSelector : MonoBehaviour
{
    public Button button127;
    public Button button220;
    public Color selectedColor = new Color(0.98f, 0.73f, 0.15f);
    public Color normalColor = new Color(0.22f, 0.25f, 0.3f);
    public Color selectedTextColor = new Color(0.1f, 0.1f, 0.12f);
    public Color normalTextColor = Color.white;

    [Tooltip("Botones de nivel: muestran sus tiempos del modo elegido.")]
    public MenuLevelEntry[] levelEntries;
    [Tooltip("Títulos de los botones de nivel; se les agrega el voltaje.")]
    public TMP_Text[] levelTitles;

    string[] baseTitles;

    void Start()
    {
        baseTitles = new string[levelTitles.Length];
        for (int i = 0; i < levelTitles.Length; i++) baseTitles[i] = levelTitles[i].text;

        button127.onClick.AddListener(() => Select(false));
        button220.onClick.AddListener(() => Select(true));
        Refresh();
    }

    void Select(bool is220)
    {
        VoltageMode.Is220 = is220;
        Refresh();
    }

    void Refresh()
    {
        bool is220 = VoltageMode.Is220;
        Paint(button127, !is220);
        Paint(button220, is220);

        for (int i = 0; i < levelTitles.Length; i++)
            levelTitles[i].text = $"{baseTitles[i]} · {VoltageMode.Label}";
        foreach (var entry in levelEntries)
            if (entry != null) entry.Refresh();
    }

    void Paint(Button button, bool selected)
    {
        if (button.targetGraphic != null) button.targetGraphic.color = selected ? selectedColor : normalColor;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            text.color = selected ? selectedTextColor : normalTextColor;
    }
}
