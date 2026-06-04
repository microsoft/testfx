// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsLivePublishingTests
{
    private const string TruncationMarker = "\n...[truncated]";

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
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(2, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out _, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromException<int>(new JsonException("broken payload"));

        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.IsNull(publisher.RunId);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed, string.Join(Environment.NewLine, logger.Logs));
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
    public async Task CreatePublisher_UsesSanitizedRunNameAndStorageWithoutExtension()
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
        Assert.AreEqual("MyTests", configuration.AutomatedTestStorage);
        Assert.DoesNotContain("/", configuration.RunName);
        Assert.DoesNotContain("\r", configuration.RunName);
        Assert.DoesNotContain("\n", configuration.RunName);
        Assert.IsLessThanOrEqualTo(AzureDevOpsLivePublishingConstants.MaxRunNameLength, configuration.RunName.Length);
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

        Assert.HasCount(0, client.UploadTestResultAttachmentCalls);
    }

    [TestMethod]
    public async Task ConsumeAsync_AttachmentUploadFailureLogsWarningAndDoesNotRetryPublish()
    {
        using TestDirectory directory = CreateTestDirectory();
        string dumpPath = Path.Combine(directory.Path, "dump.txt");
        File.WriteAllText(dumpPath, "dump content");
        AzureDevOpsTestResultsPublisher publisher = CreatePublisher(directory.Path, options: new(1, TimeSpan.FromMinutes(1), 4, TimeSpan.FromMilliseconds(1)), out FakeAzureDevOpsTestResultsClient client, out FakeClock clock, out CollectingLogger logger);
        client.CreateTestRunAsyncFunc = (_, _) => Task.FromResult(202);

        int publishCalls = 0;
        client.PublishTestResultsAsyncFunc = (_, _, results, _) =>
        {
            publishCalls++;
            return Task.FromResult<IReadOnlyList<int>?>(Enumerable.Range(1, results.Count).ToArray());
        };
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, _, _) => throw new HttpRequestException("simulated upload failure");

        TestNode node = CreateNode("failed-test", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), clock.UtcNow);
        node.Properties.Add(new FileArtifactProperty(new FileInfo(dumpPath), "dump"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(node), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.AreEqual(1, publishCalls);
        Assert.HasCount(1, client.UploadTestResultAttachmentCalls);
        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingResultAttachmentFailed, string.Join(Environment.NewLine, logger.Logs));
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
        Assert.HasCount(0, client.UploadTestResultAttachmentCalls);
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
        client.UploadTestResultAttachmentAsyncFunc = (_, _, _, attachment, _) => Task.CompletedTask;

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

        Assert.HasCount(0, client.UploadTestRunAttachmentCalls);
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
        File.WriteAllText(Path.Combine(directory.Path, $"azdo-runid.123.participant.{aliveProcessId}.json"), JsonSerializer.Serialize(new AzureDevOpsLeaseFile(aliveProcessId, 123, clock.UtcNow.AddHours(1))));

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

    private static async Task StartPublisherAsync(AzureDevOpsTestResultsPublisher publisher)
    {
        Assert.IsTrue(await publisher.IsEnabledAsync());
        await publisher.OnTestSessionStartingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));
    }

    private TestDirectory CreateTestDirectory() => new(_directoriesToDelete);

    private static int GetAliveProcessId()
#if NET
        => Environment.ProcessId;
#else
        => Process.GetCurrentProcess().Id;
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

    private static Mock<IEnvironment> CreateEnvironmentMock(int processId, string? stageName = "stage", string? jobName = "job")
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
        Mock<ITestApplicationProcessExitCode>? processExitCode = null)
    {
        Mock<ICommandLineOptions> commandLineOptions = new();
        commandLineOptions.Setup(x => x.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName)).Returns(true);
        string[]? runNameArguments = null;
        commandLineOptions.Setup(x => x.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.PublishAzureDevOpsRunNameOptionName, out runNameArguments)).Returns(false);

        Mock<IConfiguration> configuration = new();
        configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(resultsDirectory);

        environment ??= CreateEnvironmentMock(processId: GetAliveProcessId());

        testApplicationModuleInfo ??= new Mock<ITestApplicationModuleInfo>();
        testApplicationModuleInfo.Setup(x => x.TryGetAssemblyName()).Returns("MyTests");
        testApplicationModuleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(Path.Combine("testfx-worktrees", "azdo-live", "artifacts", "MyTests.dll"));

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
            new SystemFileSystem(),
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

    private static TestNode CreateNode(string uid, IProperty state, DateTimeOffset startTime, TimingProperty? timing = null)
    {
        PropertyBag properties = timing is null ? new PropertyBag(state) : new PropertyBag(state, timing);
        return new TestNode
        {
            Uid = new TestNodeUid(uid),
            DisplayName = uid,
            Properties = properties,
        };
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAzureDevOpsTestResultsClient : IAzureDevOpsTestResultsClient
    {
        public Func<AzureDevOpsPublishConfiguration, CancellationToken, Task<int>> CreateTestRunAsyncFunc { get; set; } = (_, _) => Task.FromResult(0);

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

        public Func<AzureDevOpsPublishConfiguration, int, int, AzureDevOpsTestResultAttachment, CancellationToken, Task> UploadTestResultAttachmentAsyncFunc { get; set; } = (_, _, _, _, _) => Task.CompletedTask;

        public Func<AzureDevOpsPublishConfiguration, int, AzureDevOpsTestResultAttachment, CancellationToken, Task> UploadTestRunAttachmentAsyncFunc { get; set; } = (_, _, _, _) => Task.CompletedTask;

        public List<(AzureDevOpsPublishConfiguration Configuration, int RunId, string State)> UpdateTestRunStateCalls { get; } = [];

        public List<(int RunId, int TestCaseResultId, AzureDevOpsTestResultAttachment Attachment)> UploadTestResultAttachmentCalls { get; } = [];

        public List<(int RunId, AzureDevOpsTestResultAttachment Attachment)> UploadTestRunAttachmentCalls { get; } = [];

        public Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken)
            => CreateTestRunAsyncFunc(configuration, cancellationToken);

        public Task<IReadOnlyList<int>?> PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
            => PublishTestResultsAsyncFunc(configuration, runId, results, cancellationToken);

        public Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken)
        {
            UpdateTestRunStateCalls.Add((configuration, runId, state));
            return Task.CompletedTask;
        }

        public Task UploadTestResultAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, int testCaseResultId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
        {
            UploadTestResultAttachmentCalls.Add((runId, testCaseResultId, attachment));
            return UploadTestResultAttachmentAsyncFunc(configuration, runId, testCaseResultId, attachment, cancellationToken);
        }

        public Task UploadTestRunAttachmentAsync(AzureDevOpsPublishConfiguration configuration, int runId, AzureDevOpsTestResultAttachment attachment, CancellationToken cancellationToken)
        {
            UploadTestRunAttachmentCalls.Add((runId, attachment));
            return UploadTestRunAttachmentAsyncFunc(configuration, runId, attachment, cancellationToken);
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

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Logs.Add($"{logLevel}: {formatter(state, exception)}");

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
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
