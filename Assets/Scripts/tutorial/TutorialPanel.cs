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

    [Header("Seguir al jugador")]
    public Transform head;
    public float distance = 1.3f;
    public float heightOffset = -0.15f;
    [Tooltip("Ángulo fuera de la vista a partir del cual el panel se reacomoda frente al jugador.")]
    public float repositionAngle = 35f;
    public float distanceTolerance = 0.6f;
    public float followSharpness = 4f;

    readonly List<Button> buttons = new List<Button>();
    float shownProgress;
    float targetProgress;
    float feedbackUntil;
    bool repositioning;

    void Awake()
    {
        buttonTemplate.gameObject.SetActive(false);
        feedbackText.text = string.Empty;
        skipFill.fillAmount = 0f;
    }

    void Start() => SnapInFront();

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
        ClearButtons();
        foreach (var spec in specs)
        {
            var button = Instantiate(buttonTemplate, buttonContainer);
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = spec.Label;
            var onClick = spec.OnClick;
            button.onClick.AddListener(() => onClick());
            buttons.Add(button);
        }
    }

    public void ClearButtons()
    {
        foreach (var button in buttons)
            Destroy(button.gameObject);
        buttons.Clear();
    }

    public void SnapInFront()
    {
        Vector3 forward = FlatDirection(head.forward);
        if (forward == Vector3.zero) return;
        transform.position = TargetPosition(forward);
        transform.rotation = Quaternion.LookRotation(forward);
    }

    void LateUpdate()
    {
        shownProgress = Mathf.MoveTowards(shownProgress, targetProgress, Time.deltaTime * 1.5f);
        progressFill.fillAmount = shownProgress;

        if (feedbackText.text.Length > 0 && Time.time > feedbackUntil)
            feedbackText.text = string.Empty;

        Follow();
    }

    void Follow()
    {
        Vector3 forward = FlatDirection(head.forward);
        if (forward == Vector3.zero) return;

        Vector3 toPanel = transform.position - head.position;
        toPanel.y = 0f;
        bool outOfView = Vector3.Angle(forward, toPanel) > repositionAngle;
        bool wrongDistance = Mathf.Abs(toPanel.magnitude - distance) > distanceTolerance;
        if (outOfView || wrongDistance)
            repositioning = true;

        Vector3 target = TargetPosition(forward);
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        Vector3 position = transform.position;

        if (repositioning)
        {
            position = Vector3.Lerp(position, target, blend);
            if ((position - target).sqrMagnitude < 0.0025f)
                repositioning = false;
        }
        else
        {
            position.y = Mathf.Lerp(position.y, target.y, blend);
        }

        transform.position = position;

        Vector3 look = FlatDirection(position - head.position);
        if (look != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(look);
    }

    Vector3 TargetPosition(Vector3 flatForward)
    {
        Vector3 target = head.position + flatForward * distance;
        target.y = head.position.y + heightOffset;
        return target;
    }

    static Vector3 FlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude < 1e-6f ? Vector3.zero : direction.normalized;
    }
}
