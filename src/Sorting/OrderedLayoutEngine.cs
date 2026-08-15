using System;
using System.Collections.Generic;

namespace AdvancedStashSorting.Sorting;

internal static class OrderedLayoutEngine
{
    private const int BeamWidth = 48;
    private const int MaximumCandidatesPerBeamState = 12;
    private const int MaximumBeamCandidateChecks = 1_000_000;

    public static OrderedLayoutResult Search(OrderedLayoutRequest request)
    {
        Validate(request);

        if (request.Items.Length == 0)
        {
            int emptyUsedBottom = GetUsedBottom(request.BaseLayout, request.Width, request.Height);
            int emptyPerimeter = GetPerimeter(request.BaseLayout, request.Width, request.Height);
            return OrderedLayoutResult.Success([], emptyUsedBottom, 0, emptyPerimeter, 0, 0, 0);
        }

        OrderedLayoutResult baseline = SearchDepthFirst(request);

        if (!request.SeparateBatches || baseline.Status != OrderedLayoutStatus.Success || request.Items.Length < 2)
            return baseline;

        OrderedLayoutResult optimized = SearchSeparatedBeam(request);
        return optimized?.Status == OrderedLayoutStatus.Success ? optimized : baseline;
    }

    private static OrderedLayoutResult SearchDepthFirst(OrderedLayoutRequest request)
    {
        bool[] endsBatch = BuildBatchEnds(request.Items.Length, request.BatchStarts);
        RowOccupancy occupancy = new(request.Width, request.Height, request.BaseLayout);
        FailedStateCache failedStates = new(occupancy.BlockCount, request.MaximumBacktracks);
        SearchFrame[] frames = new SearchFrame[request.Items.Length];
        PlacementCandidate[] selected = new PlacementCandidate[request.Items.Length];
        frames[0] = new SearchFrame(0, 0);

        int depth = 0;
        int candidateChecks = 0;
        int backtracks = 0;

        while (depth >= 0)
        {
            bool limitReached;
            PlacementCandidate candidate;
            bool found = TryFindNextCandidate(request, request.Items[depth], occupancy, ref frames[depth],
                ref candidateChecks, out candidate, out limitReached);

            if (limitReached) return OrderedLayoutResult.SearchLimitReached(candidateChecks, backtracks);

            if (found)
            {
                occupancy.Mark(candidate);
                selected[depth] = candidate;

                if (depth == request.Items.Length - 1)
                    return BuildSuccess(request, selected, candidateChecks, backtracks);

                SearchFrame currentFrame = frames[depth];
                int batchBottom = Math.Max(currentFrame.BatchBottom, candidate.Y + candidate.Height);
                int nextAnchor;
                int nextBatchBottom;

                if (endsBatch[depth])
                {
                    nextAnchor = request.SeparateBatches ? batchBottom * request.Width : 0;
                    nextBatchBottom = request.SeparateBatches ? batchBottom : 0;
                }
                else
                {
                    nextAnchor = currentFrame.StartAnchor;
                    nextBatchBottom = batchBottom;
                }

                if (failedStates.Contains(depth + 1, nextAnchor, nextBatchBottom, occupancy))
                {
                    occupancy.Unmark(candidate);
                    continue;
                }

                depth++;
                frames[depth] = new SearchFrame(nextAnchor, nextBatchBottom);
                continue;
            }

            SearchFrame exhaustedFrame = frames[depth];
            failedStates.Add(depth, exhaustedFrame.StartAnchor, exhaustedFrame.BatchBottom, occupancy);

            if (depth == 0) return OrderedLayoutResult.NoFit(candidateChecks, backtracks);

            if (backtracks >= request.MaximumBacktracks)
                return OrderedLayoutResult.SearchLimitReached(candidateChecks, backtracks);

            depth--;
            occupancy.Unmark(selected[depth]);
            backtracks++;
        }

        return OrderedLayoutResult.NoFit(candidateChecks, backtracks);
    }

    private static OrderedLayoutResult SearchSeparatedBeam(OrderedLayoutRequest request)
    {
        RowOccupancy occupancy = new(request.Width, request.Height, request.BaseLayout);
        PlacementCandidate[] selected = new PlacementCandidate[request.Items.Length];
        int boundaryRow = 0;
        int candidateChecks = 0;
        int generationOrdinal = 0;
        int materializedStates = 0;
        int duplicateStates = 0;
        int skippedOptimalBatches = 0;

        for (int batchIndex = 0; batchIndex < request.BatchStarts.Length; batchIndex++)
        {
            int batchStart = request.BatchStarts[batchIndex];
            int batchEnd = batchIndex + 1 < request.BatchStarts.Length
                ? request.BatchStarts[batchIndex + 1]
                : request.Items.Length;
            long itemArea = GetItemArea(request.Items, batchStart, batchEnd);
            int minimumBottom = occupancy.GetMinimumBottomForArea(boundaryRow, itemArea);

            BatchLayout greedy = BuildGreedyBatch(request, occupancy, boundaryRow, batchStart, batchEnd, ref candidateChecks);

            if (greedy == null) return null;

            BatchLayout chosen = greedy;

            if (greedy.BatchBottom == minimumBottom)
            {
                skippedOptimalBatches++;
            }
            else
            {
                BatchLayout optimized = SearchBatchBeam(request, occupancy, boundaryRow, batchStart, batchEnd,
                    ref candidateChecks, ref generationOrdinal, ref materializedStates, ref duplicateStates);

                if (optimized != null && CompareBatchLayouts(optimized, greedy) < 0) chosen = optimized;
            }

            for (int itemIndex = batchStart; itemIndex < batchEnd; itemIndex++)
                selected[itemIndex] = chosen.Placements[itemIndex - batchStart];

            occupancy = chosen.Occupancy;
            boundaryRow = chosen.BatchBottom;
        }

        return BuildSuccess(request, selected, candidateChecks, 0, materializedStates, duplicateStates,
            skippedOptimalBatches);
    }

