using System.Collections.Generic;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

internal sealed class SortConfiguration
{
    public static readonly SortConfiguration Empty = new(
        [],
        null,
        null,
        false,
        false);

    private readonly Dictionary<string, int> _categoryIndex;
    private readonly SortCriterion[] _criteria;
    private readonly IReadOnlyDictionary<int, int> _rarityTierByColor;

    private SortConfiguration(
        SortCriterion[] criteria,
        Dictionary<string, int> categoryIndex,
        IReadOnlyDictionary<int, int> rarityTierByColor,
        bool compactSortingEnabled,
        bool separatePrimaryGroups)
    {
        _criteria = criteria;
        _categoryIndex = categoryIndex;
        _rarityTierByColor = rarityTierByColor;
        CompactSortingEnabled = compactSortingEnabled;
        SeparatePrimaryGroups = separatePrimaryGroups;
    }

    public IReadOnlyList<SortCriterion> Criteria => _criteria;
    public bool HasCriteria => _criteria.Length > 0;
    public bool CompactSortingEnabled { get; }
    public bool SeparatePrimaryGroups { get; }

    public static SortConfiguration Capture(bool allowSeparation)
    {
        List<SortCriterion> criteria = [];
        bool hasCategoryCriterion = false;
        bool hasRarityCriterion = false;

        for (int index = 0; index < SortSettings.SortOrder.Count; index++)
        {
            SortTypeSetting setting = SortSettings.SortOrder[index];
            if (setting == null || !setting.Enabled) continue;

            criteria.Add(new SortCriterion(setting.Type, setting.Direction));

            switch (setting.Type)
            {
                case SortType.Rarity:
                    hasRarityCriterion = true;
                    break;
                case SortType.Amount:
                case SortType.Size:
                    break;
                case SortType.Category:
                default:
                    hasCategoryCriterion = true;
                    break;
            }
        }

        Dictionary<string, int> categoryIndex = hasCategoryCriterion
            ? SortKeyProvider.BuildCategoryIndex()
            : null;

        bool compactSortingEnabled = SortSettings.CompactSortingEnabled;
        bool separatePrimaryGroups = allowSeparation && SortSettings.SeparationEnabled &&
                                     SortSettings.CanSeparateCategories();
        return new SortConfiguration(
            criteria.ToArray(),
            categoryIndex,
            hasRarityCriterion ? RaritySettings.CaptureTierLookup() : null,
            compactSortingEnabled,
            separatePrimaryGroups);
    }

    public long ComputeKey(Item item, SortType sortType)
    {
        return SortKeyProvider.ComputeKey(item, sortType, _categoryIndex, _rarityTierByColor);
    }
}

internal readonly struct SortCriterion(SortType type, SortDirection direction)
{
    public SortType Type { get; } = type;
    public SortDirection Direction { get; } = direction;
}