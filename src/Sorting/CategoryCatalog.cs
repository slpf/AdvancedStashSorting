using System;
using System.Collections.Generic;
using System.Linq;

namespace AdvancedStashSorting.Sorting;

public static class CategoryCatalog
{
    public static readonly List<string> DefaultOrder =
    [
        "containers",
        "money",
        "ammo_other",
        "ammo_boxes_other",
        "grenades",
        "medkits",
        "drugs",
        "stimulators",
        "medicals",
        "food",
        "drinks",
        "assault_rifles",
        "assault_carbines",
        "submachine_guns",
        "shotguns",
        "machine_guns",
        "marksman_rifles",
        "sniper_rifles",
        "pistols",
        "revolvers",
        "other_weapons",
        "magazines",
        "headphones",
        "headwear",
        "face_covers",
        "visors",
        "ae_other",
        "armor",
        "vests",
        "plates",
        "backpacks",
        "keys",
        "keycards",
        "muzzles",
        "sights",
        "foregrips",
        "bipods",
        "flashlights",
        // "light_lasers",
        "tactical_combos",
        // "rail_covers",
        "gas_blocks",
        "auxiliary_mods",
        "stocks",
        // "shafts",
        "charges",
        "launchers",
        "mounts",
        "barrels",
        "handguards",
        "receivers",
        "pistol_grips",
        "dogtags",
        "barter",
        "info",
        "flyers",
        "specs",
        "maps",
        "arm_bands",
        "knives",
        "repair_kits",
        "other"
    ];

    public static readonly Dictionary<string, string> ParentMap = new()
    {
        ["ammo_other"] = "ammo",
        ["ammo_boxes_other"] = "ammo_boxes",
        ["medkits"] = "m_meds",
        ["drugs"] = "m_meds",
        ["stimulators"] = "m_meds",
        ["medicals"] = "m_meds",
        ["food"] = "m_food_drink",
        ["drinks"] = "m_food_drink",
        ["assault_rifles"] = "m_weapons",
        ["assault_carbines"] = "m_weapons",
        ["machine_guns"] = "m_weapons",
        ["marksman_rifles"] = "m_weapons",
        ["pistols"] = "m_weapons",
        ["revolvers"] = "m_weapons",
        ["shotguns"] = "m_weapons",
        ["sniper_rifles"] = "m_weapons",
        ["submachine_guns"] = "m_weapons",
        ["other_weapons"] = "m_weapons",
        ["headwear"] = "m_headwear",
        ["face_covers"] = "m_headwear",
        ["visors"] = "m_headwear",
        ["ae_other"] = "m_headwear",
        ["keys"] = "m_keys",
        ["keycards"] = "m_keys",
        ["muzzles"] = "m_weapon_mods",
        ["sights"] = "m_weapon_mods",
        ["foregrips"] = "m_weapon_mods",
        ["bipods"] = "m_weapon_mods",
        ["flashlights"] = "m_weapon_mods",
        // ["light_lasers"] = "m_weapon_mods",
        ["tactical_combos"] = "m_weapon_mods",
        // ["rail_covers"] = "m_weapon_mods",
        ["gas_blocks"] = "m_weapon_mods",
        ["auxiliary_mods"] = "m_weapon_mods",
        ["stocks"] = "m_weapon_mods",
        // ["shafts"] = "m_weapon_mods",
        ["charges"] = "m_weapon_mods",
        ["launchers"] = "m_weapon_mods",
        ["mounts"] = "m_weapon_mods",
        ["barrels"] = "m_weapon_mods",
        ["handguards"] = "m_weapon_mods",
        ["receivers"] = "m_weapon_mods",
        ["pistol_grips"] = "m_weapon_mods",
        ["dogtags"] = "m_barter",
        ["barter"] = "m_barter",
        ["info"] = "m_barter",
        ["flyers"] = "m_barter",
        ["specs"] = "m_barter",
        ["maps"] = "m_barter"
    };