    private static BatchLayout BuildGreedyBatch(OrderedLayoutRequest request, RowOccupancy occupancy, int boundaryRow,
        int batchStart, int batchEnd, ref int candidateChecks)
    {
        RowOccupancy resultOccupancy = occupancy.Clone();
        PlacementCandidate[] placements = new PlacementCandidate[batchEnd - batchStart];
        int batchBottom = boundaryRow;
        long horizontalWeight = 0L;

        for (int itemIndex = batchStart; itemIndex < batchEnd; itemIndex++)
        {
            bool found = TryFindFirstBeamCandidate(request, request.Items[itemIndex], resultOccupancy,
                boundaryRow * request.Width, ref candidateChecks, out PlacementCandidate candidate);

            if (!found) return null;

            resultOccupancy.Mark(candidate);
            placements[itemIndex - batchStart] = candidate;
            batchBottom = Math.Max(batchBottom, candidate.Y + candidate.Height);
            horizontalWeight += GetHorizontalWeight(candidate);
        }

        int packingPenalty = resultOccupancy.GetPackingPenalty(boundaryRow, batchBottom);

        return new BatchLayout(resultOccupancy, placements, batchBottom, horizontalWeight, packingPenalty);
    }

    private static BatchLayout SearchBatchBeam(OrderedLayoutRequest request, RowOccupancy occupancy, int boundaryRow,
        int batchStart, int batchEnd, ref int candidateChecks, ref int generationOrdinal, ref int materializedStates,
        ref int duplicateStates)
    {
        List<BeamState> states = [new(occupancy, null, default, boundaryRow, 0L, 0, default)];

        for (int itemIndex = batchStart; itemIndex < batchEnd; itemIndex++)
        {
            List<BeamExpansion> expansions = [];

            for (int stateIndex = 0; stateIndex < states.Count; stateIndex++)
            {
                BeamState state = states[stateIndex];
                AddBeamExpansions(request, request.Items[itemIndex], state, boundaryRow * request.Width, expansions,
                    ref candidateChecks, ref generationOrdinal);
            }

            if (expansions.Count == 0) return null;

            expansions.Sort(CompareBeamExpansions);
            states = MaterializeUniqueBeamStates(expansions, ref materializedStates, ref duplicateStates);

            if (states.Count == 0) return null;
        }

        BeamState best = states[0];
        PlacementCandidate[] placements = new PlacementCandidate[batchEnd - batchStart];
        BeamState current = best;

        for (int itemIndex = placements.Length - 1; itemIndex >= 0; itemIndex--)
        {
            placements[itemIndex] = current.Candidate;
            current = current.Parent;
        }

        return new BatchLayout(best.Occupancy, placements, best.BatchBottom, best.HorizontalWeight,
            best.PackingPenalty);
    }

    private static List<BeamState> MaterializeUniqueBeamStates(List<BeamExpansion> expansions,
        ref int materializedStates, ref int duplicateStates)
    {
        List<BeamState> result = new(BeamWidth);

        for (int expansionIndex = 0; expansionIndex < expansions.Count && result.Count < BeamWidth; expansionIndex++)
        {
            BeamExpansion expansion = expansions[expansionIndex];
            expansion.Parent.Occupancy.Mark(expansion.Candidate);
            bool duplicate = false;

            for (int stateIndex = 0; stateIndex < result.Count; stateIndex++)
                if (expansion.HashKey.Equals(result[stateIndex].HashKey) &&
                    expansion.Parent.Occupancy.SameBlocks(result[stateIndex].Occupancy))
                {
                    duplicate = true;
                    break;
                }

            if (duplicate)
            {
                expansion.Parent.Occupancy.Unmark(expansion.Candidate);
                duplicateStates++;
                continue;
            }

            RowOccupancy childOccupancy = expansion.Parent.Occupancy.Clone();
            expansion.Parent.Occupancy.Unmark(expansion.Candidate);
            BeamState child = new(childOccupancy, expansion.Parent, expansion.Candidate, expansion.BatchBottom,
                expansion.HorizontalWeight, expansion.PackingPenalty, expansion.HashKey);
            result.Add(child);
            materializedStates++;
        }

        return result;
    }

