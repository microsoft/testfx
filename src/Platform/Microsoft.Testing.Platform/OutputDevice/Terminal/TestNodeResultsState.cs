// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[Embedded]
internal sealed class TestNodeResultsState
{
    public TestNodeResultsState(long id)
    {
        Id = id;
        _summaryDetail = new(id, stopwatch: null, text: string.Empty);
    }

    public long Id { get; }

    private readonly TestDetailState _summaryDetail;
    private readonly ConcurrentDictionary<string, TestDetailState> _testNodeProgressStates = new();

    // Reusable buffer for GetRunningTasks — cleared and rebuilt each call to avoid per-tick list allocation.
    private readonly List<TestDetailState> _runningTasksBuffer = [];

    // Caches for the two summary messages rendered on the progress row. The summaries are re-rendered on
    // every renderer tick (500ms for the cursor renderer, 1s for the heartbeat one), and the cursor
    // renderer additionally repaints its frame on every write to the terminal, while the running-test
    // count only changes when a test starts or completes. Formatting on every render therefore allocates
    // a string identical to the previous one. The key is the count plus both cultures that can change the
    // result — CurrentUICulture selects the localized resource, CurrentCulture formats the number — so a
    // hit is always byte-identical to formatting right now. The cultures start null, so the first call for
    // each shape always formats.
    // Like _runningTasksBuffer, these fields are not synchronized: every caller is reached through
    // IProgressRenderer.OnTick/OnWrite, which TestProgressStateAwareTerminal serializes under its lock.
    private int _cachedFullTestsCount;
    private CultureInfo? _cachedFullTestsCulture;
    private CultureInfo? _cachedFullTestsUICulture;
    private string _cachedFullTestsText = string.Empty;
    private int _cachedMoreTestsCount;
    private CultureInfo? _cachedMoreTestsCulture;
    private CultureInfo? _cachedMoreTestsUICulture;
    private string _cachedMoreTestsText = string.Empty;

    public int Count => _testNodeProgressStates.Count;

    public void AddRunningTestNode(int id, string uid, string name, IStopwatch stopwatch) => _testNodeProgressStates[uid] = new TestDetailState(id, stopwatch, name);

    public void RemoveRunningTestNode(string uid) => _testNodeProgressStates.TryRemove(uid, out _);

    /// <summary>
    /// Returns the single active task to display on a one-line progress row, without allocating a list.
    /// This preserves the prior <c>GetRunningTasks(1).FirstOrDefault()</c> semantics:
    /// <list type="bullet">
    /// <item><description><see langword="null"/> when there are no running tasks.</description></item>
    /// <item><description>The single running task when there is exactly one.</description></item>
    /// <item><description>A reusable summary detail with text like "N tests running" when there are multiple.</description></item>
    /// </list>
    /// </summary>
    public TestDetailState? GetSingleActiveOrSummaryTask()
    {
        TestDetailState? first = null;
        int count = 0;
        foreach (KeyValuePair<string, TestDetailState> kvp in _testNodeProgressStates)
        {
            if (count == 0)
            {
                first = kvp.Value;
            }

            count++;
        }

        if (count == 0)
        {
            return null;
        }

        if (count == 1)
        {
            return first;
        }

        _summaryDetail.Text = GetFullTestsCountText(count);
        return _summaryDetail;
    }

