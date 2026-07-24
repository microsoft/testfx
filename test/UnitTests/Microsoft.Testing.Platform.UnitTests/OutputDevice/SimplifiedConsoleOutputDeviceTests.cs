// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class SimplifiedConsoleOutputDeviceTests
{
    private static readonly IOutputDeviceDataProducer Producer = Mock.Of<IOutputDeviceDataProducer>(
        producer => producer.Uid == "producer");

    public TestContext TestContext { get; set; }

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

        Assert.AreSequenceEqual(new[] { "Restoring", "Restored" }, device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterRemoval_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", null), CancellationToken.None);
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "Restoring", "Restoring" }, device.Messages);
    }

    [TestMethod]
    public async Task DisplayAsync_ProgressMessageAfterSessionFinishes_WritesSameValueAgain()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor);

        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);
        await device.OnTestSessionFinishingAsync(Mock.Of<ITestSessionContext>());
        await device.DisplayAsync(Producer, new ProgressMessageOutputDeviceData("restore", "Restoring"), CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "Restoring", "Restoring" }, device.Messages);
    }

    [TestMethod]
    public async Task ConsumeAsync_InProgressTestNode_WhenActiveTestProgressEnabled_WritesActiveTestDisplayNameOnce()
    {
        using var asyncMonitor = new SystemAsyncMonitor();
        RecordingSimplifiedOutputDevice device = CreateOutputDevice(asyncMonitor, displayActiveTestProgress: true);
        TestNodeUpdateMessage update = CreateTestNodeUpdate(InProgressTestNodeStateProperty.CachedInstance);

        await device.ConsumeAsync(null!, update, CancellationToken.None);
        await device.ConsumeAsync(null!, update, CancellationToken.None);

        Assert.AreSequenceEqual(new[] { "running BrowserTests.HangingTest" }, device.Messages);
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
            new[] { "running BrowserTests.HangingTest", "running BrowserTests.HangingTest" },
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
            new[] { "running BrowserTests.HangingTest", "running BrowserTests.HangingTest" },
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
        await device.ConsumeAsync(
            Mock.Of<IDataProducer>(),
            CreateTestNodeUpdate("uid", "CompletedTest", new PassedTestNodeStateProperty()),
            CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(10));
        await device.ReportDueSlowTestsAsync();

        Assert.IsEmpty(device.Messages);
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
        bool displayActiveTestProgress = false)
    {
        var moduleInfo = new Mock<ITestApplicationModuleInfo>();
        moduleInfo.Setup(x => x.GetDisplayName()).Returns("testhost");
        clock ??= new FakeClock();

        return new RecordingSimplifiedOutputDevice(
            Mock.Of<IConsole>(),
            moduleInfo.Object,
            asyncMonitor,
            Mock.Of<IRuntimeFeature>(),
            Mock.Of<IEnvironment>(),
            Mock.Of<IPlatformInformation>(),
            Mock.Of<IStopPoliciesService>(),
            TimeSpan.FromSeconds(60),
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

        public Task<string> MessageReported => _messageReported.Task;

        public override string DisplayName => nameof(RecordingSimplifiedOutputDevice);

        public override string Description => nameof(RecordingSimplifiedOutputDevice);

        protected override bool DisplayActiveTestProgress => _displayActiveTestProgress;

        protected override void ConsoleWarn(string? message) => RecordMessage(message);

        protected override void ConsoleError(string? message) => RecordMessage(message);

        protected override void ConsoleLog(string? message) => RecordMessage(message);

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
