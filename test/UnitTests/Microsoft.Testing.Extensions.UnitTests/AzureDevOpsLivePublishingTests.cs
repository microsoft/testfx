// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Http;
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
            return Task.CompletedTask;
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
            return Task.CompletedTask;
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
            return Task.CompletedTask;
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
    public void CreateTestCaseResult_MapsMtpStatesToAzdoResults()
    {
        DateTimeOffset startTime = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        TimingProperty timing = new(new TimingInfo(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(2)));
        AzureDevOpsTestCaseResult? passed = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("passed", new PassedTestNodeStateProperty(), startTime, timing), "tests.dll");
        AzureDevOpsTestCaseResult? failed = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("failed", new FailedTestNodeStateProperty(new InvalidOperationException("boom")), startTime, timing), "tests.dll");
        AzureDevOpsTestCaseResult? skipped = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("skipped", new SkippedTestNodeStateProperty("skip"), startTime, timing), "tests.dll");
        AzureDevOpsTestCaseResult? timeout = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("timeout", new TimeoutTestNodeStateProperty("too slow"), startTime, timing), "tests.dll");
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
        AzureDevOpsTestCaseResult? cancelled = AzureDevOpsTestResultsPublisher.CreateTestCaseResult(CreateNode("cancelled", new CancelledTestNodeStateProperty("stopped"), startTime, timing), "tests.dll");
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
        CollectionAssert.AreEqual(new[] { "send:1", "delay:3", "send:2" }, events);
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
        client.PublishTestResultsAsyncFunc = (_, _, _, _) => Task.FromException(new JsonException("publish failed"));

        await StartPublisherAsync(publisher);
        await publisher.ConsumeAsync(Mock.Of<IDataProducer>(), CreateMessage(CreateNode("test-1", new PassedTestNodeStateProperty(), clock.UtcNow)), CancellationToken.None);
        await publisher.OnTestSessionFinishingAsync(new Microsoft.Testing.Platform.Services.TestSessionContext(CancellationToken.None));

        Assert.Contains(AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed, string.Join(Environment.NewLine, logger.Logs));
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
        ITask? task = null)
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

        Mock<ITestApplicationProcessExitCode> processExitCode = new();
        processExitCode.Setup(x => x.GetProcessExitCode()).Returns(0);
        processExitCode.SetupGet(x => x.HasTestAdapterTestSessionFailure).Returns(false);

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

        public Func<AzureDevOpsPublishConfiguration, int, IReadOnlyList<AzureDevOpsTestCaseResult>, CancellationToken, Task> PublishTestResultsAsyncFunc { get; set; } = (_, _, _, _) => Task.CompletedTask;

        public List<(AzureDevOpsPublishConfiguration Configuration, int RunId, string State)> UpdateTestRunStateCalls { get; } = [];

        public Task<int> CreateTestRunAsync(AzureDevOpsPublishConfiguration configuration, CancellationToken cancellationToken)
            => CreateTestRunAsyncFunc(configuration, cancellationToken);

        public Task PublishTestResultsAsync(AzureDevOpsPublishConfiguration configuration, int runId, IReadOnlyList<AzureDevOpsTestCaseResult> results, CancellationToken cancellationToken)
            => PublishTestResultsAsyncFunc(configuration, runId, results, cancellationToken);

        public Task UpdateTestRunStateAsync(AzureDevOpsPublishConfiguration configuration, int runId, string state, CancellationToken cancellationToken)
        {
            UpdateTestRunStateCalls.Add((configuration, runId, state));
            return Task.CompletedTask;
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