    private static bool TryFindFirstBeamCandidate(OrderedLayoutRequest request, OrderedLayoutItem item,
        RowOccupancy occupancy, int startAnchor, ref int candidateChecks, out PlacementCandidate candidate)
    {
        int orientationCount = item.Width == item.Height ? 1 : 2;
        int cellCount = request.Width * request.Height;
        int anchor = startAnchor;

        while (anchor < cellCount && candidateChecks < MaximumBeamCandidateChecks)
        {
            anchor = occupancy.FindNextFreeCell(anchor);
            if (anchor >= cellCount) break;

            for (int orientationIndex = 0; orientationIndex < orientationCount; orientationIndex++)
            {
                candidateChecks++;
                bool firstRotated = item.Width < item.Height;
                bool rotated = orientationCount == 2 && (orientationIndex == 0 ? firstRotated : !firstRotated);
                int width = rotated ? item.Height : item.Width;
                int height = rotated ? item.Width : item.Height;
                int x = anchor % request.Width;
                int y = anchor / request.Width;

                if (x + width > request.Width || y + height > request.Height ||
                    !occupancy.IsFree(x, y, width, height)) continue;

                candidate = new PlacementCandidate(x, y, width, height, rotated);
                return true;
            }

            anchor++;
        }

        candidate = default;
        return false;
    }

    private static void AddBeamExpansions(OrderedLayoutRequest request, OrderedLayoutItem item, BeamState state,
        int startAnchor, List<BeamExpansion> expansions, ref int candidateChecks, ref int generationOrdinal)
    {
        int orientationCount = item.Width == item.Height ? 1 : 2;
        int cellCount = request.Width * request.Height;
        int anchor = startAnchor;
        int foundCandidates = 0;

        while (anchor < cellCount && foundCandidates < MaximumCandidatesPerBeamState &&
               candidateChecks < MaximumBeamCandidateChecks)
        {
            anchor = state.Occupancy.FindNextFreeCell(anchor);

            if (anchor >= cellCount) break;

            for (int orientationIndex = 0; orientationIndex < orientationCount; orientationIndex++)
            {
                candidateChecks++;
                bool firstRotated = item.Width < item.Height;
                bool rotated = orientationCount == 2 && (orientationIndex == 0 ? firstRotated : !firstRotated);
                int width = rotated ? item.Height : item.Width;
                int height = rotated ? item.Width : item.Height;
                int x = anchor % request.Width;
                int y = anchor / request.Width;

                if (x + width > request.Width || y + height > request.Height ||
                    !state.Occupancy.IsFree(x, y, width, height)) continue;

                PlacementCandidate candidate = new(x, y, width, height, rotated);
                int affectedEnd = Math.Min(state.BatchBottom, candidate.Y + candidate.Height);
                int previousAffectedPenalty = affectedEnd > candidate.Y
                    ? state.Occupancy.GetPackingPenalty(candidate.Y, affectedEnd)
                    : 0;
                state.Occupancy.Mark(candidate);
                int batchBottom = Math.Max(state.BatchBottom, candidate.Y + candidate.Height);
                long horizontalWeight = state.HorizontalWeight + GetHorizontalWeight(candidate);
                int packingPenalty = state.PackingPenalty - previousAffectedPenalty;

                if (affectedEnd > candidate.Y)
                    packingPenalty += state.Occupancy.GetPackingPenalty(candidate.Y, affectedEnd);

                if (batchBottom > state.BatchBottom)
                    packingPenalty += state.Occupancy.GetPackingPenalty(state.BatchBottom, batchBottom);

                state.Occupancy.GetHashes(out ulong firstHash, out ulong secondHash);
                state.Occupancy.Unmark(candidate);
                expansions.Add(new BeamExpansion(state, candidate, batchBottom, horizontalWeight, packingPenalty,
                    generationOrdinal++, new BeamHashKey(firstHash, secondHash)));
                foundCandidates++;

                if (foundCandidates >= MaximumCandidatesPerBeamState) break;
            }

            anchor++;
        }
    }

    private static int CompareBeamExpansions(BeamExpansion left, BeamExpansion right)
    {
        int comparison = left.BatchBottom.CompareTo(right.BatchBottom);

        if (comparison != 0) return comparison;

        comparison = left.PackingPenalty.CompareTo(right.PackingPenalty);

        if (comparison != 0) return comparison;

        comparison = left.HorizontalWeight.CompareTo(right.HorizontalWeight);
        return comparison != 0 ? comparison : left.GenerationOrdinal.CompareTo(right.GenerationOrdinal);
    }

    private static int CompareBatchLayouts(BatchLayout left, BatchLayout right)
    {
        int comparison = left.BatchBottom.CompareTo(right.BatchBottom);

        if (comparison != 0) return comparison;

        comparison = left.PackingPenalty.CompareTo(right.PackingPenalty);
        return comparison != 0 ? comparison : left.HorizontalWeight.CompareTo(right.HorizontalWeight);
    }

