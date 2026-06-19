// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    private bool _isHelp;
    private bool _isRetry;

    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery, bool isHelp, bool isRetry)
    {
        _isDiscovery = isDiscovery;
        _isHelp = isHelp;
        _isRetry = isRetry;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture, string executionId, string instanceId)
        => GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture, string executionId)
        => _assemblies.GetOrAdd(executionId, _ =>
        {
            lock (_lock)
            {
                IStopwatch sw = CreateStopwatch();
                var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), assembly, targetFramework, architecture, sw, _isDiscovery);
                int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
                assemblyRun.SlotIndex = slotIndex;
                return assemblyRun;
            }
        });

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
