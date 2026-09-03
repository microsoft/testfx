// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
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
public sealed class AzureDevOpsLivePublishingTests
{
    private const string TruncationMarker = "\n...[truncated]";

    /// <summary>Fixed start time for the retry tests, whose assertions never depend on the clock.</summary>
    private static readonly DateTimeOffset RetryTestStartTime = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<string> _directoriesToDelete = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (string path in _directoriesToDelete)
        {
            TryDeleteDirectory(path);
        }
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_CreatesRunAndStoresRunId()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(42);

        await StartPublisherAsync(publisher);

        Assert.AreEqual(42, publisher.RunId);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_JsonExceptionLogsWarningAndDoesNotThrow()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out CollectingLogger logger, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromException<int>(new JsonException("broken payload"));

        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsNull(publisher.RunId);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, string.Join(Environment.NewLine, logger.Logs));
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    // Regression test for https://github.com/microsoft/testfx/issues/10191: the platform disposes an
    // extension more than once during teardown, and a non-idempotent Dispose crashed the test host
    // with ObjectDisposedException after all tests had passed.
    [TestMethod]
    public async Task Dispose_CalledTwiceAfterSession_DoesNotThrow()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(77);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        publisher.Dispose();
        publisher.Dispose();
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_MissingConfiguration_StaysEnabledAndWarnsOnOutputDevice()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId());
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns((string?)null);
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out CollectingLogger logger, environment: environment, outputDevice: outputDevice);

        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsNull(publisher.RunId);
        Assert.IsEmpty(client.CreateTestRunCalls);
        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("SYSTEM_ACCESSTOKEN", outputDevice.Lines[0]);
        Assert.Contains("SYSTEM_ACCESSTOKEN", string.Join(Environment.NewLine, logger.Logs));
    }

    // Because IsEnabledAsync now stays true regardless of configuration, the platform registers the
    // publisher as a data consumer and lifetime handler even when it is inert. Those entry points must
    // therefore be safe no-ops, which they were never exercised for before.
    [TestMethod]
    public async Task MissingConfiguration_ConsumeFinishAndDispose_AreSafeNoOps()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId());
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns((string?)null);
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _, environment: environment);

        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        TestNode node = CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        publisher.Dispose();
        publisher.Dispose();

        Assert.IsNull(publisher.RunId);
        Assert.IsEmpty(client.CreateTestRunCalls);
        Assert.IsEmpty(client.UpdateTestRunStateCalls);
        Assert.IsEmpty(client.UploadTestResultAttachmentCalls);
    }

    // A run left in "InProgress" does not show up in the Azure DevOps Tests tab, so a finalization
    // failure must be reported on the output device rather than only in the diagnostic log.
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_FinalizeFailure_WarnsRunMayNotAppearOnOutputDevice()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(91);
        client.UpdateTestRunStateAsyncFunc = (_, _, _, _) => throw new HttpRequestException("boom");

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string output = string.Join(Environment.NewLine, outputDevice.Lines);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed, output);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress, output);
    }

    // Nothing drains the retry queue after the session-end flush, so unpublished results are lost for
    // good and the user must be told how many are missing from the Azure DevOps run.
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_ResultsCouldNotBePublished_WarnsWithCountOnOutputDevice()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(92);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => throw new HttpRequestException("publish rejected");

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string expected = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped, 1);
        Assert.Contains(expected, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    [TestMethod]
    public async Task ConsumeAsync_FlushesWhenBatchSizeIsReached()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(100);

        List<IReadOnlyList<AzureDevOpsTestCaseResult>> publishedBatches = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishedBatches.Add(results.ToArray());
            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = i + 1;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-2", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);

        Assert.HasCount(1, publishedBatches);
        Assert.HasCount(2, publishedBatches[0]);
    }

    [TestMethod]
    public async Task ConsumeAsync_FlushesWhenFlushIntervalElapsed()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromSeconds(5), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(101);

        List<IReadOnlyList<AzureDevOpsTestCaseResult>> publishedBatches = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishedBatches.Add(results.ToArray());
            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = i + 1;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);

        clock.UtcNow += TimeSpan.FromSeconds(6);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-2", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);

        Assert.HasCount(1, publishedBatches);
        Assert.HasCount(2, publishedBatches[0]);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_FlushesRemainingResults()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(102);

        List<IReadOnlyList<AzureDevOpsTestCaseResult>> publishedBatches = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishedBatches.Add(results.ToArray());
            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = i + 1;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, publishedBatches);
        Assert.HasCount(1, publishedBatches[0]);
        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_FailingTestsExitCode_FinalizesAsCompleted()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<ITestApplicationProcessExitCode> processExitCode = new();
        processExitCode.Setup(x => x.GetProcessExitCode()).Returns(2); // ExitCode.AtLeastOneTestFailed
        processExitCode.SetupGet(x => x.HasTestAdapterTestSessionFailure).Returns(false);
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, processExitCode: processExitCode);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(110);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_SessionAbortedExitCode_FinalizesAsAborted()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<ITestApplicationProcessExitCode> processExitCode = new();
        processExitCode.Setup(x => x.GetProcessExitCode()).Returns(3); // ExitCode.TestSessionAborted
        processExitCode.SetupGet(x => x.HasTestAdapterTestSessionFailure).Returns(false);
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, processExitCode: processExitCode);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(111);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_SessionCanceled_FinalizesAsAborted()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(112);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(new CancellationToken(canceled: true)));

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_TestAdapterFailure_FinalizesAsAborted()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<ITestApplicationProcessExitCode> processExitCode = new();
        processExitCode.Setup(x => x.GetProcessExitCode()).Returns(10); // ExitCode.TestAdapterTestSessionFailure
        processExitCode.SetupGet(x => x.HasTestAdapterTestSessionFailure).Returns(true);
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, processExitCode: processExitCode);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(113);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public void CreateTestCaseResult_MapsMtpStatesToAzdoResults()
    {
        DateTimeOffset startTime = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        TimingProperty timing = new(new TimingInfo(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(2)));
        AzureDevOpsTestCaseResult? passed = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("passed", new PassedTestNodeStateProperty(), startTime, timing), "tests.dll")?.Result;
        AzureDevOpsTestCaseResult? failed = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("failed", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), startTime, timing), "tests.dll")?.Result;
        AzureDevOpsTestCaseResult? skipped = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("skipped", new SkippedTestNodeStateProperty("skip"), startTime, timing), "tests.dll")?.Result;
        AzureDevOpsTestCaseResult? timeout = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("timeout", new TimeoutTestNodeStateProperty("too slow"), startTime, timing), "tests.dll")?.Result;
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
        AzureDevOpsTestCaseResult? cancelled = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("cancelled", new CancelledTestNodeStateProperty("stopped"), startTime, timing), "tests.dll")?.Result;
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete

        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, passed?.Outcome);
        Assert.AreEqual(2000L, passed?.DurationInMs);
        Assert.AreEqual(startTime, passed?.StartedDate);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, failed?.Outcome);
        Assert.AreEqual("boom", failed?.ErrorMessage);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.NotExecutedTestOutcome, skipped?.Outcome);
        Assert.AreEqual("skip", skipped?.ErrorMessage);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, timeout?.Outcome);
        Assert.AreEqual("Timeout: too slow", timeout?.ErrorMessage);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestOutcome, cancelled?.Outcome);
        Assert.AreEqual("stopped", cancelled?.ErrorMessage);
    }

    [TestMethod]
    public void CreateTestCaseResult_UsesFullyQualifiedNameAsAutomatedTestName()
    {
        TestNode testNode = new()
        {
            Uid = new TestNodeUid("opaque-test-node-uid"),
            DisplayName = "MyMethod",
            Properties = new PropertyBag(
                new PassedTestNodeStateProperty(),
                new TestMethodIdentifierProperty("tests.dll", "My.Namespace", "MyType", "MyMethod", 0, [], "System.Void")),
        };

        AzureDevOpsTestCaseResult? result = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(testNode, "tests.dll")?.Result;

        Assert.IsNotNull(result);
        AzureDevOpsTestCaseResult publishedResult = result!;
        Assert.AreEqual("My.Namespace.MyType.MyMethod", publishedResult.AutomatedTestName);
        Assert.AreEqual("MyMethod", publishedResult.TestCaseTitle);
    }

    [TestMethod]
    public async Task CreatePublisher_UsesSanitizedRunNameAndStorageFileName()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId(), stageName: new string('s', 240) + "/stage", jobName: "job\r\nline\u0001");
        AzureDevOpsPublishConfiguration? capturedConfiguration = null;
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        client.CreateTestRunAsyncFunc = (configuration, _) =>
        {
            capturedConfiguration = configuration;
            return Task.FromResult(55);
        };

        await StartPublisherAsync(publisher);

        Assert.IsNotNull(capturedConfiguration);
        AzureDevOpsPublishConfiguration configuration = capturedConfiguration!;
        Assert.AreEqual("mytests.dll", configuration.AutomatedTestStorage);
        Assert.DoesNotContain("/", configuration.RunName);
        Assert.DoesNotContain("\r", configuration.RunName);
        Assert.DoesNotContain("\n", configuration.RunName);
        Assert.IsLessThanOrEqualTo(AzureDevOpsLivePublishingConstants.MaxRunNameLength, configuration.RunName.Length);
    }

    [TestMethod]
    public async Task CreatePublisher_PopulatesPipelineReferenceFromPipelineVariables()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(
            processId: GetAliveProcessId(),
            stageName: "Build",
            jobName: "Test_Linux",
            phaseName: "RunTests",
            stageAttempt: "2",
            phaseAttempt: "3",
            jobAttempt: "4");
        AzureDevOpsPublishConfiguration? capturedConfiguration = null;
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        client.CreateTestRunAsyncFunc = (configuration, _) =>
        {
            capturedConfiguration = configuration;
            return Task.FromResult(56);
        };

        await StartPublisherAsync(publisher);

        Assert.IsNotNull(capturedConfiguration);
        AzureDevOpsPipelineReference? pipelineReference = capturedConfiguration!.PipelineReference;
        Assert.IsNotNull(pipelineReference);
        Assert.AreEqual("Build", pipelineReference!.StageName);
        Assert.AreEqual(2, pipelineReference.StageAttempt);
        Assert.AreEqual("RunTests", pipelineReference.PhaseName);
        Assert.AreEqual(3, pipelineReference.PhaseAttempt);
        Assert.AreEqual("Test_Linux", pipelineReference.JobName);
        Assert.AreEqual(4, pipelineReference.JobAttempt);
    }

    [TestMethod]
    public async Task CreatePublisher_MissingStagePhaseAndJob_LeavesPipelineReferenceNull()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId(), stageName: null, jobName: null);
        AzureDevOpsPublishConfiguration? capturedConfiguration = null;
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        client.CreateTestRunAsyncFunc = (configuration, _) =>
        {
            capturedConfiguration = configuration;
            return Task.FromResult(57);
        };

        await StartPublisherAsync(publisher);

        Assert.IsNotNull(capturedConfiguration);
        Assert.IsNull(capturedConfiguration!.PipelineReference);
    }

    [TestMethod]
    public async Task CreatePublisher_NonNumericAttempts_LeavesAttemptsNull()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(
            processId: GetAliveProcessId(),
            stageAttempt: "not-a-number",
            phaseAttempt: "1.5",
            jobAttempt: string.Empty);
        AzureDevOpsPublishConfiguration? capturedConfiguration = null;
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        client.CreateTestRunAsyncFunc = (configuration, _) =>
        {
            capturedConfiguration = configuration;
            return Task.FromResult(58);
        };

        await StartPublisherAsync(publisher);

        Assert.IsNotNull(capturedConfiguration);
        Assert.IsNotNull(capturedConfiguration!.PipelineReference);

        // One assertion per attempt so every parse of an unusable value is pinned, and the run is still
        // published rather than being failed by an attempt number the agent did not set sensibly.
        Assert.IsNull(capturedConfiguration.PipelineReference!.StageAttempt);
        Assert.IsNull(capturedConfiguration.PipelineReference.PhaseAttempt);
        Assert.IsNull(capturedConfiguration.PipelineReference.JobAttempt);
        Assert.IsNull(capturedConfiguration.PipelineReference.PhaseName);
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_DisplaysTestRunUrlSoResultsCanBeFollowedLive()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4242);

        await StartPublisherAsync(publisher);

        // Must be a session message, not plain informational text: the dotnet test pipe discards the
        // latter, and dotnet test is how this extension usually runs in a pipeline.
        Assert.ContainsSingle(outputDevice.SessionMessages);
        Assert.Contains("https://dev.azure.com/org/project/_TestManagement/Runs?runId=4242&_a=resultQuery", outputDevice.SessionMessages[0]);
        Assert.IsEmpty(outputDevice.Warnings);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_CreateTestRun_SendsStartedDateAndPipelineReference()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        string? capturedBody = null;
        QueueHttpMessageHandler handler = new(
            async (request, cancellationToken) =>
            {
                capturedBody = await ReadRequestBodyAsync(request, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":9}"),
                };
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results")
        {
            PipelineReference = new AzureDevOpsPipelineReference("Build", 2, "RunTests", 3, "Test_Linux", 4),
        };

        int runId = await client.CreateTestRunAsync(configuration, CancellationToken.None);

        Assert.AreEqual(9, runId);
        Assert.IsNotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody!);
        JsonElement root = document.RootElement;
        Assert.AreEqual("2025-01-01T00:00:00+00:00", root.GetProperty("startedDate").GetString());

        JsonElement pipelineReference = root.GetProperty("pipelineReference");

        // Azure DevOps requires pipelineId to match build.id.
        Assert.AreEqual(123, pipelineReference.GetProperty("pipelineId").GetInt32());
        Assert.AreEqual(root.GetProperty("build").GetProperty("id").GetInt32(), pipelineReference.GetProperty("pipelineId").GetInt32());
        Assert.AreEqual("Build", pipelineReference.GetProperty("stageReference").GetProperty("stageName").GetString());
        Assert.AreEqual(2, pipelineReference.GetProperty("stageReference").GetProperty("attempt").GetInt32());
        Assert.AreEqual("RunTests", pipelineReference.GetProperty("phaseReference").GetProperty("phaseName").GetString());
        Assert.AreEqual(3, pipelineReference.GetProperty("phaseReference").GetProperty("attempt").GetInt32());
        Assert.AreEqual("Test_Linux", pipelineReference.GetProperty("jobReference").GetProperty("jobName").GetString());
        Assert.AreEqual(4, pipelineReference.GetProperty("jobReference").GetProperty("attempt").GetInt32());
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_CreateTestRun_OmitsPipelineReferenceWhenUnavailable()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        string? capturedBody = null;
        QueueHttpMessageHandler handler = new(
            async (request, cancellationToken) =>
            {
                capturedBody = await ReadRequestBodyAsync(request, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":10}"),
                };
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);

        int runId = await client.CreateTestRunAsync(new AzureDevOpsPublishConfiguration("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results"), CancellationToken.None);

        Assert.AreEqual(10, runId);
        Assert.IsNotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody!);
        Assert.IsFalse(document.RootElement.TryGetProperty("pipelineReference", out _));
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_CreateTestRun_OmitsStageAndPhaseWhenOnlyJobIsKnown()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        string? capturedBody = null;
        QueueHttpMessageHandler handler = new(
            async (request, cancellationToken) =>
            {
                capturedBody = await ReadRequestBodyAsync(request, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":11}"),
                };
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results")
        {
            PipelineReference = new AzureDevOpsPipelineReference(StageName: null, StageAttempt: null, PhaseName: null, PhaseAttempt: null, JobName: "Test_Linux", JobAttempt: null),
        };

        int runId = await client.CreateTestRunAsync(configuration, CancellationToken.None);

        Assert.AreEqual(11, runId);
        Assert.IsNotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody!);
        JsonElement pipelineReference = document.RootElement.GetProperty("pipelineReference");
        Assert.IsFalse(pipelineReference.TryGetProperty("stageReference", out _));
        Assert.IsFalse(pipelineReference.TryGetProperty("phaseReference", out _));
        Assert.AreEqual("Test_Linux", pipelineReference.GetProperty("jobReference").GetProperty("jobName").GetString());
        Assert.IsFalse(pipelineReference.GetProperty("jobReference").TryGetProperty("attempt", out _));
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_HonorsRetryAfterOn429()
    {
        var events = new List<string>();
        FakeTask task = new(delayCallback: timeSpan => events.Add($"delay:{timeSpan.TotalSeconds}"));
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        QueueHttpMessageHandler handler = new(
            (request, cancellationToken) =>
            {
                events.Add("send:1");
                HttpResponseMessage response = new((HttpStatusCode)429)
                {
                    Content = new StringContent("{}"),
                };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                return Task.FromResult(response);
            },
            (request, cancellationToken) =>
            {
                events.Add("send:2");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":7}"),
                });
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);

        int runId = await client.CreateTestRunAsync(new AzureDevOpsPublishConfiguration("https://dev.azure.com/org/", "project", "token", 1, "run", "tests.dll", "results"), CancellationToken.None);

        Assert.AreEqual(7, runId);
        Assert.HasCount(1, task.DelayCalls);
        Assert.AreEqual(TimeSpan.FromSeconds(3), task.DelayCalls[0]);
        Assert.AreSequenceEqual(new[] { "send:1", "delay:3", "send:2" }, events);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_RetriesTaskCanceledException()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        QueueHttpMessageHandler handler = new(
            (request, cancellationToken) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("timeout")),
            (request, cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":8}"),
            }));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);

        int runId = await client.CreateTestRunAsync(new AzureDevOpsPublishConfiguration("https://dev.azure.com/org/", "project", "token", 1, "run", "tests.dll", "results"), CancellationToken.None);

        Assert.AreEqual(8, runId);
        Assert.HasCount(1, task.DelayCalls);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), task.DelayCalls[0]);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Redirect, "302")]
    [DataRow(HttpStatusCode.Unauthorized, "401")]
    public async Task AzureDevOpsTestResultsClient_AuthenticationFailure_ReportsInvalidAccessTokenGuidance(HttpStatusCode statusCode, string expectedStatus)
    {
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(new HttpResponseMessage(statusCode)));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, new FakeTask(), new FakeClock());
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 1, "run", "tests.dll", "results");

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => client.CreateTestRunAsync(configuration, CancellationToken.None));

        Assert.Contains($"(status: {expectedStatus})", exception.Message);
        Assert.Contains("SYSTEM_ACCESSTOKEN is invalid or unavailable", exception.Message);
        Assert.Contains("Make secrets available to builds of forks", exception.Message);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_BrowserOpaqueRedirect_ReportsInvalidAccessTokenGuidance()
    {
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(new HttpResponseMessage(0)
            {
                ReasonPhrase = "opaqueredirect",
            }));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, new FakeTask(), new FakeClock());
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 1, "run", "tests.dll", "results");

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => client.CreateTestRunAsync(configuration, CancellationToken.None));

        Assert.Contains("(status: opaqueredirect)", exception.Message);
        Assert.Contains("SYSTEM_ACCESSTOKEN is invalid or unavailable", exception.Message);
        Assert.Contains("Make secrets available to builds of forks", exception.Message);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_SuccessfulHtmlResponse_ReportsStatusAndContentType()
    {
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!DOCTYPE html><html></html>", Encoding.UTF8, "text/html"),
            }));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, new FakeTask(), new FakeClock());
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 1, "run", "tests.dll", "results");

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => client.CreateTestRunAsync(configuration, CancellationToken.None));

        Assert.Contains("status code 200", exception.Message);
        Assert.Contains("content type 'text/html; charset=utf-8'", exception.Message);
    }

    [TestMethod]
    public async Task ConsumeAsync_PublishFailureLogsWarningAndDoesNotThrow()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(103);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromException<IReadOnlyList<int>?>(new JsonException("publish failed"));

        await StartPublisherAsync(publisher);
        TestNodeUpdateMessage message = CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow));
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), message, CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task ConsumeAsync_PublishFailureRetriesBatchOnFinalFlush()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(104);

        List<IReadOnlyList<AzureDevOpsTestCaseResult>> publishedBatches = [];
        int publishAttempts = 0;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishAttempts++;
            publishedBatches.Add(results.ToArray());
            if (publishAttempts == 1)
            {
                return Task.FromException<IReadOnlyList<int>?>(new JsonException("publish failed"));
            }

            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = i + 1;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.AreEqual(2, publishAttempts);
        Assert.HasCount(2, publishedBatches);
        Assert.AreEqual("test-1", publishedBatches[0][0].AutomatedTestName);
        Assert.AreEqual("test-1", publishedBatches[1][0].AutomatedTestName);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task ConsumeAsync_UploadsAttachmentsForFailedTests()
    {
        using TestDirectory directory = CreateTestDirectory();
        string dumpPath = Path.Combine(directory.Path, "dump.txt");
        File.WriteAllText(dumpPath, "dump content");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(200);
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = 1_000 + i;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(dumpPath), "dump", "crash dump"));
        node.Properties.Add(new StandardOutputProperty("stdout content"));
        node.Properties.Add(new StandardErrorProperty("stderr content"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);

        Assert.HasCount(3, client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(1_000, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.AreEqual(1_000, client.UploadTestResultAttachmentCalls[1].TestCaseResultId);
        Assert.AreEqual(1_000, client.UploadTestResultAttachmentCalls[2].TestCaseResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[1].TestSubResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[2].TestSubResultId);
        Assert.AreEqual("dump.txt", client.UploadTestResultAttachmentCalls[0].Attachment.FileName);
        Assert.AreEqual(AzureDevOpsAttachmentTypes.GeneralAttachment, client.UploadTestResultAttachmentCalls[0].Attachment.AttachmentType);
        Assert.AreEqual("stdout.log", client.UploadTestResultAttachmentCalls[1].Attachment.FileName);
        Assert.AreEqual(AzureDevOpsAttachmentTypes.ConsoleLog, client.UploadTestResultAttachmentCalls[1].Attachment.AttachmentType);
        Assert.AreEqual("stderr.log", client.UploadTestResultAttachmentCalls[2].Attachment.FileName);
        Assert.AreEqual(AzureDevOpsAttachmentTypes.GeneralAttachment, client.UploadTestResultAttachmentCalls[2].Attachment.AttachmentType);
    }

    [TestMethod]
    public async Task ConsumeAsync_DoesNotUploadAttachmentsForPassedTests()
    {
        using TestDirectory directory = CreateTestDirectory();
        string dumpPath = Path.Combine(directory.Path, "passing-dump.txt");
        File.WriteAllText(dumpPath, "should not be uploaded");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(201);

        TestNode node = CreateNode("passing-test", new PassedTestNodeStateProperty(), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(dumpPath), "dump"));
        node.Properties.Add(new StandardOutputProperty("stdout content"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);

        Assert.IsEmpty(client.UploadTestResultAttachmentCalls);
    }

    [TestMethod]
    public async Task ConsumeAsync_AttachmentUploadFailureLogsWarningAndDoesNotRetryPublish()
    {
        using TestDirectory directory = CreateTestDirectory();
        string dumpPath = Path.Combine(directory.Path, "dump.txt");
        File.WriteAllText(dumpPath, "dump content");
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(202);

        int publishCalls = 0;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishCalls++;
            return Task.FromResult<IReadOnlyList<int>?>(Enumerable.Range(1, results.Count).ToArray());
        };
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, _, _, _) => throw new HttpRequestException("simulated upload failure");

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(dumpPath), "dump"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.AreEqual(1, publishCalls);
        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed, string.Join(Environment.NewLine, logger.Logs));

        // The per-attachment failure is swallowed so the flush can continue; the user still has to be
        // told at the end of the session that the attachment is missing from the run.
        string expectedSummary = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 1);
        Assert.Contains(expectedSummary, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    [TestMethod]
    public async Task ConsumeAsync_PublishReturnsNullSkipsAttachmentsAndDoesNotRetry()
    {
        using TestDirectory directory = CreateTestDirectory();
        string dumpPath = Path.Combine(directory.Path, "dump.txt");
        File.WriteAllText(dumpPath, "dump content");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(203);

        int publishCalls = 0;
        client.PublishTestResultsAsyncFunc = (_, _, _, _) =>
        {
            publishCalls++;
            return Task.FromResult<IReadOnlyList<int>?>(null);
        };

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(dumpPath), "dump"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.AreEqual(1, publishCalls);
        Assert.IsEmpty(client.UploadTestResultAttachmentCalls);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingResultIdParseFailedWarning, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task ConsumeAsync_SkipsOversizedFileAttachment()
    {
        using TestDirectory directory = CreateTestDirectory();
        string smallPath = Path.Combine(directory.Path, "small.txt");
        File.WriteAllText(smallPath, "small content");
        string bigPath = Path.Combine(directory.Path, "big.bin");
        using (FileStream fs = File.OpenWrite(bigPath))
        {
            fs.SetLength(AzureDevOpsLivePublishingConstants.MaxAttachmentSizeBytes + 1);
        }

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(204);
        // The publisher still queues the oversized attachment; the client side TryBuildAttachmentRequest
        // drops it. In this fake we just record the call regardless — the contract is exercised end-to-end
        // when running against the real client. For the unit test we only assert what the publisher sends.
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, _, attachment, _) => Task.CompletedTask;

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(smallPath), "small"));
        node.Properties.Add(new FileArtifactProperty(new FileInfo(bigPath), "big"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);

        // The publisher passes both attachments through; the client's TryBuildAttachmentRequest is what
        // skips oversized ones. We sanity-check that the publisher does forward both names so the client
        // gets a chance to filter.
        Assert.HasCount(2, client.UploadTestResultAttachmentCalls);
        Assert.Contains(c => c.Attachment.FileName == "small.txt", client.UploadTestResultAttachmentCalls);
        Assert.Contains(c => c.Attachment.FileName == "big.bin", client.UploadTestResultAttachmentCalls);
    }

    [TestMethod]
    public async Task ConsumeAsync_DoesNotTruncateStdoutAtInlineByteLimit()
    {
        string stdout = new('x', AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes);

        AzureDevOpsTestResultAttachment uploaded = await UploadStdoutAttachmentAsync(stdout);

        Assert.AreEqual(stdout, uploaded.InlineContent);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes, Encoding.UTF8.GetByteCount(uploaded.InlineContent!));
    }

    [TestMethod]
    public async Task ConsumeAsync_TruncatesLargeStdoutInline()
    {
        string oversizedStdout = new('x', AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes + 10_000);

        AzureDevOpsTestResultAttachment uploaded = await UploadStdoutAttachmentAsync(oversizedStdout);

        Assert.IsNotNull(uploaded.InlineContent);
        Assert.IsLessThanOrEqualTo(AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes, Encoding.UTF8.GetByteCount(uploaded.InlineContent!));
        Assert.EndsWith(TruncationMarker, uploaded.InlineContent!, StringComparison.Ordinal);
    }

    [DataRow("€")]
    [DataRow("😀")]
    [TestMethod]
    public async Task ConsumeAsync_TruncatesUnicodeStdoutWithinInlineByteLimit(string textElement)
    {
        int repeatCount = (AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes / Encoding.UTF8.GetByteCount(textElement)) + 10_000;
        string oversizedStdout = string.Concat(Enumerable.Repeat(textElement, repeatCount));

        AzureDevOpsTestResultAttachment uploaded = await UploadStdoutAttachmentAsync(oversizedStdout);

        Assert.IsNotNull(uploaded.InlineContent);
        Assert.IsLessThanOrEqualTo(AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes, Encoding.UTF8.GetByteCount(uploaded.InlineContent!));
        Assert.EndsWith(TruncationMarker, uploaded.InlineContent!, StringComparison.Ordinal);

        string retainedContent = uploaded.InlineContent![..^TruncationMarker.Length];
        Assert.IsFalse(retainedContent.Length > 0 && char.IsHighSurrogate(retainedContent[^1]));
    }

    [TestMethod]
    public async Task ConsumeAsync_TruncatesStdoutWithoutSplittingSurrogatePairAtBoundary()
    {
        string emoji = "😀";
        int markerBytes = Encoding.UTF8.GetByteCount(TruncationMarker);
        int budget = AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes - markerBytes;
        string prefix = new('x', budget - Encoding.UTF8.GetByteCount(emoji));
        string oversizedStdout = prefix + emoji + new string('x', markerBytes + 1);

        AzureDevOpsTestResultAttachment uploaded = await UploadStdoutAttachmentAsync(oversizedStdout);

        Assert.IsNotNull(uploaded.InlineContent);
        Assert.IsLessThanOrEqualTo(AzureDevOpsLivePublishingConstants.MaxInlineAttachmentBytes, Encoding.UTF8.GetByteCount(uploaded.InlineContent!));
        Assert.AreEqual(prefix + emoji + TruncationMarker, uploaded.InlineContent);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_UploadsCoverageSessionFileArtifacts()
    {
        using TestDirectory directory = CreateTestDirectory();
        string coveragePath = Path.Combine(directory.Path, "results.cobertura.xml");
        File.WriteAllText(coveragePath, "<coverage/>");
        string opencoverPath = Path.Combine(directory.Path, "results.opencover.xml");
        File.WriteAllText(opencoverPath, "<CoverageSession/>");
        string binaryCoveragePath = Path.Combine(directory.Path, "results.coverage");
        File.WriteAllBytes(binaryCoveragePath, [0, 1, 2, 3]);

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(206);

        await StartPublisherAsync(publisher);
        SessionUid sessionUid = new(Guid.NewGuid().ToString());
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(coveragePath), "cobertura", "coverage report"), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(opencoverPath), "opencover"), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(binaryCoveragePath), "vs-coverage"), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(3, client.UploadTestRunAttachmentCalls);
        Assert.IsTrue(client.UploadTestRunAttachmentCalls.All(c => c.Attachment.AttachmentType == AzureDevOpsAttachmentTypes.CodeCoverage));
        Assert.Contains(c => c.Attachment.FileName == "results.cobertura.xml", client.UploadTestRunAttachmentCalls);
        Assert.Contains(c => c.Attachment.FileName == "results.opencover.xml", client.UploadTestRunAttachmentCalls);
        Assert.Contains(c => c.Attachment.FileName == "results.coverage", client.UploadTestRunAttachmentCalls);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_SkipsNonCoverageSessionFileArtifacts()
    {
        using TestDirectory directory = CreateTestDirectory();
        string logPath = Path.Combine(directory.Path, "run.log");
        File.WriteAllText(logPath, "some log");

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(207);

        await StartPublisherAsync(publisher);
        SessionUid sessionUid = new(Guid.NewGuid().ToString());
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(logPath), "log"), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsEmpty(client.UploadTestRunAttachmentCalls);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_CoverageUploadFailureLogsWarning()
    {
        using TestDirectory directory = CreateTestDirectory();
        string coveragePath = Path.Combine(directory.Path, "results.cobertura.xml");
        File.WriteAllText(coveragePath, "<coverage/>");

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(208);
        client.UploadTestRunAttachmentAsyncFunc = (_, _, _, _) => throw new HttpRequestException("simulated upload failure");

        await StartPublisherAsync(publisher);
        SessionUid sessionUid = new(Guid.NewGuid().ToString());
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(coveragePath), "cobertura"), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UploadTestRunAttachmentCalls);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task RunIdCoordinator_CreateAndReadFlowSharesRunIdAcrossProcesses()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 10, TimeSpan.FromMilliseconds(1));
        Mock<IEnvironment> ownerEnvironment = CreateEnvironmentMock(processId: GetAliveProcessId());
        Mock<IEnvironment> joinerEnvironment = CreateEnvironmentMock(processId: int.MaxValue);
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator ownerCoordinator = new(fileSystem, new SystemTask(), clock, ownerEnvironment.Object, logger, options);
        AzureDevOpsRunIdCoordinator joinerCoordinator = new(fileSystem, new SystemTask(), clock, joinerEnvironment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        AzureDevOpsCoordinatedRun ownerRun = await ownerCoordinator.AcquireRunAsync(configuration, _ => Task.FromResult(88), CancellationToken.None);
        AzureDevOpsCoordinatedRun joinerRun = await joinerCoordinator.AcquireRunAsync(configuration, _ => Task.FromResult(99), CancellationToken.None);

        Assert.AreEqual(88, ownerRun.RunId);
        Assert.IsTrue(ownerRun.IsOwner);
        Assert.AreEqual(88, joinerRun.RunId);
        Assert.IsFalse(joinerRun.IsOwner);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "azdo-runid.123.json")));
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_ReplacesExpiredOwnerAndRunIdFiles()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 2, TimeSpan.FromMilliseconds(1));
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId());
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator coordinator = new(fileSystem, new FakeTask(), clock, environment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        File.WriteAllText(Path.Combine(directory.Path, "azdo-runid.123.owner"), JsonSerializer.Serialize(new AzureDevOpsLeaseFile(int.MaxValue, 123, clock.UtcNow.AddMinutes(-1))));
        File.WriteAllText(Path.Combine(directory.Path, "azdo-runid.123.json"), JsonSerializer.Serialize(new AzureDevOpsRunIdFile(7, 123, configuration.CollectionUri, configuration.Project, clock.UtcNow.AddMinutes(-1))));

        AzureDevOpsCoordinatedRun coordinatedRun = await coordinator.AcquireRunAsync(configuration, _ => Task.FromResult(88), CancellationToken.None);

        Assert.AreEqual(88, coordinatedRun.RunId);
        Assert.IsTrue(coordinatedRun.IsOwner);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Path, "azdo-runid.123.json")));
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_OverwritesExistingParticipantFile()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 2, TimeSpan.FromMilliseconds(1));
        int joinerProcessId = GetAliveProcessId();
        Mock<IEnvironment> joinerEnvironment = CreateEnvironmentMock(processId: joinerProcessId);
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator joinerCoordinator = new(fileSystem, new FakeTask(), clock, joinerEnvironment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        File.WriteAllText(Path.Combine(directory.Path, "azdo-runid.123.owner"), JsonSerializer.Serialize(new AzureDevOpsLeaseFile(int.MaxValue, 123, clock.UtcNow.AddHours(1))));
        File.WriteAllText(Path.Combine(directory.Path, "azdo-runid.123.json"), JsonSerializer.Serialize(new AzureDevOpsRunIdFile(91, 123, configuration.CollectionUri, configuration.Project, clock.UtcNow.AddHours(1))));
        File.WriteAllText(Path.Combine(directory.Path, $"azdo-runid.123.participant.{joinerProcessId}.json"), "stale");

        AzureDevOpsCoordinatedRun coordinatedRun = await joinerCoordinator.AcquireRunAsync(configuration, _ => Task.FromResult(0), CancellationToken.None);
        AzureDevOpsLeaseFile? participantLease = JsonSerializer.Deserialize<AzureDevOpsLeaseFile>(File.ReadAllText(coordinatedRun.ParticipantFilePath));

        Assert.AreEqual(91, coordinatedRun.RunId);
        Assert.IsFalse(coordinatedRun.IsOwner);
        Assert.IsNotNull(participantLease);
        AzureDevOpsLeaseFile lease = participantLease!;
        Assert.AreEqual(123, lease.BuildId);
        Assert.IsGreaterThan(clock.UtcNow, lease.ExpiresAt);
    }

    [TestMethod]
    public async Task RunIdCoordinator_FinalizeRunAsync_TimesOutAndLogsWarning()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        FakeTask task = new(timeSpan => clock.UtcNow += timeSpan);
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 5, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(30), TimeSpan.FromHours(4));
        int aliveProcessId = GetAliveProcessId();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: aliveProcessId);
        AzureDevOpsRunIdCoordinator coordinator = new(new SystemFileSystem(), task, clock, environment.Object, logger, options);
        string ownerFilePath = Path.Combine(directory.Path, "azdo-runid.123.owner");
        string runIdFilePath = Path.Combine(directory.Path, "azdo-runid.123.json");
        string participantFilePath = Path.Combine(directory.Path, $"azdo-runid.123.participant.{int.MaxValue}.json");

        File.WriteAllText(ownerFilePath, JsonSerializer.Serialize(new AzureDevOpsLeaseFile(aliveProcessId, 123, clock.UtcNow.AddHours(1))));
        File.WriteAllText(runIdFilePath, JsonSerializer.Serialize(new AzureDevOpsRunIdFile(5, 123, "https://dev.azure.com/org/", "project", clock.UtcNow.AddHours(1))));

        // A participant that cannot be vouched for: its lease is unreadable and the process in its file
        // name is not running. Those get only the short grace period — unlike a peer whose process is
        // provably alive, which the owner keeps waiting for because it is still publishing.
        File.WriteAllText(Path.Combine(directory.Path, $"azdo-runid.123.participant.{int.MaxValue - 1}.json"), "not-json");

        int finalizeCalls = 0;
        await coordinator.FinalizeRunAsync(new AzureDevOpsCoordinatedRun(5, true, 123, directory.Path, runIdFilePath, ownerFilePath, participantFilePath), _ =>
        {
            finalizeCalls++;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(1, finalizeCalls);
        Assert.Contains(log => log.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0, logger.Logs);
        Assert.IsFalse(File.Exists(ownerFilePath));
        Assert.IsFalse(File.Exists(runIdFilePath));
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_TransientUnreadableOwnerPreservesItForRetry()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        // Tiny joiner max-wait so the test gives up quickly when the owner file looks transient.
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4), TimeSpan.FromMilliseconds(20));
        FakeTask task = new(timeSpan => clock.UtcNow += timeSpan);
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId());
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator coordinator = new(fileSystem, task, clock, environment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        // Write garbage that mimics a partial owner-write (file exists, but content is unparseable JSON).
        string ownerFilePath = Path.Combine(directory.Path, "azdo-runid.123.owner");
        File.WriteAllText(ownerFilePath, "{partial");

        // Acquiring should fail because the owner file looks transient and we refuse to clobber it.
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.AcquireRunAsync(configuration, _ => Task.FromResult(99), CancellationToken.None));

        Assert.AreEqual(AzureDevOpsResources.AzureDevOpsLivePublishingMissingRunIdFile, exception.Message);
        // The owner file must still be present so the real owner can complete its write.
        Assert.IsTrue(File.Exists(ownerFilePath));
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_JoinerKeepsWaitingWhileOwnerLeaseValid()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        // CoordinationReadRetryCount=2 — the initial retry budget is small (4 ms total). Without
        // owner-lease-aware waiting the joiner would give up immediately, but the joiner max-wait
        // (200 ms) plus an active owner lease (1 h) lets it keep polling.
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4), TimeSpan.FromMilliseconds(200));
        FakeTask task = new(timeSpan => clock.UtcNow += timeSpan);
        int joinerProcessId = int.MaxValue;
        Mock<IEnvironment> joinerEnvironment = CreateEnvironmentMock(processId: joinerProcessId);
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator joinerCoordinator = new(fileSystem, task, clock, joinerEnvironment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        string ownerFilePath = Path.Combine(directory.Path, "azdo-runid.123.owner");
        string runIdFilePath = Path.Combine(directory.Path, "azdo-runid.123.json");

        // Simulate an owner that has acquired the lease but is still inside a long CreateTestRunAsync.
        File.WriteAllText(ownerFilePath, JsonSerializer.Serialize(new AzureDevOpsLeaseFile(GetAliveProcessId(), 123, clock.UtcNow.AddHours(1))));

        // Drop the run-id file after the joiner has already exhausted the base retry budget so we
        // exercise the owner-lease-aware extension of the wait loop.
        int delayCalls = 0;
        task = new(timeSpan =>
        {
            clock.UtcNow += timeSpan;
            delayCalls++;
            if (delayCalls == 3)
            {
                File.WriteAllText(runIdFilePath, JsonSerializer.Serialize(new AzureDevOpsRunIdFile(77, 123, configuration.CollectionUri, configuration.Project, clock.UtcNow.AddHours(1))));
            }
        });
        joinerCoordinator = new(fileSystem, task, clock, joinerEnvironment.Object, logger, options);

        AzureDevOpsCoordinatedRun coordinatedRun = await joinerCoordinator.AcquireRunAsync(configuration, _ => Task.FromResult(0), CancellationToken.None);

        Assert.AreEqual(77, coordinatedRun.RunId);
        Assert.IsFalse(coordinatedRun.IsOwner);
        Assert.IsGreaterThan(2, delayCalls);
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_JoinerGivesUpWhenOwnerLeaseExpires()
    {
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4), TimeSpan.FromMinutes(5));
        FakeTask task = new(timeSpan => clock.UtcNow += timeSpan);
        Mock<IEnvironment> joinerEnvironment = CreateEnvironmentMock(processId: int.MaxValue);
        SystemFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator joinerCoordinator = new(fileSystem, task, clock, joinerEnvironment.Object, logger, options);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", directory.Path);

        // Owner lease is already expired and the owner PID is a long-dead one, so the joiner should
        // take over and create the run itself rather than waiting indefinitely.
        File.WriteAllText(Path.Combine(directory.Path, "azdo-runid.123.owner"), JsonSerializer.Serialize(new AzureDevOpsLeaseFile(int.MaxValue, 123, clock.UtcNow.AddMinutes(-5))));

        AzureDevOpsCoordinatedRun coordinatedRun = await joinerCoordinator.AcquireRunAsync(configuration, _ => Task.FromResult(123), CancellationToken.None);

        Assert.AreEqual(123, coordinatedRun.RunId);
        Assert.IsTrue(coordinatedRun.IsOwner);
    }

    private async Task<AzureDevOpsTestResultAttachment> UploadStdoutAttachmentAsync(string stdout)
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(205);

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new StandardOutputProperty(stdout));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);

        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        return client.UploadTestResultAttachmentCalls[0].Attachment;
    }

    // Attachment failures are swallowed per attachment so one bad file cannot abort the drain; without
    // a summary the user loses coverage files/dumps with no explanation.
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_AttachmentUploadFailure_WarnsWithCountOnOutputDevice()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(93);
        client.UploadTestRunAttachmentAsyncFunc = (_, _, _, _) => throw new HttpRequestException("attachment rejected");

        await StartPublisherAsync(publisher);
        string coveragePath = Path.Combine(directory.Path, "results.cobertura.xml");
        File.WriteAllText(coveragePath, "<coverage/>");
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            new SessionFileArtifact(new SessionUid(Guid.NewGuid().ToString()), new FileInfo(coveragePath), "cobertura"),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string expected = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 1);
        Assert.Contains(expected, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    // Regression tests for the two teardown escape hatches: WarnAsync must survive a failing log
    // provider, and finalization must not let its own cleanup-timeout cancellation fail the run.
    [TestMethod]
    public async Task OnTestSessionStartingAsync_LogProviderThrows_DoesNotFailTheRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: GetAliveProcessId());
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns((string?)null);
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: AzureDevOpsTestResultsPublisherOptions.Default, out _, out _, out CollectingLogger logger, environment: environment, outputDevice: outputDevice);
        logger.ThrowOnLog = true;

        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        // The console copy still gets through, with the right message, even though the diagnostic
        // logger is broken.
        Assert.HasCount(1, outputDevice.Lines);
        Assert.Contains("SYSTEM_ACCESSTOKEN", outputDevice.Lines[0]);
    }

    // OnTestSessionFinishingAsync is a lifetime handler and the platform does not guard those, so a
    // throwing log provider anywhere on the teardown path would fail an otherwise successful run.
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_PublishFailsAndLogProviderThrows_DoesNotFailTheRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(95);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => throw new HttpRequestException("publish rejected");

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);

        // The diagnostic logger starts failing right before teardown drives its recovery-path logging.
        logger.ThrowOnLog = true;
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        // The console summary still reports the lost result even though the log provider is broken.
        string expected = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped, 1);
        Assert.Contains(expected, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_FinalizeCanceled_DoesNotFailTheRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(94);

        // HttpClient surfaces a timeout as TaskCanceledException, which derives from OperationCanceledException.
        client.UpdateTestRunStateAsyncFunc = (_, _, _, _) => throw new TaskCanceledException("finalization timed out");

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string output = string.Join(Environment.NewLine, outputDevice.Lines);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed, output);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress, output);
    }

    // A throwing log provider used to abort the attachment drain after the first failure, leaving the
    // rest of the queue unattempted and absent from the end-of-session count.
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_AttachmentFailsAndLogProviderThrows_CountsEveryAttachment()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out CollectingLogger logger, outputDevice: outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(96);
        client.UploadTestRunAttachmentAsyncFunc = (_, _, _, _) => throw new HttpRequestException("attachment rejected");

        await StartPublisherAsync(publisher);
        SessionUid sessionUid = new(Guid.NewGuid().ToString());
        foreach (string name in new[] { "first.cobertura.xml", "second.cobertura.xml" })
        {
            string path = Path.Combine(directory.Path, name);
            File.WriteAllText(path, "<coverage/>");
            await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), new SessionFileArtifact(sessionUid, new FileInfo(path), "cobertura"), CancellationToken.None);
        }

        logger.ThrowOnLog = true;
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        // Both attachments must be attempted and counted, not just the one before the logger blew up.
        Assert.HasCount(2, client.UploadTestRunAttachmentCalls);
        string expected = string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, 2);
        Assert.Contains(expected, string.Join(Environment.NewLine, outputDevice.Lines));
    }

    private static async Task StartPublisherAsync(AzureDevOpsTestResultsPublisher publisher)
    {
        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
    }

    #region Run lifetime across orchestrated processes (https://github.com/microsoft/testfx/issues/10360)

    [TestMethod]
    public async Task OrchestratorLifetime_BeforeRun_CreatesRunOnceAndPublishesItToOrchestratedProcesses()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _, environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4242);

        Assert.IsTrue(await lifetime.IsEnabledAsync());
        await lifetime.BeforeRunAsync(CancellationToken.None);

        Assert.HasCount(1, client.CreateTestRunCalls);
        Assert.AreEqual(4242, lifetime.RunId);
        Assert.AreEqual(4242, AzureDevOpsConstants.TryGetInheritedTestRunId(environment.Object, buildId: 123));
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_CompletesTheRunItCreated()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _, CreateEnvironmentMockWithSettableRunId());
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4243);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(2 /* ExitCode.AtLeastOneTestFailed */, CancellationToken.None);

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(4243, client.UpdateTestRunStateCalls[0].RunId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_NonTestResultExitCodeAbortsTheRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _, CreateEnvironmentMockWithSettableRunId());
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4244);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(3 /* ExitCode.TestSessionAborted */, CancellationToken.None);

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(4244, client.UpdateTestRunStateCalls[0].RunId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_StopsHandingOutTheRunIdSoLaterProcessesDoNotPublishIntoAClosedRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _, environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4245);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.IsNull(AzureDevOpsConstants.TryGetInheritedTestRunId(environment.Object, buildId: 123));
    }

    [TestMethod]
    public async Task OrchestratorLifetime_BeforeRun_RunIdAlreadyInEnvironment_LeavesTheAncestorRunAlone()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _, CreateEnvironmentMockWithSettableRunId("123:777"));

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.IsEmpty(client.CreateTestRunCalls);
        Assert.IsEmpty(client.UpdateTestRunStateCalls);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_BeforeRun_CreateRunFails_DoesNotPublishARunIdAndDoesNotThrow()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out CollectingLogger logger, environment, outputDevice);
        client.CreateTestRunAsyncFunc = (_, _) => throw new HttpRequestException("boom");

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.IsNull(lifetime.RunId);
        Assert.IsNull(AzureDevOpsConstants.TryGetInheritedTestRunId(environment.Object, buildId: 123));
        Assert.IsEmpty(client.UpdateTestRunStateCalls);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, string.Join(Environment.NewLine, outputDevice.Warnings));
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task Publisher_RunIdInEnvironment_JoinsTheRunInsteadOfCreatingItsOwn()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId("123:909");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);

        await StartPublisherAsync(publisher);

        Assert.AreEqual(909, publisher.RunId);
        Assert.IsEmpty(client.CreateTestRunCalls);
    }

    [TestMethod]
    public async Task Publisher_RunIdInEnvironment_DoesNotCompleteTheRunSoLaterAttemptsCanStillPublish()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId("123:910");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsEmpty(client.UpdateTestRunStateCalls);
    }

    [TestMethod]
    public async Task Publisher_RunIdInEnvironment_StillPublishesResultsIntoTheInheritedRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId("123:911");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        List<int> publishedToRunIds = [];
        client.PublishTestResultsAsyncFunc = (_, runId, results, _) =>
        {
            publishedToRunIds.Add(runId);
            return Task.FromResult<IReadOnlyList<int>?>([.. Enumerable.Range(1, results.Count)]);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("Test1", new PassedTestNodeStateProperty(), DateTimeOffset.UtcNow)),
            CancellationToken.None);

        Assert.HasCount(1, publishedToRunIds);
        Assert.AreEqual(911, publishedToRunIds[0]);
    }

    [TestMethod]
    public async Task Publisher_RunIdInEnvironment_WritesNoCoordinationFilesBecauseTheAncestorOwnsTheRun()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId("123:912");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out _, out _, out _, environment: environment);

        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsEmpty(Directory.GetFiles(directory.Path, "azdo-runid.*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-a-number")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("123:0")]
    [DataRow("123:-1")]
    [DataRow("123:not-a-number")]
    [DataRow(":42")]
    [DataRow("999:42")]
    public async Task Publisher_UnusableRunIdInEnvironment_FallsBackToCreatingItsOwnRun(string environmentValue)
    {
        // "999:42" is the important row: a handoff left behind by a different build must never redirect
        // this build's results into that build's (probably already completed) run.
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId(environmentValue);
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out _, environment: environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(913);

        await StartPublisherAsync(publisher);

        Assert.AreEqual(913, publisher.RunId);
        Assert.HasCount(1, client.CreateTestRunCalls);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_CanceledToken_StillClosesTheRunAsAborted()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4246);

        await lifetime.BeforeRunAsync(CancellationToken.None);

        using CancellationTokenSource canceled = new();
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; this project also targets .NET Framework.
        canceled.Cancel();
