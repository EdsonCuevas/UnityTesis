using System.Collections;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
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

    [Header("Final")]
    public string finalTitle = "¡Tutorial completado!";
    [TextArea(2, 6)] public string finalBody;
    public AudioClip finalNarration;
    public string nextSceneName = "SampleScene";

    [Header("Audio")]
    public AudioSource narrationSource;
    public AudioSource sfxSource;
    public AudioClip stepCompleteClip;
    public AudioClip tutorialCompleteClip;

    [Header("Ritmo")]
    [Tooltip("Evita que un paso se complete antes de que el jugador alcance a leerlo.")]
    public float minSecondsPerStep = 1.5f;
    public float pauseBetweenSteps = 1.2f;
    [Tooltip("Segundos manteniendo B para saltar el tutorial.")]
    public float skipHoldSeconds = 2f;

    static readonly string[] Praise = { "¡Muy bien!", "¡Excelente!", "¡Perfecto!", "¡Así se hace!" };
    static readonly Color PraiseColor = new Color(0.35f, 1f, 0.5f);

    TutorialContext context;
    TutorialStep currentStep;
    Coroutine flow;
    float skipHeld;
    bool finished;
    bool loading;

    void Start()
    {
        context = new TutorialContext
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

        ComfortSettings.ApplyToScene();
        flow = StartCoroutine(RunSteps());
    }

    void Update()
    {
        if (finished) return;

        bool holding = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        skipHeld = holding ? skipHeld + Time.deltaTime : 0f;
        panel.SetSkipProgress(skipHeld / skipHoldSeconds);

        if (skipHeld >= skipHoldSeconds)
        {
            StopCoroutine(flow);
            EndCurrentStep();
            ShowFinal();
        }
    }

    IEnumerator RunSteps()
    {
        for (int i = 0; i < steps.Length; i++)
        {
            BeginStep(steps[i], i);
            float startedAt = Time.time;

            while (!currentStep.IsComplete || Time.time - startedAt < minSecondsPerStep)
            {
                currentStep.Tick();
                panel.SetProgress(currentStep.Progress);
                yield return null;
            }

            panel.SetProgress(1f);
            Celebrate();
            EndCurrentStep();
            yield return new WaitForSeconds(pauseBetweenSteps);
        }

        ShowFinal();
    }

    void BeginStep(TutorialStep step, int index)
    {
        currentStep = step;
        step.Begin(context);
        panel.Show(step.title, step.body, index + 1, steps.Length);
        controllerHints.Show(step.highlightParts);

        if (step.worldTarget != null) guideLine.Show(step.worldTarget);
        else guideLine.Hide();

        PlayNarration(step.narration);
        StartCoroutine(PulseHaptics(true, false, 0.12f));
    }

    void EndCurrentStep()
    {
        if (currentStep == null) return;

        currentStep.End();
        currentStep = null;
        controllerHints.HideAll();
        guideLine.Hide();
    }

    void Celebrate()
    {
        panel.ShowFeedback(Praise[Random.Range(0, Praise.Length)], PraiseColor);
        if (stepCompleteClip != null) sfxSource.PlayOneShot(stepCompleteClip);
        StartCoroutine(PulseHaptics(true, true, 0.15f));
    }

    IEnumerator PulseHaptics(bool left, bool right, float seconds)
    {
        if (left) OVRInput.SetControllerVibration(1f, 0.5f, OVRInput.Controller.LTouch);
        if (right) OVRInput.SetControllerVibration(1f, 0.5f, OVRInput.Controller.RTouch);
        yield return new WaitForSeconds(seconds);
        if (left) OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        if (right) OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }

    void ShowFinal()
    {
        finished = true;
        panel.SetSkipVisible(false);
        panel.Show(finalTitle, finalBody, 0, 0);
        panel.SetProgress(1f);

        if (tutorialCompleteClip != null) sfxSource.PlayOneShot(tutorialCompleteClip);
        PlayNarration(finalNarration);

        panel.SetButtons(
            new TutorialPanel.ButtonSpec("Repetir tutorial", () => LoadScene(SceneManager.GetActiveScene().name)),
            new TutorialPanel.ButtonSpec("Ir al nivel", () => LoadScene(nextSceneName)));
    }

    void PlayNarration(AudioClip clip)
    {
        narrationSource.Stop();
        if (clip == null) return;
        narrationSource.clip = clip;
        narrationSource.Play();
    }

    void LoadScene(string sceneName)
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
