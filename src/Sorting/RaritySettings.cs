using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedStashSorting.Sorting;

public enum RarityColorPreset
{
    Original,
    OdtItemInfo,
    ItemValuation,
    Custom
}

public static class RaritySettings
{
    private const int UnknownTier = 7;

    private static readonly string[] OriginalColors =
    [
        "#1D1D1D",
        "#7F7F7F",
        "#152D00",
        "#1C4156",
        "#4C2A55",
        "#686628",
        "#FF3C3C"
    ];

    private static readonly string[] OdtItemInfoColors =
    [
        "#FFFFFF",
        "#2694DA",
        "#9F5ACF",
        "#F6F15D",
        "#FFD700",
        "#39FF14",
        "#FF4040"
    ];

    private static readonly string[] ItemValuationColors =
    [
        "#404040",
        "#A3A3A3",
        "#0C3B08",
        "#08083B",
        "#590B5E",
        "#5E470B",
        "#660415"
    ];

    public static RarityColorPreset ActivePreset = RarityColorPreset.Original;
    public static List<string> CustomColors = [..OriginalColors];
    private static readonly Dictionary<int, int> TierByColor = new(7);
    private static readonly Dictionary<int, int> OriginalTierByColor = BuildOriginalTierLookup();
    private static bool _tierLookupDirty = true;

    private static Dictionary<int, int> BuildOriginalTierLookup()
    {
        Dictionary<int, int> result = new Dictionary<int, int>(7);

        for (int i = 0; i < OriginalColors.Length; i++)
            if (TryParseColor(OriginalColors[i], out int color) && !result.ContainsKey(color))
                result[color] = i;

        return result;
    }

    public static string GetColor(int index)
    {
        if (index < 0 || index >= 7) throw new ArgumentOutOfRangeException(nameof(index));

        if (ActivePreset == RarityColorPreset.Custom) return CustomColors[index];

        return GetPresetColors(ActivePreset)[index];
    }

    public static string GetCustomLabelKey(int index)
    {
        if (index < 0 || index >= 7) throw new ArgumentOutOfRangeException(nameof(index));

        return "rarity_custom_" + (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<string> GetPresetColors(RarityColorPreset preset)
    {
        switch (preset)
        {
            case RarityColorPreset.OdtItemInfo:
                return OdtItemInfoColors;
            case RarityColorPreset.ItemValuation:
                return ItemValuationColors;
            case RarityColorPreset.Custom:
                return CustomColors;
            default:
                return OriginalColors;
        }
    }

    public static List<string> NormalizeCustomColors(IList<string> colors, out bool changed)
    {
        List<string> normalized = new List<string>(7);
        changed = colors == null || colors.Count != 7;

        for (int i = 0; i < 7; i++)
        {
            string fallback = OriginalColors[i];
            string value = colors != null && i < colors.Count ? colors[i] : fallback;

            if (!TryNormalizeHex(value, out string normalizedValue))
            {
                normalizedValue = fallback;
                changed = true;
            }

            else if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
            {
                changed = true;
            }

            normalized.Add(normalizedValue);
        }

        return normalized;
    }

    public static bool TryNormalizeHex(string value, out string normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(value)) return false;

        string digits = value.Trim();

        if (digits.StartsWith("#", StringComparison.Ordinal)) digits = digits.Substring(1);

        if (digits.Length != 6) return false;

        for (int i = 0; i < digits.Length; i++)
            if (!Uri.IsHexDigit(digits[i]))
                return false;

        normalized = "#" + digits.ToUpperInvariant();
        return true;
    }

    public static void MarkTierLookupDirty()
    {
        _tierLookupDirty = true;
    }

    internal static Dictionary<int, int> CaptureTierLookup()
    {
        EnsureTierLookup();
        Dictionary<int, int> result = new(OriginalTierByColor);

        foreach (KeyValuePair<int, int> pair in TierByColor)
            result[pair.Key] = pair.Value;

        return result;
    }

    internal static int GetTier(IReadOnlyDictionary<int, int> tierByColor, byte red, byte green, byte blue)
    {
        int color = (red << 16) | (green << 8) | blue;
        return tierByColor.TryGetValue(color, out int tier) ? tier : UnknownTier;
    }

    private static void EnsureTierLookup()
    {
        if (!_tierLookupDirty) return;

        TierByColor.Clear();
        for (int i = 0; i < 7; i++)
            if (TryParseColor(GetColor(i), out int color) && !TierByColor.ContainsKey(color))
                TierByColor[color] = i;

        _tierLookupDirty = false;
    }

    private static bool TryParseColor(string value, out int color)
    {
        color = 0;

        if (string.IsNullOrEmpty(value)) return false;

        string digits = value[0] == '#' ? value.Substring(1) : value;
        return int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color);
    }
}