#pragma warning restore VSTHRD103
        await lifetime.AfterRunAsync(3 /* ExitCode.TestSessionAborted */, canceled.Token);

        // A run left "InProgress" never appears in the build's Tests tab, so cancellation must still close it.
        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(4246, client.UpdateTestRunStateCalls[0].RunId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.AbortedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_CalledTwice_FinalizesTheRunOnlyOnce()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4247);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(4247, client.UpdateTestRunStateCalls[0].RunId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, client.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_AfterRun_WithoutBeforeRun_IsANoOp()
    {
        // The host calls AfterRunAsync on every lifetime once BeforeRunAsync has been attempted, including
        // ones whose BeforeRunAsync never ran because an earlier lifetime faulted.
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out _);

        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.IsEmpty(client.UpdateTestRunStateCalls);
    }

    [TestMethod]
    public async Task OrchestratorLifetime_HandoffFailsAfterRunCreated_StillClosesTheRun()
    {
        // Anything failing between run creation and the handoff must not discard the run: this process is
        // the only one that can close it, so losing that state would strand it in "InProgress".
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        environment.Setup(x => x.SetEnvironmentVariable(AzureDevOpsConstants.TestRunIdEnvironmentVariableName, It.IsAny<string>()))
            .Throws(new SecurityException("environment is locked down"));
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient client, out CollectingLogger logger, environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4248);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.AreEqual(4248, client.UpdateTestRunStateCalls[0].RunId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, client.UpdateTestRunStateCalls[0].State);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingRunIdHandoffFailed, string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task OrchestratorLifetime_ResultMapHandoffFailure_UsesSpecificDiagnostic()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        environment.Setup(x => x.SetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName, It.IsAny<string>()))
            .Throws(new SecurityException("result map handoff is locked down"));
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(
            directory.Path,
            out FakeAzureDevOpsTestResultsClient client,
            out CollectingLogger logger,
            environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4249);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        await lifetime.AfterRunAsync(0, CancellationToken.None);

        Assert.HasCount(1, client.UpdateTestRunStateCalls);
        Assert.Contains(
            AzureDevOpsResources.AzureDevOpsLivePublishingResultMapHandoffFailed,
            string.Join(Environment.NewLine, logger.Logs));
    }

    [TestMethod]
    public async Task OrchestratorLifetime_PeerOrchestratorsSharingAResultsDirectory_PublishIntoASingleRun()
    {
        // A multi-project 'dotnet test' runs one orchestrator process per test project, all sharing the
        // user's results directory. They must consolidate into one run for the build, exactly as
        // non-orchestrated test hosts already do. The peers are given distinct process ids so that each
        // registers its own participant lease — with a shared id they would silently overwrite one
        // another's and this test would pass even if joiners never registered at all.
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestRunOrchestratorLifetime first = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient firstClient, out _, CreateEnvironmentMockWithSettableRunId(processId: GetAliveProcessId()));
        AzureDevOpsTestRunOrchestratorLifetime second = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient secondClient, out _, CreateEnvironmentMockWithSettableRunId(processId: GetAliveProcessId() + 1));
        firstClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4249);
        secondClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(-1);

        await first.BeforeRunAsync(CancellationToken.None);
        await second.BeforeRunAsync(CancellationToken.None);

        // One run for the build, and the peer joined it rather than creating its own.
        Assert.HasCount(1, firstClient.CreateTestRunCalls);
        Assert.IsEmpty(secondClient.CreateTestRunCalls);
        Assert.AreEqual(4249, first.RunId);
        Assert.AreEqual(4249, second.RunId);

        // Both peers are registered participants, so the owner knows it must wait for the peer.
        Assert.HasCount(2, Directory.GetFiles(directory.Path, "azdo-runid.*.participant.*.json", SearchOption.TopDirectoryOnly));

        // The joiner must not close a run it does not own.
        await second.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);
        Assert.IsEmpty(secondClient.UpdateTestRunStateCalls);
        Assert.HasCount(1, Directory.GetFiles(directory.Path, "azdo-runid.*.participant.*.json", SearchOption.TopDirectoryOnly));

        // Only once every participant is gone does the owner complete the run, exactly once.
        await first.AfterRunAsync(0 /* ExitCode.Success */, CancellationToken.None);
        Assert.HasCount(1, firstClient.UpdateTestRunStateCalls);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.CompletedTestRunState, firstClient.UpdateTestRunStateCalls[0].State);
    }

    [TestMethod]
    public async Task RunIdCoordinator_OwnerLeaseExpiredButOwnerProcessAlive_PeerJoinsInsteadOfCreatingASecondRun()
    {
        // An owner that never renews its lease (a run orchestrator holds one for the whole orchestration)
        // still owns the run. Taking over from it once the wall-clock expiry passes would create a second
        // Azure DevOps run for the same build and orphan the first.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        AzureDevOpsRunIdCoordinator owner = new(new SystemFileSystem(), new FakeTask(), clock, CreateEnvironmentMock(processId: GetAliveProcessId()).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun ownedRun = await owner.AcquireRunAsync(configuration, _ => Task.FromResult(900), CancellationToken.None);
        Assert.IsTrue(ownedRun.IsOwner);

        // Move past the lease expiry without the owner ever renewing, then let a peer try to acquire.
        clock.UtcNow += TimeSpan.FromHours(5);
        AzureDevOpsRunIdCoordinator peer = new(new SystemFileSystem(), new FakeTask(), clock, CreateEnvironmentMock(processId: GetAliveProcessId() + 1).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun peerRun = await peer.AcquireRunAsync(configuration, _ => Task.FromResult(-1), CancellationToken.None);

        Assert.IsFalse(peerRun.IsOwner);
        Assert.AreEqual(900, peerRun.RunId);
    }

    [TestMethod]
    public async Task RunIdCoordinator_FinalizeRunAsync_DoesNotCloseTheRunWhileAPeerProcessIsStillAlive()
    {
        // Consolidating several projects into one run is only safe if the owner does not close it while a
        // peer is still publishing: Azure DevOps rejects everything sent to a completed run, so results
        // that previously reached a separate run would simply be lost.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        // A peer participant whose lease names a process that is genuinely running, under a file name that
        // cannot collide with the owner's own participant file. The owner reports a different process id so
        // the peer is a genuine third party rather than the owner's own lease.
        int ownerProcessId = GetAliveProcessId() + 1;
        string peerParticipantPath = Path.Combine(directory.Path, "azdo-runid.123.participant.999999.json");
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(peerParticipantPath, $"{{\"processId\":{GetAliveProcessId()},\"buildId\":123,\"expiresAt\":\"{clock.UtcNow.AddHours(4):O}\"}}");

        int delayCount = 0;
        FakeTask ownerTask = new(_ =>
        {
            delayCount++;

            // Each poll costs 20s of wall clock, so the 30s grace period lapses on the second one. The
            // peer only leaves much later; a coordinator that stops waiting for live peers would have
            // completed the run long before that.
            clock.UtcNow += TimeSpan.FromSeconds(20);
            if (delayCount == 5)
            {
                File.Delete(peerParticipantPath);
            }
        });

        AzureDevOpsRunIdCoordinator owner = new(new SystemFileSystem(), ownerTask, clock, CreateEnvironmentMock(processId: ownerProcessId).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun ownedRun = await owner.AcquireRunAsync(configuration, _ => Task.FromResult(950), CancellationToken.None);
        Assert.IsTrue(ownedRun.IsOwner);

        int finalizeCalls = 0;
        bool peerStillRegisteredWhenRunWasClosed = false;
        await owner.FinalizeRunAsync(
            ownedRun,
            _ =>
            {
                peerStillRegisteredWhenRunWasClosed = File.Exists(peerParticipantPath);
                finalizeCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.IsFalse(peerStillRegisteredWhenRunWasClosed, "The run was completed while a live peer was still registered as a participant.");
        Assert.AreEqual(1, finalizeCalls);
        Assert.IsGreaterThanOrEqualTo(5, delayCount);
    }

    [TestMethod]
    public async Task RunIdCoordinator_FinalizeRunAsync_OwnParticipantLeaseSurvivingDeletion_DoesNotWaitOnItself()
    {
        // Removing the owner's own lease is best-effort (an antivirus or indexer holding the handle makes
        // it fail, which TryDeleteFile deliberately tolerates). The owner trivially passes every liveness
        // check, so it must not treat its own leftover file as a peer that is still publishing — that
        // would hold the run open for the whole hard cap for no reason.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        int delayCount = 0;
        FakeTask task = new(_ =>
        {
            delayCount++;
            clock.UtcNow += TimeSpan.FromSeconds(20);
        });
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        UndeletableFileSystem fileSystem = new();
        AzureDevOpsRunIdCoordinator owner = new(fileSystem, task, clock, CreateEnvironmentMock(processId: GetAliveProcessId()).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun ownedRun = await owner.AcquireRunAsync(configuration, _ => Task.FromResult(960), CancellationToken.None);

        // From here every delete fails, so the owner's own participant lease stays on disk.
        fileSystem.FailDeletes = true;

        int finalizeCalls = 0;
        await owner.FinalizeRunAsync(
            ownedRun,
            _ =>
            {
                finalizeCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.IsTrue(File.Exists(ownedRun.ParticipantFilePath), "The test did not actually reproduce a surviving lease.");
        Assert.AreEqual(1, finalizeCalls);
        Assert.AreEqual(0, delayCount, "The owner polled instead of completing immediately, so it was waiting on its own lease.");
    }

    [TestMethod]
    public async Task RunIdCoordinator_AcquireRunAsync_RunIdFileWriteFailsUnderCancellation_StillReturnsTheCreatedRun()
    {
        // Once the run exists in Azure DevOps this process is the only one that can close it. If anything
        // escapes the run-id file write - including cancellation, or a log provider throwing while
        // reporting the write failure - AcquireRunAsync's cleanup deletes the leases and never hands the
        // run back, leaving it "InProgress" forever.
        using TestDirectory directory = CreateTestDirectory();
        using CancellationTokenSource canceled = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        UnwritableFileSystem fileSystem = new();
        CollectingLogger logger = new();
        AzureDevOpsRunIdCoordinator coordinator = new(fileSystem, new FakeTask(), clock, CreateEnvironmentMock(processId: GetAliveProcessId()).Object, logger, options);

        AzureDevOpsCoordinatedRun run = await coordinator.AcquireRunAsync(
            configuration,
            _ =>
            {
                // The run now exists remotely. Everything after this point must preserve ownership.
                fileSystem.FailRunIdFileWrites = true;
                logger.ThrowOnLog = true;
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; this project also targets .NET Framework.
                canceled.Cancel();
#pragma warning restore VSTHRD103
                return Task.FromResult(970);
            },
            canceled.Token);

        Assert.AreEqual(970, run.RunId);
        Assert.IsTrue(run.IsOwner);
    }

    [TestMethod]
    public async Task RunIdCoordinator_OwnerLeaseWithoutBuildContext_CanStillBeTakenOverEvenWhenThatPidIsAlive()
    {
        // A lease carrying the sentinel build id has no build context to vouch for it - that is how
        // ReadLease reports the legacy plain-pid format, which it deliberately marks replaceable. A pid
        // that has since been reused must not be mistaken for a live owner, or takeover is blocked forever.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };

        // Advance the clock on every poll so that if takeover ever regresses the joiner wait reaches its
        // deadline and the test fails, instead of spinning forever against a frozen clock.
        FakeTask task = new(_ => clock.UtcNow += TimeSpan.FromSeconds(10));
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(
            Path.Combine(directory.Path, "azdo-runid.123.owner"),
            JsonSerializer.Serialize(new AzureDevOpsLeaseFile(GetAliveProcessId(), 0, clock.UtcNow.AddHours(-1))));

        AzureDevOpsRunIdCoordinator coordinator = new(new SystemFileSystem(), task, clock, CreateEnvironmentMock(processId: GetAliveProcessId() + 1).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun run = await coordinator.AcquireRunAsync(configuration, _ => Task.FromResult(980), CancellationToken.None);

        Assert.IsTrue(run.IsOwner, "A lease with no build context should never block takeover, whatever its pid is doing now.");
        Assert.AreEqual(980, run.RunId);
    }

    [TestMethod]
    public async Task RunIdCoordinator_FinalizeRunAsync_TimeoutWarningThrows_StillClosesTheRun()
    {
        // The drain timeout warning sits directly in front of the call that completes the run. A log
        // provider throwing there would skip it and leave the run "InProgress", which is exactly what the
        // warning is reporting on.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        CollectingLogger logger = new();
        FakeTask task = new(timeSpan => clock.UtcNow += timeSpan);
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromSeconds(5), 5, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(30), TimeSpan.FromHours(4));
        int aliveProcessId = GetAliveProcessId();
        AzureDevOpsRunIdCoordinator coordinator = new(new SystemFileSystem(), task, clock, CreateEnvironmentMock(processId: aliveProcessId).Object, logger, options);

        string ownerFilePath = Path.Combine(directory.Path, "azdo-runid.123.owner");
        string runIdFilePath = Path.Combine(directory.Path, "azdo-runid.123.json");
        string participantFilePath = Path.Combine(directory.Path, $"azdo-runid.123.participant.{aliveProcessId}.json");
        File.WriteAllText(ownerFilePath, JsonSerializer.Serialize(new AzureDevOpsLeaseFile(aliveProcessId, 123, clock.UtcNow.AddHours(1))));
        File.WriteAllText(runIdFilePath, JsonSerializer.Serialize(new AzureDevOpsRunIdFile(5, 123, "https://dev.azure.com/org/", "project", clock.UtcNow.AddHours(1))));

        // A participant that cannot be vouched for, so the short grace period lapses and the warning fires.
        File.WriteAllText(Path.Combine(directory.Path, $"azdo-runid.123.participant.{int.MaxValue - 1}.json"), "not-json");
        logger.ThrowOnLog = true;

        int finalizeCalls = 0;
        await coordinator.FinalizeRunAsync(
            new AzureDevOpsCoordinatedRun(5, true, 123, directory.Path, runIdFilePath, ownerFilePath, participantFilePath),
            _ =>
            {
                finalizeCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(1, finalizeCalls);
    }

    [TestMethod]
    public async Task RunIdCoordinator_FinalizeRunAsync_LivePeerThatNeverLeaves_StillFinalizesAtTheHardCap()
    {
        // The hard cap is what stops a leaked but still-running process from holding a run open forever.
        // Without it the owner would keep renewing the grace period against a live peer indefinitely.
        using TestDirectory directory = CreateTestDirectory();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        AzureDevOpsTestResultsPublisherOptions options = new(10, TimeSpan.FromMinutes(1), 2, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromHours(4));
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "storage", directory.Path);

        // A peer that stays registered for the whole test and whose lease names a live process.
        int ownerProcessId = GetAliveProcessId() + 1;
        string peerParticipantPath = Path.Combine(directory.Path, "azdo-runid.123.participant.999998.json");
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(peerParticipantPath, $"{{\"processId\":{GetAliveProcessId()},\"buildId\":123,\"expiresAt\":\"{clock.UtcNow.AddHours(4):O}\"}}");

        int delayCount = 0;
        int maxPolls = (int)(options.CoordinationFinalizeMaxWaitTime.TotalSeconds / 20) + 10;
        FakeTask task = new(_ =>
        {
            delayCount++;

            // Fail loudly rather than spinning forever if the hard cap ever stops bounding the wait: a
            // hung suite is far harder to diagnose than a failed assertion.
            Assert.IsLessThanOrEqualTo(maxPolls, delayCount, "The owner never stopped waiting for a live peer, so the hard cap is not bounding the drain loop.");
            clock.UtcNow += TimeSpan.FromSeconds(20);
        });

        AzureDevOpsRunIdCoordinator owner = new(new SystemFileSystem(), task, clock, CreateEnvironmentMock(processId: ownerProcessId).Object, new CollectingLogger(), options);
        AzureDevOpsCoordinatedRun ownedRun = await owner.AcquireRunAsync(configuration, _ => Task.FromResult(990), CancellationToken.None);

        int finalizeCalls = 0;
        await owner.FinalizeRunAsync(
            ownedRun,
            _ =>
            {
                finalizeCalls++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.AreEqual(1, finalizeCalls);
        Assert.IsTrue(File.Exists(peerParticipantPath), "The peer was supposed to outlive the wait, so the hard cap is what released it.");

        // It waited well past the short grace period rather than giving up on a live peer immediately,
        // and still stopped rather than waiting forever.
        Assert.IsGreaterThan((int)(options.CoordinationFinalizeTimeout.TotalSeconds / 20), delayCount);
        Assert.IsLessThanOrEqualTo((int)(options.CoordinationFinalizeMaxWaitTime.TotalSeconds / 20) + 2, delayCount);
    }

    #endregion

    #region Retry attempts as sub-results of one result (https://github.com/microsoft/testfx/issues/10400)

    [TestMethod]
    public async Task InProcessRetry_PublishesEveryAttemptAndTargetsFailedAttemptAttachments()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            new AzureDevOpsTestResultsPublisherOptions(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)),
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _);
        AppendingAzureDevOpsService service = new();
        service.Connect(client);

        TestNode failedNode = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime,
            new TimingProperty(new TimingInfo(
                RetryTestStartTime,
                RetryTestStartTime + TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1))));
        failedNode.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        failedNode.Properties.Add(new StandardOutputProperty("failed attempt output"));

        TestNode passedNode = CreateNode(
            "MyTest",
            new PassedTestNodeStateProperty(),
            RetryTestStartTime + TimeSpan.FromSeconds(1),
            new TimingProperty(new TimingInfo(
                RetryTestStartTime + TimeSpan.FromSeconds(1),
                RetryTestStartTime + TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2))));
        passedNode.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(failedNode), CancellationToken.None);

        Assert.IsEmpty(service.SubResults, "A superseded attempt must wait for the final outcome before publishing.");

        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(passedNode), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestCaseResult parent = client.UpdateTestResultsCalls.Single().Results.Single();
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, parent.Outcome);
        Assert.AreEqual(3_000L, parent.DurationInMs);
        Assert.HasCount(2, service.SubResults);
        Assert.AreEqual("Attempt# 0 - MyTest", service.SubResults[0].DisplayName);
        Assert.AreEqual("Attempt# 1 - MyTest", service.SubResults[1].DisplayName);
        Assert.ContainsSingle(service.SubResults[0].Attachments);
        Assert.AreEqual("stdout.log", service.SubResults[0].Attachments[0].FileName);
        Assert.IsEmpty(service.SubResults[1].Attachments);
        Assert.IsEmpty(service.ParentAttachments);
    }

    [TestMethod]
    public async Task InProcessAndOutOfProcessRetries_ProduceOneOrderedAttemptHistory()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        AppendingAzureDevOpsService service = new();

        AzureDevOpsTestResultsPublisher firstHost = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient firstClient,
            out _,
            out _,
            environment);
        service.Connect(firstClient);

        TestNode firstFailure = CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("first")), RetryTestStartTime);
        firstFailure.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        firstFailure.Properties.Add(new StandardOutputProperty("first output"));
        TestNode secondFailure = CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("second")), RetryTestStartTime);
        secondFailure.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));
        secondFailure.Properties.Add(new StandardOutputProperty("second output"));

        await StartPublisherAsync(firstHost);
        await firstHost.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstFailure), CancellationToken.None);
        await firstHost.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(secondFailure), CancellationToken.None);
        await firstHost.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestResultsPublisher secondHost = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient secondClient,
            out _,
            out _,
            environment);
        service.Connect(secondClient);

        TestNode thirdFailure = CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("third")), RetryTestStartTime);
        thirdFailure.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        thirdFailure.Properties.Add(new StandardOutputProperty("third output"));
        TestNode finalPass = CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime);
        finalPass.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));

        await StartPublisherAsync(secondHost);
        await secondHost.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(thirdFailure), CancellationToken.None);
        await secondHost.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(finalPass), CancellationToken.None);
        await secondHost.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(4, service.SubResults);
        for (int i = 0; i < service.SubResults.Count; i++)
        {
            Assert.AreEqual(i + 1, service.SubResults[i].SequenceId);
            Assert.AreEqual($"Attempt# {i.ToString(CultureInfo.InvariantCulture)} - MyTest", service.SubResults[i].DisplayName);
        }

        Assert.ContainsSingle(service.SubResults[0].Attachments);
        Assert.ContainsSingle(service.SubResults[1].Attachments);
        Assert.ContainsSingle(service.SubResults[2].Attachments);
        Assert.IsEmpty(service.SubResults[3].Attachments);
        Assert.IsEmpty(service.ParentAttachments);
    }

    [TestMethod]
    public async Task InProcessRetry_ThreeAttemptsPublishOrderedHistoryAndAttachments()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            new AzureDevOpsTestResultsPublisherOptions(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)),
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _);
        AppendingAzureDevOpsService service = new();
        service.Connect(client);

        TestNode firstAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime,
            new TimingProperty(new TimingInfo(
                RetryTestStartTime,
                RetryTestStartTime + TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1))));
        firstAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        firstAttempt.Properties.Add(new StandardOutputProperty("first output"));

        TestNode secondAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("second")),
            RetryTestStartTime + TimeSpan.FromSeconds(1),
            new TimingProperty(new TimingInfo(
                RetryTestStartTime + TimeSpan.FromSeconds(1),
                RetryTestStartTime + TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2))));
        secondAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: true));
        secondAttempt.Properties.Add(new StandardOutputProperty("second output"));

        TestNode finalAttempt = CreateNode(
            "MyTest",
            new PassedTestNodeStateProperty(),
            RetryTestStartTime + TimeSpan.FromSeconds(3),
            new TimingProperty(new TimingInfo(
                RetryTestStartTime + TimeSpan.FromSeconds(3),
                RetryTestStartTime + TimeSpan.FromSeconds(6),
                TimeSpan.FromSeconds(3))));
        finalAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 3, isSuperseded: false));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstAttempt), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(secondAttempt), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(finalAttempt), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestCaseResult parent = client.UpdateTestResultsCalls.Single().Results.Single();
        Assert.AreEqual(6_000L, parent.DurationInMs);
        Assert.HasCount(3, service.SubResults);
        for (int i = 0; i < service.SubResults.Count; i++)
        {
            Assert.AreEqual(i + 1, service.SubResults[i].SequenceId);
            Assert.AreEqual($"Attempt# {i.ToString(CultureInfo.InvariantCulture)} - MyTest", service.SubResults[i].DisplayName);
        }

        Assert.ContainsSingle(service.SubResults[0].Attachments);
        Assert.AreEqual("first output", service.SubResults[0].Attachments[0].InlineContent);
        Assert.ContainsSingle(service.SubResults[1].Attachments);
        Assert.AreEqual("second output", service.SubResults[1].Attachments[0].InlineContent);
        Assert.IsEmpty(service.SubResults[2].Attachments);
        Assert.IsEmpty(service.ParentAttachments);
    }

    [TestMethod]
    public async Task InProcessRetry_DuplicateFoldedRowsPublishEveryExecutionIndependently()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>(Enumerable.Range(100, results.Count).ToArray());
        };

        TestNode firstRowAttempt = CreateNode(
            "SharedUid",
            new FailedTestNodeStateProperty(new InvalidOperationException("row A first")),
            RetryTestStartTime,
            displayName: "Duplicate title");
        firstRowAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        firstRowAttempt.Properties.Add(new StandardOutputProperty("row A output"));

        TestNode secondRowAttempt = CreateNode(
            "SharedUid",
            new FailedTestNodeStateProperty(new InvalidOperationException("row B first")),
            RetryTestStartTime,
            displayName: "Duplicate title");
        secondRowAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        secondRowAttempt.Properties.Add(new StandardOutputProperty("row B output"));

        TestNode firstRowFinal = CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title");
        firstRowFinal.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));
        TestNode secondRowFinal = CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title");
        secondRowFinal.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstRowAttempt), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(secondRowAttempt), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstRowFinal), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(secondRowFinal), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(4, created);
        Assert.AreEqual("row A first", created[0].ErrorMessage);
        Assert.AreEqual("row B first", created[1].ErrorMessage);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, created[2].Outcome);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, created[3].Outcome);
        Assert.IsTrue(created.All(result => result.SubResults is null));
        Assert.IsEmpty(client.UpdateTestResultsCalls);
        Assert.HasCount(2, client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(100, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.AreEqual("row A output", client.UploadTestResultAttachmentCalls[0].Attachment.InlineContent);
        Assert.AreEqual(101, client.UploadTestResultAttachmentCalls[1].TestCaseResultId);
        Assert.AreEqual("row B output", client.UploadTestResultAttachmentCalls[1].Attachment.InlineContent);
    }

    [TestMethod]
    public async Task InProcessRetry_IncompleteSequencePublishesExecutedAttemptAtSessionEnd()
    {
        using TestDirectory directory = CreateTestDirectory();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([123]);
        };

        TestNode failedAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        failedAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        failedAttempt.Properties.Add(new StandardOutputProperty("failed attempt output"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(failedAttempt), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestCaseResult result = Assert.ContainsSingle(created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, result.Outcome);
        Assert.IsNull(result.SubResults);
        Assert.ContainsSingle(client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(123, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("failed attempt output", client.UploadTestResultAttachmentCalls[0].Attachment.InlineContent);
    }

    [TestMethod]
    public async Task InProcessRetry_CanceledIncompleteSequenceIsCountedAsUnpublished()
    {
        using TestDirectory directory = CreateTestDirectory();
        CollectingOutputDevice outputDevice = new();
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            outputDevice: outputDevice);
        TestNode failedAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        failedAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(failedAttempt), CancellationToken.None);

        using var cancellationTokenSource = new CancellationTokenSource();
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; this project also targets .NET Framework.
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(cancellationTokenSource.Token));

        Assert.IsEmpty(client.UpdateTestResultsCalls);
        string expectedWarning = string.Format(
            CultureInfo.InvariantCulture,
            AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped,
            1);
        Assert.Contains(expectedWarning, outputDevice.Warnings);
    }

    [TestMethod]
    public async Task RetryAttempt_UpdatesTheResultTheEarlierAttemptCreatedInsteadOfAddingAnother()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();

        // The orchestrator publishes where the map lives; without it the attempts cannot find each other.
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        // Attempt 1: the test fails, and is created as a new result.
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient firstClient, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        firstClient.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([777]);
        };

        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), RetryTestStartTime)),
            CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, created[0].Outcome);
        Assert.IsNull(created[0].Id, "A test seen for the first time is created, not updated.");

        // Attempt 2: the same test passes. It must land on the result attempt 1 created.
        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient secondClient, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> createdBySecondAttempt = [];
        secondClient.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            createdBySecondAttempt.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([888]);
        };

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsEmpty(createdBySecondAttempt, "The retry attempt must not add a second result for the same test.");
        Assert.HasCount(1, secondClient.UpdateTestResultsCalls);

        AzureDevOpsTestCaseResult parent = secondClient.UpdateTestResultsCalls[0].Results.Single();
        Assert.AreEqual(777, parent.Id, "The update has to address the result the first attempt created.");
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.RerunResultGroupType, parent.ResultGroupType);

        // Latest attempt wins: the test ultimately passed, which is what the pipeline's exit code says too.
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, parent.Outcome);

        // The failure is not erased, it becomes the first attempt — that is what makes the flakiness visible.
        Assert.IsNotNull(parent.SubResults);
        Assert.HasCount(2, parent.SubResults);
        Assert.AreEqual(1, parent.SubResults[0].SequenceId);
        Assert.AreEqual("Attempt# 0 - MyTest", parent.SubResults[0].DisplayName);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, parent.SubResults[0].Outcome);
        Assert.AreEqual("boom", parent.SubResults[0].ErrorMessage);
        Assert.AreEqual(2, parent.SubResults[1].SequenceId);
        Assert.AreEqual("Attempt# 1 - MyTest", parent.SubResults[1].DisplayName);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, parent.SubResults[1].Outcome);
    }

    [TestMethod]
    public async Task RetryAttempt_ThatFailsAgain_KeepsTheParentFailedAndRecordsBothAttempts()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("first")), resultId: 21);

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("second")), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestCaseResult parent = client.UpdateTestResultsCalls.Single().Results.Single();
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, parent.Outcome);
        Assert.AreEqual("second", parent.ErrorMessage, "The parent reports the attempt that decided the outcome.");
        Assert.IsNotNull(parent.SubResults);
        IReadOnlyList<AzureDevOpsTestSubResult> subResults = parent.SubResults;
        Assert.HasCount(2, subResults);
        Assert.AreEqual(1, subResults[0].SequenceId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, subResults[0].Outcome);
        Assert.AreEqual("first", subResults[0].ErrorMessage);
        Assert.AreEqual(2, subResults[1].SequenceId);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, subResults[1].Outcome);
        Assert.AreEqual("second", subResults[1].ErrorMessage);
    }

    [TestMethod]
    public async Task RetryAttempt_AttachmentTargetsItsSubResultAndKeepsOriginalName()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 25);

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
        {
            IReadOnlyDictionary<int, int> subResultIds = new Dictionary<int, int>
            {
                [1] = 201,
                [2] = 202,
            };
            return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([
                new AzureDevOpsPublishedTestResult(results.Single().Id!.Value, subResultIds),
            ]);
        };
        TestNode node = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("second")),
            RetryTestStartTime);
        node.Properties.Add(new StandardOutputProperty("retry output"));

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(25, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.AreEqual(202, client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("stdout.log", client.UploadTestResultAttachmentCalls[0].Attachment.FileName);
        Assert.HasCount(1, client.UpdateTestResultsCalls);
        Assert.AreEqual(2, client.UpdateTestResultsCalls[0].Results.Single().SubResults![1].SequenceId);
    }

    [TestMethod]
    public async Task FirstAttempt_AttachmentTargetsFirstSubResultAndKeepsOriginalName()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        AzureDevOpsTestCaseResult? createdResult = null;
        AzureDevOpsTestCaseResult? seededResult = null;
        client.PublishTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
        {
            createdResult = results.Single();
            return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([
                new AzureDevOpsPublishedTestResult(1, new Dictionary<int, int>()),
            ]);
        };
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
        {
            seededResult = results.Single();
            IReadOnlyDictionary<int, int> subResultIds = new Dictionary<int, int> { [1] = 101 };
            return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([new AzureDevOpsPublishedTestResult(1, subResultIds)]);
        };
        TestNode node = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        node.Properties.Add(new StandardOutputProperty("first output"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        Assert.IsNotNull(createdResult);
        Assert.IsNull(createdResult.ResultGroupType);
        Assert.IsNull(createdResult.SubResults);
        Assert.IsNotNull(seededResult);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.RerunResultGroupType, seededResult.ResultGroupType);
        Assert.IsNotNull(seededResult.SubResults);
        Assert.ContainsSingle(seededResult.SubResults);
        Assert.AreEqual(1, seededResult.SubResults[0].SequenceId);
        Assert.AreEqual(101, client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("stdout.log", client.UploadTestResultAttachmentCalls[0].Attachment.FileName);
    }

    [TestMethod]
    public async Task RetryAttempt_AgainstAppendingAzureDevOps_DoesNotReplayFirstAttemptOrLoseItsAttachment()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        AppendingAzureDevOpsService service = new();

        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient firstClient,
            out _,
            out _,
            environment);
        service.Connect(firstClient);
        TestNode failedNode = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        failedNode.Properties.Add(new StandardOutputProperty("first output"));

        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(failedNode), CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient secondClient,
            out _,
            out _,
            environment);
        service.Connect(secondClient);
        TestNode secondFailedNode = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("second")),
            RetryTestStartTime);
        secondFailedNode.Properties.Add(new StandardOutputProperty("second output"));

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(secondFailedNode),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, service.SubResults);
        Assert.AreEqual(1, service.SubResults[0].SequenceId);
        Assert.AreEqual("Attempt# 0 - MyTest", service.SubResults[0].DisplayName);
        Assert.AreEqual(2, service.SubResults[1].SequenceId);
        Assert.AreEqual("Attempt# 1 - MyTest", service.SubResults[1].DisplayName);
        Assert.HasCount(1, service.SubResults[0].Attachments);
        Assert.AreEqual("stdout.log", service.SubResults[0].Attachments[0].FileName);
        Assert.HasCount(1, service.SubResults[1].Attachments);
        Assert.AreEqual("stdout.log", service.SubResults[1].Attachments[0].FileName);
        Assert.IsEmpty(service.ParentAttachments);

        Assert.HasCount(1, secondClient.UpdateTestResultsCalls);
        IReadOnlyList<AzureDevOpsTestSubResult> appended = secondClient.UpdateTestResultsCalls[0].Results.Single().SubResults!;
        Assert.ContainsSingle(appended);
        Assert.AreEqual(2, appended[0].SequenceId);
    }

    [TestMethod]
    public async Task RetryAttempt_ParentDurationIncludesAllAttempts()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        TimingProperty firstTiming = new(new TimingInfo(
            RetryTestStartTime,
            RetryTestStartTime + TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30)));
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient firstClient,
            out _,
            out _,
            environment);
        firstClient.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([26]);
        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode(
                "MyTest",
                new FailedTestNodeStateProperty(new InvalidOperationException("first")),
                RetryTestStartTime,
                firstTiming)),
            CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        TimingProperty secondTiming = new(new TimingInfo(
            RetryTestStartTime + TimeSpan.FromSeconds(30),
            RetryTestStartTime + TimeSpan.FromSeconds(31),
            TimeSpan.FromSeconds(1)));
        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient secondClient,
            out _,
            out _,
            environment);
        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime, secondTiming)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestCaseResult parent = secondClient.UpdateTestResultsCalls.Single().Results.Single();
        Assert.AreEqual(31_000, parent.DurationInMs);
        Assert.AreEqual(RetryTestStartTime, parent.StartedDate);
        Assert.AreEqual(RetryTestStartTime + TimeSpan.FromSeconds(31), parent.CompletedDate);
        Assert.AreEqual(30_000, parent.SubResults![0].DurationInMs);
        Assert.AreEqual(1_000, parent.SubResults[1].DurationInMs);
    }

    [TestMethod]
    public async Task FoldedDataDrivenRows_SharingUidUpdateTheirOwnResults()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient firstClient, out _, out _, environment);
        firstClient.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([401, 402]);
        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode(
                "SharedUid",
                new PassedTestNodeStateProperty(),
                RetryTestStartTime,
                displayName: "MyTest(1)")),
            CancellationToken.None);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode(
                "SharedUid",
                new FailedTestNodeStateProperty(new InvalidOperationException("row two")),
                RetryTestStartTime,
                displayName: "MyTest(2)")),
            CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient secondClient, out _, out _, environment);
        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "MyTest(1)")),
            CancellationToken.None);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "MyTest(2)")),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, secondClient.UpdateTestResultsCalls);
        IReadOnlyList<AzureDevOpsTestCaseResult> updated = secondClient.UpdateTestResultsCalls[0].Results;
        Assert.HasCount(2, updated);
        Assert.AreEqual(401, updated.Single(result => result.TestCaseTitle == "MyTest(1)").Id);
        Assert.AreEqual(402, updated.Single(result => result.TestCaseTitle == "MyTest(2)").Id);
    }

    [TestMethod]
    public async Task FoldedDataDrivenRows_SharingUidAndTitleFallBackToCreates()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient firstClient, out _, out _, environment);
        firstClient.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([411, 412]);
        await StartPublisherAsync(firstAttempt);
        for (int i = 0; i < 2; i++)
        {
            await firstAttempt.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode(
                    "SharedUid",
                    new FailedTestNodeStateProperty(new InvalidOperationException($"row {i}")),
                    RetryTestStartTime,
                    displayName: "Duplicate title")),
                CancellationToken.None);
        }

        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient secondClient, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        secondClient.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([413, 414]);
        };
        await StartPublisherAsync(secondAttempt);
        for (int i = 0; i < 2; i++)
        {
            await secondAttempt.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title")),
                CancellationToken.None);
        }

        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, created);
        Assert.IsEmpty(secondClient.UpdateTestResultsCalls, "Ambiguous rows must never be PATCHed to a guessed parent.");
    }

    [TestMethod]
    public async Task NewlyDuplicatedFoldedRow_FallsBackToCreates()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "SharedUid",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 421,
            displayName: "Duplicate title");

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([422, 423]);
        };
        await StartPublisherAsync(secondAttempt);
        for (int i = 0; i < 2; i++)
        {
            await secondAttempt.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title")),
                CancellationToken.None);
        }

        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, created);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "Repeated matches to one parent must never produce duplicate PATCH ids.");
    }

    [TestMethod]
    public async Task NewlyCreatedDuplicateInLaterBatch_FallsBackToCreate()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisherOptions options = new(1, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        int nextId = 471;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([nextId++]);
        };
        await StartPublisherAsync(publisher);

        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode(
                "SharedUid",
                new FailedTestNodeStateProperty(new InvalidOperationException("first")),
                RetryTestStartTime,
                displayName: "Duplicate title")),
            CancellationToken.None);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title")),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, created);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "A parent created in this attempt must never be reused by another row.");
    }

    [TestMethod]
    public async Task MapEntriesSharingResultId_AreIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        File.WriteAllText(
            mapPath,
            """
            {"buildId":123,"runId":42,"results":[
              {"storage":"tests","name":"First","title":"First","id":431,"attempts":[{"sequenceId":1,"displayName":"First","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":"tests","name":"First","title":"First","id":432,"attempts":[{"sequenceId":1,"displayName":"First","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":"tests","name":"First","title":"First","id":433,"attempts":[{"sequenceId":1,"displayName":"First","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":"tests","name":"Second","title":"Second","id":433,"attempts":[{"sequenceId":1,"displayName":"Second","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":"tests","name":"Malformed","title":"Malformed","id":434,"attempts":null},
              {"storage":"tests","name":"Third","title":"Third","id":434,"attempts":[{"sequenceId":1,"displayName":"Third","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":null,"name":"MalformedKey","title":"MalformedKey","id":435,"attempts":[{"sequenceId":1,"displayName":"MalformedKey","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0},
              {"storage":"tests","name":"Fourth","title":"Fourth","id":435,"attempts":[{"sequenceId":1,"displayName":"Fourth","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":0}
            ]}
            """);

        AzureDevOpsResultIdStore store = await AzureDevOpsResultIdStore.OpenAsync(new SystemFileSystem(), new CollectingLogger(), mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult first = new("First", "tests", "First", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        AzureDevOpsTestCaseResult second = new("Second", "tests", "Second", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        AzureDevOpsTestCaseResult third = new("Third", "tests", "Third", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        AzureDevOpsTestCaseResult fourth = new("Fourth", "tests", "Fourth", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);

        Assert.IsNull(store.TryGet(first));
        Assert.IsNull(store.TryGet(second));
        Assert.IsNull(store.TryGet(third));
        Assert.IsNull(store.TryGet(fourth));
    }

    [TestMethod]
    public async Task MapEntryWithTerminalSequenceId_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        File.WriteAllText(
            mapPath,
            """
            {"buildId":123,"runId":42,"results":[
              {"storage":"tests","name":"MyTest","title":"MyTest","id":441,"attempts":[{"sequenceId":2147483647,"displayName":"MyTest","outcome":"Failed","durationInMs":1}]}
            ]}
            """);

        AzureDevOpsResultIdStore store = await AzureDevOpsResultIdStore.OpenAsync(new SystemFileSystem(), new CollectingLogger(), mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);

        Assert.IsNull(store.TryGet(result));
    }

    [TestMethod]
    public async Task MapEntryWithInvalidPublishedSubResultSequence_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        File.WriteAllText(
            mapPath,
            """
            {"buildId":123,"runId":42,"results":[
              {"storage":"tests","name":"Negative","title":"Negative","id":451,"attempts":[{"sequenceId":1,"displayName":"Negative","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":-1},
              {"storage":"tests","name":"PastHistory","title":"PastHistory","id":452,"attempts":[{"sequenceId":1,"displayName":"PastHistory","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":2}
            ]}
            """);

        AzureDevOpsResultIdStore store = await AzureDevOpsResultIdStore.OpenAsync(new SystemFileSystem(), new CollectingLogger(), mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult negative = new("Negative", "tests", "Negative", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        AzureDevOpsTestCaseResult pastHistory = new("PastHistory", "tests", "PastHistory", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);

        Assert.IsNull(store.TryGet(negative));
        Assert.IsNull(store.TryGet(pastHistory));
    }

    [TestMethod]
    public async Task MapEntryWithMissingOrStalePublishedSubResultSequence_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        File.WriteAllText(
            mapPath,
            """
            {"buildId":123,"runId":42,"results":[
              {"storage":"tests","name":"Missing","title":"Missing","id":453,"attempts":[{"sequenceId":1,"displayName":"Missing","outcome":"Failed","durationInMs":1}]},
              {"storage":"tests","name":"Stale","title":"Stale","id":454,"attempts":[{"sequenceId":1,"displayName":"Stale 1","outcome":"Failed","durationInMs":1},{"sequenceId":2,"displayName":"Stale 2","outcome":"Failed","durationInMs":1}],"lastPublishedSubResultSequenceId":1}
            ]}
            """);

        AzureDevOpsResultIdStore store = await AzureDevOpsResultIdStore.OpenAsync(new SystemFileSystem(), new CollectingLogger(), mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult missing = new("Missing", "tests", "Missing", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        AzureDevOpsTestCaseResult stale = new("Stale", "tests", "Stale", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);

        Assert.IsNull(store.TryGet(missing));
        Assert.IsNull(store.TryGet(stale));
    }

    [TestMethod]
    public async Task SameTestNameInTwoAssemblies_IsNotTreatedAsARerun()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), resultId: 31);

        // Same test uid, different assembly. TestNode.Uid is only unique within a test application, so
        // keying on it alone would make the second assembly's test masquerade as a rerun of the first's.
        Mock<ITestApplicationModuleInfo> otherAssembly = new();
        otherAssembly.Setup(x => x.TryGetAssemblyName()).Returns("OtherTests");
        otherAssembly.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(Path.Combine("artifacts", "OtherTests.dll"));

        AzureDevOpsTestResultsPublisher otherPublisher = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment, otherAssembly);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([32]);
        };

        await StartPublisherAsync(otherPublisher);
        await otherPublisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await otherPublisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.AreEqual("othertests.dll", created[0].AutomatedTestStorage);
        Assert.AreEqual("MyTest", created[0].AutomatedTestName);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "A different assembly is a different test, not another attempt.");
    }

    [TestMethod]
    public async Task WithoutAnOrchestrator_ResultsAreAlwaysCreated()
    {
        using TestDirectory directory = CreateTestDirectory();

        // No orchestrator ran, so no map was published and there is nothing to merge into: this is the
        // ordinary single-process run, whose behaviour must be exactly what it was before reruns existed.
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), resultId: 41);

        AzureDevOpsTestResultsPublisher secondPublisher = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([42]);
        };

        await StartPublisherAsync(secondPublisher);
        await secondPublisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondPublisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.IsEmpty(client.UpdateTestResultsCalls);
    }

    [TestMethod]
    public async Task ResultMapWithoutInheritedRun_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        string staleMapPath = Path.Combine(directory.Path, "stale-map.json");
        const string StaleMap = """{"buildId":123,"runId":999,"results":[]}""";
        File.WriteAllText(staleMapPath, StaleMap);

        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        environment
            .Setup(x => x.GetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName))
            .Returns(AzureDevOpsConstants.FormatResultMapPath(123, staleMapPath));

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(501);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([502]);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.IsEmpty(client.UpdateTestResultsCalls);
        Assert.AreEqual(StaleMap, File.ReadAllText(staleMapPath), "A self-owned run must not open or rewrite an inherited map path.");
    }

    [TestMethod]
    public async Task WhenTheCreateResponseHasNoIds_TheNextAttemptCreatesItsOwnResultRatherThanLosingIt()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        // Azure DevOps accepted the results but the response could not be parsed, so there is no id to
        // address later. Publishing a second result is worse than a rerun, but far better than dropping it.
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient firstClient, out _, out _, environment);
        firstClient.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>(null);

        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), RetryTestStartTime)),
            CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient secondClient, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        secondClient.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([52]);
        };

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.IsEmpty(secondClient.UpdateTestResultsCalls);
    }

    [TestMethod]
    public async Task WhenTheMapIsUnreadable_TheAttemptStillPublishesItsResult()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), resultId: 61);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        Assert.IsTrue(File.Exists(mapPath), "The first attempt was supposed to leave the map behind for the next one.");
        File.WriteAllText(mapPath, "{ this is not json");

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([62]);
        };

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        // Degraded to a separate result rather than to silence.
        Assert.HasCount(1, created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, created[0].Outcome);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "An unreadable map must not merge into stale history.");
    }

    [TestMethod]
    public async Task ResultMapPathHandoff_IsScopedToTheBuildAndWithdrawnWhenTheRunCloses()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);

        await lifetime.BeforeRunAsync(CancellationToken.None);
        string? inheritedMapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123);
        Assert.IsNotNull(inheritedMapPath);
        Assert.Contains(directory.Path, inheritedMapPath);

        // A value inherited from an earlier build on a reused agent must not redirect this build's updates.
        Assert.IsNull(AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 999));

        await lifetime.AfterRunAsync(0, CancellationToken.None);
        Assert.IsNull(
            AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123),
            "A process started after the run closed must not try to update results in it.");
    }

    [TestMethod]
    public async Task WhenAnUpdateFails_ForcedFlushRetriesAsCreate()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), resultId: 71);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        Assert.IsTrue(File.Exists(mapPath));

        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([72]);
        };
        client.UpdateTestResultsAsyncFunc = (_, _, _, _) => Task.FromException(new HttpRequestException("transient"));

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UpdateTestResultsCalls);
        Assert.HasCount(2, client.UpdateTestResultsCalls[0].Results.Single().SubResults!);
        Assert.HasCount(1, created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, created[0].Outcome);

        // The map is invalidated before PATCH so a crash after an accepted update can never expose stale
        // history. The forced session-end flush immediately retries the forgotten attempt as a safe create.
        Assert.IsTrue(File.Exists(mapPath));
        Assert.DoesNotContain("\"id\":71", File.ReadAllText(mapPath));
    }

    [TestMethod]
    public async Task TheMapIsWrittenOncePerSessionRatherThanOncePerBatch()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;

        // A batch size of one forces a publish per result, so a per-batch save would rewrite the whole map
        // for every test — quadratic in the size of the suite, and paid even by runs that never retry.
        AzureDevOpsTestResultsPublisherOptions options = new(1, TimeSpan.FromSeconds(5), 40, TimeSpan.FromMilliseconds(250));
        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        int nextResultId = 100;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            int[] ids = new int[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                ids[i] = nextResultId++;
            }

            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };

        await StartPublisherAsync(publisher);
        for (int i = 0; i < 5; i++)
        {
            await publisher.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode($"MyTest{i}", new FailedTestNodeStateProperty(new InvalidOperationException($"failure {i}")), RetryTestStartTime)),
                CancellationToken.None);

            Assert.IsFalse(File.Exists(mapPath), "The map is only needed by the next attempt, so it must not be rewritten as results are published.");
        }

        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsTrue(File.Exists(mapPath), "The next attempt still needs the map once the session ends.");
        string map = File.ReadAllText(mapPath);
        Assert.Contains("MyTest0", map);
        Assert.Contains("MyTest4", map);
    }

    [TestMethod]
    public async Task WhenNothingWasPublished_NoMapFileIsLeftBehind()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out _, out _, out _, environment);
        await StartPublisherAsync(publisher);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsFalse(File.Exists(mapPath), "An empty map only leaves a coordination file in the results directory for nobody to read.");
    }

    [TestMethod]
    public async Task MapFromAnotherRunInTheSameBuild_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient lifetimeClient, out _, environment);
        lifetimeClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4242);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        await PublishSingleResultAsync(directory.Path, environment, "MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), resultId: 81);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        string map = File.ReadAllText(mapPath);
        string foreignRunMap = map.Replace("\"runId\":4242", "\"runId\":9999");
        Assert.AreNotEqual(map, foreignRunMap, "The test expected the map to carry the run id.");
        File.WriteAllText(mapPath, foreignRunMap);

        AzureDevOpsTestResultsPublisher nextAttempt = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([82]);
        };

        await StartPublisherAsync(nextAttempt);
        await nextAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await nextAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.IsNull(created[0].Id);
        Assert.IsNull(created[0].SubResults);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "Result ids from another run must never be PATCHed.");
        Assert.AreEqual(foreignRunMap, File.ReadAllText(mapPath), "A foreign map path must remain read-only.");
    }

    [TestMethod]
    public async Task MapEntryWithoutAttemptHistory_IsIgnored()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient lifetimeClient, out _, environment);
        lifetimeClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(4242);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        File.WriteAllText(
            mapPath,
            """{"buildId":123,"runId":4242,"results":[null,{"storage":"MyTests","name":"MyTest","title":"MyTest","id":81,"attempts":null}]}""");

        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([82]);
        };

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.IsNull(created[0].Id);
        Assert.IsNull(created[0].SubResults);
        Assert.IsEmpty(client.UpdateTestResultsCalls, "An incomplete history must fall back to a safe create.");
    }

    [TestMethod]
    public async Task OrchestrationsWithTheSameProcessId_UseDifferentMapPaths()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId(processId: 4321);

        AzureDevOpsTestRunOrchestratorLifetime first = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient firstClient, out _, environment);
        firstClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(1001);
        await first.BeforeRunAsync(CancellationToken.None);
        string firstPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        await first.AfterRunAsync(0, CancellationToken.None);

        AzureDevOpsTestRunOrchestratorLifetime second = CreateOrchestratorLifetime(directory.Path, out FakeAzureDevOpsTestResultsClient secondClient, out _, environment);
        secondClient.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(1002);
        await second.BeforeRunAsync(CancellationToken.None);
        string secondPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        await second.AfterRunAsync(0, CancellationToken.None);

        Assert.AreNotEqual(firstPath, secondPath, "A recycled process id must not revive a crashed orchestration's map.");
    }

    [TestMethod]
    public async Task FailedMapSave_RemovesTheStaleMap()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        CollectingLogger logger = new();
        SystemFileSystem fileSystem = new();

        AzureDevOpsResultIdStore initial = await AzureDevOpsResultIdStore.OpenAsync(fileSystem, logger, mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult first = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 1, "first", null, null, null);
        initial.RecordCreated(first, resultId: 91);
        await initial.SaveAsync(CancellationToken.None);
        Assert.IsTrue(File.Exists(mapPath));

        UnwritableFileSystem failingFileSystem = new() { FailMoves = true };
        AzureDevOpsResultIdStore updated = await AzureDevOpsResultIdStore.OpenAsync(failingFileSystem, logger, mapPath, buildId: 123, runId: 42);
        AzureDevOpsPublishedResult published = updated.TryGet(first)!;
        IReadOnlyList<AzureDevOpsTestSubResult> attempts = AzureDevOpsResultIdStore.BuildNextAttempts(
            published,
            first with { ErrorMessage = "second" });
        updated.RecordAttempts(published, attempts, lastPublishedSubResultSequenceId: 2, totalDurationInMs: 2, startedDate: null, completedDate: null);
        await updated.SaveAsync(CancellationToken.None);

        Assert.IsFalse(File.Exists(mapPath), "A stale map would let the next attempt erase accepted server history.");
    }

    [TestMethod]
    public async Task CreateOnlySaveFailure_PreservesStillValidExistingEntries()
    {
        using TestDirectory directory = CreateTestDirectory();
        string mapPath = Path.Combine(directory.Path, "azdo-results.json");
        CollectingLogger logger = new();
        SystemFileSystem fileSystem = new();

        AzureDevOpsResultIdStore initial = await AzureDevOpsResultIdStore.OpenAsync(fileSystem, logger, mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult existing = new("ExistingTest", "tests", "ExistingTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 1, "first", null, null, null);
        initial.RecordCreated(existing, resultId: 95);
        await initial.SaveAsync(CancellationToken.None);

        UnwritableFileSystem failingFileSystem = new() { FailMoves = true };
        AzureDevOpsResultIdStore updated = await AzureDevOpsResultIdStore.OpenAsync(failingFileSystem, logger, mapPath, buildId: 123, runId: 42);
        AzureDevOpsTestCaseResult newlyCreated = new("NewTest", "tests", "NewTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 1, null, null, null, null);
        updated.RecordCreated(newlyCreated, resultId: 96);
        await updated.SaveAsync(CancellationToken.None);

        Assert.IsTrue(File.Exists(mapPath), "A create-only failure does not make the already persisted entries stale.");
        string survivingMap = File.ReadAllText(mapPath);
        Assert.Contains("ExistingTest", survivingMap);
        Assert.DoesNotContain("NewTest", survivingMap);
    }

    [TestMethod]
    public async Task WhenTheExistingMapCannotBeInvalidated_RetryFallsBackToCreate()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        UnwritableFileSystem fileSystem = new();
        AzureDevOpsTestResultsPublisher firstAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient firstClient,
            out _,
            out _,
            environment,
            fileSystem: fileSystem);
        firstClient.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([97]);
        await StartPublisherAsync(firstAttempt);
        await firstAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), RetryTestStartTime)),
            CancellationToken.None);
        await firstAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        fileSystem.FailDeletes = true;
        AzureDevOpsTestResultsPublisher secondAttempt = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient secondClient,
            out _,
            out _,
            environment,
            fileSystem: fileSystem);
        List<AzureDevOpsTestCaseResult> created = [];
        int createAttempts = 0;
        secondClient.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            createAttempts++;
            if (createAttempts == 1)
            {
                return Task.FromException<IReadOnlyList<int>?>(new HttpRequestException("transient safe-create failure"));
            }

            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([98]);
        };

        await StartPublisherAsync(secondAttempt);
        await secondAttempt.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await secondAttempt.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created, "The retry should degrade to a separate result when stale history cannot be invalidated.");
        Assert.IsEmpty(secondClient.UpdateTestResultsCalls);
    }

    [TestMethod]
    public async Task AttachmentCancellationAfterCreate_StillRecordsTheWholeAcceptedBatch()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([101, 102]);
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, _, _, _) => Task.FromException(new OperationCanceledException());
        await StartPublisherAsync(publisher);

        TestNode first = CreateNode("FirstTest", new FailedTestNodeStateProperty(new InvalidOperationException("first")), RetryTestStartTime);
        first.Properties.Add(new StandardOutputProperty("first output"));
        TestNode second = CreateNode("SecondTest", new FailedTestNodeStateProperty(new InvalidOperationException("second")), RetryTestStartTime);
        second.Properties.Add(new StandardOutputProperty("second output"));

        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(first), CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(second), CancellationToken.None));
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        string map = File.ReadAllText(mapPath);
        Assert.Contains("\"id\":101", map);
        Assert.Contains("\"id\":102", map);
        Assert.Contains("FirstTest", map);
        Assert.Contains("SecondTest", map);
    }

    [TestMethod]
    public async Task FirstAttemptSeedCancellation_ForgetsTheUncertainSubResultState()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([111]);
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, _, _) =>
            Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new OperationCanceledException());
        TestNode node = CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("first")), RetryTestStartTime);
        node.Properties.Add(new StandardOutputProperty("first output"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        string map = File.ReadAllText(mapPath);
        Assert.DoesNotContain("\"id\":111", map);
        Assert.DoesNotContain("MyTest", map);
    }

    [TestMethod]
    public async Task FirstAttemptSeedFailure_UploadsAttachmentToParentAndForgetsMapping()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out CollectingLogger logger,
            environment);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([121]);
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, _, _) =>
            Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new HttpRequestException("seed failed"));
        TestNode node = CreateNode("MyTest", new FailedTestNodeStateProperty(new InvalidOperationException("first")), RetryTestStartTime);
        node.Properties.Add(new StandardOutputProperty("first output"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(121, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("stdout.log", client.UploadTestResultAttachmentCalls[0].Attachment.FileName);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed, string.Join(Environment.NewLine, logger.Logs));

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        string map = File.ReadAllText(mapPath);
        Assert.DoesNotContain("\"id\":121", map);
        Assert.DoesNotContain("MyTest", map);
    }

    [TestMethod]
    public async Task InProcessRetry_SeedFailurePublishesEarlierAttemptIndependently()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            AzureDevOpsTestResultsPublisherOptions.Default,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        List<AzureDevOpsTestCaseResult> created = [];
        int nextResultId = 121;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([nextResultId++]);
        };
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, _, _) =>
            Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new HttpRequestException("seed failed"));

        TestNode firstAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        firstAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        firstAttempt.Properties.Add(new StandardOutputProperty("first output"));
        TestNode finalAttempt = CreateNode("MyTest", new PassedTestNodeStateProperty(), RetryTestStartTime);
        finalAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstAttempt), CancellationToken.None);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(finalAttempt), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, created[0].Outcome);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, created[1].Outcome);
        Assert.AreEqual("first", created[1].ErrorMessage);
        Assert.ContainsSingle(client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(122, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("first output", client.UploadTestResultAttachmentCalls[0].Attachment.InlineContent);
    }

    [TestMethod]
    public async Task InProcessRetry_SeedCancellationRequeuesEarlierAttempt()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);

        AzureDevOpsTestResultsPublisherOptions options = new(1, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(
            directory.Path,
            options,
            out FakeAzureDevOpsTestResultsClient client,
            out _,
            out _,
            environment);
        List<AzureDevOpsTestCaseResult> created = [];
        int nextResultId = 131;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([nextResultId++]);
        };
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, _, _) =>
            Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new OperationCanceledException());

        TestNode firstAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            RetryTestStartTime);
        firstAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 1, isSuperseded: true));
        TestNode finalAttempt = CreateNode(
            "MyTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("final")),
            RetryTestStartTime);
        finalAttempt.Properties.Add(new RetryAttemptProperty(attemptNumber: 2, isSuperseded: false));
        finalAttempt.Properties.Add(new StandardOutputProperty("final output"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(firstAttempt), CancellationToken.None);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(finalAttempt), CancellationToken.None));
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, created);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, created[0].Outcome);
        Assert.AreEqual("final", created[0].ErrorMessage);
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, created[1].Outcome);
        Assert.AreEqual("first", created[1].ErrorMessage);
        Assert.ContainsSingle(client.UploadTestResultAttachmentCalls);
        Assert.AreEqual(131, client.UploadTestResultAttachmentCalls[0].TestCaseResultId);
        Assert.IsNull(client.UploadTestResultAttachmentCalls[0].TestSubResultId);
        Assert.AreEqual("final output", client.UploadTestResultAttachmentCalls[0].Attachment.InlineContent);
    }

    [TestMethod]
    public async Task FirstAttemptSeedCancellationInMixedBatch_RequeuesTheUntouchedUpdate()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "ExistingTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 211);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([212]);
        client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
        {
            AzureDevOpsTestCaseResult result = results.Single();
            if (result.AutomatedTestName == "NewTest")
            {
                return Task.FromException<IReadOnlyList<AzureDevOpsPublishedTestResult>?>(new OperationCanceledException());
            }

            var subResultIds = result.SubResults!.ToDictionary(attempt => attempt.SequenceId, attempt => attempt.SequenceId);
            return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([
                new AzureDevOpsPublishedTestResult(result.Id!.Value, subResultIds),
            ]);
        };
        await StartPublisherAsync(publisher);

        TestNode newTest = CreateNode("NewTest", new FailedTestNodeStateProperty(new InvalidOperationException("new")), RetryTestStartTime);
        newTest.Properties.Add(new StandardOutputProperty("new output"));
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(newTest), CancellationToken.None);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => publisher.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode("ExistingTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
                CancellationToken.None));

        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(2, client.UpdateTestResultsCalls);
        Assert.AreEqual("NewTest", client.UpdateTestResultsCalls[0].Results.Single().AutomatedTestName);
        Assert.AreEqual("ExistingTest", client.UpdateTestResultsCalls[1].Results.Single().AutomatedTestName);
    }

    [TestMethod]
    public async Task AttachmentCancellationInMixedBatch_DoesNotSkipUpdates()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "ExistingTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 201);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        using AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([202]);
        };
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, _, _, _) => Task.FromException(new OperationCanceledException());
        await StartPublisherAsync(publisher);

        TestNode newTest = CreateNode("NewTest", new FailedTestNodeStateProperty(new InvalidOperationException("new")), RetryTestStartTime);
        newTest.Properties.Add(new StandardOutputProperty("new output"));

        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(newTest), CancellationToken.None);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => publisher.ConsumeAsync(
                Mock.Of<IDataProducer>(),
                CreateMessage(CreateNode("ExistingTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
                CancellationToken.None));

        Assert.HasCount(2, client.UpdateTestResultsCalls, "Both the creation seed and existing update must reach Azure DevOps before attachments are uploaded.");
        Assert.AreEqual("NewTest", client.UpdateTestResultsCalls[0].Results.Single().AutomatedTestName);
        Assert.AreEqual("ExistingTest", client.UpdateTestResultsCalls[1].Results.Single().AutomatedTestName);
        Assert.HasCount(1, created);
        Assert.AreEqual("NewTest", created[0].AutomatedTestName);
    }

    [TestMethod]
    public async Task FailedCreationPost_DoesNotConsumeUnattemptedUpdateClaim()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "ExistingTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 451);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        int createAttempts = 0;
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            createAttempts++;
            if (createAttempts == 1)
            {
                return Task.FromException<IReadOnlyList<int>?>(new HttpRequestException("transient create failure"));
            }

            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([452]);
        };
        await StartPublisherAsync(publisher);

        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("NewTest", new FailedTestNodeStateProperty(new InvalidOperationException("new")), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("ExistingTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, created);
        Assert.AreEqual("NewTest", created[0].AutomatedTestName);
        Assert.HasCount(1, client.UpdateTestResultsCalls, "The untouched parent claim must be available on the forced retry.");
        Assert.AreEqual(451, client.UpdateTestResultsCalls[0].Results.Single().Id);
    }

    [TestMethod]
    public async Task FailedDuplicateCreate_DoesNotReleaseEarlierSuccessfulClaim()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "SharedUid",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 461,
            displayName: "Duplicate title");

        AzureDevOpsTestResultsPublisherOptions options = new(1, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        int createAttempts = 0;
        List<AzureDevOpsTestCaseResult> created = [];
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            createAttempts++;
            if (createAttempts == 1)
            {
                return Task.FromException<IReadOnlyList<int>?>(new HttpRequestException("transient duplicate create failure"));
            }

            created.AddRange(results);
            return Task.FromResult<IReadOnlyList<int>?>([462]);
        };
        await StartPublisherAsync(publisher);

        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title")),
            CancellationToken.None);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("SharedUid", new PassedTestNodeStateProperty(), RetryTestStartTime, displayName: "Duplicate title")),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.HasCount(1, client.UpdateTestResultsCalls, "The persisted parent may be updated only once in this attempt.");
        Assert.HasCount(1, created);
        Assert.AreEqual("SharedUid", created[0].AutomatedTestName);
    }

    [TestMethod]
    public async Task FailedPatchInMixedBatch_DoesNotResaveStaleHistory()
    {
        using TestDirectory directory = CreateTestDirectory();
        Mock<IEnvironment> environment = CreateEnvironmentMockWithSettableRunId();
        AzureDevOpsTestRunOrchestratorLifetime lifetime = CreateOrchestratorLifetime(directory.Path, out _, out _, environment);
        await lifetime.BeforeRunAsync(CancellationToken.None);
        await PublishSingleResultAsync(
            directory.Path,
            environment,
            "ExistingTest",
            new FailedTestNodeStateProperty(new InvalidOperationException("first")),
            resultId: 211);

        AzureDevOpsTestResultsPublisherOptions options = new(2, TimeSpan.FromMinutes(1), 40, TimeSpan.FromMilliseconds(250));
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        List<string> createdTests = [];
        int nextResultId = 212;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            createdTests.AddRange(results.Select(result => result.AutomatedTestName));
            int[] ids = [.. Enumerable.Range(nextResultId, results.Count)];
            nextResultId += results.Count;
            return Task.FromResult<IReadOnlyList<int>?>(ids);
        };
        client.UpdateTestResultsAsyncFunc = (_, _, _, _) => Task.FromException(new HttpRequestException("ambiguous failure"));
        await StartPublisherAsync(publisher);

        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("NewTest", new FailedTestNodeStateProperty(new InvalidOperationException("new")), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode("ExistingTest", new PassedTestNodeStateProperty(), RetryTestStartTime)),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.Contains("NewTest", createdTests);
        Assert.Contains("ExistingTest", createdTests, "The ambiguous PATCH must retry as a safe create.");
        Assert.HasCount(2, createdTests);
        Assert.AreEqual("NewTest", createdTests.Single(name => name == "NewTest"));
        Assert.AreEqual("ExistingTest", createdTests.Single(name => name == "ExistingTest"));

        string mapPath = AzureDevOpsConstants.TryGetInheritedResultMapPath(environment.Object, buildId: 123)!;
        Assert.DoesNotContain("\"id\":211", File.ReadAllText(mapPath), "The pre-PATCH result id must not be resurrected.");
    }

    [TestMethod]
    public void CappedAttemptHistory_ContinuesIncreasingSequenceIds()
    {
        var attempts = new AzureDevOpsTestSubResult[AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult];
        for (int i = 0; i < attempts.Length; i++)
        {
            attempts[i] = new AzureDevOpsTestSubResult(
                i + 1,
                "MyTest",
                AzureDevOpsLivePublishingConstants.FailedTestOutcome,
                1,
                null,
                null,
                null,
                null);
        }

        AzureDevOpsPublishedResult published = new("tests", "MyTest", "MyTest", 123, attempts)
        {
            TotalDurationInMs = 1000,
        };
        AzureDevOpsTestCaseResult nextResult = new(
            "MyTest",
            "tests",
            "MyTest",
            AzureDevOpsLivePublishingConstants.PassedTestOutcome,
            1,
            null,
            null,
            null,
            null);

        IReadOnlyList<AzureDevOpsTestSubResult> next = AzureDevOpsResultIdStore.BuildNextAttempts(published, nextResult);
        long? nextTotalDuration = AzureDevOpsResultIdStore.BuildNextTotalDuration(published, nextResult);
        IReadOnlyList<AzureDevOpsTestSubResult> afterNext = AzureDevOpsResultIdStore.BuildNextAttempts(
            published with { Attempts = next },
            nextResult);
        long? afterNextTotalDuration = AzureDevOpsResultIdStore.BuildNextTotalDuration(
            published with { Attempts = next, TotalDurationInMs = nextTotalDuration },
            nextResult);

        Assert.HasCount(AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult, next);
        Assert.AreEqual(2, next[0].SequenceId);
        Assert.AreEqual(1001, next[^1].SequenceId);
        Assert.AreEqual(1002, afterNext[^1].SequenceId);
        Assert.AreEqual(1001, nextTotalDuration);
        Assert.AreEqual(1002, afterNextTotalDuration);
    }

    /// <summary>
    /// Runs one publisher to completion for a single test, so a following attempt has something to merge into.
    /// </summary>
    private async Task PublishSingleResultAsync(
        string resultsDirectory,
        Mock<IEnvironment> environment,
        string uid,
        IProperty state,
        int resultId,
        string? displayName = null)
    {
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(resultsDirectory, AzureDevOpsTestResultsPublisherOptions.Default, out FakeAzureDevOpsTestResultsClient client, out _, out _, environment);
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromResult<IReadOnlyList<int>?>([resultId]);

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateMessage(CreateNode(uid, state, RetryTestStartTime, displayName: displayName)),
            CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
    }

    // The rerun shape is a contract with Azure DevOps rather than with our own code, so assert on the
    // bytes actually sent: the verb, the results URI, and the camelCase resultGroupType the service
    // expects (it rejects the PascalCase spelling used by the client SDK's enum).
    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_PatchesRerunAndMapsReorderedSubResults()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        Uri? capturedUri = null;
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"count\":1,\"value\":[{\"id\":777,\"subResults\":[{\"id\":1002,\"sequenceId\":2},{\"id\":1001,\"sequenceId\":1}]}]}"),
        };
        QueueHttpMessageHandler handler = new(
            async (request, cancellationToken) =>
            {
                capturedMethod = request.Method;
                capturedUri = request.RequestUri;
                capturedBody = await ReadRequestBodyAsync(request, cancellationToken);
                return response;
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");

        AzureDevOpsTestCaseResult parent = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null)
        {
            Id = 777,
            ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
            SubResults =
            [
                new AzureDevOpsTestSubResult(1, "MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 3, "boom", "at Foo()", null, null),
                new AzureDevOpsTestSubResult(2, "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null),
            ],
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [parent], CancellationToken.None);

        Assert.AreEqual("PATCH", capturedMethod!.Method);
        Assert.AreEqual("https://dev.azure.com/org/project/_apis/test/runs/42/results?api-version=7.1", capturedUri!.ToString());
        Assert.IsNotNull(capturedBody);

        using var document = JsonDocument.Parse(capturedBody!);
        JsonElement result = document.RootElement[0];
        Assert.AreEqual(777, result.GetProperty("id").GetInt32());
        Assert.AreEqual("rerun", result.GetProperty("resultGroupType").GetString());
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.PassedTestOutcome, result.GetProperty("outcome").GetString());
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("errorMessage").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, result.GetProperty("stackTrace").ValueKind);

        JsonElement subResults = result.GetProperty("subResults");
        Assert.AreEqual(2, subResults.GetArrayLength());
        Assert.AreEqual(1, subResults[0].GetProperty("sequenceId").GetInt32());
        Assert.AreEqual(AzureDevOpsLivePublishingConstants.FailedTestOutcome, subResults[0].GetProperty("outcome").GetString());
        Assert.AreEqual("boom", subResults[0].GetProperty("errorMessage").GetString());
        Assert.AreEqual(2, subResults[1].GetProperty("sequenceId").GetInt32());
        Assert.IsNotNull(publishedResults);
        Assert.IsTrue(publishedResults[0].TryGetSubResultId(sequenceId: 1, out int firstSubResultId));
        Assert.IsTrue(publishedResults[0].TryGetSubResultId(sequenceId: 2, out int secondSubResultId));
        Assert.AreEqual(1001, firstSubResultId);
        Assert.AreEqual(1002, secondSubResultId);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_MapsAppendedSubResultFromFullHistoryResponse()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"count\":1,\"value\":[{\"id\":777,\"subResults\":[{\"id\":1001,\"sequenceId\":1},{\"id\":1002,\"sequenceId\":2}]}]}"),
        };
        QueueHttpMessageHandler handler = new((_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, new FakeTask(), new FakeClock());
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult parent = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "second", null, null, null)
        {
            Id = 777,
            ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
            SubResults =
            [
                new AzureDevOpsTestSubResult(2, "Attempt# 1 - MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "second", null, null, null),
            ],
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [parent], CancellationToken.None);

        Assert.IsNotNull(publishedResults);
        Assert.IsFalse(publishedResults[0].TryGetSubResultId(sequenceId: 1, out _));
        Assert.IsTrue(publishedResults[0].TryGetSubResultId(sequenceId: 2, out int secondSubResultId));
        Assert.AreEqual(1002, secondSubResultId);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_MapsPatchResponseThatOmitsSequenceIds()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"count\":1,\"value\":[{\"id\":777,\"subResults\":[{\"id\":1001},{\"id\":1002}]}]}"),
        };
        QueueHttpMessageHandler handler = new((_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, new FakeTask(), new FakeClock());
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult parent = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "second", null, null, null)
        {
            Id = 777,
            ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
            SubResults =
            [
                new AzureDevOpsTestSubResult(1, "Attempt# 0 - MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "first", null, null, null),
                new AzureDevOpsTestSubResult(2, "Attempt# 1 - MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "second", null, null, null),
            ],
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [parent], CancellationToken.None);

        Assert.IsNotNull(publishedResults);
        Assert.IsTrue(publishedResults[0].TryGetSubResultId(sequenceId: 1, out int firstSubResultId));
        Assert.IsTrue(publishedResults[0].TryGetSubResultId(sequenceId: 2, out int secondSubResultId));
        Assert.AreEqual(1001, firstSubResultId);
        Assert.AreEqual(1002, secondSubResultId);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UploadTestResultAttachment_TargetsTheSubResult()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        Uri? capturedUri = null;
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        };
        QueueHttpMessageHandler handler = new(
            (request, _) =>
            {
                capturedUri = request.RequestUri;
                return Task.FromResult(response);
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        var attachment = AzureDevOpsTestResultAttachment.FromString("output", "stdout.log", AzureDevOpsAttachmentTypes.ConsoleLog);

        await client.UploadTestResultAttachmentAsync(configuration, runId: 42, testCaseResultId: 777, testSubResultId: 2, attachment, CancellationToken.None);

        Assert.AreEqual(
            "https://dev.azure.com/org/project/_apis/test/runs/42/results/777/attachments?testSubResultId=2&api-version=7.1",
            capturedUri!.ToString());
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_PublishTestResults_MissingSubResultsPreservesParentId()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"count\":1,\"value\":[{\"id\":777,\"automatedTestName\":\"MyTest\"}]}"),
        };
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "boom", null, null, null)
        {
            ResultGroupType = AzureDevOpsLivePublishingConstants.RerunResultGroupType,
            SubResults =
            [
                new AzureDevOpsTestSubResult(1, "Attempt# 0 - MyTest", AzureDevOpsLivePublishingConstants.FailedTestOutcome, 5, "boom", null, null, null),
            ],
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.PublishTestResultsWithSubResultsAsync(configuration, runId: 42, [result], CancellationToken.None);

        Assert.IsNotNull(publishedResults);
        Assert.AreEqual(777, publishedResults[0].Id);
        Assert.IsEmpty(publishedResults[0].SubResultIdsBySequenceId);
        Assert.IsFalse(publishedResults[0].TryGetSubResultId(sequenceId: 1, out _));
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_ResponseReadFailureDoesNotReplayAcceptedPatch()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new ThrowingHttpContent(new IOException("response stream failed")),
        };
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null)
        {
            Id = 777,
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [result], CancellationToken.None);

        Assert.IsNull(publishedResults);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_ResponseBodyReadHonorsCancellation()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        TaskCompletionSource<bool> responseBodyReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new BlockingHttpContent(responseBodyReadStarted),
        };
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null)
        {
            Id = 777,
        };
        using CancellationTokenSource cancellationTokenSource = new();
        Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> updateTask =
            client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [result], cancellationTokenSource.Token);

        await responseBodyReadStarted.Task;
