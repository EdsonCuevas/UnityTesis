using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arma en el UIMenu la fila "Alumno ••••1234 · Cambiar" y el teclado del número de cuenta (StudentKeypad),
/// con su pantalla de confirmación.
/// Se puede volver a ejecutar: borra lo que haya armado antes.
/// </summary>
public static class StudentKeypadBuilder
{
    const string RowName = "Fila_Alumno";
    const string OverlayName = "Teclado_Alumno";
    const float ExtraHeight = 40f;
    const float BaseHeight = 500f;

    static readonly Color Accent = new Color(1f, 0.702f, 0f);
    static readonly Color Background = new Color(0.07f, 0.09f, 0.11f);
    static readonly Color Field = new Color(0.13f, 0.15f, 0.18f);
    static readonly Color Dark = new Color(0.22f, 0.25f, 0.3f);
    static readonly Color Ok = new Color(0.35f, 0.82f, 0.45f);
    static readonly Color Gray = new Color(0.62f, 0.66f, 0.7f);
    static readonly Color DarkText = new Color(0.09f, 0.09f, 0.1f);

    static TMP_FontAsset bold;
    static TMP_FontAsset regular;
    static Sprite sprite;
    static Button styleButton;

    [MenuItem("Tesis/Construir teclado de alumno (UIMenu)")]
    public static void Build()
    {
        var canvasGo = GameObject.Find("UI/Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("Abre la escena UIMenu: no se encontró UI/Canvas.");
            return;
        }

        var canvas = canvasGo.transform;
        var column = canvas.Find("UIElement");
        var subtitle = column.Find("Subtitle");
        styleButton = column.Find("Btn_Tutorial").GetComponent<Button>();
        bold = column.Find("Btn_Tutorial/Title").GetComponent<TMP_Text>().font;
        regular = subtitle.GetComponent<TMP_Text>().font;
        sprite = styleButton.GetComponent<Image>().sprite;

        DestroyIfExists(column.Find(RowName));
        DestroyIfExists(canvas.Find(OverlayName));
        DestroyIfExists(canvas.GetComponent<StudentKeypad>());

        // Más alto para que quepa la fila nueva.
        float height = BaseHeight + ExtraHeight;
        ((RectTransform)canvas).sizeDelta = new Vector2(250f, height);
        ((RectTransform)canvas.Find("Panel_Background")).sizeDelta = new Vector2(250f, height);
        ((RectTransform)canvas.Find("Panel_Border")).sizeDelta = new Vector2(258f, height + 8f);

        var keypad = canvasGo.AddComponent<StudentKeypad>();
        BuildRow(column, subtitle.GetSiblingIndex() + 1, keypad);
        BuildOverlay(canvas, keypad);

        EditorUtility.SetDirty(canvasGo);
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        Debug.Log("Teclado de alumno armado en UIMenu.");
    }

    static void BuildRow(Transform column, int index, StudentKeypad keypad)
    {
        var row = NewRect(RowName, column);
        row.SetSiblingIndex(index);
        Layout(row.gameObject, 210f, 30f);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        var label = Text("Label", row, "SIN ALUMNO", bold, 14f, Accent, TextAlignmentOptions.Left);
        var le = label.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.preferredHeight = 30f;

        var change = MakeButton("Btn_Cambiar", row, "CAMBIAR", Dark, Color.white, 11f);
        Layout(change.gameObject, 80f, 30f);

        keypad.studentLabel = label;
        keypad.changeButton = change;
    }

