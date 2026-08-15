using System;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class ItemAmountClassifier
{
    private const long Precision = 1000L;

    public static long GetAmount(Item item)
    {
        if (item.StackObjectsCount > 1) return Scale(item.StackObjectsCount);

        if (item.TryGetItemComponent(out DogtagComponent dogtagComponent)) return Scale(dogtagComponent.Level);

        if (item is IAmmoContainer ammoContainer) return Scale(ammoContainer.Count);

        if (item.TryGetItemComponent(out MedKitComponent medKitComponent)) return Scale(medKitComponent.HpResource);

        if (item.TryGetItemComponent(out FoodDrinkComponent foodDrinkComponent))
            return Scale(foodDrinkComponent.HpPercent);

        if (item.TryGetItemComponent(out RepairKitComponent repairKitComponent))
            return Scale(repairKitComponent.Resource);

        if (item.TryGetItemComponent(out RepairableComponent repairableComponent) &&
            repairableComponent.MaxDurability > 0f) return Scale(repairableComponent.Durability);

        if (item.TryGetItemComponent(out KeyComponent keyComponent) &&
            keyComponent.Template.MaximumNumberOfUsage > 0)
        {
            int remainingUses = Math.Max(0, keyComponent.Template.MaximumNumberOfUsage - keyComponent.NumberOfUsages);
            return Scale(remainingUses);
        }

        if (item.TryGetItemComponent(out IResourceComponent resourceComponent) && resourceComponent.MaxResource > 0f)
            return Scale(resourceComponent.Value);

        if (item is CompoundItem compoundItem && compoundItem.Grids.Length > 0)
            return Scale(GetGridCapacity(compoundItem));

        return Scale(item.StackObjectsCount);
    }

    private static int GetGridCapacity(CompoundItem item)
    {
        int capacity = 0;

        foreach (Grid grid in item.Grids) capacity += grid.GridWidth * grid.GridHeight;

        return capacity;
    }

    private static long Scale(float amount)
    {
        if (float.IsNaN(amount) || amount <= 0f) return 0L;

        double scaled = Math.Round(amount * Precision, MidpointRounding.AwayFromZero);
        return scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
    }
}