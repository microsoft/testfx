// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class SimplifiedConsoleOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LifecycleBannerIsWrittenAtMostOnceAndHonorsInheritedSuppression(bool suppressed)
    {
        using var monitor = new SystemAsyncMonitor();
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER))
            .Returns(suppressed ? "1" : null);
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero, environment: environment.Object);

        await device.DisplayBannerAsync("exact banner", TestContext.CancellationToken);
        await device.DisplayBannerAsync("must not be written", TestContext.CancellationToken);

        Assert.AreSequenceEqual(suppressed ? Array.Empty<string>() : ["exact banner"], device.Logs);
        environment.Verify(value => value.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1"), suppressed ? Times.Never : Times.Once);
        Assert.IsEmpty(device.Errors);
    }

    [TestMethod]
    public async Task LifecycleAssemblyPresentationDoesNotInventFrameworkOrArchitectureForStaticRuntime()
    {
        using var monitor = new SystemAsyncMonitor();
        var console = new Mock<IConsole>();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero, console: console.Object);

        await device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);

        console.Verify(value => value.WriteLine("testhost"), Times.Once);
    }

    [TestMethod]
    [DataRow("net8.0", null, "testhost (net8.0|)")]
    [DataRow(null, "x64", "testhost (x64)")]
    [DataRow("net8.0", "x64", "testhost (net8.0|x64)")]
    public async Task LifecycleAssemblyPresentationPreservesEachOptionalRuntimeComponent(string? framework, string? architecture, string expected)
    {
        using var monitor = new SystemAsyncMonitor();
        var console = new Mock<IConsole>();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero, console: console.Object);
        typeof(SimplifiedConsoleOutputDeviceBase).GetField("_targetFramework", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, framework);
        typeof(SimplifiedConsoleOutputDeviceBase).GetField("_longArchitecture", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, architecture);

        await device.DisplayBeforeSessionStartAsync(TestContext.CancellationToken);

        console.Verify(value => value.WriteLine(expected), Times.Once);
    }

    [TestMethod]
    public async Task LifecycleGeneratedBannerUsesVersionShortCommitAndUtcDateWithoutRuntimeForStaticHost()
    {
        using var monitor = new SystemAsyncMonitor();
        var platform = new Mock<IPlatformInformation>();
        platform.SetupGet(value => value.Name).Returns("Fixture.Platform");
        platform.SetupGet(value => value.Version).Returns("1.2.3");
        platform.SetupGet(value => value.CommitHash).Returns("1234567890abcdef");
        platform.SetupGet(value => value.BuildDate).Returns(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero, platform: platform.Object);

        await device.DisplayBannerAsync(null, TestContext.CancellationToken);

        // The Framework StringBuilder polyfill receives an already formatted interpolation,
        // while modern runtimes honor the interpolated handler's invariant provider.
#if NETFRAMEWORK
        string date = new DateTime(2026, 1, 2).ToString("d", CultureInfo.CurrentCulture);
#else
        string date = new DateTime(2026, 1, 2).ToString("d", CultureInfo.InvariantCulture);
#endif
        Assert.AreEqual("Fixture.Platform v1.2.3+1234567890 (UTC " + date + ")", Assert.ContainsSingle(device.Logs));
    }

    [TestMethod]
    [DataRow(0, 0, 0, false, "Zero tests ran", true)]
    [DataRow(2, 0, 1, false, "Passed!", false)]
    [DataRow(0, 0, 2, false, "Passed!", false)]
    [DataRow(1, 1, 1, false, "Failed!", true)]
    [DataRow(1, 0, 0, true, "Aborted", true)]
    public async Task LifecycleSummaryPinsActualCountersVerdictAndOutputRouting(int passed, int failed, int skipped, bool canceled, string verdict, bool errorRoute)
    {
        using var monitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero);
        await device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);
        Assert.IsEmpty(device.Messages, "No summary may be printed before any session starts.");
        await device.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        for (int i = 0; i < passed; i++)
        {
            await device.ConsumeAsync(null!, CreateTestNodeUpdate("p" + i, "passed", PassedTestNodeStateProperty.CachedInstance), TestContext.CancellationToken);
        }

        for (int i = 0; i < failed; i++)
        {
            await device.ConsumeAsync(null!, CreateTestNodeUpdate("f" + i, "failed", new FailedTestNodeStateProperty("failure")), TestContext.CancellationToken);
        }

        for (int i = 0; i < skipped; i++)
        {
            await device.ConsumeAsync(null!, CreateTestNodeUpdate("s" + i, "skipped", SkippedTestNodeStateProperty.CachedInstance), TestContext.CancellationToken);
        }

        using CancellationTokenSource cancellation = new();
        if (canceled)
        {
            cancellation.Cancel();
        }

        int logsBefore = device.Logs.Count;
        int errorsBefore = device.Errors.Count;
        await device.DisplayAfterSessionEndRunAsync(canceled ? cancellation.Token : TestContext.CancellationToken);
        string expected = $"""
            Test run summary: {verdict}
              total: {passed + failed + skipped}
              failed: {failed}
              succeeded: {passed}
              skipped: {skipped}
            """;
        Assert.AreEqual(expected, errorRoute ? device.Errors[errorsBefore] : device.Logs[logsBefore]);
        Assert.HasCount(logsBefore + (errorRoute ? 0 : 1), device.Logs);
        Assert.HasCount(errorsBefore + (errorRoute ? 1 : 0), device.Errors);
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
    }

    [TestMethod]
    [DataRow("failed")]
    [DataRow("error")]
    [DataRow("timeout")]
    [DataRow("cancelled")]
    public async Task LifecycleFailureStatesPreserveExplanationExceptionDurationAndCount(string stateName)
    {
        using var monitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero);
        await device.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        InvalidOperationException exception = new("failure sentinel");
