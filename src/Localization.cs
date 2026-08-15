using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AdvancedStashSorting;

public static class Localization
{
    private static readonly Dictionary<string, Dictionary<string, string>> Locales = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["categories"] = "Category Order",
            ["sortby"] = "Sort Priority",
            ["toggles"] = "Options",
            ["Category"] = "Category",
            ["Size"] = "Size",
            ["Amount"] = "Amount",
            ["Rarity"] = "Rarity",
            ["folding"] = "Fold Items",
            ["stacking"] = "Merge Stacks",
            ["nesting"] = "Move Into Containers",
            ["compact_sorting"] = "Compact Layout",
            ["separation"] = "Separate Categories",
            ["container_categories_title"] = "Categories to Move on Sort",
            ["enable_all"] = "Enable all",
            ["disable_all"] = "Disable all",
            ["ascending"] = "Ascending",
            ["descending"] = "Descending",
            ["rarity_preset_Original"] = "Original",
            ["rarity_preset_OdtItemInfo"] = "ODT ItemInfo",
            ["rarity_preset_ItemValuation"] = "ItemValuation",
            ["rarity_preset_Custom"] = "Custom",
            ["rarity_custom_1"] = "Tier 1",
            ["rarity_custom_2"] = "Tier 2",
            ["rarity_custom_3"] = "Tier 3",
            ["rarity_custom_4"] = "Tier 4",
            ["rarity_custom_5"] = "Tier 5",
            ["rarity_custom_6"] = "Tier 6",
            ["rarity_custom_7"] = "Tier 7",
            ["m_ammo_boxes"] = "Ammo / Boxes",
            ["ammo"] = "Ammo",
            ["ammo_boxes"] = "Ammo Boxes",
            ["m_meds"] = "Meds",
            ["medkits"] = "Medkits",
            ["drugs"] = "Drugs",
            ["stimulators"] = "Stimulators",
            ["medicals"] = "Injury Treatment",
            ["m_food_drink"] = "Food / Drinks",
            ["food"] = "Food",
            ["drinks"] = "Drinks",
            ["m_headwear"] = "Headwear",
            ["face_covers"] = "Face Covers",
            ["visors"] = "Visors",
            ["ae_other"] = "Accessories",
            ["m_keys"] = "Keys",
            ["keycards"] = "Keycards",
            ["m_weapon_mods"] = "Mods",
            ["muzzles"] = "Muzzles",
            ["sights"] = "Sights",
            ["foregrips"] = "Foregrips",
            ["bipods"] = "Bipods",
            ["flashlights"] = "Flashlights",
            ["tactical_combos"] = "Tactical Combos",
            ["gas_blocks"] = "Gas Blocks",
            ["auxiliary_mods"] = "Auxiliary Mods",
            ["stocks"] = "Stocks",
            ["charges"] = "Charges",
            ["launchers"] = "Launchers",
            ["mounts"] = "Mounts",
            ["barrels"] = "Barrels",
            ["handguards"] = "Handguards",
            ["receivers"] = "Receivers",
            ["pistol_grips"] = "Pistol Grips",
            ["m_barter"] = "Junk",
            ["dogtags"] = "Dogtags",
            ["barter"] = "Barter Items",
            ["info"] = "Info Items",
            ["flyers"] = "Flyers",
            ["specs"] = "Special Items",
            ["maps"] = "Maps",
            ["containers"] = "Containers",
            ["grenades"] = "Grenades",
            ["weapons"] = "Weapons",
            ["magazines"] = "Magazines",
            ["headphones"] = "Headphones",
            ["headwear"] = "Headwear",
            ["armor"] = "Armor",
            ["vests"] = "Vests",
            ["plates"] = "Plates",
            ["backpacks"] = "Backpacks",
            ["keys"] = "Keys",
            ["money"] = "Money",
            ["arm_bands"] = "Armbands",
            ["knives"] = "Knives",
            ["repair_kits"] = "Repair Kits",
            ["other"] = "Other"
        }
    };

    public static string Culture { get; set; } = "en";

    public static string Get(string key)
    {
        string value =
            Locales.GetValueOrDefault(Culture)?.GetValueOrDefault(key) ??
            Locales.GetValueOrDefault("en")?.GetValueOrDefault(key) ??
            key;

        return value.ToUpperInvariant();
    }

    public static void LoadLocales(string directory)
    {
        directory = Path.Combine(directory, "locales");

        if (!Directory.Exists(directory)) return;

        foreach (string file in Directory.GetFiles(directory, "*.json"))
        {
            string culture = Path.GetFileNameWithoutExtension(file);

            try
            {
                string json = File.ReadAllText(file);
                Dictionary<string, string> loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                if (loaded == null) continue;

                if (!Locales.TryGetValue(culture, out Dictionary<string, string> locale))
                {
                    locale = new Dictionary<string, string>();
                    Locales[culture] = locale;
                }

                foreach (KeyValuePair<string, string> entry in loaded) locale[entry.Key] = entry.Value;
            }
#if DEBUG
            catch (Exception exception)
#else
            catch (Exception)
#endif
            {
#if DEBUG
                Plugin.LogSource?.LogWarning($"Failed to load locale file '{file}': {exception.Message}");
#endif
            }
        }
    }
}
