using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialPanel : MonoBehaviour
{
    public readonly struct ButtonSpec
    {
        public readonly string Label;
        public readonly Action OnClick;

        public ButtonSpec(string label, Action onClick)
        {
            Label = label;
            OnClick = onClick;
        }
    }

    [Header("Textos")]
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public TMP_Text counterText;
    public TMP_Text feedbackText;

    [Header("Progreso")]
    public Image progressFill;
    public Image skipFill;
    public GameObject skipHint;

    [Header("Botones")]
    public RectTransform buttonContainer;
    public Button buttonTemplate;
    [Tooltip("Objetos ISDK de rayo y toque. Solo se activan cuando hay botones, para no bloquear el movimiento ni los agarres.")]
    public GameObject[] interactionSurfaces;

    [Header("Muñeca")]
    public Transform head;
    [Tooltip("LeftControllerAnchor: el panel flota sobre esta mano.")]
    public Transform wrist;
    public Vector3 offsetAboveWrist = new Vector3(0f, 0.14f, 0f);
    public float followSharpness = 20f;

    [Header("Atención")]
    public CanvasGroup canvasGroup;
    [Tooltip("Ángulo desde la mirada dentro del cual el panel se ve completamente opaco.")]
    public float lookAngle = 30f;
    public float idleAlpha = 0.35f;

    readonly List<Button> buttons = new List<Button>();
    float shownProgress;
    float targetProgress;
    float feedbackUntil;

    void Awake()
    {
        buttonTemplate.gameObject.SetActive(false);
        feedbackText.text = string.Empty;
        skipFill.fillAmount = 0f;
        SetInteractable(false);
    }

    void Start() => transform.position = wrist.position + offsetAboveWrist;

    public void Show(string title, string body, int stepNumber, int stepCount)
    {
        titleText.text = title;
        bodyText.text = body;
        counterText.text = stepCount > 0 ? $"Paso {stepNumber} de {stepCount}" : string.Empty;
        shownProgress = targetProgress = 0f;
        progressFill.fillAmount = 0f;
    }

    public void SetProgress(float value) => targetProgress = Mathf.Clamp01(value);

    public void SetSkipProgress(float value) => skipFill.fillAmount = Mathf.Clamp01(value);

    public void SetSkipVisible(bool visible) => skipHint.SetActive(visible);

    public void ShowFeedback(string text, Color color, float seconds = 2f)
    {
        feedbackText.text = text;
        feedbackText.color = color;
        feedbackUntil = Time.time + seconds;
    }

    public void SetButtons(params ButtonSpec[] specs)
    {
        DestroyButtons();
        foreach (var spec in specs)
        {
            var button = Instantiate(buttonTemplate, buttonContainer);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = spec.Label;
            var onClick = spec.OnClick;
            button.onClick.AddListener(() => onClick());
            buttons.Add(button);
        }
        SetInteractable(specs.Length > 0);
    }

    public void ClearButtons()
    {
        DestroyButtons();
        SetInteractable(false);
    }

    void DestroyButtons()
    {
        foreach (var button in buttons)
            Destroy(button.gameObject);
        buttons.Clear();
    }

    void SetInteractable(bool interactable)
    {
        foreach (var surface in interactionSurfaces)
            surface.SetActive(interactable);
    }

    void LateUpdate()
    {
        shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Time.deltaTime * 1.5f);
        progressFill.fillAmount = shownProgress;

        if (feedbackText.text.Length > 0 && Time.time > feedbackUntil)
            feedbackText.text = string.Empty;

        FollowWrist();
    }

    void FollowWrist()
    {
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, wrist.position + offsetAboveWrist, blend);

        Vector3 fromHead = transform.position - head.position;
        if (fromHead.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(fromHead);

        float targetAlpha = Vector3.Angle(head.forward, fromHead) <= lookAngle ? 1f : idleAlpha;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 4f);
    }
}
