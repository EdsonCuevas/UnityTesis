using System.Collections;
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

    Coroutine flow;
    bool loading;

    protected virtual void Start()
    {
        Context = new TutorialContext
        {
            Head = head,
            RigRoot = rigRoot,
            LeftHand = leftHand,
            RightHand = rightHand,
            Locomotor = locomotor,
            Panel = panel
        };

        foreach (var step in steps)
            step.SetVisible(false);
    }

    protected void StartFlow() => flow = StartCoroutine(RunSteps());

    protected void StopFlow()
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
        EndCurrentStep();
    }

    /// <summary>Se llama al terminar cada paso, antes de ocultarlo.</summary>
    protected virtual void OnStepCompleted(int index, float seconds, int hints) { }

    protected abstract void OnAllStepsCompleted();

    IEnumerator RunSteps()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            BeginStep(steps[i], i);
            float startedAt = Time.time;
            int hintsAtStart = Context.HintCount;

            while (!CurrentStep.IsComplete || Time.time - startedAt < minSecondsPerStep)
            {
                CurrentStep.Tick();
                panel.SetProgress(CurrentStep.Progress);
                yield return null;
            }

            panel.SetProgress(1f);
            Celebrate();
            OnStepCompleted(i, Time.time - startedAt, Context.HintCount - hintsAtStart);
            EndCurrentStep();
            yield return new WaitForSeconds(pauseBetweenSteps);
        }

        flow = null;
        OnAllStepsCompleted();
    }

    void BeginStep(TutorialStep step, int index)
    {
        CurrentStep = step;
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

        controllerHints.Show(step.highlightParts);
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

    protected void LoadScene(string sceneName)
    {
        if (loading) return;
        loading = true;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        narrationSource.Stop();
        if (screenFade != null)
        {
            screenFade.FadeOut();
            yield return new WaitForSeconds(screenFade.fadeTime);
        }
        SceneManager.LoadScene(sceneName);
    }
}
