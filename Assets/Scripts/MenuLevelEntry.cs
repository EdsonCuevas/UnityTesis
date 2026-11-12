using TMPro;
using UnityEngine;

/// <summary>Muestra en un botón del menú los mejores tiempos del alumno actual en un nivel.</summary>
public class MenuLevelEntry : MonoBehaviour
{
    public string levelId;
    public TMP_Text detailText;

    void OnEnable() => StudentSession.Changed += Refresh;
    void OnDisable() => StudentSession.Changed -= Refresh;

    void Start() => Refresh();

    /// <summary>Vuelve a leer los tiempos; el id depende del voltaje elegido en el menú.</summary>
    public void Refresh()
    {
        if (!StudentSession.HasStudent)
        {
            detailText.text = "Escribe tu número de cuenta";
            return;
        }

        var record = LevelResultsStore.Load(VoltageMode.LevelIdFor(levelId));
        string alumno = StudentSession.StudentId;
        float practice = record.BestSeconds(LevelManager.ModeLabel(LevelManager.Mode.Practica), alumno);
        float exam = record.BestSeconds(LevelManager.ModeLabel(LevelManager.Mode.Evaluacion), alumno);
        if (practice == 0f && exam == 0f)
        {
            detailText.text = "Sin intentos";
            return;
        }

        detailText.text = $"Práctica {BestLabel(practice)}  ·  Evaluación {BestLabel(exam)}";
    }

    static string BestLabel(float best) => best > 0f ? LevelManager.FormatTime(best) : "--:--";
}
