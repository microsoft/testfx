// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    private bool _isHelp;

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery, bool isHelp, bool isRetry)
    {
        _isDiscovery = isDiscovery;
        _isHelp = isHelp;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture, string executionId, string instanceId)
        // instanceId: reserved for the SDK orchestrator's instanceId-based retry-counting logic (follow-up PR).
        // It is not used by the in-process host path, which registers a single assembly per run.
        => GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture, string executionId)
    {
        // NOTE: we intentionally do not use ConcurrentDictionary.GetOrAdd with a value factory here. GetOrAdd may
        // invoke the factory more than once under contention and discard all but one result; that would allocate a
        // worker slot (and bump _counter) for every losing race, leaking a fixed-size progress slot that is never
        // RemoveWorker'd. Double-checked locking around AddWorker guarantees exactly one worker per executionId.
        if (_assemblies.TryGetValue(executionId, out TestProgressState? result))
        {
            return result;
        }

        lock (_lock)
        {
            if (_assemblies.TryGetValue(executionId, out result))
            {
                return result;
            }

            IStopwatch sw = CreateStopwatch();
            result = new TestProgressState(Interlocked.Increment(ref _counter), assembly, targetFramework, architecture, sw, _isDiscovery);
            int slotIndex = _terminalWithProgress.AddWorker(result);
            result.SlotIndex = slotIndex;
            _assemblies[executionId] = result;
            return result;
        }
    }

    internal void AssemblyRunCompleted(string executionId)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? assemblyRun))
        {
            return;
        }

        assemblyRun.Stopwatch.Stop();
        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);
    }

    public void TestExecutionCompleted(DateTimeOffset endTime, int? exitCode)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        if (!_isHelp)
        {
            _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);
        }

        // This is relevant for HotReload scenarios. We want the next test sessions to start fresh.
        _assemblies.Clear();

        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }
}
