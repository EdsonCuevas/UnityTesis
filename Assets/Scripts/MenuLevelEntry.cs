using TMPro;
using UnityEngine;

/// <summary>Muestra en un botón del menú los mejores tiempos guardados de un nivel.</summary>
public class MenuLevelEntry : MonoBehaviour
{
    public string levelId;
    public TMP_Text detailText;

    void Start()
    {
        var record = LevelResultsStore.Load(levelId);
        if (record.intentos.Count == 0)
        {
            detailText.text = "Sin intentos";
            return;
        }

        detailText.text =
            $"Práctica {BestLabel(record, LevelManager.Mode.Practica)}  ·  " +
            $"Evaluación {BestLabel(record, LevelManager.Mode.Evaluacion)}";
    }

    static string BestLabel(LevelRecord record, LevelManager.Mode mode)
    {
        float best = record.BestSeconds(LevelManager.ModeLabel(mode));
        return best > 0f ? LevelManager.FormatTime(best) : "--:--";
    }
}
