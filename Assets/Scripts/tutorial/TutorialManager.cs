using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : GuidedFlowManager
{
    [Header("Final")]
    public Transform finalPanelAnchor;
    public string finalTitle = "¡Tutorial completado!";
    [TextArea(2, 6)] public string finalBody;
    public AudioClip finalNarration;
    public AudioClip tutorialCompleteClip;
    public string nextSceneName = "SampleScene";

    [Header("Saltar")]
    [Tooltip("Segundos manteniendo B para saltar el tutorial.")]
    public float skipHoldSeconds = 2f;

    float skipHeld;
    bool finished;

    protected override void Start()
    {
        base.Start();
        ComfortSettings.ApplyToScene();
        StartFlow();
    }

    void Update()
    {
        if (finished) return;

        bool holding = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        skipHeld = holding ? skipHeld + Time.deltaTime : 0f;
        panel.SetSkipProgress(skipHeld / skipHoldSeconds);

        if (skipHeld >= skipHoldSeconds)
        {
            StopFlow();
            ShowFinal();
        }
    }

    protected override void OnAllStepsCompleted() => ShowFinal();

    void ShowFinal()
    {
        finished = true;
        panel.SetSkipVisible(false);
        panel.MoveTo(finalPanelAnchor);
        panel.Show(finalTitle, finalBody, 0, 0);
        panel.SetProgress(1f);

        if (tutorialCompleteClip != null) sfxSource.PlayOneShot(tutorialCompleteClip);
        PlayNarration(finalNarration);

        panel.SetButtons(
            new TutorialPanel.ButtonSpec("Repetir tutorial", () => LoadScene(SceneManager.GetActiveScene().name)),
            new TutorialPanel.ButtonSpec("Ir al nivel", () => LoadScene(nextSceneName)));
    }
}
