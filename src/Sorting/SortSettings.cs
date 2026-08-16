using System.Collections.Generic;
using System.Linq;

namespace AdvancedStashSorting.Sorting;

public enum SortType
{
    Category,
    Size,
    Amount,
    Rarity
}

public enum SortDirection
{
    Ascending,
    Descending
}

public class SortTypeSetting
{
    public SortType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}

public static class SortSettings
{
    public static List<SortTypeSetting> SortOrder = CreateDefaultSortOrder();

    public static List<string> CategoryOrder = [..CategoryCatalog.DefaultOrder];

    public static bool CompactSortingEnabled = true;
    public static bool FoldingEnabled = true;
    public static bool StackingEnabled = true;
    public static bool NestingEnabled = false;
    public static bool RecursiveNestingEnabled = false;
    public static bool SeparationEnabled = false;

    public static List<SortTypeSetting> CreateDefaultSortOrder()
    {
        return
        [
            new SortTypeSetting { Type = SortType.Category },
            new SortTypeSetting { Type = SortType.Size, Direction = SortDirection.Descending },
            new SortTypeSetting { Type = SortType.Rarity, Enabled = false },
            new SortTypeSetting { Type = SortType.Amount, Direction = SortDirection.Descending }
        ];
    }

    public static SortTypeSetting Get(SortType type)
    {
        return SortOrder.FirstOrDefault(setting => setting.Type == type);
    }

    public static bool IsAscending(SortType type)
    {
        SortTypeSetting setting = Get(type);
        return setting == null || setting.Direction == SortDirection.Ascending;
    }

    public static bool HasEnabledCriterion()
    {
        for (int i = 0; i < SortOrder.Count; i++)
            if (SortOrder[i] != null && SortOrder[i].Enabled)
                return true;

        return false;
    }

    internal static bool CanSeparateCategories()
    {
        if (!CompactSortingEnabled || SortOrder.Count == 0) return false;

        SortTypeSetting first = SortOrder[0];
        return first != null && first.Enabled && first.Type == SortType.Category;
    }
}