// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using ghactions::Microsoft.Testing.Extensions.GitHubActionsReport;

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
public sealed class GitHubActionsSlowTestReporterTests
{
    private static readonly DateTimeOffset Start = new(2025, 05, 16, 12, 00, 00, TimeSpan.Zero);

    [TestMethod]
    public void BuildNoticeLine_FormatsNoticeWithEscapedTitleAndMessage()
    {
        string line = GitHubActionsSlowTestReporter.BuildNoticeLine("Ns.MyTest", TimeSpan.FromSeconds(75));

        Assert.AreEqual("::notice title=Slow test%3A Ns.MyTest::Ns.MyTest still running after 75s", line);
    }

    [TestMethod]
    public async Task ScanOnce_AfterThreshold_EmitsSingleNoticeAsync()
    {
        CapturingOutputDevice outputDevice = new();
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        // Default threshold is 60s; nothing before it.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(90), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("::notice", outputDevice.Lines[0]);
        Assert.Contains("Ns.T1 still running after", outputDevice.Lines[0]);
    }

    [TestMethod]
    public async Task ScanOnce_UsesExponentialBackoffAsync()
    {
        CapturingOutputDevice outputDevice = new();
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        // First emit at >= 60s; threshold then doubles to 120s, so 90s does not emit again.
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(70), CancellationToken.None).ConfigureAwait(false);
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(90), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(130), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(2, outputDevice.Lines);
    }

    [TestMethod]
    public async Task ConsumeAsync_TerminalState_StopsTrackingAsync()
    {
        CapturingOutputDevice outputDevice = new();
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);
    }

    [TestMethod]
    public async Task ScanOnce_WhenDisabled_DoesNotEmitAsync()
    {
        CapturingOutputDevice outputDevice = new();
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: false);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);

        Assert.IsEmpty(outputDevice.Lines);
    }

    [TestMethod]
    public async Task ScanOnce_RespectsConfiguredThresholdAsync()
    {
        CapturingOutputDevice outputDevice = new();
        Dictionary<string, string[]> options = new(StringComparer.OrdinalIgnoreCase)
        {
            [GitHubActionsCommandLineOptions.GitHubActionsSlowTestThreshold] = ["2"],
        };
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: true, options);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
        Assert.IsEmpty(outputDevice.Lines);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(3), CancellationToken.None).ConfigureAwait(false);
        Assert.HasCount(1, outputDevice.Lines);
    }

    [TestMethod]
    public async Task ScanOnce_ParameterizedTests_EmitDistinctLabelsAsync()
    {
        CapturingOutputDevice outputDevice = new();
        GitHubActionsSlowTestReporter reporter = CreateReporter(outputDevice, githubActions: true);
        await reporter.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        // Two data-driven instances that share one fully-qualified name but differ by display name.
        await reporter.ConsumeAsync(null!, CreateMessage("u1", "Ns.T.M", new InProgressTestNodeStateProperty(), displayName: "M (net8.0)"), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(null!, CreateMessage("u2", "Ns.T.M", new InProgressTestNodeStateProperty(), displayName: "M (net9.0)"), CancellationToken.None).ConfigureAwait(false);

        await reporter.ScanOnceAsync(Start + TimeSpan.FromSeconds(90), CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(2, outputDevice.Lines);
        string joined = string.Join("\n", outputDevice.Lines);
        Assert.Contains("Ns.T.M (net8.0) still running after", joined);
        Assert.Contains("Ns.T.M (net9.0) still running after", joined);
    }

    private static GitHubActionsSlowTestReporter CreateReporter(CapturingOutputDevice outputDevice, bool githubActions, Dictionary<string, string[]>? options = null)
    {
        Mock<IEnvironment> environmentMock = new();
        environmentMock.Setup(x => x.GetEnvironmentVariable("GITHUB_ACTIONS")).Returns(githubActions ? "true" : null);

        // The extension is enabled only when both the GITHUB_ACTIONS env var and the --report-gh master switch
        // are set, so always seed the master switch here; these tests exercise the env/knob behavior on top of it.
        Dictionary<string, string[]> commandLineOptions = options is null
            ? new(StringComparer.OrdinalIgnoreCase)
            : new(options, StringComparer.OrdinalIgnoreCase);
        commandLineOptions[GitHubActionsCommandLineOptions.GitHubActionsOptionName] = [];

        return new GitHubActionsSlowTestReporter(
            new FakeCommandLineOptions(commandLineOptions),
            environmentMock.Object,
            outputDevice,
            new NonRunningTask(),
            new FixedClock(Start),
            new StubLoggerFactory());
    }

    private static TestNodeUpdateMessage CreateMessage(string uid, string fullyQualifiedName, TestNodeStateProperty state, string? displayName = null)
    {
        PropertyBag propertyBag = new();
        propertyBag.Add(state);
        propertyBag.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", fullyQualifiedName));

        return new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = uid,
            DisplayName = displayName ?? fullyQualifiedName,
            Properties = propertyBag,
        });
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
