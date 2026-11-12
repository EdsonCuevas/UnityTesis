using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Recorre una lista de pasos guiados con panel, línea guía e indicadores en los controles.
/// Lo usan el tutorial y los niveles.
/// </summary>
public abstract class GuidedFlowManager : MonoBehaviour
{
    [Header("Jugador")]
    public Transform head;
    public Transform rigRoot;
    public Transform leftHand;
    public Transform rightHand;
    public FirstPersonLocomotor locomotor;

    [Header("Guías")]
    public TutorialPanel panel;
    public ControllerHints controllerHints;
    public GuideLine guideLine;
    public OVRScreenFade screenFade;

    [Header("Pasos en orden")]
    public TutorialStep[] steps;

    [Header("Audio")]
    public AudioSource narrationSource;
    public AudioSource sfxSource;
    public AudioClip stepCompleteClip;

    [System.Serializable]
    public class VoiceCue
    {
        [TextArea(1, 3)] public string text;
        public AudioClip clip;
    }

    [Header("Avisos hablados")]
    [Tooltip("Voz de cada aviso; se busca por el texto exacto que muestra el panel.")]
    public VoiceCue[] voiceCues;
    [Tooltip("Un aviso que llega mientras se narra espera a lo más este tiempo; si no, se descarta.")]
    public float hintMaxWait = 3f;
    [Tooltip("Tiempo mínimo antes de volver a decir el mismo aviso.")]
    public float hintRepeatSeconds = 10f;

    [Header("Ritmo")]
    [Tooltip("Evita que un paso se complete antes de que el jugador alcance a leerlo.")]
    public float minSecondsPerStep = 1.5f;
    public float pauseBetweenSteps = 1.2f;

    static readonly string[] Praise = { "¡Muy bien!", "¡Excelente!", "¡Perfecto!", "¡Así se hace!" };
    static readonly Color PraiseColor = new Color(0.35f, 1f, 0.5f);

    protected TutorialContext Context { get; private set; }
    protected TutorialStep CurrentStep { get; private set; }

    /// <summary>Panel con descripción, línea guía, indicadores, marcadores y narración.</summary>
    protected virtual bool ShowGuides => true;

    /// <summary>Flechas sobre los botones de los controles; solo aplica si ShowGuides está activo.</summary>
    protected virtual bool ShowControllerHints => true;

    /// <summary>Id para las estadísticas (nivel1, nivel1_220, tutorial). Vacío = no se registra.</summary>
    protected virtual string StatsLevelId => string.Empty;

    /// <summary>Modo que se anota en las estadísticas (Práctica, Evaluación, Tutorial).</summary>
    protected virtual string StatsMode => string.Empty;

    /// <summary>Falso en el tutorial, que no depende del voltaje.</summary>
    protected virtual bool StatsHasVoltage => true;

    Coroutine flow;
    bool loading;

    StatsAttempt attempt;
    float attemptStartedAt;
    int currentStepIndex = -1;
    float currentStepStartedAt;
    int currentStepHintsAtStart;

    Coroutine hintVoice;
    AudioClip pendingHint;
    float pendingHintSince;
    readonly Dictionary<AudioClip, float> hintSpokenAt = new Dictionary<AudioClip, float>();

    protected virtual void Start()
    {
        Context = new TutorialContext
        {
            Head = head,
            RigRoot = rigRoot,
            LeftHand = leftHand,
            RightHand = rightHand,
            Locomotor = locomotor,
            Panel = panel,
            GuideLine = guideLine,
            Narrate = PlayNarration,
            SpeakHint = QueueHintVoice
        };

        foreach (var step in steps)
            step.SetVisible(false);
    }

    protected void StartFlow()
    {
        CloseAttempt("abandonado");
        if (!string.IsNullOrEmpty(StatsLevelId))
            attempt = StatsLogger.BeginAttempt(StatsLevelId, StatsMode, steps.Length, StatsHasVoltage);
        attemptStartedAt = Time.time;
        flow = StartCoroutine(RunSteps());
    }

