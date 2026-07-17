// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class SilenceDrivenHeartbeatRendererTests
{
    private static readonly TimeSpan Silence30s = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Slow60s = TimeSpan.FromSeconds(60);

    [TestMethod]
    public void SilenceHeartbeat_WhenNoCompletionForThreshold_EmitsSingleSummaryLine()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(Silence30s, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        TestProgressState asm = CreateAssembly(clock, totalTests: 5, failedTests: 2, activeTestName: "MyTest");
        TestProgressState?[] items = [asm];

        // Below threshold: nothing.
        clock.Advance(TimeSpan.FromSeconds(29));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(string.Empty, terminal.Output);

        // At/over threshold: single heartbeat line.
        clock.Advance(TimeSpan.FromSeconds(2));
        renderer.OnTick(terminal, items);

        Assert.Contains("running...", terminal.Output);
        Assert.Contains("5 completed", terminal.Output);
        Assert.Contains("2 failed", terminal.Output);
        Assert.Contains("active: MyTest", terminal.Output);
        Assert.AreEqual(1, terminal.LineCount);
    }

    [TestMethod]
    public void SilenceHeartbeat_DuringProlongedSilence_RepeatsOncePerInterval()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(Silence30s, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        TestProgressState?[] items = [CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null)];

        clock.Advance(TimeSpan.FromSeconds(31));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(1, terminal.LineCount);

        // Not yet another full interval since the previous heartbeat.
        clock.Advance(TimeSpan.FromSeconds(20));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(1, terminal.LineCount);

        // Another full interval elapsed -> second heartbeat.
        clock.Advance(TimeSpan.FromSeconds(15));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(2, terminal.LineCount);
    }

    [TestMethod]
    public void SilenceHeartbeat_WhenCompletionsKeepHappening_DoesNotEmit()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(Silence30s, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        TestProgressState?[] items = [CreateAssembly(clock, totalTests: 1, failedTests: 0, activeTestName: null)];

        clock.Advance(TimeSpan.FromSeconds(20));
        renderer.OnTestCompleted();
        clock.Advance(TimeSpan.FromSeconds(20));
        renderer.OnTick(terminal, items);

        // Only 20s since the last completion, below the 30s silence threshold.
        Assert.AreEqual(string.Empty, terminal.Output);
    }

    [TestMethod]
    public void SilenceHeartbeat_WhenThresholdIsZero_NeverEmits()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.Zero, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        TestProgressState?[] items = [CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null)];

        clock.Advance(TimeSpan.FromMinutes(10));
        renderer.OnTick(terminal, items);

        Assert.AreEqual(string.Empty, terminal.Output);
    }

    [TestMethod]
    public void SlowTest_WhenExceedingThreshold_EmitsWithExponentialBackoff()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        // Disable the silence heartbeat to isolate slow-test behavior.
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.Zero, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        var results = new TestNodeResultsState(1);
        results.AddRunningTestNode(id: 10, uid: "uid-1", name: "SlowTest.IntegrationFoo", clock.CreateStopwatch());
        TestProgressState asm = CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null);
        asm.TestNodeResultsState = results;
        TestProgressState?[] items = [asm];

        // Below threshold: nothing.
        clock.Advance(TimeSpan.FromSeconds(59));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(0, terminal.LineCount);

        // Crosses 60s -> first slow line.
        clock.Advance(TimeSpan.FromSeconds(1));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(1, terminal.LineCount);
        Assert.Contains("[slow]", terminal.Output);
        Assert.Contains("SlowTest.IntegrationFoo", terminal.Output);
        Assert.Contains(HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(60), wrapInParentheses: false)!, terminal.Output);

        // Between 60s and 120s: no new line (backoff).
        clock.Advance(TimeSpan.FromSeconds(59));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(1, terminal.LineCount);

        // Crosses 120s -> second slow line.
        clock.Advance(TimeSpan.FromSeconds(1));
        renderer.OnTick(terminal, items);
        Assert.AreEqual(2, terminal.LineCount);
        Assert.Contains(HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(120), wrapInParentheses: false)!, terminal.Output);
    }

    [TestMethod]
    public void SlowTest_WhenTickIsDelayedPastThreshold_ReportsActualElapsedNotThreshold()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        // Disable the silence heartbeat to isolate slow-test behavior.
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.Zero, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        var results = new TestNodeResultsState(1);
        results.AddRunningTestNode(id: 10, uid: "uid-1", name: "SlowTest.IntegrationFoo", clock.CreateStopwatch());
        TestProgressState asm = CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null);
        asm.TestNodeResultsState = results;
        TestProgressState?[] items = [asm];

        // The first tick is delayed (e.g. GC pause / CPU starvation): the test has actually been running
        // 95s by the time we render, even though the crossed threshold was 60s.
        clock.Advance(TimeSpan.FromSeconds(95));
        renderer.OnTick(terminal, items);

        Assert.AreEqual(1, terminal.LineCount);
        Assert.Contains("[slow]", terminal.Output);
        // The emitted duration reflects the real elapsed time, not the 60s scheduled threshold.
        Assert.Contains(HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(95), wrapInParentheses: false)!, terminal.Output);
        Assert.DoesNotContain(HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(60), wrapInParentheses: false)!, terminal.Output);
    }

    [TestMethod]
    public void SlowTest_WhenBelowThreshold_EmitsNothing()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(TimeSpan.Zero, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        var results = new TestNodeResultsState(1);
        results.AddRunningTestNode(id: 10, uid: "uid-1", name: "FastTest", clock.CreateStopwatch());
        TestProgressState asm = CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null);
        asm.TestNodeResultsState = results;
        TestProgressState?[] items = [asm];

        clock.Advance(TimeSpan.FromSeconds(30));
        renderer.OnTick(terminal, items);

        Assert.AreEqual(0, terminal.LineCount);
    }

    [TestMethod]
    public void SlowTest_WhenThresholdIsZero_NeverEmits()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(Silence30s, TimeSpan.Zero, clock.CreateStopwatch);
        renderer.OnStart();

        var results = new TestNodeResultsState(1);
        results.AddRunningTestNode(id: 10, uid: "uid-1", name: "SlowTest", clock.CreateStopwatch());
        TestProgressState asm = CreateAssembly(clock, totalTests: 0, failedTests: 0, activeTestName: null);
        asm.TestNodeResultsState = results;
        TestProgressState?[] items = [asm];

        clock.Advance(TimeSpan.FromMinutes(10));
        renderer.OnTick(terminal, items);

        Assert.DoesNotContain("[slow]", terminal.Output);
    }

    [TestMethod]
    public void OnWrite_DoesNotEraseOrRenderProgress_JustWrites()
    {
        var clock = new FakeClock();
        var terminal = new RecordingStringTerminal();
        var renderer = new SilenceDrivenHeartbeatRenderer(Silence30s, Slow60s, clock.CreateStopwatch);
        renderer.OnStart();

        renderer.OnWrite(terminal, [], t => t.AppendLine("user-line"));

        Assert.AreEqual("user-line" + Environment.NewLine, terminal.Output);
        Assert.IsFalse(terminal.EraseProgressCalled);
        Assert.IsFalse(terminal.RenderProgressCalled);
    }

    private static TestProgressState CreateAssembly(FakeClock clock, int totalTests, int failedTests, string? activeTestName)
    {
        var asm = new TestProgressState(1, "MyAcceptance.dll", "net9.0", "x64", clock.CreateStopwatch(), isDiscovery: false);

        // Counts are now derived from reported per-test results (to support retry de-duplication), so seed them by
        // reporting the requested number of failed + passed tests under a single attempt.
        asm.NotifyHandshake("inst-1");
        for (int i = 0; i < failedTests; i++)
        {
            asm.ReportFailedTest($"fail-{i}", "inst-1");
        }

        for (int i = 0; i < totalTests - failedTests; i++)
        {
            asm.ReportPassingTest($"pass-{i}", "inst-1");
        }

        if (activeTestName is not null)
        {
            var results = new TestNodeResultsState(1);
            results.AddRunningTestNode(id: 1, uid: "active-uid", name: activeTestName, clock.CreateStopwatch());
            asm.TestNodeResultsState = results;
        }

        return asm;
    }

    private sealed class FakeClock
    {
        private TimeSpan _now = TimeSpan.Zero;

        public void Advance(TimeSpan delta) => _now += delta;

        public IStopwatch CreateStopwatch() => new FakeStopwatch(this, _now);

        private sealed class FakeStopwatch(FakeClock clock, TimeSpan start) : IStopwatch
        {
            public TimeSpan Elapsed => clock._now - start;

            public void Start()
            {
            }

            public void Stop()
            {
            }
        }
    }

    private sealed class RecordingStringTerminal : ITerminal
    {
        private readonly StringBuilder _output = new();

        public string Output => _output.ToString();

        public int LineCount { get; private set; }

        public bool EraseProgressCalled { get; private set; }

        public bool RenderProgressCalled { get; private set; }

        public int Width => int.MaxValue;

        public int Height => int.MaxValue;

        public void Append(char value) => _output.Append(value);

        public void Append(string value) => _output.Append(value);

        public void AppendLine() => _output.AppendLine();

        public void AppendLine(string value)
        {
            _output.AppendLine(value);
            LineCount++;
        }

        public void AppendLink(string path, int? lineNumber) => _output.Append(path);

        public void EraseProgress() => EraseProgressCalled = true;

        public void RenderProgress(TestProgressState?[] progress, TerminalProgressMessageState[] messages) => RenderProgressCalled = true;

        public void HideCursor()
        {
        }

        public void ShowCursor()
        {
        }

        public void StartUpdate()
        {
        }

        public void StopUpdate()
        {
        }

        public void StartBusyIndicator()
        {
        }

        public void StopBusyIndicator()
        {
        }

        public void SetColor(TerminalColor color)
        {
        }

        public void ResetColor()
        {
        }
    }
}
