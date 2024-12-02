// Copyright (c) Microsoft Corporation. All rights reserved.
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

    public void AddRunningTestNode(int id, string uid, string name, IStopwatch stopwatch)
    {
        _testNodeProgressStates[uid] = new TestDetailState(id, stopwatch, name);
        UpdateSummaryDetail();
    }

    public void RemoveRunningTestNode(string uid)
    {
        _testNodeProgressStates.TryRemove(uid, out _);
        UpdateSummaryDetail();
    }

    private void UpdateSummaryDetail()
        => _summaryDetail.Text = _testNodeProgressStates.Count > 5
            ? $"... {string.Format(CultureInfo.CurrentCulture, PlatformResources.MoreTestsRunning, _testNodeProgressStates.Count)}"
            : string.Empty;

    // Note: Show up to 5 long running tests per project.
    public IEnumerable<TestDetailState> GetRunningTasks()
    {
        IEnumerable<TestDetailState> sortedDetails = _testNodeProgressStates
            .Select(d => d.Value)
            .OrderBy(d => d.Stopwatch?.Elapsed ?? TimeSpan.Zero)
            .Take(5);

        foreach (TestDetailState? detail in sortedDetails)
        {
            yield return detail;
        }

        if (!RoslynString.IsNullOrEmpty(_summaryDetail.Text))
        {
            yield return _summaryDetail;
        }
    }
}
