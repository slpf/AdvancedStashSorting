namespace AdvancedStashSorting.Sorting;

internal static class SortInterceptionPolicy
{
    public static bool ShouldRunOriginal(bool hasCriteria, bool foldingEnabled, bool stackingEnabled,
        bool nestingEnabled, out bool handledSort)
    {
        handledSort = hasCriteria || foldingEnabled || stackingEnabled || nestingEnabled;
        return !handledSort;
    }
}