    private static long GetItemArea(OrderedLayoutItem[] items, int start, int end)
    {
        long result = 0L;

        for (int index = start; index < end; index++) result += (long)items[index].Width * items[index].Height;

        return result;
    }

    private static long GetHorizontalWeight(PlacementCandidate candidate)
    {
        long rowWeight = (long)candidate.Width * candidate.X + (long)candidate.Width * (candidate.Width - 1) / 2L;
        return rowWeight * candidate.Height;
    }

    private static bool TryFindNextCandidate(OrderedLayoutRequest request, OrderedLayoutItem item,
        RowOccupancy occupancy, ref SearchFrame frame, ref int candidateChecks, out PlacementCandidate candidate,
        out bool limitReached)
    {
        int orientationCount = item.Width == item.Height ? 1 : 2;
        int cellCount = request.Width * request.Height;

        while (frame.NextAnchor < cellCount)
        {
            if (frame.NextOrientation == 0)
            {
                frame.NextAnchor = occupancy.FindNextFreeCell(frame.NextAnchor);

                if (frame.NextAnchor >= cellCount) break;
            }

            if (candidateChecks >= request.MaximumCandidateChecks)
            {
                candidate = default;
                limitReached = true;
                return false;
            }

            int anchor = frame.NextAnchor;
            int orientationIndex = frame.NextOrientation;
            frame.NextOrientation++;

            if (frame.NextOrientation >= orientationCount)
            {
                frame.NextAnchor++;
                frame.NextOrientation = 0;
            }

            candidateChecks++;

            bool firstRotated = item.Width < item.Height;
            bool rotated = orientationCount == 2 && (orientationIndex == 0 ? firstRotated : !firstRotated);
            int width = rotated ? item.Height : item.Width;
            int height = rotated ? item.Width : item.Height;
            int x = anchor % request.Width;
            int y = anchor / request.Width;

            if (x + width > request.Width || y + height > request.Height ||
                !occupancy.IsFree(x, y, width, height)) continue;

            candidate = new PlacementCandidate(x, y, width, height, rotated);
            limitReached = false;
            return true;
        }

        candidate = default;
        limitReached = false;
        return false;
    }

    private static OrderedLayoutResult BuildSuccess(OrderedLayoutRequest request, PlacementCandidate[] selected,
        int candidateChecks, int backtracks, int materializedStates = 0, int duplicateStates = 0,
        int skippedOptimalBatches = 0)
    {
        OrderedLayoutPlacement[] placements = new OrderedLayoutPlacement[selected.Length];
        bool[] finalLayout = new bool[request.BaseLayout.Length];
        Array.Copy(request.BaseLayout, finalLayout, request.BaseLayout.Length);
        int placedBottom = 0;
        long horizontalWeight = 0L;

        for (int index = 0; index < selected.Length; index++)
        {
            PlacementCandidate candidate = selected[index];
            placements[index] = new OrderedLayoutPlacement(candidate.X, candidate.Y, candidate.Width, candidate.Height,
                candidate.Rotated);
            Mark(finalLayout, request.Width, candidate.X, candidate.Y, candidate.Width, candidate.Height);
            placedBottom = Math.Max(placedBottom, candidate.Y + candidate.Height);
            long rowWeight = (long)candidate.Width * candidate.X + (long)candidate.Width * (candidate.Width - 1) / 2L;
            horizontalWeight += rowWeight * candidate.Height;
        }

        int normalizedHorizontalWeight = horizontalWeight >= int.MaxValue ? int.MaxValue : (int)horizontalWeight;
        int usedBottom = GetUsedBottom(finalLayout, request.Width, request.Height);
        int perimeter = GetPerimeter(finalLayout, request.Width, request.Height);
        return OrderedLayoutResult.Success(placements, usedBottom, placedBottom, perimeter, normalizedHorizontalWeight,
            candidateChecks, backtracks, materializedStates, duplicateStates, skippedOptimalBatches);
    }

    private static bool[] BuildBatchEnds(int itemCount, int[] batchStarts)
    {
        bool[] result = new bool[itemCount];

        for (int index = 1; index < batchStarts.Length; index++) result[batchStarts[index] - 1] = true;

        result[itemCount - 1] = true;
        return result;
    }

    private static void Validate(OrderedLayoutRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.Width <= 0) throw new ArgumentOutOfRangeException(nameof(request.Width));

        if (request.Height <= 0) throw new ArgumentOutOfRangeException(nameof(request.Height));

        long cellCount = (long)request.Width * request.Height;

        if (cellCount > int.MaxValue || request.BaseLayout.Length != (int)cellCount)
            throw new ArgumentException("Base layout dimensions do not match the grid.", nameof(request));

