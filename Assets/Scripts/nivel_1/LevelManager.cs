using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : GuidedFlowManager
{
    public enum Mode
    {
        Practica,
        Evaluacion
    }

    [Header("Nivel")]
    public string levelId = "nivel1";
    public string levelTitle = "Nivel 1";
    [TextArea(2, 8)] public string introBody;
    public Transform introPanelAnchor;
    public Transform resultsPanelAnchor;
    public AudioClip levelCompleteClip;
    public string menuSceneName = "UIMenu";

    public Mode CurrentMode { get; private set; }

    protected override bool ShowGuides => CurrentMode == Mode.Practica;

    readonly List<LevelStepResult> stepResults = new List<LevelStepResult>();
    float startedAt;

    protected override void Start()
    {
        base.Start();
        panel.SetSkipVisible(false);
        panel.MoveTo(introPanelAnchor);
        panel.Show(levelTitle, introBody, 0, 0);
        panel.SetButtons(
            new TutorialPanel.ButtonSpec("Modo práctica", () => BeginLevel(Mode.Practica)),
            new TutorialPanel.ButtonSpec("Modo evaluación", () => BeginLevel(Mode.Evaluacion)));
    }

    void BeginLevel(Mode mode)
    {
        panel.ClearButtons();
        CurrentMode = mode;
        Context.HintsEnabled = mode == Mode.Practica;
        stepResults.Clear();
        startedAt = Time.time;
        StartFlow();
    }

    protected override void OnStepCompleted(int index, float seconds, int hints)
    {
        stepResults.Add(new LevelStepResult { paso = steps[index].title, segundos = seconds, avisos = hints });
    }

    protected override void OnAllStepsCompleted()
    {
        int hints = 0;
        LevelStepResult slowest = null;
        foreach (var result in stepResults)
        {
            hints += result.avisos;
            if (slowest == null || result.segundos > slowest.segundos) slowest = result;
        }

        var attempt = new LevelAttempt
        {
            modo = ModeLabel(CurrentMode),
            fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            segundosTotales = Time.time - startedAt,
            avisos = hints,
            pasos = stepResults.ToArray()
        };
        var record = LevelResultsStore.SaveAttempt(levelId, attempt);

        string body =
            $"Modo: {attempt.modo}\n" +
            $"Tiempo total: {FormatTime(attempt.segundosTotales)}\n" +
            $"Mejor tiempo en este modo: {FormatTime(record.BestSeconds(attempt.modo))}\n" +
            $"{(CurrentMode == Mode.Practica ? "Avisos recibidos" : "Dificultades detectadas")}: {hints}\n" +
            (slowest != null ? $"Paso más tardado: {slowest.paso} ({FormatTime(slowest.segundos)})" : string.Empty);

        panel.MoveTo(resultsPanelAnchor);
        panel.Show("¡Nivel completado!", body, 0, 0);
        panel.SetProgress(1f);
        if (levelCompleteClip != null) sfxSource.PlayOneShot(levelCompleteClip);

        panel.SetButtons(
            new TutorialPanel.ButtonSpec("Reintentar", () => LoadScene(SceneManager.GetActiveScene().name)),
            new TutorialPanel.ButtonSpec("Menú principal", () => LoadScene(menuSceneName)));
    }

    static string ModeLabel(Mode mode) => mode == Mode.Practica ? "Práctica" : "Evaluación";

    static string FormatTime(float seconds)
    {
        int total = Mathf.RoundToInt(seconds);
        return $"{total / 60:00}:{total % 60:00}";
    }
}