#if NET
        await cancellationTokenSource.CancelAsync();
#else
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; this project also targets .NET Framework.
        cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
#endif

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => updateTask);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_PublishTestResults_InvalidCharsetReturnsNullWithoutReplay()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using StringContent responseContent = new("{\"count\":1,\"value\":[{\"id\":777,\"automatedTestName\":\"MyTest\"}]}");
        responseContent.Headers.ContentType!.CharSet = "unsupported-charset";
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = responseContent,
        };
        int sendCount = 0;
        QueueHttpMessageHandler handler = new(
            (_, _) =>
            {
                sendCount++;
                return Task.FromResult(response);
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null);

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.PublishTestResultsWithSubResultsAsync(configuration, runId: 42, [result], CancellationToken.None);

        Assert.IsNull(publishedResults);
        Assert.AreEqual(1, sendCount);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UpdateTestResults_InvalidCharsetReturnsNullWithoutReplay()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using StringContent responseContent = new("{\"count\":1,\"value\":[{\"id\":777}]}");
        responseContent.Headers.ContentType!.CharSet = "unsupported-charset";
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = responseContent,
        };
        int sendCount = 0;
        QueueHttpMessageHandler handler = new(
            (_, _) =>
            {
                sendCount++;
                return Task.FromResult(response);
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null)
        {
            Id = 777,
        };

        IReadOnlyList<AzureDevOpsPublishedTestResult>? publishedResults =
            await client.UpdateTestResultsWithSubResultsAsync(configuration, runId: 42, [result], CancellationToken.None);

        Assert.IsNull(publishedResults);
        Assert.AreEqual(1, sendCount);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_NonSuccessResponse_BodyReadThrows_DisposesResponse()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using ThrowingHttpContent content1 = new(new IOException("response stream failed 1"));
        using HttpResponseMessage response1 = new(HttpStatusCode.BadRequest)
        {
            Content = content1,
        };
        using ThrowingHttpContent content2 = new(new IOException("response stream failed 2"));
        using HttpResponseMessage response2 = new(HttpStatusCode.BadRequest)
        {
            Content = content2,
        };
        using ThrowingHttpContent content3 = new(new IOException("response stream failed 3"));
        using HttpResponseMessage response3 = new(HttpStatusCode.BadRequest)
        {
            Content = content3,
        };
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(response1),
            (_, _) => Task.FromResult(response2),
            (_, _) => Task.FromResult(response3));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");

        HttpRequestException exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(
            () => client.CreateTestRunAsync(configuration, CancellationToken.None));
        Assert.IsInstanceOfType<IOException>(exception.InnerException);
        Assert.IsTrue(content1.IsDisposed);
        Assert.IsTrue(content2.IsDisposed);
        Assert.IsTrue(content3.IsDisposed);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_RetryableResponse_DelayThrows_DisposesResponse()
    {
        FakeTask task = new(delayCallback: _ => throw new IOException("delay failed"));
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        using ThrowingHttpContent content = new(new InvalidOperationException("content should not be read"));
        using HttpResponseMessage response = new(HttpStatusCode.ServiceUnavailable)
        {
            Content = content,
        };
        QueueHttpMessageHandler handler = new(
            (_, _) => Task.FromResult(response));
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => client.CreateTestRunAsync(configuration, CancellationToken.None));

        Assert.IsTrue(content.IsDisposed);
    }

    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_UploadTestResultAttachment_TargetsTheParentWhenSubResultIsNotSpecified()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        Uri? capturedUri = null;
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{}"),
        };
        QueueHttpMessageHandler handler = new(
            (request, _) =>
            {
                capturedUri = request.RequestUri;
                return Task.FromResult(response);
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");
        var attachment = AzureDevOpsTestResultAttachment.FromString("output", "stdout.log", AzureDevOpsAttachmentTypes.ConsoleLog);

        await client.UploadTestResultAttachmentAsync(configuration, runId: 42, testCaseResultId: 777, testSubResultId: null, attachment, CancellationToken.None);

        Assert.AreEqual(
            "https://dev.azure.com/org/project/_apis/test/runs/42/results/777/attachments?api-version=7.1",
            capturedUri!.ToString());
    }

    // A result being created must not carry any of the rerun fields: sending an explicit null id would
    // make Azure DevOps treat the create as an update of result 0.
    [TestMethod]
    public async Task AzureDevOpsTestResultsClient_PublishTestResults_OmitsRerunFieldsForANewResult()
    {
        FakeTask task = new();
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        string? capturedBody = null;
        QueueHttpMessageHandler handler = new(
            async (request, cancellationToken) =>
            {
                capturedBody = await ReadRequestBodyAsync(request, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"count\":1,\"value\":[{\"id\":5,\"automatedTestName\":\"MyTest\"}]}"),
                };
            });
        using HttpClient httpClient = new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        AzureDevOpsTestResultsClient client = new(httpClient, task, clock);
        AzureDevOpsPublishConfiguration configuration = new("https://dev.azure.com/org/", "project", "token", 123, "run", "tests.dll", "results");

        AzureDevOpsTestCaseResult result = new("MyTest", "tests", "MyTest", AzureDevOpsLivePublishingConstants.PassedTestOutcome, 5, null, null, null, null);
        IReadOnlyList<int>? ids = await client.PublishTestResultsAsync(configuration, runId: 42, [result], CancellationToken.None);

        Assert.IsNotNull(ids);
        Assert.AreEqual(5, ids![0]);

        using var document = JsonDocument.Parse(capturedBody!);
        JsonElement created = document.RootElement[0];
        Assert.IsFalse(created.TryGetProperty("id", out _));
        Assert.IsFalse(created.TryGetProperty("resultGroupType", out _));
        Assert.IsFalse(created.TryGetProperty("subResults", out _));
        Assert.IsFalse(created.TryGetProperty("errorMessage", out _));
        Assert.IsFalse(created.TryGetProperty("stackTrace", out _));
    }

    #endregion

    private static AzureDevOpsTestRunOrchestratorLifetime CreateOrchestratorLifetime(
        string resultsDirectory,
        out FakeAzureDevOpsTestResultsClient client,
        out CollectingLogger logger,
        Mock<IEnvironment>? environment = null,
        CollectingOutputDevice? outputDevice = null)
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions.Setup(x => x.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName)).Returns(true);
        string[]? runNameArguments = null;
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(false);

        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(resultsDirectory);

        environment ??= CreateEnvironmentMockWithSettableRunId();

        Mock<ITestApplicationModuleInfo> testApplicationModuleInfo = new();
        testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns("MyTests");
        testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(Path.Combine("testfx-worktrees", "azdo-live", "artifacts", "MyTests.dll"));

        client = new FakeAzureDevOpsTestResultsClient();
        logger = new CollectingLogger();

        // The fake clock advances on every poll so any coordination wait reaches its bound instead of
        // spinning forever: FakeTask.Delay returns immediately, so a frozen clock would turn a future
        // regression into a hung suite rather than a failing test.
        FakeClock clock = new() { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        FakeTask task = new(_ => clock.UtcNow += TimeSpan.FromSeconds(30));

        return new AzureDevOpsTestRunOrchestratorLifetime(
            commandLineOptions.Object,
            configuration.Object,
            environment.Object,
            new SystemFileSystem(),
            outputDevice ?? new CollectingOutputDevice(),
            testApplicationModuleInfo.Object,
            client,
            task,
            clock,
            logger,
            new AzureDevOpsTestResultsPublisherOptions(10, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)));
    }

    /// <summary>
    /// Makes the environment mock behave like a real process environment for the run-id handoff, so a value
    /// written by the orchestrator lifetime is observable by everything that reads it afterwards.
    /// </summary>
    private static Mock<IEnvironment> CreateEnvironmentMockWithSettableRunId(string? initialRunId = null, int? processId = null)
    {
        Mock<IEnvironment> environment = CreateEnvironmentMock(processId: processId ?? GetAliveProcessId());
        string? runId = initialRunId;
        environment.Setup(x => x.GetEnvironmentVariable(AzureDevOpsConstants.TestRunIdEnvironmentVariableName)).Returns(() => runId);
        environment.Setup(x => x.SetEnvironmentVariable(AzureDevOpsConstants.TestRunIdEnvironmentVariableName, It.IsAny<string>()))
            .Callback<string, string>((_, value) => runId = value);

        // The result map path is handed down the same way, so the attempts of one orchestration can find
        // the results the earlier ones created.
        string? resultMapPath = null;
        environment.Setup(x => x.GetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName)).Returns(() => resultMapPath);
        environment.Setup(x => x.SetEnvironmentVariable(AzureDevOpsConstants.ResultMapPathEnvironmentVariableName, It.IsAny<string>()))
            .Callback<string, string>((_, value) => resultMapPath = value);
        return environment;
    }

    private TestDirectory CreateTestDirectory() => new(_directoriesToDelete);

    private static int GetAliveProcessId()
