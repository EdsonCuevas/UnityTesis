using System;
using System.Collections;
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

    [Header("Ubicación")]
    [Tooltip("El panel gira sobre su eje vertical para quedar de frente a esta cabeza.")]
    public Transform head;
    public CanvasGroup canvasGroup;
    [Tooltip("Duración de cada mitad del desvanecido al cambiar de lugar.")]
    public float fadeDuration = 0.25f;
    public float turnSharpness = 6f;

    readonly List<Button> buttons = new List<Button>();
    float shownProgress;
    float targetProgress;
    float feedbackUntil;
    Transform currentAnchor;
    Coroutine moveRoutine;

    void Awake()
    {
        buttonTemplate.gameObject.SetActive(false);
        feedbackText.text = string.Empty;
        skipFill.fillAmount = 0f;
        canvasGroup.alpha = 1f;
        SetInteractable(false);
    }

    public void MoveTo(Transform anchor)
    {
        if (anchor == null || anchor == currentAnchor) return;

        bool firstPlacement = currentAnchor == null;
        currentAnchor = anchor;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        if (firstPlacement)
        {
            PlaceAt(anchor);
            return;
        }
        moveRoutine = StartCoroutine(FadeToAnchor(anchor));
    }

    public void Show(string title, string body, int stepNumber, int stepCount)
    {
        titleText.text = title;
        bodyText.text = body;
        counterText.text = stepCount > 0 ? $"Paso {stepNumber} de {stepCount}" : string.Empty;
        shownProgress = targetProgress = 0f;
        progressFill.fillAmount = 0f;
    }

    public void SetBody(string body) => bodyText.text = body;

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

    IEnumerator FadeToAnchor(Transform anchor)
    {
        yield return Fade(0f);
        PlaceAt(anchor);
        yield return Fade(1f);
        moveRoutine = null;
    }

    IEnumerator Fade(float target)
    {
        float start = canvasGroup.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
    }

    void PlaceAt(Transform anchor)
    {
        transform.position = anchor.position;
        Vector3 look = FlatFromHead();
        transform.rotation = look != Vector3.zero ? Quaternion.LookRotation(look) : anchor.rotation;
    }

    void LateUpdate()
    {
        shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Time.deltaTime * 1.5f);
        progressFill.fillAmount = shownProgress;

        if (feedbackText.text.Length > 0 && Time.time > feedbackUntil)
            feedbackText.text = string.Empty;

        Vector3 look = FlatFromHead();
        if (look != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look),
                1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
    }

    Vector3 FlatFromHead()
    {
        Vector3 look = transform.position - head.position;
        look.y = 0f;
        return look.sqrMagnitude > 1e-4f ? look.normalized : Vector3.zero;
    }
}
