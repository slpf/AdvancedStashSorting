using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class ContainerCategoryAvailability
{
    private static readonly Dictionary<string, List<string>> Cache = new();

    private static Dictionary<string, List<CategoryProbe>> _categoryProbes;
    private static int _catalogVersion = -1;

    public static List<string> GetAvailable(CompoundItem container)
    {
        if (!ContainerNesting.CanConfigureCategories(container)) return [];

        AmmoCategoryCatalog.Refresh();

        if (_catalogVersion != AmmoCategoryCatalog.Version)
        {
            Cache.Clear();
            _categoryProbes = null;
            _catalogVersion = AmmoCategoryCatalog.Version;
        }

        if (Cache.TryGetValue(container.TemplateId, out List<string> cached)) return [..cached];

        EnsureProbes();

        if (_categoryProbes == null) return [];

        List<string> available = [];

        foreach (string category in CategoryCatalog.DefaultOrder)
        {
            if (!CategoryCatalog.IsContainerFilterCategory(category)) continue;

            if (!_categoryProbes.TryGetValue(category, out List<CategoryProbe> probes)) continue;

            if (AcceptsAny(container, probes)) available.Add(category);
        }

        Cache[container.TemplateId] = available;

        return [..available];
    }

    private static bool AcceptsAny(CompoundItem container, List<CategoryProbe> probes)
    {
        foreach (CategoryProbe probe in probes)
        foreach (Grid grid in container.Grids)
            if (CheckFilters(grid.Filters, probe))
                return true;

        return false;
    }

    private static bool CheckFilters(ItemFilter[] filters, CategoryProbe probe)
    {
        if (filters == null || filters.Length == 0) return true;

        foreach (ItemFilter filter in filters)
            if (filter == null || !Matches(filter.Filter, probe) || Matches(filter.ExcludedFilter, probe))
                return false;

        return true;
    }

    private static bool Matches(MongoID[] nodes, CategoryProbe probe)
    {
        if (nodes == null || nodes.Length == 0) return false;

        foreach (MongoID node in nodes)
        {
            if (string.Equals(node.ToString(), probe.TemplateId, StringComparison.Ordinal)) return true;

            if (JsonTypes.TypeTable.TryGetValue(node, out Type allowedType) &&
                allowedType.IsAssignableFrom(probe.ItemType)) return true;
        }

        return false;
    }

    private static void EnsureProbes()
    {
        if (_categoryProbes != null) return;

        if (!Singleton<ItemFactory>.Instantiated) return;

        Dictionary<string, List<CategoryProbe>> categoryProbes = new Dictionary<string, List<CategoryProbe>>();
        ItemFactory factory = Singleton<ItemFactory>.Instance;

        if (factory.ItemTemplates.Count == 0) return;

        foreach (KeyValuePair<MongoID, ItemTemplate> entry in factory.ItemTemplates)
        {
            if (entry.Value == null || entry.Value._type != NodeType.Item) continue;

            MongoID? parentId = entry.Value.ParentId;
            Type itemType = null;

            if (parentId != null) JsonTypes.TypeTable.TryGetValue(parentId.Value, out itemType);

            itemType ??= entry.Value switch
            {
                AmmoTemplate => typeof(Ammo),
                AmmoBoxTemplate => typeof(AmmoBox),
                _ => null
            };

            if (itemType == null) continue;

            string category = ItemClassifier.Classify(entry.Value, itemType);

            if (!CategoryCatalog.DefaultOrder.Contains(category)) continue;

            CategoryProbe probe = new(entry.Key.ToString(), itemType);
            AddProbe(categoryProbes, category, probe);

            string fallback = entry.Value switch
            {
                AmmoTemplate => "ammo_other",
                AmmoBoxTemplate => "ammo_boxes_other",
                _ => null
            };

            if (fallback != null && fallback != category) AddProbe(categoryProbes, fallback, probe);
        }

        _categoryProbes = categoryProbes;
    }

    private static void AddProbe(Dictionary<string, List<CategoryProbe>> categoryProbes, string category,
        CategoryProbe probe)
    {
        if (!categoryProbes.TryGetValue(category, out List<CategoryProbe> probes))
        {
            probes = [];
            categoryProbes[category] = probes;
        }

        probes.Add(probe);
    }

    private sealed class CategoryProbe(string templateId, Type itemType)
    {
        public string TemplateId { get; } = templateId;
        public Type ItemType { get; } = itemType;
    }
}