#if NET
        => Environment.ProcessId;
#else
        => Process.GetCurrentProcess().Id;
#endif

    private static async Task<string> ReadRequestBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
#if NET
        => await request.Content!.ReadAsStringAsync(cancellationToken);
#else
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await request.Content!.ReadAsStringAsync();
    }
#endif

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Mock<IEnvironment> CreateEnvironmentMock(
        int processId,
        string? stageName = "stage",
        string? jobName = "job",
        string? phaseName = null,
        string? stageAttempt = null,
        string? phaseAttempt = null,
        string? jobAttempt = null)
    {
        Mock<IEnvironment> environment = new();
        environment.SetupGet(x => x.ProcessId).Returns(processId);
        environment.SetupGet(x => x.MachineName).Returns("agent-name");
        environment.Setup(x => x.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")).Returns("https://dev.azure.com/org/");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_TEAMPROJECT")).Returns("project");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN")).Returns("token");
        environment.Setup(x => x.GetEnvironmentVariable("BUILD_BUILDID")).Returns("123");
        environment.Setup(x => x.GetEnvironmentVariable("AGENT_NAME")).Returns("agent-name");
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGENAME")).Returns(stageName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBNAME")).Returns(jobName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASENAME")).Returns(phaseName);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_STAGEATTEMPT")).Returns(stageAttempt);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_PHASEATTEMPT")).Returns(phaseAttempt);
        environment.Setup(x => x.GetEnvironmentVariable("SYSTEM_JOBATTEMPT")).Returns(jobAttempt);
        return environment;
    }

    private static AzureDevOpsTestResultsPublisher CreatePublisher(
        string resultsDirectory,
        AzureDevOpsTestResultsPublisherOptions options,
        out FakeAzureDevOpsTestResultsClient client,
        out FakeClock clock,
        out CollectingLogger logger,
        Mock<IEnvironment>? environment = null,
        Mock<ITestApplicationModuleInfo>? testApplicationModuleInfo = null,
        ITask? task = null,
        Mock<ITestApplicationProcessExitCode>? processExitCode = null,
        CollectingOutputDevice? outputDevice = null,
        IFileSystem? fileSystem = null)
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions.Setup(x => x.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName)).Returns(true);
        string[]? runNameArguments = null;
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(false);

        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(resultsDirectory);

        environment ??= CreateEnvironmentMock(processId: GetAliveProcessId());

        // Only fill in defaults for a mock we created: a caller that supplies one is describing a
        // different test application on purpose, and overwriting it would silently undo that.
        if (testApplicationModuleInfo is null)
        {
            testApplicationModuleInfo = new Mock<ITestApplicationModuleInfo>();
            testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns("MyTests");
            testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(Path.Combine("testfx-worktrees", "azdo-live", "artifacts", "MyTests.dll"));
        }

        if (processExitCode is null)
        {
            processExitCode = new Mock<ITestApplicationProcessExitCode>();
            processExitCode.Setup(x => x.GetProcessExitCode()).Returns(0);
            processExitCode.SetupGet(x => x.HasTestAdapterTestSessionFailure).Returns(false);
        }

        client = new FakeAzureDevOpsTestResultsClient();
        clock = new FakeClock { UtcNow = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero) };
        logger = new CollectingLogger();

        return new AzureDevOpsTestResultsPublisher(
            commandLineOptions.Object,
            configuration.Object,
            environment.Object,
            fileSystem ?? new SystemFileSystem(),
            outputDevice ?? new CollectingOutputDevice(),
            testApplicationModuleInfo.Object,
            processExitCode.Object,
            client,
            task ?? new FakeTask(),
            clock,
            logger,
            options);
    }

    private static TestNodeUpdateMessage CreateMessage(TestNode node)
        => new(new SessionUid(Guid.NewGuid().ToString()), node);

    private static TestNode CreateNode(string uid, IProperty state, DateTimeOffset startTime, TimingProperty? timing = null, string? displayName = null)
    {
        PropertyBag properties = timing is null
            ? new PropertyBag(state, new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", uid))
            : new PropertyBag(state, timing, new SerializableKeyValuePairStringProperty("vstest.TestCase.FullyQualifiedName", uid));
        return new TestNode
        {
            Uid = new TestNodeUid(uid),
            DisplayName = displayName ?? uid,
            Properties = properties,
        };
    }

    /// <summary>
    /// A real file system whose run-id coordination file writes fail on demand.
    /// </summary>
    private sealed class UnwritableFileSystem : IFileSystem
    {
        private readonly SystemFileSystem _inner = new();

        public bool FailRunIdFileWrites { get; set; }

        public bool FailMoves { get; set; }

        public bool FailDeletes { get; set; }

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access, FileShare share)
            => FailRunIdFileWrites && Path.GetFileName(path).EndsWith(".json", StringComparison.Ordinal) && !Path.GetFileName(path).Contains("participant")
                ? throw new IOException($"The process cannot access the file '{path}' because it is being used by another process.")
                : _inner.NewFileStream(path, mode, access, share);

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) => _inner.CopyFile(sourceFileName, destFileName, overwrite);

        public string CreateDirectory(string path) => _inner.CreateDirectory(path);

        public void DeleteFile(string path)
        {
            if (FailDeletes)
            {
                throw new IOException($"The process cannot delete the file '{path}'.");
            }

            _inner.DeleteFile(path);
        }

        public bool ExistDirectory(string? path) => _inner.ExistDirectory(path);

        public bool ExistFile(string path) => _inner.ExistFile(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => _inner.GetFiles(path, searchPattern, searchOption);

        public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false)
        {
            if (FailMoves)
            {
                throw new IOException($"The process cannot move the file '{sourceFileName}' to '{destFileName}'.");
            }

            _inner.MoveFile(sourceFileName, destFileName, overwrite);
        }

        public void ReplaceFile(string sourceFileName, string destFileName)
        {
            if (FailMoves)
            {
                throw new IOException($"The process cannot replace the file '{destFileName}'.");
            }

            _inner.ReplaceFile(sourceFileName, destFileName);
        }

        public IFileStream NewFileStream(string path, FileMode mode) => _inner.NewFileStream(path, mode);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => _inner.NewFileStream(path, mode, access);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public Task<string> ReadAllTextAsync(string path) => _inner.ReadAllTextAsync(path);
    }

    /// <summary>
    /// A real file system whose deletes fail, emulating a handle held by an antivirus or search indexer -
    /// the situation the coordinator's best-effort deletes exist to tolerate.
    /// </summary>
    private sealed class UndeletableFileSystem : IFileSystem
    {
        private readonly SystemFileSystem _inner = new();

        public bool FailDeletes { get; set; }

        public void DeleteFile(string path)
        {
            if (FailDeletes)
            {
                throw new IOException($"The process cannot access the file '{path}' because it is being used by another process.");
            }

            _inner.DeleteFile(path);
        }

        public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) => _inner.CopyFile(sourceFileName, destFileName, overwrite);

        public string CreateDirectory(string path) => _inner.CreateDirectory(path);

        public bool ExistDirectory(string? path) => _inner.ExistDirectory(path);

        public bool ExistFile(string path) => _inner.ExistFile(path);

        public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => _inner.GetFiles(path, searchPattern, searchOption);

        public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) => _inner.MoveFile(sourceFileName, destFileName, overwrite);

        public void ReplaceFile(string sourceFileName, string destFileName) => _inner.ReplaceFile(sourceFileName, destFileName);

        public IFileStream NewFileStream(string path, FileMode mode) => _inner.NewFileStream(path, mode);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => _inner.NewFileStream(path, mode, access);

        public IFileStream NewFileStream(string path, FileMode mode, FileAccess access, FileShare share) => _inner.NewFileStream(path, mode, access, share);

        public string ReadAllText(string path) => _inner.ReadAllText(path);

        public Task<string> ReadAllTextAsync(string path) => _inner.ReadAllTextAsync(path);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class CollectingOutputDevice : IOutputDevice
    {
        public List<string> Lines { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> SessionMessages { get; } = [];

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            switch (data)
            {
                case WarningMessageOutputDeviceData warning:
                    Warnings.Add(warning.Message);
                    Lines.Add(warning.Message);
                    break;

                case SessionMessageOutputDeviceData sessionMessage:
                    SessionMessages.Add(sessionMessage.Message);
                    Lines.Add(sessionMessage.Message);
                    break;

                default:
                    Assert.Fail($"Unexpected output device data type '{data.GetType()}'.");
                    break;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Minimal stateful Azure DevOps substitute that models the service's append-only PATCH behavior for
    /// sub-results and keeps attachments on the concrete sub-result id they target.
    /// </summary>
    private sealed class AppendingAzureDevOpsService
    {
        private const int ParentResultId = 100_000;
        private int _nextSubResultId = 1;

        public List<ServiceSubResult> SubResults { get; } = [];

        public List<AzureDevOpsTestResultAttachment> ParentAttachments { get; } = [];

        public void Connect(FakeAzureDevOpsTestResultsClient client)
        {
            client.PublishTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
            {
                Append(results);
                return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([CreatePublishedResult()]);
            };
            client.UpdateTestResultsWithSubResultsAsyncFunc = (_, _, results, _) =>
            {
                Append(results);
                return Task.FromResult<IReadOnlyList<AzureDevOpsPublishedTestResult>?>([CreatePublishedResult()]);
            };
            client.UploadTestResultAttachmentAsyncFunc = (_, _, testCaseResultId, testSubResultId, attachment, _) =>
            {
                Assert.AreEqual(ParentResultId, testCaseResultId);
                if (testSubResultId is null)
                {
                    ParentAttachments.Add(attachment);
                }
                else
                {
                    ServiceSubResult subResult = SubResults.Single(result => result.Id == testSubResultId);
                    subResult.Attachments.Add(attachment);
                }

                return Task.CompletedTask;
            };
        }

        private void Append(IReadOnlyList<AzureDevOpsTestCaseResult> results)
        {
            AzureDevOpsTestCaseResult parent = results.Single();
            if (parent.SubResults is null)
            {
                return;
            }

            foreach (AzureDevOpsTestSubResult subResult in parent.SubResults)
            {
                SubResults.Add(new ServiceSubResult(
                    _nextSubResultId++,
                    subResult.SequenceId,
                    subResult.DisplayName));
            }
        }

        private AzureDevOpsPublishedTestResult CreatePublishedResult()
        {
            Dictionary<int, int> subResultIds = [];
            foreach (ServiceSubResult subResult in SubResults)
            {
                // Azure DevOps can contain duplicate sequence ids; its latest row is the one consumers resolve.
                subResultIds[subResult.SequenceId] = subResult.Id;
            }

            return new AzureDevOpsPublishedTestResult(ParentResultId, subResultIds);
        }

        internal sealed class ServiceSubResult(int id, int sequenceId, string displayName)
        {
            public int Id { get; } = id;

            public int SequenceId { get; } = sequenceId;

            public string DisplayName { get; } = displayName;

            public List<AzureDevOpsTestResultAttachment> Attachments { get; } = [];
        }
    }

    private sealed class FakeAzureDevOpsTestResultsClient : IAzureDevOpsTestResultsClient
    {
        public Func<AzureDevOpsPublishConfiguration, CancellationToken, Task<int>> CreateTestRunAsyncFunc { get; set; } = (_, _) => Task.FromResult(1);

        public Func<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken, Task<IReadOnlyList<int>?>> PublishTestResultsAsyncFunc { get; set; } =
            (_, _, results, _) =>
            {
                int[] ids = new int[results.Count];
                for (int i = 0; i < results.Count; i++)
                {
                    ids[i] = i + 1;
                }

                return Task.FromResult<IReadOnlyList<int>?>(ids);
            };

        public Func<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken, Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?>>? PublishTestResultsWithSubResultsAsyncFunc { get; set; }

        public Func<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken, Task> UpdateTestResultsAsyncFunc { get; set; } = (_, _, _, _) => Task.CompletedTask;

        public Func<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken, Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?>>? UpdateTestResultsWithSubResultsAsyncFunc { get; set; }

        public List<(int RunId, IReadOnlyList<AzureDevOpsTestCaseResult> Results)> UpdateTestResultsCalls { get; } = [];

        public Func<AzureDevOpsPublishConfiguration, int, int, int?, AzureDevOpsTestResultAttachment, CancellationToken, Task> UploadTestResultAttachmentAsyncFunc { get; set; } = (_, _, _, _, _, _) => Task.CompletedTask;

        public Func<AzureDevOpsPublishConfiguration, int, AzureDevOpsTestResultAttachment, CancellationToken, Task> UploadTestRunAttachmentAsyncFunc { get; set; } = (_, _, _, _) => Task.CompletedTask;

        public Func<AzureDevOpsPublishConfiguration, int, string, CancellationToken, Task> UpdateTestRunStateAsyncFunc { get; set; } = (_, _, _, _) => Task.CompletedTask;

        public List<(AzureDevOpsPublishConfiguration Configuration, int RunId, string State)> UpdateTestRunStateCalls { get; } = [];

        public List<(int RunId, int TestCaseResultId, int? TestSubResultId, AzureDevOpsTestResultAttachment Attachment)> UploadTestResultAttachmentCalls { get; } = [];

        public List<(int RunId, AzureDevOpsTestResultAttachment Attachment)> UploadTestRunAttachmentCalls { get; } = [];

        public List<AzureDevOpsPublishConfiguration> CreateTestRunCalls { get; } = [];

        public Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken)
        {
            CreateTestRunCalls.Add(configuration);
            return CreateTestRunAsyncFunc(configuration, cancellationToken);
        }

        public Task<IReadOnlyList<int>?> PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
            => PublishTestResultsAsyncFunc(configuration, runId, results, cancellationToken);

        public async Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> PublishTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
        {
            if (PublishTestResultsWithSubResultsAsyncFunc is not null)
            {
                return await PublishTestResultsWithSubResultsAsyncFunc(configuration, runId, results, cancellationToken);
            }

            IReadOnlyList<int>? ids = await PublishTestResultsAsyncFunc(configuration, runId, results, cancellationToken);
            return ids is null ? null : CreatePublishedResults(ids, results);
        }

        public async Task UpdateTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
            => _ = await UpdateTestResultsWithSubResultsAsync(configuration, runId, results, cancellationToken);

        public async Task<IReadOnlyList<AzureDevOpsPublishedTestResult>?> UpdateTestResultsWithSubResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
        {
            UpdateTestResultsCalls.Add((runId, results));
            await UpdateTestResultsAsyncFunc(configuration, runId, results, cancellationToken);
            return UpdateTestResultsWithSubResultsAsyncFunc is null
                ? CreatePublishedResults(results.Select(static result => result.Id!.Value).ToArray(), results)
                : await UpdateTestResultsWithSubResultsAsyncFunc(configuration, runId, results, cancellationToken);
        }

        public Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken)
        {
            UpdateTestRunStateCalls.Add((configuration, runId, state));
            return UpdateTestRunStateAsyncFunc(configuration, runId, state, cancellationToken);
        }

        public Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, int? testSubResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
        {
            UploadTestResultAttachmentCalls.Add((runId, testCaseResultId, testSubResultId, attachment));
            return UploadTestResultAttachmentAsyncFunc(configuration, runId, testCaseResultId, testSubResultId, attachment, cancellationToken);
        }

        public Task UploadTestRunAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
        {
            UploadTestRunAttachmentCalls.Add((runId, attachment));
            return UploadTestRunAttachmentAsyncFunc(configuration, runId, attachment, cancellationToken);
        }

        private static IReadOnlyList<AzureDevOpsPublishedTestResult> CreatePublishedResults(
            IReadOnlyList<int> ids,
            IReadOnlyList<AzureDevOpsTestCaseResult> results)
        {
            var publishedResults = new AzureDevOpsPublishedTestResult[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                Dictionary<int, int> subResultIds = [];
                if (results[i].SubResults is { } subResults)
                {
                    foreach (AzureDevOpsTestSubResult subResult in subResults)
                    {
                        subResultIds[subResult.SequenceId] = subResult.SequenceId;
                    }
                }

                publishedResults[i] = new AzureDevOpsPublishedTestResult(ids[i], subResultIds);
            }

            return publishedResults;
        }
    }

    private sealed class FakeTask(Action<TimeSpan>? delayCallback = null) : ITask
    {
        public List<TimeSpan> DelayCalls { get; } = [];

        public Task Delay(int millisecondDelay)
        {
            DelayCalls.Add(TimeSpan.FromMilliseconds(millisecondDelay));
            return Task.CompletedTask;
        }

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            DelayCalls.Add(timeSpan);
            delayCallback?.Invoke(timeSpan);
            return Task.CompletedTask;
        }

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

    private sealed class CollectingLogger : ILogger
    {
        public List<string> Logs { get; } = [];

        /// <summary>Gets or sets a value indicating whether logging throws, emulating a failing log provider.</summary>
        public bool ThrowOnLog { get; set; }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (ThrowOnLog)
            {
                throw new IOException("simulated log provider failure");
            }

            Logs.Add($"{logLevel}: {formatter(state, exception)}");
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (ThrowOnLog)
            {
                throw new IOException("simulated log provider failure");
            }

            Logs.Add($"{logLevel}: {formatter(state, exception)}");
            return Task.CompletedTask;
        }
    }

    private sealed class QueueHttpMessageHandler(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responses.Dequeue().Invoke(request, cancellationToken);
    }

    private sealed class ThrowingHttpContent(Exception exception) : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.FromException(exception);

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class BlockingHttpContent : HttpContent
    {
        private readonly TaskCompletionSource<bool> _responseBodyReadStarted;

        public BlockingHttpContent(TaskCompletionSource<bool> responseBodyReadStarted)
            => _responseBodyReadStarted = responseBodyReadStarted;

#if NET
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            _responseBodyReadStarted.TrySetResult(true);
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
#endif

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new BlockingReadStream(_responseBodyReadStarted));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource<bool> _responseBodyReadStarted;

        public BlockingReadStream(TaskCompletionSource<bool> responseBodyReadStarted)
            => _responseBodyReadStarted = responseBodyReadStarted;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set
            {
                _ = value;
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _responseBodyReadStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory(ICollection<string> trackedDirectories)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), nameof(AzureDevOpsLivePublishingTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            trackedDirectories.Add(Path);
        }

        public string Path { get; }

        public void Dispose()
            => TryDeleteDirectory(Path);
    }
}
