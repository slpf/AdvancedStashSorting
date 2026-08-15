using System;
using System.Collections.Generic;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

internal static class OrderedStashLayoutPlanner
{
    private const int MaximumCandidateChecks = 5_000_000;
    private const int MaximumBacktracks = 50_000;
    private static readonly object CacheLock = new();
    private static CacheEntry _cache;

    public static OrderedLayoutSearchResult Search(Grid grid, IReadOnlyList<Item> orderedItems,
        IReadOnlyList<int> primaryBatchStarts, bool separateBatches)
    {
        if (grid == null) throw new ArgumentNullException(nameof(grid));
        if (orderedItems == null) throw new ArgumentNullException(nameof(orderedItems));
        if (primaryBatchStarts == null) throw new ArgumentNullException(nameof(primaryBatchStarts));

        int gridWidth = grid.GridWidth;
        int gridHeight = grid.GridHeight;
        bool[] baseLayout = CopyLayout(grid);
        OrderedLayoutItem[] layoutItems = BuildLayoutItems(orderedItems);
        int[] batchStarts = CopyBatchStarts(primaryBatchStarts);
        CacheEntry cached;

        lock (CacheLock)
        {
            cached = _cache;
        }

        if (cached != null && cached.Matches(gridWidth, gridHeight, baseLayout, orderedItems, layoutItems, batchStarts,
                separateBatches))
            return new OrderedLayoutSearchResult(
                cached.CreatePlan(orderedItems),
                OrderedLayoutStatus.Success,
                false,
                0,
                0,
                0,
                0);

        OrderedLayoutRequest request = new(gridWidth, gridHeight, baseLayout, layoutItems, batchStarts, separateBatches,
            MaximumCandidateChecks, MaximumBacktracks);
        OrderedLayoutResult result = OrderedLayoutEngine.Search(request);

        if (result.Status != OrderedLayoutStatus.Success)
        {
#if DEBUG
            if (result.Status == OrderedLayoutStatus.SearchLimitReached)
                Plugin.LogSource?.LogWarning(
                    $"Ordered layout search limit reached: candidates={result.CandidateChecks}, backtracks={result.Backtracks}.");
            else
                Plugin.LogSource?.LogWarning(
                    $"Ordered layout failed: status={result.Status}, candidates={result.CandidateChecks}, backtracks={result.Backtracks}.");
#endif
            return new OrderedLayoutSearchResult(
                null,
                result.Status,
                true,
                result.UsedBottom,
                result.PlacedBottom,
                result.Perimeter,
                result.HorizontalWeight);
        }

        OrderedLayoutPlan plan = CreatePlan(orderedItems, result);
        CacheEntry newCache = new(gridWidth, gridHeight, baseLayout, orderedItems, layoutItems, batchStarts,
            separateBatches, result);

        lock (CacheLock)
        {
            _cache = newCache;
        }

#if DEBUG
        Plugin.LogSource?.LogInfo(
            $"Ordered layout search: candidates={result.CandidateChecks}, backtracks={result.Backtracks}, " +
            $"materialized={result.MaterializedStates}, deduplicated={result.DuplicateStates}, " +
            $"skippedOptimalBatches={result.SkippedOptimalBatches}.");
#endif
        return new OrderedLayoutSearchResult(
            plan,
            OrderedLayoutStatus.Success,
            true,
            result.UsedBottom,
            result.PlacedBottom,
            result.Perimeter,
            result.HorizontalWeight);
    }

    private static OrderedLayoutPlan CreatePlan(IReadOnlyList<Item> orderedItems, OrderedLayoutResult result)
    {
        List<OrderedPlannedPlacement> placements = new(result.Placements.Length);

        for (int index = 0; index < result.Placements.Length; index++)
        {
            OrderedLayoutPlacement placement = result.Placements[index];
            ItemRotation rotation = placement.Rotated ? ItemRotation.Vertical : ItemRotation.Horizontal;
            placements.Add(new OrderedPlannedPlacement(orderedItems[index],
                new LocationInGrid(placement.X, placement.Y, rotation)));
        }

        return new OrderedLayoutPlan(placements);
    }

    private static OrderedLayoutItem[] BuildLayoutItems(IReadOnlyList<Item> orderedItems)
    {
        OrderedLayoutItem[] result = new OrderedLayoutItem[orderedItems.Count];

        for (int index = 0; index < orderedItems.Count; index++)
        {
            Item item = orderedItems[index] ??
                        throw new ArgumentException("Items cannot contain null.", nameof(orderedItems));
            IntVec2 size = item.CalculateCellSize();
            result[index] = new OrderedLayoutItem(size.X, size.Y);
        }

        return result;
    }

    private static int[] CopyBatchStarts(IReadOnlyList<int> primaryBatchStarts)
    {
        int[] result = new int[primaryBatchStarts.Count];

        for (int index = 0; index < result.Length; index++) result[index] = primaryBatchStarts[index];

        return result;
    }

