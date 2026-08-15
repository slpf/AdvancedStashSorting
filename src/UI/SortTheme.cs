using UnityEngine;

namespace AdvancedStashSorting.UI;

public static class SortTheme
{
    public const float DisabledAlpha = 0.4f;

    public static readonly Color PanelFill = Hex("0D0D0D");
    public static readonly Color PanelBorder = Hex("585C5F");
    public static readonly Color HeaderBg = Hex("232426");
    public static readonly Color HeaderText = Hex("EEEFF1");
    public static readonly Color CategoryRowBg = Hex("353638");
    public static readonly Color RowSubmenuOpen = Hex("4B4D50");
    public static readonly Color RowDrag = RowSubmenuOpen;
    public static readonly Color CategoryText = Hex("FBFBE1");
    public static readonly Color HandleColor = Hex("6B6E70");
    public static readonly Color PlaceholderFill = Hex("1C1D1F");
    public static readonly Color PlaceholderBorder = Hex("585C5F");
    public static readonly Color CriterionDisabledBg = Hex("1C1D1F");
    public static readonly Color CriterionDisabledText = Hex("979795");
    public static readonly Color InputBg = Hex("111214");
    public static readonly Color InputFocusedBg = Hex("20272D");
    public static readonly Color InputFocusedBorder = Hex("8A969F");
    public static readonly Color InputSelection = Hex("596C7D");
    public static readonly Color TagCategoryHeaderBg = Hex("191B1B");
    public static readonly Color TagCategoryHeaderText = Hex("FDFDFD");
    public static readonly Color TagCategoryFilterColor = Hex("E0EBF2");
    public static readonly Color TagCategoryToggleNormal = Hex("303133");
    public static readonly Color TagCategoryToggleHover = Hex("4A5050");
    public static readonly Color TagCategoryTogglePressed = Hex("303133");
    public static readonly Color Transparent = new(0f, 0f, 0f, 0f);

    public static float Width => 160f;
    public static float BorderThickness => 1f;
    public static float Padding => 2f;
    public static float Spacing => 1.5f;
    public static float SpacerSize => 2f;
    public static float HeaderHeight => 20f;
    public static float HeaderTextPadding => 2f;
    public static float HeaderFontSize => 10f;
    public static float CategoryRowHeight => 20f;
    public static float CategorySpacing => 1.5f;
    public static float SubMenuOffset => 3.5f;
    public static float CategoryFontSize => 9f;
    public static float HandleWidth => 12f;
    public static float SubmenuHitWidth => 22f;
    public static float HandleBarHeight => 1f;
    public static float HandleBarSpacing => 2f;
    public static float RowTextPadding => 8f;
    public static float ToggleBoxSize => 12f;
    public static float ToggleCheckThickness => 1.5f;
    public static int TagCategoryColumnCount => 4;
    public static float TagCategoryHorizontalMargin => 2f;
    public static float TagCategoryTopPadding => 12f;
    public static float TagCategorySectionSpacing => 2f;
    public static float TagCategoryGridSpacing => 2f;
    public static float TagCategoryTitleHeight => 17f;
    public static float TagCategoryCellHeight => 20f;
    public static float TagCategoryMinimumWidth => 100f;
    public static float TagCategoryTitleFontSize => 13f;
    public static float TagCategoryCellFontSize => 8f;
    public static float TagCategoryTextPadding => 3f;
    public static float TagCategoryFilterPadding => 4f;
    public static float TagCategoryTogglePadding => 0f;
    public static float TagCategoryHeaderIconSize => 14f;
    public static float TagCategoryHeaderIconSpacing => 4f;
    public static float TagCategoryToggleWidth => 26f;
    public static float TagCategoryToggleIconSize => 12f;
    public static float TagCategoryToggleIconOffsetY => -1f;
    public static float TagCategoryToggleChamfer => 5f;
    public static float TagCategoryToggleFadeDuration => 0.08f;
    public static int TagCategoryIconTextureSize => 64;
    public static float TagCategoryFilterSpriteStroke => 4.5f;
    public static float TagCategoryIconAntialias => 3f;
    public static float TagCategoryGridSpritePadding => 8f;
    public static float TagCategoryGridSpriteGap => 8f;
    public static float TagCategoryGridSpriteRadius => 3f;

    public static float NormalizeScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale)) return 1f;

        return Mathf.Clamp(scale, 0.25f, 4f);
    }

    private static Color Hex(string html)
    {
        ColorUtility.TryParseHtmlString("#" + html, out Color color);
        return color;
    }
}