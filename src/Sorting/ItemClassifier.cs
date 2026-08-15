using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class ItemClassifier
{
    private static readonly Dictionary<string, string> CategoryByTemplateId = new(StringComparer.Ordinal)
    {
        ["544fb3f34bdc2d03748b456a"] = "stimulators"
    };

    private static readonly Dictionary<Type, string> CategoryByItemComponent = new()
    {
        [typeof(DogtagComponent)] = "dogtags"
    };

    private static readonly (Type Type, string Category)[] CategoryByType =
    [
        (typeof(SimpleContainer), "containers"),
        (typeof(Ammo), "ammo"),
        (typeof(AmmoBox), "ammo_boxes"),
        (typeof(ThrowWeap), "grenades"),
        (typeof(MedKit), "medkits"),
        (typeof(Stimulator), "stimulators"),
        (typeof(Drugs), "drugs"),
        (typeof(Medical), "medicals"),
        (typeof(Food), "food"),
        (typeof(Drink), "drinks"),
        (typeof(Weapon), "weapons"),
        (typeof(Magazine), "magazines"),
        (typeof(Headphones), "headphones"),
        (typeof(Headwear), "headwear"),
        (typeof(FaceCover), "face_covers"),
        (typeof(Visors), "visors"),
        (typeof(Armor), "armor"),
        (typeof(ArmorPlate), "plates"),
        (typeof(Vest), "vests"),
        (typeof(Backpack), "backpacks"),
        (typeof(Keycard), "keycards"),
        (typeof(KeyMechanical), "keys"),
        (typeof(MuzzleMod), "muzzles"),
        (typeof(SightMod), "sights"),
        (typeof(Foregrip), "foregrips"),
        (typeof(Bipod), "bipods"),
        (typeof(Flashlight), "flashlights"),
        // (typeof(LightLaser),  "light_lasers"),
        (typeof(TacticalCombo), "tactical_combos"),
        // (typeof(RailCovers), "rail_covers"),
        (typeof(Gasblock), "gas_blocks"),
        (typeof(AuxiliaryMod), "auxiliary_mods"),
        (typeof(Stock), "stocks"),
        // (typeof(Shaft), "shafts"),
        (typeof(Charge), "charges"),
        (typeof(Launcher), "launchers"),
        (typeof(Mount), "mounts"),
        (typeof(Barrel), "barrels"),
        (typeof(Handguard), "handguards"),
        (typeof(Receiver), "receivers"),
        (typeof(PistolGrip), "pistol_grips"),
        (typeof(BarterItem), "barter"),
        (typeof(Info), "info"),
        (typeof(Flyer), "flyers"),
        (typeof(SpecItem), "specs"),
        (typeof(Map), "maps"),
        (typeof(Money), "money"),
        (typeof(ArmBand), "arm_bands"),
        (typeof(Knife), "knives"),
        (typeof(RepairKit), "repair_kits"),
        (typeof(ArmoredEquipment), "ae_other")
    ];

    private static readonly Dictionary<Type, string> ResolvedCategoryByType = new();

    public static string Classify(Item item)
    {
        if (item == null) return "other";

        if (TryClassifyTemplate(item.TemplateId.ToString(), out string category)) return category;

        foreach (IItemComponent component in item.Components)
            if (component != null && TryClassifyItemComponent(component.GetType(), out category))
                return category;

        return ClassifyType(item.GetType());
    }

    internal static string Classify(ItemTemplate itemTemplate, Type itemType)
    {
        if (itemTemplate == null) return ClassifyType(itemType);

        if (TryClassifyTemplate(itemTemplate._id.ToString(), out string category)) return category;

        if (itemTemplate is BarterOtherTemplate { DogTagQualities: true } &&
            TryClassifyItemComponent(typeof(DogtagComponent), out category))
            return category;

        return ClassifyType(itemType);
    }

    private static bool TryClassifyTemplate(string templateId, out string category)
    {
        category = null;
        return templateId != null && CategoryByTemplateId.TryGetValue(templateId, out category);
    }

    private static bool TryClassifyItemComponent(Type itemComponentType, out string category)
    {
        foreach (KeyValuePair<Type, string> entry in CategoryByItemComponent)
            if (entry.Key.IsAssignableFrom(itemComponentType))
            {
                category = entry.Value;
                return true;
            }

        category = null;
        return false;
    }

    private static string ClassifyType(Type itemType)
    {
        if (itemType == null) return "other";

        if (ResolvedCategoryByType.TryGetValue(itemType, out string cached)) return cached;

        string category = "other";

        foreach ((Type type, string ruleCategory) in CategoryByType)
            if (type.IsAssignableFrom(itemType))
            {
                category = ruleCategory;
                break;
            }

        ResolvedCategoryByType[itemType] = category;

        return category;
    }
}