    private static bool[] CopyLayout(Grid grid)
    {
        long expectedCellCount = (long)grid.GridWidth * grid.GridHeight;

        if (expectedCellCount <= 0L || expectedCellCount > int.MaxValue || grid.Layout.Count != (int)expectedCellCount)
            throw new InvalidOperationException("Grid layout dimensions are inconsistent.");

        bool[] result = new bool[(int)expectedCellCount];

        for (int index = 0; index < result.Length; index++) result[index] = grid.Layout[index];

        return result;
    }

    private sealed class CacheEntry
    {
        private readonly bool[] _baseLayout;
        private readonly int[] _batchStarts;
        private readonly int _gridHeight;
        private readonly int _gridWidth;
        private readonly string[] _itemIds;
        private readonly Item[] _itemsWithoutIds;
        private readonly OrderedLayoutItem[] _layoutItems;
        private readonly CachedPlacement[] _placements;
        private readonly bool _separateBatches;
        private readonly string[] _templateIds;

        public CacheEntry(int gridWidth, int gridHeight, bool[] baseLayout, IReadOnlyList<Item> orderedItems,
            OrderedLayoutItem[] layoutItems, int[] batchStarts, bool separateBatches, OrderedLayoutResult result)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _baseLayout = (bool[])baseLayout.Clone();
            _itemIds = new string[orderedItems.Count];
            _templateIds = new string[orderedItems.Count];
            _itemsWithoutIds = new Item[orderedItems.Count];
            _layoutItems = (OrderedLayoutItem[])layoutItems.Clone();
            _batchStarts = (int[])batchStarts.Clone();
            _separateBatches = separateBatches;
            _placements = new CachedPlacement[result.Placements.Length];

            for (int index = 0; index < orderedItems.Count; index++)
            {
                Item item = orderedItems[index];
                _itemIds[index] = item.Id;
                _templateIds[index] = item.TemplateId;
                if (item.Id == null) _itemsWithoutIds[index] = item;

                OrderedLayoutPlacement placement = result.Placements[index];
                _placements[index] = new CachedPlacement(placement.X, placement.Y, placement.Rotated);
            }
        }

        public bool Matches(int gridWidth, int gridHeight, bool[] baseLayout, IReadOnlyList<Item> orderedItems,
            OrderedLayoutItem[] layoutItems, int[] batchStarts, bool separateBatches)
        {
            if (_gridWidth != gridWidth || _gridHeight != gridHeight || _separateBatches != separateBatches ||
                _baseLayout.Length != baseLayout.Length || _itemIds.Length != orderedItems.Count ||
                _batchStarts.Length != batchStarts.Length)
                return false;

            for (int index = 0; index < _baseLayout.Length; index++)
                if (_baseLayout[index] != baseLayout[index])
                    return false;

            for (int index = 0; index < _batchStarts.Length; index++)
                if (_batchStarts[index] != batchStarts[index])
                    return false;

            for (int index = 0; index < orderedItems.Count; index++)
            {
                Item item = orderedItems[index];

                if (!string.Equals(_itemIds[index], item.Id, StringComparison.Ordinal) ||
                    !string.Equals(_templateIds[index], item.TemplateId, StringComparison.Ordinal) ||
                    _layoutItems[index].Width != layoutItems[index].Width ||
                    _layoutItems[index].Height != layoutItems[index].Height)
                    return false;

                if (_itemIds[index] == null && !ReferenceEquals(_itemsWithoutIds[index], item)) return false;
            }

            return true;
        }

        public OrderedLayoutPlan CreatePlan(IReadOnlyList<Item> orderedItems)
        {
            List<OrderedPlannedPlacement> placements = new(_placements.Length);

            for (int index = 0; index < _placements.Length; index++)
            {
                CachedPlacement placement = _placements[index];
                ItemRotation rotation = placement.Rotated ? ItemRotation.Vertical : ItemRotation.Horizontal;
                placements.Add(new OrderedPlannedPlacement(orderedItems[index],
                    new LocationInGrid(placement.X, placement.Y, rotation)));
            }

            return new OrderedLayoutPlan(placements);
        }
    }

    private readonly struct CachedPlacement(int x, int y, bool rotated)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public bool Rotated { get; } = rotated;
    }
}

internal sealed class OrderedLayoutSearchResult(
    OrderedLayoutPlan plan,
    OrderedLayoutStatus status,
    bool searchPerformed,
    int usedBottom,
    int placedBottom,
    int perimeter,
    int horizontalWeight)
{
    public OrderedLayoutPlan Plan { get; } = plan;
    public OrderedLayoutStatus Status { get; } = status;
    public bool SearchPerformed { get; } = searchPerformed;
    public int UsedBottom { get; } = usedBottom;
    public int PlacedBottom { get; } = placedBottom;
    public int Perimeter { get; } = perimeter;
    public int HorizontalWeight { get; } = horizontalWeight;
}

internal sealed class OrderedLayoutPlan(List<OrderedPlannedPlacement> placements)
{
    public List<OrderedPlannedPlacement> Placements { get; } = placements;
}

internal sealed class OrderedPlannedPlacement(Item item, LocationInGrid location)
{
    public Item Item { get; } = item;
    public LocationInGrid Location { get; } = location;
}