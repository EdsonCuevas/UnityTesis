using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Teclado numérico del menú para el número de cuenta (8 dígitos). Tapa el menú mientras no haya alumno,
/// así ningún intento queda sin dueño. Antes de aceptar muestra el número en grande para confirmarlo,
/// porque un dígito equivocado separaría o mezclaría los datos de los alumnos.
/// "Cambiar" lo vuelve a abrir para el siguiente alumno.
/// </summary>
public class StudentKeypad : MonoBehaviour
{
    [Header("Teclado")]
    public GameObject overlay;
    public TMP_Text titleText;
    public TMP_Text hintText;
    public TMP_Text displayText;
    public TMP_Text messageText;
    public GameObject keysRoot;
    [Tooltip("Botón de cada dígito; el índice es el número (0 a 9).")]
    public Button[] digitButtons = new Button[10];
    public Button deleteButton;
    public Button okButton;
    [Tooltip("Solo aparece si ya había un alumno, para volver sin cambiarlo.")]
    public Button cancelButton;

    [Header("Confirmación")]
    public GameObject confirmRoot;
    public Button yesButton;
    public Button fixButton;

    [Header("Fila del menú")]
    public TMP_Text studentLabel;
    public Button changeButton;

    static readonly Color WarningColor = new Color(1f, 0.55f, 0.4f);
    static readonly Color HintColor = new Color(0.62f, 0.66f, 0.7f);

    string typed = string.Empty;

    void Start()
    {
        for (int d = 0; d < digitButtons.Length; d++)
        {
            int digit = d;
            if (digitButtons[d] != null) digitButtons[d].onClick.AddListener(() => Type(digit));
        }
        deleteButton.onClick.AddListener(Delete);
        okButton.onClick.AddListener(AskConfirmation);
        cancelButton.onClick.AddListener(Close);
        yesButton.onClick.AddListener(Accept);
        fixButton.onClick.AddListener(ShowKeys);
        changeButton.onClick.AddListener(Open);

        RefreshLabel();
        if (StudentSession.HasStudent) Close();
        else Open();
    }

    public void Open()
    {
        typed = string.Empty;
        ShowKeys();
        overlay.SetActive(true);
    }

    public void Close() => overlay.SetActive(false);

    void Type(int digit)
    {
        SetMessage(string.Empty);
        if (typed.Length >= StudentSession.AccountDigits) return;
        typed += digit.ToString();
        RefreshDisplay();
    }

    void Delete()
    {
        SetMessage(string.Empty);
        if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
        RefreshDisplay();
    }

    void AskConfirmation()
    {
        int missing = StudentSession.AccountDigits - typed.Length;
        if (missing > 0)
        {
            SetMessage(missing == 1 ? "Falta 1 dígito" : $"Faltan {missing} dígitos", WarningColor);
            return;
        }

        titleText.text = "¿ES TU NÚMERO?";
        hintText.text = "Revísalo bien antes de seguir";
        SetMessage(string.Empty);
        keysRoot.SetActive(false);
        cancelButton.gameObject.SetActive(false);
        confirmRoot.SetActive(true);
    }

    void Accept()
    {
        StudentSession.Begin(typed);
        RefreshLabel();
        Close();
    }

    void ShowKeys()
    {
        titleText.text = "NÚMERO DE CUENTA";
        hintText.text = $"Escribe tus {StudentSession.AccountDigits} dígitos";
        SetMessage(string.Empty);
        confirmRoot.SetActive(false);
        keysRoot.SetActive(true);
        cancelButton.gameObject.SetActive(StudentSession.HasStudent);
        RefreshDisplay();
    }

    void SetMessage(string text, Color? color = null)
    {
        messageText.text = text;
        messageText.color = color ?? HintColor;
    }

    void RefreshDisplay() => displayText.text = StudentSession.Format(typed);

    void RefreshLabel() => studentLabel.text = StudentSession.HasStudent ? StudentSession.Label.ToUpper() : "SIN ALUMNO";
}