#pragma warning disable CS0618, MTP0001 // Exercise retained canceled-state compatibility behavior.
        TestNodeStateProperty state = stateName switch
        {
            "failed" => new FailedTestNodeStateProperty(exception, "exact explanation"),
            "error" => new ErrorTestNodeStateProperty(exception, "exact explanation"),
            "timeout" => new TimeoutTestNodeStateProperty(exception, "exact explanation"),
            _ => new CancelledTestNodeStateProperty(exception, "exact explanation"),
        };
#pragma warning restore CS0618, MTP0001
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        TestNodeUpdateMessage update = CreateTestNodeUpdate("id", "observed failure", state);
        update.TestNode.Properties.Add(new TimingProperty(new TimingInfo(start, start.AddMilliseconds(10), TimeSpan.FromMilliseconds(10))));

        await device.ConsumeAsync(null!, update, TestContext.CancellationToken);

        Assert.AreEqual("failed observed failure (10ms)" + Environment.NewLine + "  exact explanation" + Environment.NewLine + "  " + exception, Assert.ContainsSingle(device.Errors));
        await device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);
        Assert.AreEqual(
            """
            Test run summary: Failed!
              total: 1
              failed: 1
              succeeded: 0
              skipped: 0
            """,
            device.Errors[1]);
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
    }

    [TestMethod]
    public async Task LifecycleSupersededRetryAndCanceledUpdatesCannotAddFalseFailureCounts()
    {
        using var monitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero);
        await device.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        TestNodeUpdateMessage superseded = CreateTestNodeUpdate("same", "superseded", new FailedTestNodeStateProperty("not final"));
        superseded.TestNode.Properties.Add(new RetryAttemptProperty(1, isSuperseded: true));
        await device.ConsumeAsync(null!, superseded, TestContext.CancellationToken);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await device.ConsumeAsync(null!, CreateTestNodeUpdate("ignored", "ignored", new FailedTestNodeStateProperty("canceled update")), canceled.Token);
        await device.ConsumeAsync(null!, CreateTestNodeUpdate("same", "final", PassedTestNodeStateProperty.CachedInstance), TestContext.CancellationToken);

        await device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        Assert.IsEmpty(device.Errors);
        Assert.AreEqual(
            """
            Test run summary: Passed!
              total: 1
              failed: 0
              succeeded: 1
              skipped: 0
            """,
            Assert.ContainsSingle(device.Logs));
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
    }

    [TestMethod]
    public async Task LifecycleAbortAndMaximumFailureCallbacksUseActualPolicyRegistrationAndVerdict()
    {
        using var monitor = new SystemAsyncMonitor();
        var policies = new Mock<IStopPoliciesService>();
        Func<Task>? abort = null;
        Func<int, CancellationToken, Task>? maximumFailure = null;
        policies.Setup(value => value.RegisterOnAbortCallbackAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(value => abort = value).Returns(Task.CompletedTask);
        policies.Setup(value => value.RegisterOnMaxFailedTestsCallbackAsync(It.IsAny<Func<int, CancellationToken, Task>>()))
            .Callback<Func<int, CancellationToken, Task>>(value => maximumFailure = value).Returns(Task.CompletedTask);
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero, policies: policies.Object);

        await device.InitializeAsync();
        await device.HandleProcessRoleAsync(TestProcessRole.TestHostController, TestContext.CancellationToken);
        Assert.IsNull(maximumFailure, "A controller must not register the test-host-only failure callback.");
        await device.HandleProcessRoleAsync(TestProcessRole.TestHost, TestContext.CancellationToken);
        await maximumFailure!(3, TestContext.CancellationToken);
        await device.OnTestSessionStartingAsync(Mock.Of<ITestSessionContext>());
        await device.ConsumeAsync(null!, CreateTestNodeUpdate(PassedTestNodeStateProperty.CachedInstance), TestContext.CancellationToken);
        await abort!();
        await device.DisplayAfterSessionEndRunAsync(TestContext.CancellationToken);

        Assert.AreSequenceEqual(
            [
                "Test session is aborting due to reaching failures ('3') specified by the '--maximum-failed-tests' option.",
                "Canceling the test session...",
                "Press Ctrl+C again to force exit.",
            ],
            device.Logs);
        Assert.AreEqual(
            """
            Test run summary: Aborted
              total: 1
              failed: 0
              succeeded: 1
              skipped: 0
            """,
            Assert.ContainsSingle(device.Errors));
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
    }

    [TestMethod]
    public async Task LifecycleFormattedWarningErrorAndExceptionMessagesKeepConcreteOutputRoutes()
    {
        using var monitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(monitor, slowTestThreshold: TimeSpan.Zero);
        InvalidOperationException exception = new("display sentinel");

        await device.DisplayAsync(Producer, new FormattedTextOutputDeviceData("formatted") { Padding = 7 }, TestContext.CancellationToken);
        await device.DisplayAsync(Producer, new TextOutputDeviceData("plain"), TestContext.CancellationToken);
        await device.DisplayAsync(Producer, new WarningMessageOutputDeviceData("warning"), TestContext.CancellationToken);
        await device.DisplayAsync(Producer, new ErrorMessageOutputDeviceData("error"), TestContext.CancellationToken);
        await device.DisplayAsync(Producer, new ExceptionOutputDeviceData(exception), TestContext.CancellationToken);

        Assert.AreSequenceEqual(["formatted", "plain"], device.Logs);
        Assert.AreSequenceEqual(["warning"], device.Warnings);
        Assert.AreSequenceEqual(["error", exception.ToString()], device.Errors);
    }

    [TestMethod]
    public async Task DisplayAsync_SessionMessage_WritesDurableOutput()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new SessionMessageOutputDeviceData("Restoring assets"), CancellationToken.None);

        Assert.HasCount(1, device.Messages);
        Assert.AreEqual("Restoring assets", device.Messages[0]);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessage_WritesOnlyChangedValues()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restored"), CancellationToken.None);

        Assert.AreSequenceEqual(["Restoring", "Restored"], device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterRemoval_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(["Restoring", "Restoring"], device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterSessionFinishes_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(["Restoring", "Restoring"], device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_TestNodeFileArtifact_WritesDisplayNameAndFullPath()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);
        var artifact = new FileInfo(Path.Combine("artifacts", "test-result.trx"));

        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(new FileArtifactProperty(artifact, "Test Result")),
            CancellationToken.None);

        Assert.AreSequenceEqual([$"Test Result: {artifact.FullName}"], device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_SessionFileArtifact_WritesDisplayNameAndFullPath()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);
        var artifact = new FileInfo(Path.Combine("artifacts", "session-result.trx"));

        await device.ConsumeAsync(
            null!,
            new SessionFileArtifact(new SessionUid("session"), artifact, "Session Result"),
            CancellationToken.None);

        Assert.AreSequenceEqual([$"Session Result: {artifact.FullName}"], device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_FileArtifact_WritesDisplayNameAndFullPath()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);
        var artifact = new FileInfo(Path.Combine("artifacts", "run-result.trx"));

        await device.ConsumeAsync(
            null!,
            new FileArtifact(artifact, "Run Result"),
            CancellationToken.None);

        Assert.AreSequenceEqual([$"Run Result: {artifact.FullName}"], device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_InProgressTestNode_WhenActiveTestProgressEnabled_WritesActiveTestDisplayNameOnce()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, displayActiveTestProgress: true);
        TestNodeUpdateMessage update = CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance);

        await device.ConsumeAsync(null!, update, CancellationToken.None);
        await device.ConsumeAsync(null!, update, CancellationToken.None);

        Assert.AreSequenceEqual(["running BrowserTests.HangingTest"], device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_InProgressTestNode_WhenActiveTestProgressDisabled_DoesNotWrite()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.IsEmpty(device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_CompletedTestNode_AllowsSameTestToReportProgressAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, displayActiveTestProgress: true);

        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(PassedTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            ["running BrowserTests.HangingTest", "running BrowserTests.HangingTest"],
            device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_EmptyResult_AllowsSameTestToReportProgressAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, displayActiveTestProgress: true);

        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);
        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(TestNodeExecutionCompletedProperty.CachedInstance),
            CancellationToken.None);
        await device.ConsumeAsync(
            null!,
            CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance),
            CancellationToken.None);

        Assert.AreSequenceEqual(
            ["running BrowserTests.HangingTest", "running BrowserTests.HangingTest"],
            device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_InProgressTest_ReportsOnlyAfterSlowThreshold()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "AsyncHang", new InProgressTestNodeStateProperty()),
            CancellationToken.None);

        Assert.IsEmpty(device.Messages);

        clock.Advance(TimeSpan.FromSeconds(60));
        await device.ReportDueSlowTestsAsync();

        Assert.HasCount(1, device.Messages);
        string message = device.Messages[0]!;
        Assert.Contains("[slow]", message);
        Assert.Contains("AsyncHang", message);
        Assert.Contains("1m", message);
    }

    [TestMethod]
    public async Task ConsumeAsync_CompletedTest_IsRemovedFromSlowTestTracking()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "CompletedTest", new InProgressTestNodeStateProperty()),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(60));
        await device.ReportDueSlowTestsAsync();
        Assert.HasCount(1, device.Messages);
        Assert.Contains("CompletedTest", device.Messages[0]!);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "CompletedTest", new PassedTestNodeStateProperty()),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(10));
        await device.ReportDueSlowTestsAsync();

        Assert.HasCount(1, device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_EmptyResultFollowedBySlowTest_ReportsOnlySlowTest()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("dropped-uid", "DroppedTest", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("dropped-uid", "DroppedTest", TestNodeExecutionCompletedProperty.CachedInstance),
            CancellationToken.None);
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("slow-uid", "SlowTest", new InProgressTestNodeStateProperty()),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(60));
        await device.ReportDueSlowTestsAsync();

        Assert.HasCount(1, device.Messages);
        string message = device.Messages[0]!;
        Assert.Contains("SlowTest", message);
        Assert.DoesNotContain("DroppedTest", message);
    }

    [TestMethod]
    public async Task ConsumeAsync_SameNameTests_AreTrackedIndependentlyByUid()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid-1", "SameName", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid-2", "SameName", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid-1", "SameName", new PassedTestNodeStateProperty()),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(60));
        await device.ReportDueSlowTestsAsync();

        Assert.HasCount(1, device.Messages);
        string message = device.Messages[0]!;
        Assert.Contains("SameName", message);
    }

    [TestMethod]
    public async Task SessionLifecycle_CancelsAndAwaitsCooperativeReporter()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);
        ITestSessionContext context = Mock.Of<ITestSessionContext>(
            x => x.CancellationToken == CancellationToken.None);

        await device.OnTestSessionStartingAsync(context);
        await device.OnTestSessionStartingAsync(context);
        await Task.WhenAll(
            device.OnTestSessionFinishingAsync(context),
            device.OnTestSessionFinishingAsync(context));

        Assert.IsEmpty(device.Messages);
    }

    [TestMethod]
    public async Task ReportSlowTestsOnceAsync_TestCompletesWhileWaitingForOutputLock_DoesNotReportIt()
    {
        var lockRequested = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lockGranted = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asyncMonitor = new Mock<IAsyncMonitor>();
        asyncMonitor
            .Setup(monitor => monitor.LockAsync(It.IsAny<TimeSpan>()))
            .Callback(() => lockRequested.TrySetResult(null))
            .Returns(lockGranted.Task);
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor.Object, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "CompletedWhileWaiting", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        Task reportTask = device.ReportDueSlowTestsAsync();
        await lockRequested.Task;
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "CompletedWhileWaiting", new PassedTestNodeStateProperty()),
            CancellationToken.None);
        lockGranted.SetResult(Mock.Of<IDisposable>());
        await reportTask;

        Assert.IsEmpty(device.Messages);
    }

    [TestMethod]
    public async Task ReportSlowTestsOnceAsync_TestRestartsWithSameUidWhileWaitingForOutputLock_DoesNotReportStaleDiagnostic()
    {
        var lockRequested = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lockGranted = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asyncMonitor = new Mock<IAsyncMonitor>();
        asyncMonitor
            .Setup(monitor => monitor.LockAsync(It.IsAny<TimeSpan>()))
            .Callback(() => lockRequested.TrySetResult(null))
            .Returns(lockGranted.Task);
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor.Object, clock);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "OriginalExecution", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        Task reportTask = device.ReportDueSlowTestsAsync();
        await lockRequested.Task;
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "OriginalExecution", new PassedTestNodeStateProperty()),
            CancellationToken.None);
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "ReplacementExecution", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        lockGranted.SetResult(Mock.Of<IDisposable>());
        await reportTask;

        Assert.IsEmpty(device.Messages);
    }

    [TestMethod]
    public async Task CooperativeReporter_WhenTestIsDue_EmitsSlowDiagnostic()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        var clock = new FakeClock();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, clock, TimeSpan.FromMilliseconds(1));
        ITestSessionContext context = Mock.Of<ITestSessionContext>(
            x => x.CancellationToken == CancellationToken.None);

        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "AsyncHang", new InProgressTestNodeStateProperty()),
            CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        await device.OnTestSessionStartingAsync(context);
        Task completedTask = await Task.WhenAny(device.MessageReported, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
        await device.OnTestSessionFinishingAsync(context);

        Assert.AreSame(device.MessageReported, completedTask);
        Assert.Contains("[slow]", await device.MessageReported);
        Assert.Contains("AsyncHang", await device.MessageReported);
    }

    private static TestNodeUpdateMessage CreateTestNodeUpdate(IProperty property)
        => CreateTestNodeUpdate("hanging-test", "BrowserTests.HangingTest", property);

    private static TestNodeUpdateMessage CreateTestNodeUpdate(
        string uid,
        string displayName,
        IProperty property)
        => new(
            default,
            new TestNode
            {
                Uid = new TestNodeUid(uid),
                DisplayName = displayName,
                Properties = new PropertyBag(property),
            });

    private static RecordingSimplifiedOutputDevice CreateOutputDevice(
        IAsyncMonitor asyncMonitor,
        FakeClock? clock = null,
        TimeSpan? slowTestPollInterval = null,
        bool displayActiveTestProgress = false,
        TimeSpan? slowTestThreshold = null,
        IEnvironment? environment = null,
        IConsole? console = null,
        IStopPoliciesService? policies = null,
        IPlatformInformation? platform = null)
    {
        var moduleInfo = new Mock<ITestApplicationModuleInfo>();
        moduleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");
        clock ??= new FakeClock();

        return new RecordingSimplifiedOutputDevice(
            console ?? Mock.Of<IConsole>(),
            moduleInfo.Object,
            asyncMonitor,
            Mock.Of<IRuntimeFeature>(),
            environment ?? Mock.Of<IEnvironment>(),
            platform ?? Mock.Of<IPlatformInformation>(),
            policies ?? Mock.Of<IStopPoliciesService>(),
            slowTestThreshold ?? TimeSpan.FromSeconds(60),
            clock.CreateStopwatch,
            slowTestPollInterval ?? TimeSpan.FromSeconds(1),
            displayActiveTestProgress);
    }

    private sealed class RecordingSimplifiedOutputDevice : SimplifiedConsoleOutputDeviceBase
    {
        private readonly TaskCompletionSource<string> _messageReported = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _displayActiveTestProgress;

        public RecordingSimplifiedOutputDevice(
            IConsole console,
            ITestApplicationModuleInfo testApplicationModuleInfo,
            IAsyncMonitor asyncMonitor,
            IRuntimeFeature runtimeFeature,
            IEnvironment environment,
            IPlatformInformation platformInformation,
            IStopPoliciesService policiesService,
            TimeSpan slowTestThreshold,
            Func<IStopwatch> createStopwatch,
            TimeSpan slowTestPollInterval,
            bool displayActiveTestProgress)
            : base(
                console,
                testApplicationModuleInfo,
                asyncMonitor,
                runtimeFeature,
                environment,
                platformInformation,
                policiesService,
                slowTestThreshold,
                createStopwatch,
                slowTestPollInterval)
        {
            _displayActiveTestProgress = displayActiveTestProgress;
        }

        public List<string?> Messages { get; } = [];

        public List<string?> Logs { get; } = [];

        public List<string?> Errors { get; } = [];

        public List<string?> Warnings { get; } = [];

        public Task<string> MessageReported => _messageReported.Task;

        public override string DisplayName => nameof(RecordingSimplifiedOutputDevice);

        public override string Description => nameof(RecordingSimplifiedOutputDevice);

        protected override bool DisplayActiveTestProgress => _displayActiveTestProgress;

        protected override void ConsoleWarn(string? message)
        {
            Warnings.Add(message);
            RecordMessage(message);
        }

        protected override void ConsoleError(string? message)
        {
            Errors.Add(message);
            RecordMessage(message);
        }

        protected override void ConsoleLog(string? message)
        {
            Logs.Add(message);
            RecordMessage(message);
        }

        public Task ReportDueSlowTestsAsync()
            => ReportSlowTestsOnceAsync(CancellationToken.None);

        private void RecordMessage(string? message)
        {
            Messages.Add(message);
            if (message is not null)
            {
                _messageReported.TrySetResult(message);
            }
        }
    }

    private sealed class FakeClock
    {
        private TimeSpan _now;

        public void Advance(TimeSpan duration) => _now += duration;

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
}
