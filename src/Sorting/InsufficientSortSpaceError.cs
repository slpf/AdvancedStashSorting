using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

internal sealed class InsufficientSortSpaceError : InventoryError
{
    public override string GetLocalizedDescription()
    {
        return Localization.GetRaw("sort_error_not_enough_space");
    }

    public override string ToString()
    {
        return GetLocalizedDescription();
    }
}
