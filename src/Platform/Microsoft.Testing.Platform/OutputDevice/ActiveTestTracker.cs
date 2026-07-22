// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class ActiveTestTracker
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly TimeSpan _slowTestThreshold;
    private readonly Func<IStopwatch> _createStopwatch;
    private readonly Dictionary<TestNodeUid, ActiveTest> _activeTests = [];

    internal ActiveTestTracker(TimeSpan slowTestThreshold, Func<IStopwatch> createStopwatch)
    {
        _slowTestThreshold = slowTestThreshold;
        _createStopwatch = createStopwatch;
    }

    internal bool IsEnabled => _slowTestThreshold > TimeSpan.Zero;

    internal void Start(TestNodeUid uid, string displayName)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_lock)
        {
            if (!_activeTests.ContainsKey(uid))
            {
                _activeTests.Add(uid, new(displayName, _createStopwatch(), new(_slowTestThreshold)));
            }
        }
    }

    internal void Complete(TestNodeUid uid)
    {
        lock (_lock)
        {
            _activeTests.Remove(uid);
        }
    }

    internal SlowTestDiagnostic[] GetDueDiagnostics()
    {
        lock (_lock)
        {
            if (_activeTests.Count == 0)
            {
                return [];
            }

            var diagnostics = new List<SlowTestDiagnostic>();
            foreach ((TestNodeUid uid, ActiveTest activeTest) in _activeTests.OrderBy(static pair => pair.Key.Value, StringComparer.Ordinal))
            {
                TimeSpan elapsed = activeTest.Stopwatch.Elapsed;
                if (!activeTest.SlowTestThreshold.IsDue(elapsed))
                {
                    continue;
                }

                diagnostics.Add(new(uid, activeTest.DisplayName, elapsed));
            }

            return [.. diagnostics];
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _activeTests.Clear();
        }
    }

    private sealed class ActiveTest(string displayName, IStopwatch stopwatch, SlowTestThresholdState slowTestThreshold)
    {
        public string DisplayName { get; } = displayName;

        public IStopwatch Stopwatch { get; } = stopwatch;

        public SlowTestThresholdState SlowTestThreshold { get; } = slowTestThreshold;
    }
}

internal sealed class SlowTestDiagnostic(TestNodeUid uid, string displayName, TimeSpan elapsed)
{
    internal TestNodeUid Uid { get; } = uid;

    internal string DisplayName { get; } = displayName;

    internal TimeSpan Elapsed { get; } = elapsed;
}