        if (request.MaximumCandidateChecks <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.MaximumCandidateChecks));

        if (request.MaximumBacktracks <= 0) throw new ArgumentOutOfRangeException(nameof(request.MaximumBacktracks));

        if (request.Items.Length == 0)
        {
            if (request.BatchStarts.Length != 0)
                throw new ArgumentException("An empty item list cannot contain batches.", nameof(request));

            return;
        }

        if (request.BatchStarts.Length == 0 || request.BatchStarts[0] != 0)
            throw new ArgumentException("The first batch must start at item zero.", nameof(request));

        int previousStart = -1;

        for (int index = 0; index < request.BatchStarts.Length; index++)
        {
            int start = request.BatchStarts[index];

            if (start <= previousStart || start < 0 || start >= request.Items.Length)
                throw new ArgumentException("Batch starts must be strictly increasing item indexes.", nameof(request));

            previousStart = start;
        }

        for (int index = 0; index < request.Items.Length; index++)
            if (request.Items[index].Width <= 0 || request.Items[index].Height <= 0)
                throw new ArgumentException("Item dimensions must be positive.", nameof(request));
    }

    private static void Mark(bool[] layout, int gridWidth, int x, int y, int width, int height)
    {
        for (int row = y; row < y + height; row++)
        {
            int offset = row * gridWidth + x;

            for (int column = 0; column < width; column++) layout[offset + column] = true;
        }
    }

    private static int GetUsedBottom(bool[] layout, int gridWidth, int gridHeight)
    {
        for (int row = gridHeight - 1; row >= 0; row--)
        {
            int offset = row * gridWidth;

            for (int column = 0; column < gridWidth; column++)
                if (layout[offset + column])
                    return row + 1;
        }

        return 0;
    }

    private static int GetPerimeter(bool[] layout, int gridWidth, int gridHeight)
    {
        int perimeter = 0;

        for (int y = 0; y < gridHeight; y++)
        for (int x = 0; x < gridWidth; x++)
        {
            int index = y * gridWidth + x;

            if (!layout[index]) continue;

            if (x == 0 || !layout[index - 1]) perimeter++;

            if (x == gridWidth - 1 || !layout[index + 1]) perimeter++;

            if (y == 0 || !layout[index - gridWidth]) perimeter++;

            if (y == gridHeight - 1 || !layout[index + gridWidth]) perimeter++;
        }

        return perimeter;
    }

    private struct SearchFrame(int nextAnchor, int batchBottom)
    {
        public readonly int StartAnchor = nextAnchor;
        public int NextAnchor = nextAnchor;
        public int NextOrientation = 0;
        public readonly int BatchBottom = batchBottom;
    }

    private readonly struct PlacementCandidate(int x, int y, int width, int height, bool rotated)
    {
        public int X { get; } = x;
        public int Y { get; } = y;
        public int Width { get; } = width;
        public int Height { get; } = height;
        public bool Rotated { get; } = rotated;
    }

    private sealed class BeamState(
        RowOccupancy occupancy,
        BeamState parent,
        PlacementCandidate candidate,
        int batchBottom,
        long horizontalWeight,
        int packingPenalty,
        BeamHashKey hashKey)
    {
        public RowOccupancy Occupancy { get; } = occupancy;
        public BeamState Parent { get; } = parent;
        public PlacementCandidate Candidate { get; } = candidate;
        public int BatchBottom { get; } = batchBottom;
        public long HorizontalWeight { get; } = horizontalWeight;
        public int PackingPenalty { get; } = packingPenalty;
        public BeamHashKey HashKey { get; } = hashKey;
    }

    private readonly struct BeamExpansion(
        BeamState parent,
        PlacementCandidate candidate,
        int batchBottom,
        long horizontalWeight,
        int packingPenalty,
        int generationOrdinal,
        BeamHashKey hashKey)
    {
        public BeamState Parent { get; } = parent;
        public PlacementCandidate Candidate { get; } = candidate;
        public int BatchBottom { get; } = batchBottom;
        public long HorizontalWeight { get; } = horizontalWeight;
        public int PackingPenalty { get; } = packingPenalty;
        public int GenerationOrdinal { get; } = generationOrdinal;
        public BeamHashKey HashKey { get; } = hashKey;
    }

    private readonly struct BeamHashKey(ulong firstHash, ulong secondHash) : IEquatable<BeamHashKey>
    {
        private ulong FirstHash { get; } = firstHash;
        private ulong SecondHash { get; } = secondHash;

        public bool Equals(BeamHashKey other)
        {
            return FirstHash == other.FirstHash && SecondHash == other.SecondHash;
        }

        public override bool Equals(object obj)
        {
            return obj is BeamHashKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)FirstHash;
                hash = (hash * 397) ^ (int)(FirstHash >> 32);
                hash = (hash * 397) ^ (int)SecondHash;
                return (hash * 397) ^ (int)(SecondHash >> 32);
            }
        }
    }

    private sealed class BatchLayout(
        RowOccupancy occupancy,
        PlacementCandidate[] placements,
        int batchBottom,
        long horizontalWeight,
        int packingPenalty)
    {
        public RowOccupancy Occupancy { get; } = occupancy;
        public PlacementCandidate[] Placements { get; } = placements;
        public int BatchBottom { get; } = batchBottom;
        public long HorizontalWeight { get; } = horizontalWeight;
        public int PackingPenalty { get; } = packingPenalty;
    }

    private sealed class RowOccupancy
    {
        private readonly ulong[] _blocks;
        private readonly int _blocksPerRow;
        private readonly int _height;
        private readonly int _width;
        private ulong _firstHash;
        private ulong _secondHash;

        public RowOccupancy(int width, int height, bool[] baseLayout)
        {
            _width = width;
            _height = height;
            _blocksPerRow = checked((int)((width + 63L) / 64L));
            int blockCount = checked((int)((long)_blocksPerRow * height));
            _blocks = new ulong[blockCount];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (baseLayout[y * width + x])
                    _blocks[y * _blocksPerRow + x / 64] |= 1UL << (x & 63);

            int usedBitsInLastBlock = width & 63;

            if (usedBitsInLastBlock != 0)
            {
                ulong paddingMask = ulong.MaxValue << usedBitsInLastBlock;

                for (int row = 0; row < height; row++) _blocks[(row + 1) * _blocksPerRow - 1] |= paddingMask;
            }

            InitializeHashes();
        }

        private RowOccupancy(RowOccupancy source)
        {
            _width = source._width;
            _height = source._height;
            _blocksPerRow = source._blocksPerRow;
            _blocks = (ulong[])source._blocks.Clone();
            _firstHash = source._firstHash;
            _secondHash = source._secondHash;
        }

        public int BlockCount => _blocks.Length;

        public RowOccupancy Clone()
        {
            return new RowOccupancy(this);
        }

        public int GetPackingPenalty(int startRow, int endRow)
        {
            int penalty = 0;

            for (int row = startRow; row < endRow; row++)
            {
                int rightmostOccupied = -1;

                for (int column = _width - 1; column >= 0; column--)
                    if (IsOccupied(column, row))
                    {
                        rightmostOccupied = column;
                        break;
                    }

                for (int column = 0; column < rightmostOccupied; column++)
                    if (!IsOccupied(column, row))
                        penalty++;
            }

            return penalty;
        }

        public int GetMinimumBottomForArea(int startRow, long area)
        {
            long freeCells = 0L;

            for (int row = startRow; row < _height; row++)
            {
                for (int column = 0; column < _width; column++)
                    if (!IsOccupied(column, row))
                        freeCells++;

                if (freeCells >= area) return row + 1;
            }

            return _height + 1;
        }

        public int FindNextFreeCell(int anchor)
        {
            int row = anchor / _width;
            int column = anchor % _width;

            for (; row < _height; row++)
            {
                int firstBlock = column / 64;
                int firstBit = column & 63;
                int rowOffset = row * _blocksPerRow;

                for (int block = firstBlock; block < _blocksPerRow; block++)
                {
                    ulong available = ~_blocks[rowOffset + block];

                    if (block == firstBlock && firstBit > 0) available &= ulong.MaxValue << firstBit;

                    if (available == 0UL) continue;

                    int bit = GetTrailingZeroCount(available);
                    int foundColumn = block * 64 + bit;
                    return row * _width + foundColumn;
                }

                column = 0;
            }

            return _width * _height;
        }

        public bool IsFree(int x, int y, int width, int height)
        {
            int rangeEnd = x + width;
            int firstBlock = x / 64;
            int lastBlock = (rangeEnd - 1) / 64;

            for (int row = y; row < y + height; row++)
            {
                int rowOffset = row * _blocksPerRow;

                if (firstBlock == lastBlock)
                {
                    ulong mask = CreateSingleBlockMask(x & 63, width);

                    if ((_blocks[rowOffset + firstBlock] & mask) != 0UL) return false;

                    continue;
                }

                ulong firstMask = ulong.MaxValue << (x & 63);

                if ((_blocks[rowOffset + firstBlock] & firstMask) != 0UL) return false;

                for (int block = firstBlock + 1; block < lastBlock; block++)
                    if (_blocks[rowOffset + block] != 0UL)
                        return false;

                int lastBits = rangeEnd & 63;
                ulong lastMask = lastBits == 0 ? ulong.MaxValue : CreateLowMask(lastBits);

                if ((_blocks[rowOffset + lastBlock] & lastMask) != 0UL) return false;
            }

            return true;
        }

        public void Mark(PlacementCandidate candidate)
        {
            Set(candidate.X, candidate.Y, candidate.Width, candidate.Height, true);
        }

        public void Unmark(PlacementCandidate candidate)
        {
            Set(candidate.X, candidate.Y, candidate.Width, candidate.Height, false);
        }

        private void Set(int x, int y, int width, int height, bool occupied)
        {
            int rangeEnd = x + width;
            int firstBlock = x / 64;
            int lastBlock = (rangeEnd - 1) / 64;

            for (int row = y; row < y + height; row++)
            {
                int rowOffset = row * _blocksPerRow;

                if (firstBlock == lastBlock)
                {
                    ulong mask = CreateSingleBlockMask(x & 63, width);
                    Apply(rowOffset + firstBlock, mask, occupied);
                    continue;
                }

                ulong firstMask = ulong.MaxValue << (x & 63);
                Apply(rowOffset + firstBlock, firstMask, occupied);

                for (int block = firstBlock + 1; block < lastBlock; block++)
                    Apply(rowOffset + block, ulong.MaxValue, occupied);

                int lastBits = rangeEnd & 63;
                ulong lastMask = lastBits == 0 ? ulong.MaxValue : CreateLowMask(lastBits);
                Apply(rowOffset + lastBlock, lastMask, occupied);
            }
        }

        private void Apply(int index, ulong mask, bool occupied)
        {
            ulong previous = _blocks[index];
            ulong current = occupied ? previous | mask : previous & ~mask;

            if (previous == current) return;

            _firstHash ^= GetFirstContribution(index, previous) ^ GetFirstContribution(index, current);
            _secondHash ^= GetSecondContribution(index, previous) ^ GetSecondContribution(index, current);
            _blocks[index] = current;
        }

        private bool IsOccupied(int x, int y)
        {
            int index = y * _blocksPerRow + x / 64;
            return (_blocks[index] & (1UL << (x & 63))) != 0UL;
        }

        private static ulong CreateSingleBlockMask(int offset, int length)
        {
            return length == 64 ? ulong.MaxValue : CreateLowMask(length) << offset;
        }

        private static ulong CreateLowMask(int bitCount)
        {
            return bitCount == 64 ? ulong.MaxValue : (1UL << bitCount) - 1UL;
        }

        public void GetHashes(out ulong first, out ulong second)
        {
            first = _firstHash;
            second = _secondHash;
        }

        private void InitializeHashes()
        {
            _firstHash = 1469598103934665603UL;
            _secondHash = 1099511628211UL;

            for (int index = 0; index < _blocks.Length; index++)
            {
                _firstHash ^= GetFirstContribution(index, _blocks[index]);
                _secondHash ^= GetSecondContribution(index, _blocks[index]);
            }
        }

        private static ulong GetFirstContribution(int index, ulong value)
        {
            return Mix(value ^ (((ulong)index + 1UL) * 0x9E3779B97F4A7C15UL));
        }

        private static ulong GetSecondContribution(int index, ulong value)
        {
            return Mix((value + 0xD6E8FEB86659FD93UL) ^ (((ulong)index + 1UL) * 0xA0761D6478BD642FUL));
        }

        public ulong[] CopyBlocks()
        {
            return (ulong[])_blocks.Clone();
        }

        public bool SameBlocks(ulong[] other)
        {
            if (other.Length != _blocks.Length) return false;

            for (int index = 0; index < _blocks.Length; index++)
                if (_blocks[index] != other[index])
                    return false;

            return true;
        }

        public bool SameBlocks(RowOccupancy other)
        {
            if (other == null || other._blocks.Length != _blocks.Length) return false;

            for (int index = 0; index < _blocks.Length; index++)
                if (_blocks[index] != other._blocks[index])
                    return false;

            return true;
        }

        private static int GetTrailingZeroCount(ulong value)
        {
            int count = 0;

            while ((value & 1UL) == 0UL)
            {
                value >>= 1;
                count++;
            }

            return count;
        }

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private sealed class FailedStateCache
    {
        private const long MaximumCachedLayoutBytes = 32L * 1024L * 1024L;
        private readonly int _maximumLayouts;

        private readonly Dictionary<SearchStateKey, List<ulong[]>> _states = new();
        private int _layoutCount;

        public FailedStateCache(int blockCount, int maximumBacktracks)
        {
            const int estimatedStateOverhead = 128;
            long estimatedStateBytes = checked((long)blockCount * sizeof(ulong) + estimatedStateOverhead);
            long byteLimitedMaximum = MaximumCachedLayoutBytes / estimatedStateBytes;
            _maximumLayouts = (int)Math.Min(maximumBacktracks, byteLimitedMaximum);
        }

        public bool Contains(int depth, int cursor, int batchBottom, RowOccupancy occupancy)
        {
            if (_layoutCount == 0) return false;

            SearchStateKey key = CreateKey(depth, cursor, batchBottom, occupancy);

            if (!_states.TryGetValue(key, out List<ulong[]> layouts)) return false;

            for (int index = 0; index < layouts.Count; index++)
                if (occupancy.SameBlocks(layouts[index]))
                    return true;

            return false;
        }

        public void Add(int depth, int cursor, int batchBottom, RowOccupancy occupancy)
        {
            if (_layoutCount >= _maximumLayouts) return;

            SearchStateKey key = CreateKey(depth, cursor, batchBottom, occupancy);

            if (!_states.TryGetValue(key, out List<ulong[]> layouts))
            {
                layouts = [];
                _states.Add(key, layouts);
            }
            else
            {
                for (int index = 0; index < layouts.Count; index++)
                    if (occupancy.SameBlocks(layouts[index]))
                        return;
            }

            layouts.Add(occupancy.CopyBlocks());
            _layoutCount++;
        }

        private static SearchStateKey CreateKey(int depth, int cursor, int batchBottom, RowOccupancy occupancy)
        {
            occupancy.GetHashes(out ulong firstHash, out ulong secondHash);
            return new SearchStateKey(depth, cursor, batchBottom, firstHash, secondHash);
        }
    }

    private readonly struct SearchStateKey(int depth, int cursor, int batchBottom, ulong firstHash, ulong secondHash)
        : IEquatable<SearchStateKey>
    {
        private int Depth { get; } = depth;
        private int Cursor { get; } = cursor;
        private int BatchBottom { get; } = batchBottom;
        private ulong FirstHash { get; } = firstHash;
        private ulong SecondHash { get; } = secondHash;

        public bool Equals(SearchStateKey other)
        {
            return Depth == other.Depth && Cursor == other.Cursor && BatchBottom == other.BatchBottom &&
                   FirstHash == other.FirstHash && SecondHash == other.SecondHash;
        }

        public override bool Equals(object obj)
        {
            return obj is SearchStateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Depth;
                hash = (hash * 397) ^ Cursor;
                hash = (hash * 397) ^ BatchBottom;
                hash = (hash * 397) ^ (int)FirstHash;
                hash = (hash * 397) ^ (int)(FirstHash >> 32);
                hash = (hash * 397) ^ (int)SecondHash;
                return (hash * 397) ^ (int)(SecondHash >> 32);
            }
        }
    }
}