    /// <summary>
    /// Returns a snapshot of currently running tasks, sorted by elapsed time descending and
    /// truncated to <paramref name="maxCount"/> entries (the last entry becomes a "... N more
    /// running" summary detail when truncation occurs).
    /// </summary>
    /// <remarks>
    /// The returned <see cref="List{T}"/> is a cached buffer reused across calls on the same
    /// <see cref="TestNodeResultsState"/> instance to avoid per-render-tick allocation.
    /// Callers MUST NOT call <see cref="GetRunningTasks"/> on the same instance again before
    /// finishing use of the previously-returned buffer — the next call on that instance will
    /// <see cref="List{T}.Clear"/> and rebuild it in place, silently invalidating the prior
    /// caller's view. Buffers from different instances are independent and may be held
    /// concurrently. This type is not designed for concurrent calls on the same instance;
    /// the production caller (<c>AnsiTerminalTestProgressFrame</c>) only invokes this from
    /// the single-threaded render loop.
    /// </remarks>
    public List<TestDetailState> GetRunningTasks(int maxCount)
    {
        // Reuse the cached buffer to avoid allocating a new List on every render tick.
        _runningTasksBuffer.Clear();

        // Pre-size the buffer to the current snapshot size so the first calls (and any
        // call that grows past the previous high-water mark) don't trigger multiple
        // internal array reallocations as items are added. Capacity only grows.
        int snapshotCount = _testNodeProgressStates.Count;
        if (_runningTasksBuffer.Capacity < snapshotCount)
        {
            _runningTasksBuffer.Capacity = snapshotCount;
        }

        foreach (KeyValuePair<string, TestDetailState> kvp in _testNodeProgressStates)
        {
            _runningTasksBuffer.Add(kvp.Value);
        }

        // Sort descending by elapsed time without LINQ overhead.
        _runningTasksBuffer.Sort(static (a, b) => (b.Stopwatch?.Elapsed ?? TimeSpan.Zero).CompareTo(a.Stopwatch?.Elapsed ?? TimeSpan.Zero));

        bool tooManyItems = _runningTasksBuffer.Count > maxCount;

        if (tooManyItems)
        {
            // Note: If there's too many items to display, the summary will take up one line.
            // As such, we can only take maxCount - 1 items.
            int itemsToTake = maxCount - 1;
            _summaryDetail.Text =
                itemsToTake == 0
                    // Note: If itemsToTake is 0, then we only show two lines, the project summary and the number of running tests.
                    ? GetFullTestsCountText(_runningTasksBuffer.Count)
                    // If itemsToTake is larger, then we show the project summary, active tests, and the number of active tests that are not shown.
                    : GetMoreTestsCountText(_runningTasksBuffer.Count - itemsToTake);

            // Truncate in-place to avoid allocating a second list/array.
            if (itemsToTake < _runningTasksBuffer.Count)
            {
                _runningTasksBuffer.RemoveRange(itemsToTake, _runningTasksBuffer.Count - itemsToTake);
            }

            _runningTasksBuffer.Add(_summaryDetail);
        }

        return _runningTasksBuffer;
    }

    /// <summary>
    /// Returns the "N tests running" text for <paramref name="count"/>, reusing the previously
    /// formatted string when the count and effective cultures are unchanged since the last call.
    /// </summary>
    private string GetFullTestsCountText(int count)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        if (_cachedFullTestsCount != count
            || !ReferenceEquals(_cachedFullTestsCulture, culture)
            || !ReferenceEquals(_cachedFullTestsUICulture, uiCulture))
        {
            _cachedFullTestsText = string.Format(culture, TerminalResources.ActiveTestsRunning_FullTestsCount, count);
            _cachedFullTestsCount = count;
            _cachedFullTestsCulture = culture;
            _cachedFullTestsUICulture = uiCulture;
        }

        return _cachedFullTestsText;
    }

    /// <summary>
    /// Returns the "... N more running" text for <paramref name="count"/>, reusing the previously
    /// formatted string when the count and effective cultures are unchanged since the last call.
    /// </summary>
    private string GetMoreTestsCountText(int count)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        if (_cachedMoreTestsCount != count
            || !ReferenceEquals(_cachedMoreTestsCulture, culture)
            || !ReferenceEquals(_cachedMoreTestsUICulture, uiCulture))
        {
            _cachedMoreTestsText = $"... {string.Format(culture, TerminalResources.ActiveTestsRunning_MoreTestsCount, count)}";
            _cachedMoreTestsCount = count;
            _cachedMoreTestsCulture = culture;
            _cachedMoreTestsUICulture = uiCulture;
        }

        return _cachedMoreTestsText;
    }
}
