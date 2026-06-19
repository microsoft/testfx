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

    /// <summary>
    /// Orchestrator overload: completes an assembly run with the child process exit code and captured output. When
    /// the <paramref name="executionId"/> was never registered (the process exited before a usable handshake), the
    /// completion is surfaced as a <see cref="HandshakeFailure(string, string?, int, string, string, bool)"/> rather
    /// than throwing. On a non-zero exit code the executable summary (exit code + stdout/stderr) is printed.
    /// </summary>
    internal void AssemblyRunCompleted(string executionId, int exitCode, string? outputData, string? errorData)
    {
        if (!_assemblies.TryGetValue(executionId, out TestProgressState? assemblyRun))
        {
            HandshakeFailure(assemblyPath: string.Empty, targetFramework: null, exitCode, outputData ?? string.Empty, errorData ?? string.Empty);
            return;
        }

        assemblyRun.Success = exitCode == 0 && assemblyRun.FailedTests == 0;
        assemblyRun.Stopwatch.Stop();
        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (!_isHelp && !_isDiscovery && _options.ShowAssembly && _options.ShowAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }

        if (exitCode == 0)
        {
            // Report nothing on success; we don't want to report on test-discovery etc.
            return;
        }

        _terminalWithProgress.WriteToTerminal(terminal => AppendExecutableSummary(terminal, exitCode, outputData, errorData));
    }

    public void TestExecutionCompleted(DateTimeOffset endTime, int? exitCode)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        if (!_isHelp)
        {
            _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);
        }

        // This is relevant for HotReload scenarios. We want the next test sessions to start fresh, so we reset all
        // per-run state here (after the summary above has consumed it): the per-assembly runs, the collected
        // artifacts, the handshake failures, and the cancellation flag. Otherwise a later session would re-print the
        // previous session's artifacts/handshake failures or stay stuck in the aborted state.
        _assemblies.Clear();
        _artifacts.Clear();
        WasCancelled = false;
        lock (_handshakeFailuresLock)
        {
            _handshakeFailures.Clear();
        }

        _handshakeFailuresCount = 0;

        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }
}
