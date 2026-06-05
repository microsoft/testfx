// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

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

    public int Count => _testNodeProgressStates.Count;

    public void AddRunningTestNode(int id, string uid, string name, IStopwatch stopwatch) => _testNodeProgressStates[uid] = new TestDetailState(id, stopwatch, name);

    public void RemoveRunningTestNode(string uid) => _testNodeProgressStates.TryRemove(uid, out _);

    public List<TestDetailState> GetRunningTasks(int maxCount)
    {
        // Build the list directly from the dictionary to avoid LINQ iterator chain allocations.
        var sortedDetails = new List<TestDetailState>(_testNodeProgressStates.Count);
        foreach (KeyValuePair<string, TestDetailState> kvp in _testNodeProgressStates)
        {
            sortedDetails.Add(kvp.Value);
        }

        // Sort descending by elapsed time without LINQ overhead.
        sortedDetails.Sort(static (a, b) => (b.Stopwatch?.Elapsed ?? TimeSpan.Zero).CompareTo(a.Stopwatch?.Elapsed ?? TimeSpan.Zero));

        bool tooManyItems = sortedDetails.Count > maxCount;

        if (tooManyItems)
        {
            // Note: If there's too many items to display, the summary will take up one line.
            // As such, we can only take maxCount - 1 items.
            int itemsToTake = maxCount - 1;
            _summaryDetail.Text =
                itemsToTake == 0
                    // Note: If itemsToTake is 0, then we only show two lines, the project summary and the number of running tests.
                    ? string.Format(CultureInfo.CurrentCulture, PlatformResources.ActiveTestsRunning_FullTestsCount, sortedDetails.Count)
                    // If itemsToTake is larger, then we show the project summary, active tests, and the number of active tests that are not shown.
                    : $"... {string.Format(CultureInfo.CurrentCulture, PlatformResources.ActiveTestsRunning_MoreTestsCount, sortedDetails.Count - itemsToTake)}";

            // Truncate in-place to avoid allocating a second list/array.
            if (itemsToTake < sortedDetails.Count)
            {
                sortedDetails.RemoveRange(itemsToTake, sortedDetails.Count - itemsToTake);
            }

            sortedDetails.Add(_summaryDetail);
        }

        return sortedDetails;
    }
}