    static void BuildOverlay(Transform canvas, StudentKeypad keypad)
    {
        var overlay = NewRect(OverlayName, canvas);
        overlay.SetAsLastSibling();
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.sizeDelta = Vector2.zero;
        var bg = overlay.gameObject.AddComponent<Image>();
        bg.sprite = sprite;
        bg.type = Image.Type.Sliced;
        bg.color = Background;

        var v = overlay.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(20, 20, 22, 20);
        v.spacing = 10f;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = false;

        var accent = NewRect("Header_Accent", overlay).gameObject.AddComponent<Image>();
        accent.color = Accent;
        accent.raycastTarget = false;
        Layout(accent.gameObject, 210f, 6f);

        var title = Text("Titulo", overlay, "NÚMERO DE CUENTA", bold, 20f, Accent, TextAlignmentOptions.Center);
        Layout(title.gameObject, 210f, 30f);
        var hint = Text("Indicacion", overlay, "Escribe tus 8 dígitos", regular, 12f, Gray, TextAlignmentOptions.Center);
        Layout(hint.gameObject, 210f, 18f);

        var field = NewRect("Pantalla", overlay);
        var fieldImage = field.gameObject.AddComponent<Image>();
        fieldImage.sprite = sprite;
        fieldImage.type = Image.Type.Sliced;
        fieldImage.color = Field;
        fieldImage.raycastTarget = false;
        Layout(field.gameObject, 210f, 60f);
        var display = Text("Numero", field, "____ ____", bold, 34f, Color.white, TextAlignmentOptions.Center);
        Stretch(display.rectTransform);

        var message = Text("Mensaje", overlay, string.Empty, regular, 12f, Gray, TextAlignmentOptions.Center);
        Layout(message.gameObject, 210f, 18f);

        var grid = NewRect("Teclas", overlay);
        Layout(grid.gameObject, 210f, 52f * 4 + 10f * 3);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(62f, 52f);
        g.spacing = new Vector2(12f, 10f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 3;
        g.childAlignment = TextAnchor.UpperCenter;

        var digits = new Button[10];
        for (int d = 1; d <= 9; d++) digits[d] = DigitButton(grid, d);
        var delete = MakeButton("Btn_Borrar", grid, "BORRAR", Dark, Color.white, 12f);
        digits[0] = DigitButton(grid, 0);
        var ok = MakeButton("Btn_OK", grid, "OK", Ok, DarkText, 20f);

        // Se muestra en lugar de las teclas para confirmar el número antes de aceptarlo.
        var confirm = NewRect("Confirmar", overlay);
        Layout(confirm.gameObject, 210f, 52f * 2 + 10f);
        var cv = confirm.gameObject.AddComponent<VerticalLayoutGroup>();
        cv.spacing = 10f;
        cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = cv.childForceExpandHeight = false;
        var yes = MakeButton("Btn_Si", confirm, "SÍ, ES MI NÚMERO", Ok, DarkText, 15f);
        Layout(yes.gameObject, 210f, 52f);
        var fix = MakeButton("Btn_Corregir", confirm, "CORREGIR", Dark, Color.white, 13f);
        Layout(fix.gameObject, 210f, 52f);
        confirm.gameObject.SetActive(false);

        var cancel = MakeButton("Btn_Cancelar", overlay, "CANCELAR", Dark, Color.white, 12f);
        Layout(cancel.gameObject, 210f, 36f);

        keypad.overlay = overlay.gameObject;
        keypad.titleText = title;
        keypad.hintText = hint;
        keypad.displayText = display;
        keypad.messageText = message;
        keypad.keysRoot = grid.gameObject;
        keypad.digitButtons = digits;
        keypad.deleteButton = delete;
        keypad.okButton = ok;
        keypad.cancelButton = cancel;
        keypad.confirmRoot = confirm.gameObject;
        keypad.yesButton = yes;
        keypad.fixButton = fix;
    }

    static Button DigitButton(Transform parent, int digit)
    {
        // Mismo color y tinte que los botones del menú.
        var button = MakeButton("Btn_" + digit, parent, digit.ToString(), styleButton.GetComponent<Image>().color, DarkText, 24f);
        button.colors = styleButton.colors;
        return button;
    }

    static Button MakeButton(string name, Transform parent, string label, Color color, Color textColor, float size)
    {
        var rect = NewRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = color;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        var text = Text("Title", rect, label, bold, size, textColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    static TMP_Text Text(string name, Transform parent, string value, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Layout(GameObject go, float width, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    static void DestroyIfExists(Object obj)
    {
        if (obj == null) return;
        if (obj is Transform t) Object.DestroyImmediate(t.gameObject);
        else Object.DestroyImmediate(obj);
    }
}
