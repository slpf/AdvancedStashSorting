using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class AmmoCategoryCatalog
{
    private static HashSet<string> _calibers = new(StringComparer.Ordinal);
    private static HashSet<string> _boxCalibers = new(StringComparer.Ordinal);
    private static ItemFactory _factory;
    private static int _templateCount;
    private static int _templateFingerprint;

    public static bool IsInitialized => _factory != null;
    public static int Version { get; private set; }

    public static bool Refresh()
    {
        CaliberUnderNameCompat.Refresh();

        if (!Singleton<ItemFactory>.Instantiated) return false;

        ItemFactory factory = Singleton<ItemFactory>.Instance;

        if (factory.ItemTemplates.Count == 0) return false;

        HashSet<string> calibers = new(StringComparer.Ordinal);
        HashCode fingerprint = new();

        foreach (KeyValuePair<MongoID, ItemTemplate> entry in factory.ItemTemplates)
        {
            ItemTemplate template = entry.Value;

            if (template is not AmmoTemplate and not AmmoBoxTemplate) continue;

            fingerprint.Add(entry.Key);
            fingerprint.Add(template._id);
            fingerprint.Add(template.ParentId);
            fingerprint.Add(template._type);
            fingerprint.Add(template is AmmoTemplate);

            if (template is AmmoTemplate ammo)
            {
                fingerprint.Add(ammo.Caliber, StringComparer.Ordinal);

                if (!string.IsNullOrWhiteSpace(ammo.Caliber) &&
                    !string.IsNullOrWhiteSpace(GetCaliberName(ammo.Caliber)))
                    calibers.Add(ammo.Caliber);
            }
            else if (template is AmmoBoxTemplate box)
            {
                StackSlot slot = box.StackSlots != null && box.StackSlots.Length > 0 ? box.StackSlots[0] : null;
                fingerprint.Add(slot != null);

                if (slot != null) AddFiltersFingerprint(ref fingerprint, slot.Filters);
            }
        }

        int templateFingerprint = fingerprint.ToHashCode();

        if (_factory == factory && _templateCount == factory.ItemTemplates.Count &&
            _templateFingerprint == templateFingerprint && _calibers.SetEquals(calibers))
            return false;

        _calibers = calibers;
        _factory = factory;
        _templateCount = factory.ItemTemplates.Count;
        _templateFingerprint = templateFingerprint;
        Version++;
        _boxCalibers = new HashSet<string>(StringComparer.Ordinal);

        foreach (ItemTemplate template in factory.ItemTemplates.Values)
        {
            if (template is not AmmoBoxTemplate { _type: NodeType.Item } box) continue;

            string caliber = AmmoCaliberResolver.GetCaliber(box);

            if (caliber != null && calibers.Contains(caliber)) _boxCalibers.Add(caliber);
        }

        CategoryCatalog.SetAmmoCalibers(
            calibers.OrderBy(GetCaliberName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(caliber => caliber, StringComparer.Ordinal),
            _boxCalibers.OrderBy(GetCaliberName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(caliber => caliber, StringComparer.Ordinal));
        Config.RefreshCategories();
        return true;
    }

    private static void AddFiltersFingerprint(ref HashCode fingerprint, ItemFilter[] filters)
    {
        fingerprint.Add(filters?.Length ?? -1);

        if (filters == null) return;

        foreach (ItemFilter filter in filters)
        {
            fingerprint.Add(filter != null);

            if (filter == null) continue;

            AddNodesFingerprint(ref fingerprint, filter.Filter);
            AddNodesFingerprint(ref fingerprint, filter.ExcludedFilter);
        }
    }

    private static void AddNodesFingerprint(ref HashCode fingerprint, MongoID[] nodes)
    {
        fingerprint.Add(nodes?.Length ?? -1);

        if (nodes == null) return;

        foreach (MongoID node in nodes) fingerprint.Add(node);
    }

    public static string GetCategory(string parent, string caliber)
    {
        HashSet<string> calibers = parent == "ammo_boxes" ? _boxCalibers : _calibers;
        return caliber != null && calibers.Contains(caliber) ? parent + ":" + caliber : parent + "_other";
    }

    public static bool TryGetCaliberName(string category, out string name)
    {
        string prefix = category != null && category.StartsWith("ammo:", StringComparison.Ordinal)
            ? "ammo:"
            : "ammo_boxes:";

        if (category == null || !category.StartsWith(prefix, StringComparison.Ordinal))
        {
            name = null;
            return false;
        }

        string caliber = category.Substring(prefix.Length);

        if (!CaliberUnderNameCompat.TryGetName(caliber, out name)) name = GetCaliberName(caliber);

        return true;
    }

    private static string GetCaliberName(string caliber)
    {
        return caliber.StartsWith("Caliber", StringComparison.Ordinal) ? caliber.Substring("Caliber".Length) : caliber;
    }
}