internal sealed class OrderedLayoutRequest(
    int width,
    int height,
    bool[] baseLayout,
    OrderedLayoutItem[] items,
    int[] batchStarts,
    bool separateBatches,
    int maximumCandidateChecks,
    int maximumBacktracks)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public bool[] BaseLayout { get; } = baseLayout ?? throw new ArgumentNullException(nameof(baseLayout));
    public OrderedLayoutItem[] Items { get; } = items ?? throw new ArgumentNullException(nameof(items));
    public int[] BatchStarts { get; } = batchStarts ?? throw new ArgumentNullException(nameof(batchStarts));
    public bool SeparateBatches { get; } = separateBatches;
    public int MaximumCandidateChecks { get; } = maximumCandidateChecks;
    public int MaximumBacktracks { get; } = maximumBacktracks;
}

internal readonly struct OrderedLayoutItem(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
}

internal readonly struct OrderedLayoutPlacement(int x, int y, int width, int height, bool rotated)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public bool Rotated { get; } = rotated;
}

internal enum OrderedLayoutStatus
{
    Success,
    NoFit,
    SearchLimitReached
}

internal sealed class OrderedLayoutResult
{
    private OrderedLayoutResult(OrderedLayoutStatus status, OrderedLayoutPlacement[] placements, int usedBottom,
        int placedBottom, int perimeter, int horizontalWeight, int candidateChecks, int backtracks,
        int materializedStates, int duplicateStates, int skippedOptimalBatches)
    {
        Status = status;
        Placements = placements;
        UsedBottom = usedBottom;
        PlacedBottom = placedBottom;
        Perimeter = perimeter;
        HorizontalWeight = horizontalWeight;
        CandidateChecks = candidateChecks;
        Backtracks = backtracks;
        MaterializedStates = materializedStates;
        DuplicateStates = duplicateStates;
        SkippedOptimalBatches = skippedOptimalBatches;
    }

