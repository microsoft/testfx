// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsSlowTestReporterTests
{
    private static readonly DateTimeOffset Start = new(2025, 05, 16, 12, 00, 00, TimeSpan.Zero);

    [TestMethod]
    public async Task OnTestSessionStarting_WhenNotInAzureDevOps_NoOpsAndIgnoresUpdatesAsync()
    {
        CapturingOutputDevice outputDevice = new();
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: false);

        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);

        // No output device emission and nothing tracked when running outside Azure DevOps.
        Assert.IsEmpty(outputDevice.Lines);
    }

    [TestMethod]
    public async Task ScanOnce_AfterThreshold_EmitsSingleSlowTestLineAsync()
    {
        CapturingOutputDevice outputDevice = new();
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        // Default static threshold is 60s; nothing emitted before it.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(90), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("[slow] still running after", outputDevice.Lines[0]);
        Assert.Contains("Ns.T1", outputDevice.Lines[0]);

        // No history for this test, so no decoration suffix.
        Assert.DoesNotContain("historical", outputDevice.Lines[0]);
    }

    [TestMethod]
    public async Task ScanOnce_WithHistory_LowersThresholdAndDecoratesLineAsync()
    {
        CapturingOutputDevice outputDevice = new();
        FakeHistoryService history = new();
        history.Add("Ns.Fast", new DurationHistoryStats(2000, 3000, sampleCount: 120));
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: true, history: history);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.Fast", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        // p99 = 3s, multiplier 3 => 9s threshold (below the 60s static default).
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(8), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("(historical p95 = 2s, p99 = 3s, samples = 120)", outputDevice.Lines[0]);
    }

    [TestMethod]
    public async Task ScanOnce_UsesExponentialBackoffAsync()
    {
        CapturingOutputDevice outputDevice = new();
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        // First emit at >= 60s; threshold then doubles to 120s.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(90), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);

        // Still below 120s -> no new emission.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(110), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);

        // Past 120s -> second emission.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(130), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(2, outputDevice.Lines);
    }

    [TestMethod]
    public async Task ConsumeAsync_TerminalState_StopsTrackingAsync()
    {
        CapturingOutputDevice outputDevice = new();
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);
    }

    [TestMethod]
    public async Task OnTestSessionFinishing_DrainsLoopAndClearsTrackingAsync()
    {
        CapturingOutputDevice outputDevice = new();
        AzureDevOpsSlowTestReporter reporter = CreateReporter(outputDevice, tfBuild: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await reporter.OnTestSessionFinishingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        // After finishing, the reporter is inactive: further updates are ignored and nothing is tracked.
        await reporter.ConsumeAsync(null!, CreateMessage("u2", "Ns.T2", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);
    }

    private static AzureDevOpsSlowTestReporter CreateReporter(CapturingOutputDevice outputDevice, bool tfBuild, FakeHistoryService? history = null)
    {
        Dictionary<string, string[]> options = new(StringComparer.OrdinalIgnoreCase)
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsSlowTestHistory] = ["30"],
        };

        Mock<IEnvironment> environmentMock = new();
        environmentMock.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns(tfBuild ? "true" : null);

        return new AzureDevOpsSlowTestReporter(
            new FakeCommandLineOptions(options),
            environmentMock.Object,
            outputDevice,
            new NonRunningTask(),
            new FixedClock(Start),
            new StubLoggerFactory(),
            history ?? new FakeHistoryService());
    }

    private static TestNodeUpdateMessage CreateMessage(string uid, string fullyQualifiedName, TestNodeStateProperty state)
    {
        PropertyBag propertyBag = new();
        propertyBag.Add(state);
        propertyBag.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", fullyQualifiedName));

        return new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = uid,
            DisplayName = fullyQualifiedName,
            Properties = propertyBag,
        });
    }

    private sealed class FakeHistoryService : IAzureDevOpsHistoryService
    {
        private readonly Dictionary<string, DurationHistoryStats> _durationStats = [];

        public int HistoryWindowInDays => 30;

        public void Add(string testName, DurationHistoryStats stats)
            => _durationStats[testName] = stats;

        public bool TryGetStats(string testName, out FlakyStats stats)
        {
            stats = default;
            return false;
        }

        public bool IsLikelyFlaky(string testName, double threshold)
            => false;

        public bool TryGetDurationStats(string testName, out DurationHistoryStats stats)
            => _durationStats.TryGetValue(testName, out stats);
    }

    private sealed class FakeCommandLineOptions(IReadOnlyDictionary<string, string[]> options) : ICommandLineOptions
    {
        private readonly IReadOnlyDictionary<string, string[]> _options = options;

        public bool IsOptionSet(string optionName)
            => _options.ContainsKey(optionName);

        public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments)
        {
            if (_options.TryGetValue(optionName, out string[]? values))
            {
                arguments = values;
                return true;
            }

            arguments = null;
            return false;
        }
    }

    private sealed class CapturingOutputDevice : IOutputDevice
    {
        public List<string> Lines { get; } = [];

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            Lines.Add(((TextOutputDeviceData)data).Text);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    // Does not actually run the background scan loop, so OnTestSessionStartingAsync returns immediately
    // and tests can drive ScanOnceAsync deterministically.
    private sealed class NonRunningTask : ITask
    {
        public Task Delay(int millisecondDelay)
            => Task.CompletedTask;

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task Run(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task Run(Func<Task> function, CancellationToken cancellationToken)
            => function();

        public Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken)
            => function()!;

        public Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task WhenAll(params Task[] tasks)
            => Task.WhenAll(tasks);
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class StubLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName)
            => new NullLogger();

        private sealed class NullLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel)
                => false;

            public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }

            public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Task.CompletedTask;
        }
    }
}
