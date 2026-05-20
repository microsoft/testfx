// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalTestReporter
{
    public void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery)
    {
        _isDiscovery = isDiscovery;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    public void AssemblyRunStarted()
        => GetOrAddAssemblyRun();

    private TestProgressState GetOrAddAssemblyRun()
    {
        if (_testProgressState is not null)
        {
            return _testProgressState;
        }

        lock (_lock)
        {
            if (_testProgressState is not null)
            {
                return _testProgressState;
            }

            IStopwatch sw = CreateStopwatch();
            var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), _assembly, _targetFramework, _architecture, sw, _isDiscovery);
            int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
            assemblyRun.SlotIndex = slotIndex;
            _testProgressState = assemblyRun;
            return assemblyRun;
        }
    }

    internal void AssemblyRunCompleted()
    {
        TestProgressState assemblyRun = GetOrAddAssemblyRun();
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);
    }

    public void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        // Consume the saved console mode so a later Dispose does not try to restore again.
        _originalConsoleMode = null;

        // This is relevant for HotReload scenarios. We want the next test sessions to start
        // on a new TestProgressState
        _testProgressState = null;

        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }
}