    /// <summary>Detiene los pasos; si había un intento abierto se guarda con ese resultado (por ejemplo, "saltado").</summary>
    protected void StopFlow(string result = "abandonado")
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
        CloseAttempt(result);
        EndCurrentStep();
    }

    /// <summary>Segundos desde que empezó el intento actual.</summary>
    protected float AttemptSeconds => Time.time - attemptStartedAt;

    /// <summary>Se llama al terminar cada paso, antes de ocultarlo.</summary>
    protected virtual void OnStepCompleted(int index, float seconds, int hints, int errors) { }

    protected abstract void OnAllStepsCompleted();

    IEnumerator RunSteps()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            BeginStep(steps[i], i);
            currentStepIndex = i;
            currentStepStartedAt = Time.time;
            currentStepHintsAtStart = Context.HintCount;

            while (!CurrentStep.IsComplete || Time.time - currentStepStartedAt < minSecondsPerStep)
            {
                CurrentStep.Tick();
                panel.SetProgress(CurrentStep.Progress);
                yield return null;
            }

            panel.SetProgress(1f);
            Celebrate();
            float seconds = Time.time - currentStepStartedAt;
            int hints = Context.HintCount - currentStepHintsAtStart;
            int errors = StepErrors(i);
            currentStepIndex = -1;
            attempt?.LogStep(i, steps[i].title, seconds, hints, errors, true);
            OnStepCompleted(i, seconds, hints, errors);
            EndCurrentStep();
            yield return new WaitForSeconds(pauseBetweenSteps);
        }

        flow = null;
        CloseAttempt("completado");
        OnAllStepsCompleted();
    }

    int StepErrors(int index) => steps[index] is IStepErrors counted ? counted.Errors : 0;

    /// <summary>Guarda el intento abierto. Si quedó un paso a medias se anota como no terminado.</summary>
    void CloseAttempt(string result)
    {
        if (attempt == null) return;

        if (currentStepIndex >= 0 && currentStepIndex < steps.Length && Context != null)
        {
            int i = currentStepIndex;
            attempt.LogStep(i, steps[i].title, Time.time - currentStepStartedAt,
                Context.HintCount - currentStepHintsAtStart, StepErrors(i), false);
        }
        currentStepIndex = -1;
        attempt.Close(AttemptSeconds, result);
        attempt = null;
    }

    // Salir al menú, reintentar o cerrar la app a la mitad deja el intento como abandonado.
    protected virtual void OnDestroy() => CloseAttempt("abandonado");

    void OnApplicationQuit() => CloseAttempt("abandonado");

    void BeginStep(TutorialStep step, int index)
    {
        CurrentStep = step;
        pendingHint = null;
        Context.GuidesVisible = ShowGuides;
        step.Begin(Context);
        panel.MoveTo(step.panelAnchor);
        panel.Show(step.title, ShowGuides ? step.body : string.Empty, index + 1, steps.Length);

        if (!ShowGuides)
        {
            step.SetVisible(false);
            controllerHints.HideAll();
            guideLine.Hide();
            PlayNarration(null);
            return;
        }

        if (ShowControllerHints) controllerHints.Show(step.highlightParts);
        else controllerHints.HideAll();
        if (step.worldTarget != null) guideLine.Show(step.worldTarget, step.pathPoints);
        else guideLine.Hide();
        PlayNarration(step.narration);
    }

    void EndCurrentStep()
    {
        if (CurrentStep == null) return;

        CurrentStep.End();
        CurrentStep = null;
        controllerHints.HideAll();
        guideLine.Hide();
    }

    void Celebrate()
    {
        panel.ShowFeedback(Praise[Random.Range(0, Praise.Length)], PraiseColor);
        if (stepCompleteClip != null) sfxSource.PlayOneShot(stepCompleteClip);
        StartCoroutine(PulseHaptics(0.15f));
    }

    IEnumerator PulseHaptics(float seconds)
    {
        OVRInput.SetControllerVibration(1f, 0.5f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(1f, 0.5f, OVRInput.Controller.RTouch);
        yield return new WaitForSeconds(seconds);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    protected void PlayNarration(AudioClip clip)
    {
        narrationSource.Stop();
        if (clip == null) return;
        narrationSource.clip = clip;
        narrationSource.Play();
    }

    void QueueHintVoice(string text)
    {
        AudioClip clip = FindHintClip(text);
        if (clip == null) return;
        if (hintSpokenAt.TryGetValue(clip, out float spokenAt) && Time.time - spokenAt < hintRepeatSeconds) return;

        pendingHint = clip;
        pendingHintSince = Time.time;
        if (hintVoice == null) hintVoice = StartCoroutine(SpeakPendingHint());
    }

    /// <summary>Dice el aviso cuando termina la narración, sin cortarla.</summary>
    IEnumerator SpeakPendingHint()
    {
        while (pendingHint != null && narrationSource.isPlaying && Time.time - pendingHintSince <= hintMaxWait)
            yield return null;

        if (pendingHint != null && !narrationSource.isPlaying)
        {
            hintSpokenAt[pendingHint] = Time.time;
            PlayNarration(pendingHint);
        }
        pendingHint = null;
        hintVoice = null;
    }

    AudioClip FindHintClip(string text)
    {
        if (voiceCues == null) return null;
        foreach (var cue in voiceCues)
            if (cue.clip != null && cue.text == text) return cue.clip;
        return null;
    }

    protected void LoadScene(string sceneName)
    {
        if (loading) return;
        loading = true;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        pendingHint = null;
        narrationSource.Stop();
        if (screenFade != null)
        {
            screenFade.FadeOut();
            yield return new WaitForSeconds(screenFade.fadeTime);
        }
        SceneManager.LoadScene(sceneName);
    }
}