    public OrderedLayoutStatus Status { get; }
    public OrderedLayoutPlacement[] Placements { get; }
    public int UsedBottom { get; }
    public int PlacedBottom { get; }
    public int Perimeter { get; }
    public int HorizontalWeight { get; }
    public int CandidateChecks { get; }
    public int Backtracks { get; }
    public int MaterializedStates { get; }
    public int DuplicateStates { get; }
    public int SkippedOptimalBatches { get; }

    public static OrderedLayoutResult Success(OrderedLayoutPlacement[] placements, int usedBottom, int placedBottom,
        int perimeter, int horizontalWeight, int candidateChecks, int backtracks, int materializedStates = 0,
        int duplicateStates = 0, int skippedOptimalBatches = 0)
    {
        return new OrderedLayoutResult(OrderedLayoutStatus.Success, placements, usedBottom, placedBottom, perimeter,
            horizontalWeight, candidateChecks, backtracks, materializedStates, duplicateStates, skippedOptimalBatches);
    }

    public static OrderedLayoutResult NoFit(int candidateChecks, int backtracks)
    {
        return new OrderedLayoutResult(OrderedLayoutStatus.NoFit, [], 0, 0, 0,
            0, candidateChecks, backtracks, 0, 0, 0);
    }

    public static OrderedLayoutResult SearchLimitReached(int candidateChecks, int backtracks)
    {
        return new OrderedLayoutResult(OrderedLayoutStatus.SearchLimitReached, [], 0, 0,
            0, 0, candidateChecks, backtracks, 0, 0, 0);
    }
}