﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;

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

    public TestDetailState? GetFirstRunningTask() => _testNodeProgressStates.FirstOrDefault().Value;

    public IEnumerable<TestDetailState> GetRunningTasks(int maxCount)
    {
        var sortedDetails = _testNodeProgressStates
            .Select(d => d.Value)
            .OrderBy(d => d.Stopwatch?.Elapsed ?? TimeSpan.Zero)
            .ToList();

        bool tooManyItems = sortedDetails.Count > maxCount;

        if (tooManyItems)
        {
            // Note: If there's too many items to display, the summary will take up one line.
            // As such, we can only take maxCount - 1 items.
            int itemsToTake = maxCount - 1;
            _summaryDetail.Text =
                $"... {string.Format(CultureInfo.CurrentCulture, PlatformResources.MoreTestsRunning, sortedDetails.Count - itemsToTake)}";
            sortedDetails = sortedDetails.Take(itemsToTake).ToList();
        }

        foreach (TestDetailState? detail in sortedDetails)
        {
            yield return detail;
        }

        if (tooManyItems)
        {
            yield return _summaryDetail;
        }
    }
}
