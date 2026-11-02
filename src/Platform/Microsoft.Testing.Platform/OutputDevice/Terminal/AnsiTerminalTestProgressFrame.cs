// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Captures <see cref="TestProgressState"/> that was rendered to screen, so we can only partially update the screen on next update.
/// </summary>
[Embedded]
internal sealed partial class AnsiTerminalTestProgressFrame
{
    private const int MaxColumn = 250;

    // Pre-computed ANSI escape sequences for the duration-only update hot path.
    // Re-computing them via AnsiCodes methods on every render tick allocates a new string each time.
    private static readonly string SetCursorHorizontalMaxColumn = AnsiCodes.SetCursorHorizontal(MaxColumn);
    private static readonly string[] MoveCursorBackwardCache = CreateMoveCursorBackwardCache();

    // Reusable working buffers for GenerateLinesToRender, cached across render ticks on the same frame
    // object to eliminate 4+ per-tick heap allocations (3 arrays + 1 List, plus the sort comparer).
    private readonly List<object> _linesToRenderBuffer = [];
    private readonly ProgressCountComparer _progressCountComparer = new();
    private TestProgressState[] _progressItemsBuffer = [];
    private int[] _sortedIndicesBuffer = [];
    private List<TestDetailState>?[] _detailItemsBuffer = [];

    // Pooled RenderedProgressItem instances — reused across render ticks to avoid per-line heap allocations.
    // RenderedLinesCount tracks how many entries are valid in the current frame; the array itself is never
    // shrunk so slots from previous (higher-watermark) ticks remain available for reuse.
    private RenderedProgressItem[] _renderedLines = [];

    public AnsiTerminalTestProgressFrame(int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int RenderedLinesCount { get; private set; }

    /// <summary>
    /// Resets this frame for reuse as the next render target, avoiding a heap allocation per render tick.
    /// </summary>
    internal void Reset(int width, int height)
    {
        Width = Math.Min(width, MaxColumn);
        Height = height;
        RenderedLinesCount = 0;
        _linesToRenderBuffer.Clear();
    }

    public void Clear() => RenderedLinesCount = 0;

    /// <summary>
    /// Returns the next available <see cref="RenderedProgressItem"/> slot, growing the pool on demand.
    /// Increments <see cref="RenderedLinesCount"/> so the slot is considered active.
    /// </summary>
    private RenderedProgressItem GetOrAllocateNextSlot()
    {
        int idx = RenderedLinesCount++;
        if ((uint)idx >= (uint)_renderedLines.Length)
        {
            int newSize = _renderedLines.Length == 0 ? 8 : _renderedLines.Length * 2;
            if (newSize <= idx)
            {
                newSize = idx + 1;
            }

            Array.Resize(ref _renderedLines, newSize);
        }

        _renderedLines[idx] ??= new RenderedProgressItem();
        return _renderedLines[idx];
    }

    private static string[] CreateMoveCursorBackwardCache()
    {
        // Duration strings are at most 16 chars ("(1d 23h 59m 59s)" with parens).
        // A cache of 17 slots (indices 0-16) covers every realistic case with zero allocation.
        const int cacheSize = 17;
        string[] cache = new string[cacheSize];
        cache[0] = string.Empty;
        for (int i = 1; i < cacheSize; i++)
        {
            cache[i] = AnsiCodes.MoveCursorBackward(i);
        }

        return cache;
    }

    /// <summary>
    /// Reusable comparer for sorting progress-item indices by running-task count.
    /// Cached as a field to avoid new allocations on every render tick.
    /// </summary>
    private sealed class ProgressCountComparer : IComparer<int>
    {
        internal TestProgressState[] Buffer { get; set; } = [];

        public int Compare(int a, int b)
            => (Buffer[a].TestNodeResultsState?.Count ?? 0)
                .CompareTo(Buffer[b].TestNodeResultsState?.Count ?? 0);
    }

    internal sealed class RenderedProgressItem
    {
        public long ProgressId { get; private set; }

        public long ProgressVersion { get; private set; }

        public int RenderedDurationLength { get; set; }

        /// <summary>Resets this instance for reuse with new identity and version values.</summary>
        internal void Reset(long id, long version)
        {
            ProgressId = id;
            ProgressVersion = version;
            RenderedDurationLength = 0;
        }
    }
}
