using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class ContainerCategorySettings
{
    private static Dictionary<string, HashSet<string>> _categories = new();

    public static bool IsAllowed(CompoundItem container, string category)
    {
        if (container == null || string.IsNullOrEmpty(container.Id)) return false;

        return _categories.TryGetValue(container.Id, out HashSet<string> selected) && selected.Contains(category);
    }

    public static bool HasSelection(string containerId)
    {
        return !string.IsNullOrEmpty(containerId) && _categories.ContainsKey(containerId);
    }

    public static HashSet<string> GetSelection(CompoundItem container, IEnumerable<string> availableCategories)
    {
        HashSet<string> available =
            new HashSet<string>(availableCategories?.Where(CategoryCatalog.IsContainerFilterCategory) ?? []);

        if (container == null || string.IsNullOrEmpty(container.Id)) return [];

        if (!_categories.TryGetValue(container.Id, out HashSet<string> selected)) return [];

        available.IntersectWith(selected);

        return available;
    }

    public static void SetSelection(CompoundItem container, IEnumerable<string> categories,
        IEnumerable<string> availableCategories)
    {
        if (container == null || string.IsNullOrEmpty(container.Id)) return;

        HashSet<string> available =
            new HashSet<string>(availableCategories?.Where(CategoryCatalog.IsContainerFilterCategory) ?? []);
        HashSet<string> selected = new HashSet<string>(categories?.Where(available.Contains) ?? []);

        if (selected.Count == 0)
        {
            _categories.Remove(container.Id);
            return;
        }

        _categories[container.Id] = selected;
    }

    public static bool Load(IDictionary<string, List<string>> categories)
    {
        _categories = new Dictionary<string, HashSet<string>>();

        if (categories == null) return true;

        bool changed = false;

        foreach (KeyValuePair<string, List<string>> entry in categories)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                changed = true;
                continue;
            }

            HashSet<string> selected = new HashSet<string>(entry.Value?.Where(CategoryCatalog.IsContainerFilterCategory) ?? []);

            if (selected.Count == 0)
            {
                changed = true;
                continue;
            }

            List<string> normalized = CategoryCatalog.DefaultOrder.Where(selected.Contains).ToList();

            if (!SameSet(entry.Value, selected)) changed = true;

            _categories[entry.Key] = new HashSet<string>(normalized);
        }

        return changed;
    }

    private static bool SameSet(List<string> values, HashSet<string> selected)
    {
        if (values.Count != selected.Count) return false;

        for (int i = 0; i < values.Count; i++)
            if (!selected.Contains(values[i]))
                return false;

        return true;
    }

    public static Dictionary<string, List<string>> Export()
    {
        Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

        foreach (KeyValuePair<string, HashSet<string>> entry in _categories)
            result[entry.Key] = CategoryCatalog.DefaultOrder.Where(entry.Value.Contains).ToList();

        return result;
    }
}