    private static readonly HashSet<string> ParentKeys = ParentMap.Values.ToHashSet();

    internal static void SetAmmoCalibers(IEnumerable<string> ammoCalibers, IEnumerable<string> boxCalibers)
    {
        foreach (string category in ParentMap.Keys.Where(key => key.StartsWith("ammo:", StringComparison.Ordinal) ||
                                                               key.StartsWith("ammo_boxes:", StringComparison.Ordinal))
                     .ToList())
        {
            ParentMap.Remove(category);
            DefaultOrder.Remove(category);
        }

        foreach (string parent in new[] { "ammo", "ammo_boxes" })
        {
            int index = DefaultOrder.IndexOf(parent + "_other");

            foreach (string caliber in parent == "ammo" ? ammoCalibers : boxCalibers)
            {
                string category = parent + ":" + caliber;
                DefaultOrder.Insert(index++, category);
                ParentMap[category] = parent;
            }
        }
    }

    internal static IEnumerable<string> ExpandLegacyCategory(string category)
    {
        if (category is "ammo" or "ammo_boxes")
            return DefaultOrder.Where(key => GetMainCategory(key) == category);

        if (category == "m_ammo_boxes")
            return DefaultOrder.Where(key => GetMainCategory(key) is "ammo" or "ammo_boxes");

        return [category];
    }

    public static bool HasChildren(string key)
    {
        return ParentKeys.Contains(key);
    }

    public static bool IsContainerFilterCategory(string key)
    {
        return key != "containers" && DefaultOrder.Contains(key);
    }

    public static string GetMainCategory(string key)
    {
        if (key == null) return null;

        int remaining = ParentMap.Count;

        while (remaining-- > 0 && ParentMap.TryGetValue(key, out string parent)) key = parent;

        return key;
    }

    public static List<string> GetMainOrder()
    {
        HashSet<string> added = [];
        return SortSettings.CategoryOrder.Select(GetMainCategory).Where(added.Add)
            .ToList();
    }

    public static List<string> GetSubOrder(string parent)
    {
        return SortSettings.CategoryOrder.Where(key => ParentMap.TryGetValue(key, out string p) && p == parent).ToList();
    }

    public static List<string> NormalizeOrder(IEnumerable<string> order)
    {
        HashSet<string> known = new HashSet<string>(DefaultOrder);
        HashSet<string> added = [];

        List<string> normalized = order?.SelectMany(ExpandLegacyCategory)
            .Where(key => key != null && known.Contains(key) && added.Add(key)).ToList() ?? [];

        foreach (string category in DefaultOrder.Where(added.Add))
        {
            string main = GetMainCategory(category);
            int index = main is "ammo" or "ammo_boxes"
                ? normalized.FindLastIndex(key => GetMainCategory(key) == main)
                : -1;

            if (index >= 0)
                normalized.Insert(normalized[index] == main + "_other" ? index : index + 1, category);
            else
                normalized.Add(category);
        }

        return normalized;
    }

    public static void ApplyMainOrder(IEnumerable<string> mainOrder)
    {
        List<string> oldOrder = SortSettings.CategoryOrder;
        List<string> newOrder = [];

        foreach (string main in mainOrder)
            if (ParentMap.ContainsValue(main))
                newOrder.AddRange(oldOrder.Where(key => ParentMap.TryGetValue(key, out string parent) && parent == main));
            else
                newOrder.Add(main);

        SortSettings.CategoryOrder = newOrder;
    }

    public static void ApplySubOrder(string parent, List<string> newSubOrder)
    {
        List<string> order = SortSettings.CategoryOrder;

        int firstIndex =
            order.FindIndex(key => ParentMap.TryGetValue(key, out string currentParent) && currentParent == parent);

        if (firstIndex < 0) return;

        order.RemoveAll(key => ParentMap.TryGetValue(key, out string currentParent) && currentParent == parent);
        order.InsertRange(firstIndex, newSubOrder);
    }
}
