using System;
using System.Collections.Generic;
using EFT.InventoryLogic;
using JsonType;
using UnityEngine;

namespace AdvancedStashSorting.Sorting;

public static class SortKeyProvider
{
    private static readonly int TaxonomyColorCount = Enum.GetValues(typeof(TaxonomyColor)).Length;

    public static Dictionary<string, int> BuildCategoryIndex()
    {
        Dictionary<string, int> result = new Dictionary<string, int>();

        for (int i = 0; i < SortSettings.CategoryOrder.Count; i++)
        {
            string category = SortSettings.CategoryOrder[i];
            result.TryAdd(category, i);
        }

        return result;
    }

    internal static long ComputeKey(Item item, SortType sortType, Dictionary<string, int> categoryIndex,
        IReadOnlyDictionary<int, int> rarityTierByColor)
    {
        switch (sortType)
        {
            case SortType.Amount:
            {
                return ItemAmountClassifier.GetAmount(item);
            }
            case SortType.Size:
            {
                IntVec2 size = item.CalculateCellSize();
                return (long)size.X * size.Y;
            }
            case SortType.Rarity:
            {
                return GetRarityTier(item, rarityTierByColor);
            }
            case SortType.Category:
            default:
            {
                string category = ItemClassifier.Classify(item);
                return categoryIndex.TryGetValue(category, out int index) ? index : categoryIndex.Count;
            }
        }
    }

    private static long GetRarityTier(Item item, IReadOnlyDictionary<int, int> rarityTierByColor)
    {
        int rawColor = (int)item.BackgroundColor;

        byte red;
        byte green;
        byte blue;

        if (rawColor >= 0 && rawColor < TaxonomyColorCount)
        {
            Color color = item.BackgroundColor.ToColor();
            red = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
            green = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
            blue = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
        }
        else
        {
            int colorValue = rawColor - TaxonomyColorCount;
            red = (byte)((colorValue >> 16) & 0xFF);
            green = (byte)((colorValue >> 8) & 0xFF);
            blue = (byte)(colorValue & 0xFF);
        }

        return RaritySettings.GetTier(rarityTierByColor, red, green, blue);
    }
}