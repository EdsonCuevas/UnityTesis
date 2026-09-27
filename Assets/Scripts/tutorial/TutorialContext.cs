using Oculus.Interaction.Locomotion;
using UnityEngine;

public class TutorialContext
{
    static readonly Color HintColor = new Color(1f, 0.8f, 0.3f);
    const float SameHintWindow = 8f;

    public Transform Head;
    public Transform RigRoot;
    public Transform LeftHand;
    public Transform RightHand;
    public FirstPersonLocomotor Locomotor;
    public TutorialPanel Panel;
    public GuideLine GuideLine;

    // Falso en modo evaluación: sin línea guía ni marcadores.
    public bool GuidesVisible = true;

    // En modo evaluación los avisos se cuentan pero no se muestran.
    public bool HintsEnabled = true;

    // Los asigna el manager: narración que interrumpe lo que suena y voz de un aviso, que espera su turno.
    public System.Action<AudioClip> Narrate;
    public System.Action<string> SpeakHint;

    public int HintCount { get; private set; }

    string lastHint;
    float lastHintTime = float.NegativeInfinity;

    public void ShowHint(string text, float seconds = 2.5f)
    {
        // A hint repeated while the same problem persists counts once.
        if (text != lastHint || Time.time - lastHintTime > SameHintWindow)
            HintCount++;
        lastHint = text;
        lastHintTime = Time.time;

        if (!HintsEnabled) return;
        Panel.ShowFeedback(text, HintColor, seconds);
        SpeakHint?.Invoke(text);
    }
}
