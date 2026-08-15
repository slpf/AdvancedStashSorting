using System;
using AdvancedStashSorting.Sorting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public class RarityColorRow : MonoBehaviour
{
    private static TMP_InputField _focusedInput;
    private TMP_InputField _input;
    private Image _inputBackground;
    private Outline _inputOutline;
    private TextMeshProUGUI _label;
    private Action<string> _onChanged;
    private Image _swatch;
    private string _value;

    public static bool IsInputFocused =>
        _focusedInput != null && _focusedInput.isFocused && _focusedInput.gameObject.activeInHierarchy;

    private void OnDisable()
    {
        ClearFocusedInput();
    }

    public static RarityColorRow Create(Transform parent, Action<string> onChanged, PhysicalPixelGrid pixelGrid)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        GameObject rowObject = new GameObject("RarityColor", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();

        Image background = rowObject.GetComponent<Image>();
        background.color = SortTheme.CategoryRowBg;
        background.raycastTarget = true;

        LayoutElement rowLayoutElement = rowObject.GetComponent<LayoutElement>();
        float rowHeight = pixelGrid.Snap(SortTheme.CategoryRowHeight);
        rowLayoutElement.preferredHeight = rowHeight;

        RectTransform rowContent = UiLayout.CreateHorizontalContent(
            rowRect,
            pixelGrid.Snap(SortTheme.RowTextPadding),
            pixelGrid.Snap(4f),
            pixelGrid.Snap(4f),
            false,
            TextAnchor.MiddleLeft);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
        labelObject.transform.SetParent(rowContent, false);
        LayoutElement labelLayoutElement = labelObject.GetComponent<LayoutElement>();
        labelLayoutElement.flexibleWidth = 1f;
        labelLayoutElement.preferredHeight = rowHeight;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(label);
        label.fontSize = SortTheme.CategoryFontSize;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.color = SortTheme.CategoryText;

        GameObject swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Outline),
            typeof(LayoutElement));
        swatchObject.transform.SetParent(rowContent, false);

        Image swatch = swatchObject.GetComponent<Image>();
        swatch.raycastTarget = false;

        Outline swatchOutline = swatchObject.GetComponent<Outline>();
        swatchOutline.effectColor = SortTheme.PanelBorder;
        float borderThickness = pixelGrid.Snap(SortTheme.BorderThickness);
        swatchOutline.effectDistance = new Vector2(borderThickness, borderThickness);

        LayoutElement swatchLayout = swatchObject.GetComponent<LayoutElement>();
        float swatchSize = pixelGrid.Snap(16f);
        swatchLayout.preferredWidth = swatchSize;
        swatchLayout.minWidth = swatchSize;
        swatchLayout.preferredHeight = swatchSize;

        GameObject inputObject = new GameObject("HexInput", typeof(RectTransform), typeof(Image), typeof(Outline),
            typeof(LayoutElement));
        inputObject.SetActive(false);
        inputObject.transform.SetParent(rowContent, false);

        Image inputBackground = inputObject.GetComponent<Image>();
        inputBackground.color = SortTheme.InputBg;

        Outline outline = inputObject.GetComponent<Outline>();
        outline.effectColor = SortTheme.PanelBorder;
        outline.effectDistance = new Vector2(borderThickness, borderThickness);

        LayoutElement inputLayoutElement = inputObject.GetComponent<LayoutElement>();
        float inputWidth = pixelGrid.Snap(68f);
        float inputHeight = pixelGrid.Snap(16f);
        inputLayoutElement.preferredWidth = inputWidth;
        inputLayoutElement.minWidth = inputWidth;
        inputLayoutElement.preferredHeight = inputHeight;

        GameObject viewportObject = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(inputObject.transform, false);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        float horizontalPadding = pixelGrid.Snap(3f);
        viewportRect.offsetMin = new Vector2(horizontalPadding, 0f);
        viewportRect.offsetMax = new Vector2(-horizontalPadding, 0f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(viewportObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI inputText = textObject.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(inputText);
        inputText.fontSize = SortTheme.CategoryFontSize;
        inputText.alignment = TextAlignmentOptions.Left;
        inputText.enableWordWrapping = false;
        inputText.overflowMode = TextOverflowModes.Overflow;
        inputText.raycastTarget = false;
        inputText.color = SortTheme.CategoryText;

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.textViewport = viewportRect;
        input.textComponent = inputText;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 7;
        input.readOnly = false;
        input.richText = false;
        input.transition = Selectable.Transition.None;
        input.customCaretColor = true;
        input.caretColor = SortTheme.CategoryText;
        input.caretWidth = Mathf.Max(1, Mathf.RoundToInt(SortTheme.BorderThickness));
        input.selectionColor = SortTheme.InputSelection;
        input.onValidateInput = ValidateHexCharacter;
        input.SetTextWithoutNotify("#");

        RarityColorRow row = rowObject.AddComponent<RarityColorRow>();
        row._label = label;
        row._input = input;
        row._inputBackground = inputBackground;
        row._inputOutline = outline;
        row._swatch = swatch;
        row._onChanged = onChanged;
        input.onValueChanged.AddListener(row.HandleValueChanged);
        input.onEndEdit.AddListener(row.HandleEndEdit);
        input.onSelect.AddListener(row.HandleSelect);
        input.onDeselect.AddListener(row.HandleDeselect);
        inputObject.SetActive(true);

        return row;
    }

    public void SetValue(string label, string value)
    {
        _label.text = label;
        _value = value;
        _input.SetTextWithoutNotify(value);
        SetFocused(_input.isFocused);

        if (ColorUtility.TryParseHtmlString(value, out Color color)) _swatch.color = color;
    }

    private void HandleValueChanged(string value)
    {
        string filtered = FilterEditingValue(value);

        if (string.Equals(value, filtered, StringComparison.Ordinal)) return;

        int caretPosition = _input.stringPosition;
        _input.SetTextWithoutNotify(filtered);
        int adjustedPosition = value.StartsWith("#", StringComparison.Ordinal) ? caretPosition : caretPosition + 1;
        adjustedPosition = Mathf.Clamp(adjustedPosition, 1, filtered.Length);
        _input.stringPosition = adjustedPosition;
        _input.caretPosition = adjustedPosition;
    }

    private void HandleEndEdit(string value)
    {
        if (!RaritySettings.TryNormalizeHex(value, out string normalized))
        {
            _input.SetTextWithoutNotify(_value);
            return;
        }

        if (string.Equals(normalized, _value, StringComparison.Ordinal))
        {
            _input.SetTextWithoutNotify(_value);
            return;
        }

        _value = normalized;
        _input.SetTextWithoutNotify(normalized);
        UpdateSwatch(normalized);
        _onChanged?.Invoke(normalized);
    }

    private void UpdateSwatch(string value)
    {
        if (ColorUtility.TryParseHtmlString(value, out Color color)) _swatch.color = color;
    }

    private void HandleSelect(string value)
    {
        if (string.IsNullOrEmpty(_input.text)) _input.SetTextWithoutNotify("#");

        _focusedInput = _input;
        SetFocused(true);
    }

    private void HandleDeselect(string value)
    {
        ClearFocusedInput();
        SetFocused(false);
    }

    private void ClearFocusedInput()
    {
        if (_focusedInput == _input) _focusedInput = null;
    }

    private void SetFocused(bool focused)
    {
        _inputBackground.color = focused ? SortTheme.InputFocusedBg : SortTheme.InputBg;
        _inputOutline.effectColor = focused ? SortTheme.InputFocusedBorder : SortTheme.PanelBorder;
    }

    private static char ValidateHexCharacter(string text, int characterIndex, char character)
    {
        return Uri.IsHexDigit(character) ? char.ToUpperInvariant(character) : '\0';
    }

    private static string FilterEditingValue(string value)
    {
        char[] digits = new char[6];
        int count = 0;

        if (value != null)
            for (int i = 0; i < value.Length && count < digits.Length; i++)
                if (Uri.IsHexDigit(value[i]))
                {
                    digits[count] = char.ToUpperInvariant(value[i]);
                    count++;
                }

        return "#" + new string(digits, 0, count);
    }
}