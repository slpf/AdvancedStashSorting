using System.Collections.Generic;
using System.Linq;

namespace AdvancedStashSorting.Sorting;

public static class CategoryCatalog
{
    public static readonly List<string> DefaultOrder =
    [
        "containers",
        "money",
        "ammo",
        "ammo_boxes",
        "grenades",
        "medkits",
        "drugs",
        "stimulators",
        "medicals",
        "food",
        "drinks",
        "weapons",
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
        ["ammo"] = "m_ammo_boxes",
        ["ammo_boxes"] = "m_ammo_boxes",
        ["medkits"] = "m_meds",
        ["drugs"] = "m_meds",
        ["stimulators"] = "m_meds",
        ["medicals"] = "m_meds",
        ["food"] = "m_food_drink",
        ["drinks"] = "m_food_drink",
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

    public static bool HasChildren(string key)
    {
        return ParentKeys.Contains(key);
    }

    public static bool IsContainerFilterCategory(string key)
    {
        return key != "containers" && DefaultOrder.Contains(key);
    }

    public static List<string> GetMainOrder()
    {
        HashSet<string> added = [];
        return SortSettings.CategoryOrder.Select(key => ParentMap.GetValueOrDefault(key, key)).Where(added.Add)
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

        List<string> normalized = order?.Where(key => key != null && known.Contains(key) && added.Add(key)).ToList() ??
                                  [];
        normalized.AddRange(DefaultOrder.Where(added.Add));

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