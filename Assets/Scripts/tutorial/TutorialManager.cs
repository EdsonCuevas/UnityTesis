using UnityEngine;
using UnityEngine.XR;
using TMPro;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public enum Step
    {
        Move,
        Turn,
        Grab,
        Jump,
        Complete
    }

    [Header("UI")]
    public TextMeshProUGUI instructionText;

    [Header("Escena siguiente")]
    public string UIMenuSceneName = "SampleScene";

    // Highlights
    private HighlightPart leftStickHL;
    private HighlightPart rightStickHL;
    private HighlightPart gripHL;
    private HighlightPart buttonAHL;

    private Step currentStep = Step.Move;

    private bool loadingScene = false;

    void Start()
    {
        // Busca automáticamente los objetos por nombre

        leftStickHL = BuscarHL("b_thumbstick_left");
        rightStickHL = BuscarHL("b_thumbstick");
        gripHL = BuscarHL("fb_trigger_grip");
        buttonAHL = BuscarHL("b_button_a");
    }

    HighlightPart BuscarHL(string nombreObjeto)
    {
        GameObject obj = GameObject.Find(nombreObjeto);

        if (obj == null)
        {
            Debug.LogWarning($"No encontré el objeto: {nombreObjeto}");
            return null;
        }

        HighlightPart hp = obj.GetComponent<HighlightPart>();

        if (hp == null)
        {
            Debug.LogWarning($"{nombreObjeto} no tiene HighlightPart");
        }

        return hp;
    }

    void Update()
    {
        InputDevice left =
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        InputDevice right =
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        switch (currentStep)
        {
            // =========================
            // MOVERSE
            // =========================
            case Step.Move:

                SetHighlight(leftStickHL);

                instructionText.text =
                    "🕹️ MUÉVETE\n\n" +
                    "Usa el joystick IZQUIERDO\n" +
                    "para caminar.";

                left.TryGetFeatureValue(
                    CommonUsages.primary2DAxis,
                    out Vector2 moveAxis
                );

                if (moveAxis.magnitude > 0.7f)
                {
                    NextStep();
                }

                break;

            // =========================
            // GIRAR
            // =========================
            case Step.Turn:

                SetHighlight(rightStickHL);

                instructionText.text =
                    "🔄 GIRA LA CÁMARA\n\n" +
                    "Usa el joystick DERECHO\n" +
                    "para voltear.";

                right.TryGetFeatureValue(
                    CommonUsages.primary2DAxis,
                    out Vector2 turnAxis
                );

                if (Mathf.Abs(turnAxis.x) > 0.7f)
                {
                    NextStep();
                }

                break;

            // =========================
            // AGARRAR
            // =========================
            case Step.Grab:

                SetHighlight(gripHL);

                instructionText.text =
                    "✊ AGARRA OBJETOS\n\n" +
                    "Aprieta el botón lateral\n" +
                    "(GRIP).";

                right.TryGetFeatureValue(
                    CommonUsages.grip,
                    out float gripValue
                );

                if (gripValue > 0.8f)
                {
                    NextStep();
                }

                break;

            // =========================
            // SALTAR
            // =========================
            case Step.Jump:

                SetHighlight(buttonAHL);

                instructionText.text =
                    "🦘 SALTA\n\n" +
                    "Presiona el botón A.";

                right.TryGetFeatureValue(
                    CommonUsages.primaryButton,
                    out bool aPressed
                );

                if (aPressed)
                {
                    NextStep();
                }

                break;

            // =========================
            // COMPLETADO
            // =========================
            case Step.Complete:

                ClearHighlights();

                instructionText.text =
                    "✅ ¡LISTO!\n\n" +
                    "Ya sabes usar los controles.\n\n" +
                    "Entrando al juego...";

                if (!loadingScene)
                {
                    loadingScene = true;

                    // Espera 5 segundos
                    Invoke(nameof(LoadNextScene), 5f);
                }

                break;
        }
    }

    void SetHighlight(HighlightPart active)
    {
        if (leftStickHL != null)
            leftStickHL.Highlight(active == leftStickHL);

        if (rightStickHL != null)
            rightStickHL.Highlight(active == rightStickHL);

        if (gripHL != null)
            gripHL.Highlight(active == gripHL);

        if (buttonAHL != null)
            buttonAHL.Highlight(active == buttonAHL);
    }

    void ClearHighlights()
    {
        if (leftStickHL != null)
            leftStickHL.Highlight(false);

        if (rightStickHL != null)
            rightStickHL.Highlight(false);

        if (gripHL != null)
            gripHL.Highlight(false);

        if (buttonAHL != null)
            buttonAHL.Highlight(false);
    }

    void NextStep()
    {
        currentStep++;
    }

    void LoadNextScene()
    {
        SceneManager.LoadScene(UIMenuSceneName);
    }
}