using System;
using System.Collections.Generic;

namespace AdvancedStashSorting.Sorting;

internal static class ConfiguredOrderEngine
{
    public static ConfiguredOrderResult Build(
        long[][] keys,
        bool[] ascending,
        string[] templateIds,
        string[] primaryTextKeys)
    {
        Validate(keys, ascending, templateIds, primaryTextKeys);
        int itemCount = templateIds.Length;
        int[] order = new int[itemCount];

        for (int index = 0; index < itemCount; index++) order[index] = index;

        Array.Sort(order, (leftIndex, rightIndex) =>
        {
            for (int criterionIndex = 0; criterionIndex < keys.Length; criterionIndex++)
            {
                int comparison = keys[criterionIndex][leftIndex]
                    .CompareTo(keys[criterionIndex][rightIndex]);
                if (!ascending[criterionIndex]) comparison = -comparison;

                if (comparison != 0) return comparison;

                if (criterionIndex == 0 && primaryTextKeys != null)
                {
                    comparison = string.CompareOrdinal(
                        primaryTextKeys[leftIndex],
                        primaryTextKeys[rightIndex]);
                    if (comparison != 0) return ascending[criterionIndex] ? comparison : -comparison;
                }
            }

            int templateComparison = string.CompareOrdinal(templateIds[leftIndex], templateIds[rightIndex]);
            return templateComparison != 0 ? templateComparison : leftIndex.CompareTo(rightIndex);
        });

        if (itemCount == 0) return new ConfiguredOrderResult(order, []);

        List<int> batchStarts = [0];

        for (int index = 1; index < order.Length; index++)
        {
            int previousIndex = order[index - 1];
            int currentIndex = order[index];
            bool samePrimaryKey = primaryTextKeys == null
                ? keys.Length == 0 || keys[0][previousIndex] == keys[0][currentIndex]
                : string.Equals(
                    primaryTextKeys[previousIndex],
                    primaryTextKeys[currentIndex],
                    StringComparison.Ordinal);

            if (!samePrimaryKey) batchStarts.Add(index);
        }

        return new ConfiguredOrderResult(order, batchStarts.ToArray());
    }

    private static void Validate(
        long[][] keys,
        bool[] ascending,
        string[] templateIds,
        string[] primaryTextKeys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        if (ascending == null) throw new ArgumentNullException(nameof(ascending));
        if (templateIds == null) throw new ArgumentNullException(nameof(templateIds));
        if (keys.Length != ascending.Length)
            throw new ArgumentException("Criterion keys and directions must have the same length.");
        if (primaryTextKeys != null && (keys.Length == 0 || primaryTextKeys.Length != templateIds.Length))
            throw new ArgumentException("Primary text keys must match the item list.", nameof(primaryTextKeys));

        for (int criterionIndex = 0; criterionIndex < keys.Length; criterionIndex++)
            if (keys[criterionIndex] == null || keys[criterionIndex].Length != templateIds.Length)
                throw new ArgumentException("Every criterion must contain one key per item.", nameof(keys));
    }
}

internal sealed class ConfiguredOrderResult(int[] order, int[] primaryBatchStarts)
{
    public int[] Order { get; } = order;
    public int[] PrimaryBatchStarts { get; } = primaryBatchStarts;
}