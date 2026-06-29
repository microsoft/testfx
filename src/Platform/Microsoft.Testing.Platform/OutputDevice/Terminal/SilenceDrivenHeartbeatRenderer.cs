// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Renderer for non-cursor terminals (<see cref="AnsiMode.SimpleAnsi"/> and <see cref="AnsiMode.NoAnsi"/>,
/// i.e. CI / piped / file-redirected output). Instead of redrawing a progress block in place, it emits
/// single, durable scrollback lines and only when there is a reason to:
/// <list type="number">
/// <item><description>E1 silence heartbeat — one summary line when no test completed for the silence threshold.</description></item>
/// <item><description>E2 slow-test surfacing — one line per test that exceeds the slow-test threshold, with exponential backoff.</description></item>
/// </list>
/// A healthy fast suite emits zero heartbeat lines.
/// </summary>
[UnsupportedOSPlatform("browser")]
[Embedded]
internal sealed class SilenceDrivenHeartbeatRenderer : IProgressRenderer
{
    private readonly TimeSpan _silenceThreshold;
    private readonly TimeSpan _slowTestThreshold;
    private readonly Func<IStopwatch> _createStopwatch;

    // Per running-test backoff state: test detail id -> next elapsed threshold (in ticks) at which to emit.
    // Only touched from the single rendering thread inside OnTick.
    private readonly Dictionary<long, long> _slowTestNextThresholdTicks = [];

    private IStopwatch? _clock;

    // Ticks (on _clock's timeline) of the last activity (test completion) or last heartbeat emission.
    // Read/written from multiple threads, so guarded with Interlocked.
    private long _lastActivityTicks;

    public SilenceDrivenHeartbeatRenderer(TimeSpan silenceThreshold, TimeSpan slowTestThreshold, Func<IStopwatch> createStopwatch)
    {
        _silenceThreshold = silenceThreshold;
        _slowTestThreshold = slowTestThreshold;
        _createStopwatch = createStopwatch;
    }

    // The heartbeat rules only need second-level granularity, so we tick once per second instead of the
    // sub-second cadence the cursor renderer uses, avoiding an unnecessary running-test scan/sort twice
    // per second in CI / redirected runs.
    public TimeSpan TickInterval => TimeSpan.FromSeconds(1);

    // _clock is published once in OnStart() and then read from other threads (OnTick on the refresher
    // thread, OnTestCompleted from the message pump), so use Volatile to make the publication explicit
    // and avoid reading a stale null reference (which would make NowTicks return 0).
    private long NowTicks => Volatile.Read(ref _clock)?.Elapsed.Ticks ?? 0;

    public void OnStart()
    {
        _slowTestNextThresholdTicks.Clear();
        IStopwatch clock = _createStopwatch();
        Volatile.Write(ref _clock, clock);
        Interlocked.Exchange(ref _lastActivityTicks, clock.Elapsed.Ticks);
    }

    public void OnTestCompleted()
        => Interlocked.Exchange(ref _lastActivityTicks, NowTicks);

    public void OnWrite(ITerminal terminal, TestProgressState?[] progressItems, Action<ITerminal> write)
        // Heartbeat lines are durable scrollback; a user write does not need to erase or re-render any progress.
        => write(terminal);

    public void OnTick(ITerminal terminal, TestProgressState?[] progressItems)
    {
        EmitSilenceHeartbeat(terminal, progressItems);
        EmitSlowTests(terminal, progressItems);
    }

    private void EmitSilenceHeartbeat(ITerminal terminal, TestProgressState?[] progressItems)
    {
        if (_silenceThreshold <= TimeSpan.Zero)
        {
            return;
        }

        long now = NowTicks;
        long last = Interlocked.Read(ref _lastActivityTicks);
        if (now - last < _silenceThreshold.Ticks)
        {
            return;
        }

        // Reset the baseline so that, during a prolonged silence, the heartbeat repeats once per threshold interval.
        Interlocked.Exchange(ref _lastActivityTicks, now);

        int completed = 0;
        int failed = 0;
        string? activeTest = null;
        foreach (TestProgressState? item in progressItems)
        {
            if (item is null)
            {
                continue;
            }

            completed += item.TotalTests;
            failed += item.FailedTests;

            if (activeTest is null)
            {
                TestDetailState? active = item.TestNodeResultsState?.GetSingleActiveOrSummaryTask();
                if (!RoslynString.IsNullOrWhiteSpace(active?.Text))
                {
                    activeTest = active.Text;
                }
            }
        }

        string line = string.Format(CultureInfo.CurrentCulture, TerminalResources.TerminalProgressHeartbeat, completed, failed);
        if (activeTest is not null)
        {
            line += string.Format(CultureInfo.CurrentCulture, TerminalResources.TerminalProgressHeartbeatActiveSuffix, activeTest);
        }

        terminal.AppendLine(line);
    }

    private void EmitSlowTests(ITerminal terminal, TestProgressState?[] progressItems)
    {
        if (_slowTestThreshold <= TimeSpan.Zero)
        {
            return;
        }

        long slowTicks = _slowTestThreshold.Ticks;

        foreach (TestProgressState? item in progressItems)
        {
            TestNodeResultsState? results = item?.TestNodeResultsState;
            if (item is null || results is null)
            {
                continue;
            }

            // int.MaxValue avoids the truncation/summary path so we evaluate every running test.
            foreach (TestDetailState detail in results.GetRunningTasks(int.MaxValue))
            {
                if (detail.Stopwatch is null)
                {
                    continue;
                }

                long elapsed = detail.Stopwatch.Elapsed.Ticks;
                long next = _slowTestNextThresholdTicks.TryGetValue(detail.Id, out long stored) ? stored : slowTicks;
                if (elapsed < next)
                {
                    continue;
                }

                // Exponential backoff: next emission at twice the crossed threshold (60s -> 2m -> 4m -> 8m ...).
                _slowTestNextThresholdTicks[detail.Id] = next * 2;

                // Report the test's actual elapsed time rather than the scheduled threshold so a delayed
                // tick (GC pause, debugger break, CPU starvation) does not under-report the runtime.
                string duration = HumanReadableDurationFormatter.Render(TimeSpan.FromTicks(elapsed), wrapInParentheses: false);
                terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, TerminalResources.TerminalProgressSlowTest, duration, BuildSlowTestDescription(item, detail)));
            }
        }
    }

    private static string BuildSlowTestDescription(TestProgressState item, TestDetailState detail)
    {
        var builder = new StringBuilder();
        builder.Append(detail.Text);
        builder.Append(" (");
        builder.Append(item.AssemblyName);
        if (item.TargetFramework is not null)
        {
            builder.Append('|');
            builder.Append(item.TargetFramework);
        }

        if (item.Architecture is not null)
        {
            builder.Append('|');
            builder.Append(item.Architecture);
        }

        builder.Append(')');
        return builder.ToString();
    }
}
