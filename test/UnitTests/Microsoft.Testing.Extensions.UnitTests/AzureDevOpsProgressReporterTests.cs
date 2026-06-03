// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
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
public sealed class AzureDevOpsProgressReporterTests
{
    public TestContext TestContext { get; set; } = null!;

    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IOutputDevice> _outputDeviceMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly List<IOutputDeviceData> _outputData = [];

    public AzureDevOpsProgressReporterTests()
    {
        _ = _testApplicationModuleInfoMock.Setup(info => info.TryGetAssemblyName()).Returns("MyAssembly");
        _ = _loggerFactoryMock.Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _ = _outputDeviceMock
            .Setup(outputDevice => outputDevice.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _outputData.Add(data))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenProgressOptionNotSetAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: false);
        Assert.IsFalse(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenProgressOptionSetAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        Assert.IsTrue(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task SessionStarting_DoesNothingWhenTfBuildIsNotSetAndEmitsWarningAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
        Assert.Contains(AzureDevOpsResources.ProgressRequiresTfBuildWarning, GetWarnings());
    }

    [TestMethod]
    public async Task SessionStarting_EmitsLogDetailInProgressAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);

        string[] lines = GetFormattedLines();
        Assert.HasCount(1, lines);
        Assert.StartsWith("##vso[task.logdetail id=", lines[0]);
        Assert.Contains(";type=Build;", lines[0]);
        Assert.Contains(";state=InProgress;", lines[0]);
        Assert.Contains(";progress=0]", lines[0]);
        Assert.Contains("MyAssembly", lines[0]);
    }

    [TestMethod]
    public async Task ConsumeAsync_EmitsMonotonicProgress_WithUidDedupAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        _outputData.Clear();

        // Two distinct tests, both pass — should at least let us see a progress update on the last one.
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        // Same uid re-emitted: should be deduped (no impact on completed counter).
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await Task.Delay(AzureDevOpsProgressReporter.MinimumEmissionIntervalMs + 50, TestContext.CancellationToken).ConfigureAwait(false);

        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t2", new InProgressTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t2", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string[] lines = GetFormattedLines();
        Assert.IsNotEmpty(lines);
        // Last line is the finish: progress=100;state=Completed;result=Succeeded
        Assert.Contains(";progress=100;state=Completed;result=Succeeded]", lines[^1]);

        // All progress values should be non-decreasing.
        int previous = -1;
        foreach (string line in lines)
        {
            int idx = line.IndexOf(";progress=", StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            int valueStart = idx + ";progress=".Length;
            int valueEnd = line.IndexOfAny([';', ']'], valueStart);
            int parsed = int.Parse(line[valueStart..valueEnd], System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsGreaterThanOrEqualTo(previous, parsed, $"Progress regressed: line was '{line}'.");
            previous = parsed;
        }
    }

    [TestMethod]
    public async Task SessionFinishing_EmitsFailedResult_WhenAnyTestFailedAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new FailedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string[] lines = GetFormattedLines();
        Assert.Contains(";progress=100;state=Completed;result=Failed]", lines[^1]);
    }

    [TestMethod]
    public async Task ConsumeAsync_CapsInProgressEmissionAt99_ReservesHundredForCompletedAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        _outputData.Clear();

        // Two tests, both completed without any prior `InProgress` event so seen==completed==2.
        // The raw percentage is 100; the in-progress emission must cap at 99 so the final
        // state=Completed emission can still surface as 100.
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await Task.Delay(AzureDevOpsProgressReporter.MinimumEmissionIntervalMs + 50, TestContext.CancellationToken).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t2", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);

        string[] inProgressLines = GetFormattedLines();
        foreach (string line in inProgressLines)
        {
            Assert.DoesNotContain(";progress=100;", line);
            Assert.DoesNotContain(";progress=100]", line);
        }

        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string[] allLines = GetFormattedLines();
        Assert.Contains(";progress=100;state=Completed;result=Succeeded]", allLines[^1]);
    }

    [TestMethod]
    public async Task SessionFinishing_DoesNothing_WhenTfBuildNotSetAsync()
    {
        AzureDevOpsProgressReporter reporter = CreateReporter(enabled: true);
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        _outputData.Clear();
        await reporter.ConsumeAsync(CreateProducer(), CreateTestNodeUpdateMessage("t1", new PassedTestNodeStateProperty()), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
    }

    private AzureDevOpsProgressReporter CreateReporter(bool enabled)
    {
        Dictionary<string, string[]> options = enabled
            ? new Dictionary<string, string[]> { [AzureDevOpsCommandLineOptions.AzureDevOpsProgress] = [] }
            : [];
        return new AzureDevOpsProgressReporter(
            new TestCommandLineOptions(options),
            _environmentMock.Object,
            _outputDeviceMock.Object,
            _testApplicationModuleInfoMock.Object,
            _loggerFactoryMock.Object);
    }

    private static TestNodeUpdateMessage CreateTestNodeUpdateMessage(string uid, TestNodeStateProperty state)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(state),
            });

    private static IDataProducer CreateProducer()
        => new TestProducer();

    private string[] GetFormattedLines()
        => [.. _outputData.OfType<FormattedTextOutputDeviceData>().Select(output => output.Text)];

    private string[] GetWarnings()
        => [.. _outputData.OfType<WarningMessageOutputDeviceData>().Select(output => output.Message)];

    private sealed class TestProducer : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

        public string Uid => "TestProducer";

        public string Version => "1.0.0";

        public string DisplayName => "TestProducer";

        public string Description => "TestProducer";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class TestSessionContext : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }
}
