using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

internal static class AmmoCaliberResolver
{
    private static readonly Dictionary<MongoID, string> CaliberByTemplate = new();
    private static ItemFactory _factory;
    private static int _catalogVersion = -1;

    public static string GetCaliber(AmmoBoxTemplate template)
    {
        if (template?.StackSlots == null || template.StackSlots.Length == 0 || template.StackSlots[0] == null ||
            !Singleton<ItemFactory>.Instantiated)
            return null;

        ItemFactory factory = Singleton<ItemFactory>.Instance;

        if (factory.ItemTemplates == null || factory.ItemTemplates.Count == 0) return null;

        int catalogVersion = AmmoCategoryCatalog.Version;

        if (!ReferenceEquals(_factory, factory) || _catalogVersion != catalogVersion)
        {
            CaliberByTemplate.Clear();
            _factory = factory;
            _catalogVersion = catalogVersion;
        }

        if (CaliberByTemplate.TryGetValue(template._id, out string cached)) return cached;

        string caliber = ResolveCaliber(template.StackSlots[0].Filters, factory.ItemTemplates);
        CaliberByTemplate[template._id] = caliber;
        return caliber;
    }

    private static string ResolveCaliber(ItemFilter[] filters, IDictionary<MongoID, ItemTemplate> templates)
    {
        string caliber = null;

        foreach (KeyValuePair<MongoID, ItemTemplate> entry in templates)
        {
            if (entry.Value is not AmmoTemplate { _type: NodeType.Item } ammoTemplate) continue;

            Type itemType = typeof(Ammo);
            MongoID? parentId = ammoTemplate.ParentId;

            if (parentId != null && JsonTypes.TypeTable.TryGetValue(parentId.Value, out Type mappedType))
                itemType = mappedType;

            if (!Accepts(filters, entry.Key, itemType)) continue;

            if (string.IsNullOrWhiteSpace(ammoTemplate.Caliber)) return null;

            if (caliber != null && !string.Equals(caliber, ammoTemplate.Caliber, StringComparison.Ordinal)) return null;

            caliber = ammoTemplate.Caliber;
        }

        return caliber;
    }

    private static bool Accepts(ItemFilter[] filters, MongoID templateId, Type itemType)
    {
        if (filters == null || filters.Length == 0) return true;

        foreach (ItemFilter filter in filters)
            if (filter == null || !Matches(filter.Filter, templateId, itemType) ||
                Matches(filter.ExcludedFilter, templateId, itemType))
                return false;

        return true;
    }

    private static bool Matches(MongoID[] nodes, MongoID templateId, Type itemType)
    {
        if (nodes == null || nodes.Length == 0) return false;

        foreach (MongoID node in nodes)
            if (node == templateId || JsonTypes.TypeTable.TryGetValue(node, out Type allowedType) &&
                allowedType.IsAssignableFrom(itemType))
                return true;

        return false;
    }
}
