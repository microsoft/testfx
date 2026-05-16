// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
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
public sealed class AzureDevOpsHistoryServiceTests
{
    [TestMethod]
    public void QuarantineFile_IgnoresCommentsAndBlanksAndMatchesGlobsCaseSensitively()
    {
        string quarantineFilePath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.quarantine.txt");
        File.WriteAllText(quarantineFilePath, "# comment\n\nNamespace.Tests.FlakyTest\nNamespace.Tests.Slow*\nNamespace.Tests.Quarantined?\n");

        try
        {
            QuarantineFile quarantineFile = new(quarantineFilePath, new SystemFileSystem(), new TextLogger());

            Assert.IsTrue(quarantineFile.Matches("Namespace.Tests.FlakyTest"));
            Assert.IsTrue(quarantineFile.Matches("Namespace.Tests.Slowest"));
            Assert.IsTrue(quarantineFile.Matches("Namespace.Tests.Quarantined1"));
            Assert.IsFalse(quarantineFile.Matches("Namespace.Tests.Other"));
            Assert.IsFalse(quarantineFile.Matches("namespace.tests.flakytest"));
        }
        finally
        {
            File.Delete(quarantineFilePath);
        }
    }

    [TestMethod]
    public async Task HistoryService_AggregatesStatsFromSinglePageAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsTestRun("https://example/_apis/test/Runs/1")]);
        historyClientMock
            .Setup(x => x.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "https://example/_apis/test/Runs/1", 0, 1000, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage(
            [
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Passed"),
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Failed"),
                new AzureDevOpsTestResult("Namespace.Tests.Stable", "Passed"),
            ],
            continuationToken: null));

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        Assert.IsTrue(historyService.TryGetStats("Namespace.Tests.Flaky", out FlakyStats flakyStats));
        Assert.AreEqual(1, flakyStats.PassCount);
        Assert.AreEqual(1, flakyStats.FailCount);
        Assert.AreEqual(14, historyService.HistoryWindowInDays);

        Assert.IsTrue(historyService.TryGetStats("Namespace.Tests.Stable", out FlakyStats stableStats));
        Assert.AreEqual(1, stableStats.PassCount);
        Assert.AreEqual(0, stableStats.FailCount);
    }

    [TestMethod]
    public async Task HistoryService_AggregatesStatsAcrossPagesAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsTestRun("https://example/_apis/test/Runs/1")]);
        historyClientMock
            .Setup(x => x.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "https://example/_apis/test/Runs/1", 0, 1000, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage(
            [
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Passed"),
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Failed"),
            ],
            continuationToken: "next-page"));
        historyClientMock
            .Setup(x => x.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "https://example/_apis/test/Runs/1", 0, 1000, "next-page", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage(
            [
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Failed"),
                new AzureDevOpsTestResult("Namespace.Tests.Flaky", "Passed"),
            ],
            continuationToken: null));

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        Assert.IsTrue(historyService.TryGetStats("Namespace.Tests.Flaky", out FlakyStats flakyStats));
        Assert.AreEqual(2, flakyStats.PassCount);
        Assert.AreEqual(2, flakyStats.FailCount);
        Assert.IsTrue(historyService.IsLikelyFlaky("Namespace.Tests.Flaky", 0.25));
    }

    [TestMethod]
    public async Task HistoryService_LogsWhenRunsAreCappedAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 501).Select(static i => new AzureDevOpsTestRun($"https://example/_apis/test/Runs/{i}")).ToArray());
        historyClientMock
            .Setup(x => x.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage([], continuationToken: null));
        TextLogger logger = new();

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock, logger: logger);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        string cappedRunsLog = string.Join("\n", logger.Logs);
        Assert.Contains(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryRunsCappedInfo, 501, 500), cappedRunsLog, cappedRunsLog);
    }

    [TestMethod]
    public async Task HistoryService_DegradesGracefullyWhenBudgetExpiresAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<AzureDevOpsHistoryQuery, int, CancellationToken>(async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return [];
            });
        TextLogger logger = new();

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock, task: new TestTask(delay: (_, _) => Task.CompletedTask), logger: logger);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        Assert.IsFalse(historyService.TryGetStats("Namespace.Tests.Flaky", out _));
        Assert.AreEqual(0, historyService.HistoryWindowInDays);
        string timeoutLogs = string.Join("\n", logger.Logs);
        Assert.Contains(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryLoadTimedOutInfo, 30), timeoutLogs, timeoutLogs);
    }

    [TestMethod]
    public async Task Reporter_AppendsRegressionAnnotationOnlyWhenMinimumSamplesAreAvailableAsync()
    {
        AzureDevOpsHistoryService belowThresholdHistoryService = await CreateHistoryServiceWithStatsAsync("Namespace.Tests.Regression", passCount: 4, failCount: 0).ConfigureAwait(false);
        var belowThresholdOutputDevice = new CapturingOutputDevice();
        AzureDevOpsReporter belowThresholdReporter = CreateReporter(belowThresholdHistoryService, belowThresholdOutputDevice, CreateCommandLineOptions());

        await belowThresholdReporter.ConsumeAsync(Mock.Of<IDataProducer>(), CreateFailedMessage("Namespace.Tests.Regression", "RegressionTestBelowThreshold"), CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(1, belowThresholdOutputDevice.Lines);
        Assert.DoesNotContain("[REGRESSION]", belowThresholdOutputDevice.Lines[0]);

        AzureDevOpsHistoryService atThresholdHistoryService = await CreateHistoryServiceWithStatsAsync("Namespace.Tests.Regression", passCount: 5, failCount: 0).ConfigureAwait(false);
        var atThresholdOutputDevice = new CapturingOutputDevice();
        AzureDevOpsReporter atThresholdReporter = CreateReporter(atThresholdHistoryService, atThresholdOutputDevice, CreateCommandLineOptions());

        await atThresholdReporter.ConsumeAsync(Mock.Of<IDataProducer>(), CreateFailedMessage("Namespace.Tests.Regression", "RegressionTestAtThreshold"), CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(1, atThresholdOutputDevice.Lines);
        Assert.Contains("[REGRESSION]", atThresholdOutputDevice.Lines[0]);
    }

    [TestMethod]
    public async Task Reporter_DemotesKnownFlakyFailuresWhenEnabledAsync()
    {
        AzureDevOpsHistoryService historyService = await CreateHistoryServiceWithStatsAsync("Namespace.Tests.Flaky", passCount: 3, failCount: 1).ConfigureAwait(false);
        var outputDevice = new CapturingOutputDevice();
        AzureDevOpsReporter reporter = CreateReporter(
            historyService,
            outputDevice,
            CreateCommandLineOptions((AzureDevOpsCommandLineOptions.AzureDevOpsDemoteKnownFlaky, [])));

        await reporter.ConsumeAsync(Mock.Of<IDataProducer>(), CreateFailedMessage("Namespace.Tests.Flaky", "FlakyTest"), CancellationToken.None).ConfigureAwait(false);

        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("type=warning;", outputDevice.Lines[0]);
        Assert.Contains("[flaky: failed 1/4 in last 14d]", outputDevice.Lines[0]);
    }

    [TestMethod]
    public async Task Reporter_AnnotatesQuarantinedFailuresAndEmitsBuildTagOnceAsync()
    {
        string quarantineFilePath = Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.quarantine.txt");
        File.WriteAllText(quarantineFilePath, "Namespace.Tests.Quarantined\n");

        try
        {
            Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
            AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock, CreateCommandLineOptions((AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, [quarantineFilePath])));
            var outputDevice = new CapturingOutputDevice();
            AzureDevOpsReporter reporter = CreateReporter(
                historyService,
                outputDevice,
                CreateCommandLineOptions((AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, [quarantineFilePath])));

            TestNodeUpdateMessage message = CreateFailedMessage("Namespace.Tests.Quarantined", "QuarantinedTest");
            await reporter.ConsumeAsync(Mock.Of<IDataProducer>(), message, CancellationToken.None).ConfigureAwait(false);
            await reporter.ConsumeAsync(Mock.Of<IDataProducer>(), message, CancellationToken.None).ConfigureAwait(false);

            Assert.HasCount(3, outputDevice.Lines);
            Assert.AreEqual("##vso[build.addbuildtag]has-quarantined-test-failure", outputDevice.Lines[0]);
            Assert.Contains("type=warning;", outputDevice.Lines[1]);
            Assert.Contains("[quarantined]", outputDevice.Lines[1]);
            Assert.Contains("[quarantined]", outputDevice.Lines[2]);
        }
        finally
        {
            File.Delete(quarantineFilePath);
        }
    }

    [TestMethod]
    public async Task Reporter_LogsMissingQuarantineFileWarningAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock);
        TextLogger logger = new();
        Mock<ILoggerFactory> loggerFactoryMock = CreateLoggerFactory(logger);
        AzureDevOpsReporter reporter = new(
            CreateCommandLineOptions((AzureDevOpsCommandLineOptions.AzureDevOpsQuarantineFile, [Path.Combine(AppContext.BaseDirectory, $"{Guid.NewGuid():N}.missing.txt")])),
            CreateEnvironmentMock().Object,
            new SystemFileSystem(),
            new CapturingOutputDevice(),
            loggerFactoryMock.Object,
            historyService);

        bool isEnabled = await reporter.IsEnabledAsync().ConfigureAwait(false);

        Assert.IsTrue(isEnabled);
        string reporterLogs = string.Join("\n", logger.Logs);
        Assert.Contains("does not exist", reporterLogs, reporterLogs);
    }

    [TestMethod]
    public async Task HistoryService_DegradesGracefullyOnNetworkFailureAsync()
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network failure"));

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);

        Assert.IsFalse(historyService.TryGetStats("Namespace.Tests.Flaky", out _));
        Assert.AreEqual(0, historyService.HistoryWindowInDays);
    }

    private static async Task<AzureDevOpsHistoryService> CreateHistoryServiceWithStatsAsync(string testName, int passCount, int failCount)
    {
        Mock<IAzureDevOpsHistoryClient> historyClientMock = new();
        historyClientMock
            .Setup(x => x.GetRunsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AzureDevOpsTestRun("https://example/_apis/test/Runs/1")]);

        List<AzureDevOpsTestResult> results = [];
        for (int i = 0; i < passCount; i++)
        {
            results.Add(new AzureDevOpsTestResult(testName, "Passed"));
        }

        for (int i = 0; i < failCount; i++)
        {
            results.Add(new AzureDevOpsTestResult(testName, "Failed"));
        }

        historyClientMock
            .Setup(x => x.GetResultsAsync(It.IsAny<AzureDevOpsHistoryQuery>(), "https://example/_apis/test/Runs/1", 0, 1000, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AzureDevOpsTestResultsPage(results, continuationToken: null));

        AzureDevOpsHistoryService historyService = CreateHistoryService(historyClientMock);
        await historyService.OnTestSessionStartingAsync(new TestSessionContextStub()).ConfigureAwait(false);
        return historyService;
    }

    private static AzureDevOpsHistoryService CreateHistoryService(
        Mock<IAzureDevOpsHistoryClient> historyClientMock,
        ICommandLineOptions? commandLineOptions = null,
        ITask? task = null,
        TextLogger? logger = null)
    {
        commandLineOptions ??= CreateCommandLineOptions();
        Mock<IEnvironment> environmentMock = CreateEnvironmentMock();
        logger ??= new TextLogger();
        task ??= new TestTask();
        Mock<ILoggerFactory> loggerFactoryMock = CreateLoggerFactory(logger);

        return new AzureDevOpsHistoryService(commandLineOptions, environmentMock.Object, new TestClock(), historyClientMock.Object, task, loggerFactoryMock.Object);
    }

    private static AzureDevOpsReporter CreateReporter(IAzureDevOpsHistoryService historyService, IOutputDevice outputDevice, ICommandLineOptions commandLineOptions)
    {
        Mock<IEnvironment> environmentMock = CreateEnvironmentMock();
        TextLogger logger = new();
        Mock<ILoggerFactory> loggerFactoryMock = CreateLoggerFactory(logger);

        return new AzureDevOpsReporter(commandLineOptions, environmentMock.Object, new SystemFileSystem(), outputDevice, loggerFactoryMock.Object, historyService);
    }

    private static ICommandLineOptions CreateCommandLineOptions(params (string Name, string[] Arguments)[] options)
    {
        Dictionary<string, string[]> values = new(StringComparer.OrdinalIgnoreCase)
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [],
            [AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory] = ["14"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsReportSeverity] = ["error"],
        };

        foreach ((string name, string[] arguments) in options)
        {
            values[name] = arguments;
        }

        return new FakeCommandLineOptions(values);
    }

    private static Mock<IEnvironment> CreateEnvironmentMock()
    {
        Mock<IEnvironment> environmentMock = new();
        environmentMock.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environmentMock.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns("https://dev.azure.com/example/");
        environmentMock.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns("testfx");
        environmentMock.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns("token");
        environmentMock.Setup(x => x.GetEnvironmentVariable("BUILD_DEFINITIONID")).Returns("123");
        return environmentMock;
    }

    private static Mock<ILoggerFactory> CreateLoggerFactory(TextLogger logger)
    {
        Mock<ILoggerFactory> loggerFactoryMock = new();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger);
        return loggerFactoryMock;
    }

    private static TestNodeUpdateMessage CreateFailedMessage(string fullyQualifiedName, string displayName)
    {
        Exception exception = CreateException();
        PropertyBag propertyBag = new();
        propertyBag.Add(new FailedTestNodeStateProperty(exception));
        propertyBag.Add(new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", fullyQualifiedName));

        return new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = "test",
            DisplayName = displayName,
            Properties = propertyBag,
        });
    }

    private static Exception CreateException()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            return ex;
        }
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

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2025, 05, 16, 12, 00, 00, TimeSpan.Zero);
    }

    private sealed class TestTask(Func<TimeSpan, CancellationToken, Task>? delay = null) : ITask
    {
        private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? ((_, _) => Task.CompletedTask);

        public Task Delay(int millisecondDelay)
            => Task.CompletedTask;

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
            => _delay(timeSpan, cancellationToken);

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
            => action();

        public Task WhenAll(params Task[] tasks)
            => Task.WhenAll(tasks);
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class TextLogger : ILogger
    {
        public List<string> Logs { get; } = [];

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add($"{logLevel.ToString().ToUpperInvariant()}: {formatter(state, exception)}");
            return Task.CompletedTask;
        }
    }
}
