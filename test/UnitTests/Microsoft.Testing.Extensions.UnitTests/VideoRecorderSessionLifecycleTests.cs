// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderSessionLifecycleTests
{
    private static readonly DateTimeOffset RecordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan RendezvousTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Finish_WaitsForStopBeforeReadingSegmentsAndDeletingScratchDirectory()
    {
        string directory = CreateSegmentDirectory();
        var context = new HandlerContext(directory);
        var stopStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Recorder.Setup(recorder => recorder.StopAsync(CancellationToken.None)).Returns(() =>
        {
            stopStarted.SetResult(true);
            return releaseStop.Task;
        });
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        Task finishing = context.Handler.OnTestSessionFinishingAsync(context.Session.Object);
        Exception? testFailure = null;
        try
        {
            await AwaitWithinAsync(stopStarted.Task, RendezvousTimeout);
            Assert.IsFalse(finishing.IsCompleted);
            Assert.IsTrue(Directory.Exists(directory));
            context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());

            releaseStop.SetResult(true);
            await AwaitWithinAsync(finishing, RendezvousTimeout);

            context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Once());
            Assert.IsFalse(Directory.Exists(directory));
        }
        catch (Exception exception)
        {
            testFailure = exception;
            throw;
        }
        finally
        {
            releaseStop.TrySetResult(true);
            try
            {
                await AwaitWithinAsync(finishing, CleanupTimeout);
            }
            catch (Exception cleanupFailure) when (testFailure is not null)
            {
                ReportCleanupFailure(testFailure, cleanupFailure);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Finish_ConcatFaultOrCancellation_DeletesSegmentsWithoutPublishingArtifact(bool canceled)
    {
        string directory = CreateSegmentDirectory();
        var context = new HandlerContext(directory);
        using var cancellation = new CancellationTokenSource();
        Exception expected = canceled
            ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("concat failure");
        context.Recorder.Setup(recorder => recorder.ReadSegments()).Returns([new VideoSegment("segment.mp4", 0, 4)]);
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None))
            .Returns(Task.FromException<string?>(expected));
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.Handler.ConsumeAsync(null!, Update(new FailedTestNodeStateProperty("failed"), Timing(0, 3)), CancellationToken.None);

        Exception actual = canceled
            ? await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => context.Handler.OnTestSessionFinishingAsync(context.Session.Object))
            : await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));

        Assert.AreSame(expected, actual);
        Assert.IsFalse(Directory.Exists(directory));
        context.Recorder.Verify(recorder => recorder.StopAsync(CancellationToken.None), Times.Once());
        Assert.IsEmpty(context.Artifacts);
    }

    [TestMethod]
    public async Task Consume_DuplicateInProgressAndSupersededFailure_FinalClipRetainsFirstObservedStart()
    {
        var context = new HandlerContext(CreateSegmentDirectory());
        string clipPath = Path.Combine(TestContext.TestTempDirectory!, "clip.mp4");
        var first = new VideoSegment("first.mp4", 0, 2);
        var second = new VideoSegment("second.mp4", 2, 4);
        context.Recorder.Setup(recorder => recorder.ReadSegments()).Returns([first, second]);
        IReadOnlyList<VideoSegment>? captured = null;
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, _, _, _) => captured = segments)
            .ReturnsAsync(clipPath);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        context.Now = RecordingStart.AddSeconds(1);
        await context.Handler.ConsumeAsync(null!, Update(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        context.Now = RecordingStart.AddSeconds(3);
        await context.Handler.ConsumeAsync(null!, Update(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await context.Handler.ConsumeAsync(null!, Update(
            new FailedTestNodeStateProperty("retry"), new RetryAttemptProperty(1, isSuperseded: true)), CancellationToken.None);
        context.Now = RecordingStart.AddSeconds(4);
        await context.Handler.ConsumeAsync(null!, Update(
            PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)), CancellationToken.None);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.IsNotNull(captured);
        Assert.AreSequenceEqual(new[] { first, second }, captured);
        SessionFileArtifact artifact = Assert.ContainsSingle(context.Artifacts);
        Assert.AreEqual(new SessionUid("video-session"), artifact.SessionUid);
        Assert.AreEqual(clipPath, artifact.FileInfo.FullName);
    }

    [TestMethod]
    public async Task Consume_AuthoritativeTiming_OverridesObservedStartAndClockAtCompletion()
    {
        var context = new HandlerContext(CreateSegmentDirectory());
        var earlier = new VideoSegment("earlier.mp4", 0, 2);
        var authoritative = new VideoSegment("authoritative.mp4", 10, 12);
        context.Recorder.Setup(recorder => recorder.ReadSegments()).Returns([earlier, authoritative]);
        IReadOnlyList<VideoSegment>? captured = null;
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, _, _, _) => captured = segments)
            .ReturnsAsync((string?)null);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.Now = RecordingStart;
        await context.Handler.ConsumeAsync(null!, Update(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        context.Now = RecordingStart.AddSeconds(30);

        await context.Handler.ConsumeAsync(null!, Update(PassedTestNodeStateProperty.CachedInstance, Timing(10, 12)), CancellationToken.None);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.IsNotNull(captured);
        Assert.AreSequenceEqual(new[] { authoritative }, captured);
        Assert.IsEmpty(context.Artifacts);
    }

    [TestMethod]
    [DataRow("failed", true)]
    [DataRow("error", true)]
    [DataRow("timeout", true)]
    [DataRow("passed", false)]
    [DataRow("skipped", false)]
    public async Task Consume_OnFailurePersistence_KeepsOnlyFailureOutcomes(string outcome, bool producesVideo)
    {
        var context = new HandlerContext(CreateSegmentDirectory(), VideoRecorderPersistenceMode.OnFailure);
        string clipPath = Path.Combine(TestContext.TestTempDirectory!, "failure.mp4");
        context.Recorder.Setup(recorder => recorder.ReadSegments()).Returns([new VideoSegment("segment.mp4", 0, 4)]);
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None)).ReturnsAsync(clipPath);
        TestNodeStateProperty state = outcome switch
        {
            "failed" => new FailedTestNodeStateProperty("failed"),
            "error" => new ErrorTestNodeStateProperty("error"),
            "timeout" => new TimeoutTestNodeStateProperty("timeout"),
            "skipped" => SkippedTestNodeStateProperty.CachedInstance,
            _ => PassedTestNodeStateProperty.CachedInstance,
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        await context.Handler.ConsumeAsync(null!, Update(state, Timing(0, 3)), CancellationToken.None);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        context.Recorder.Verify(
            recorder => recorder.ConcatAsync(
                It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, CancellationToken.None),
            producesVideo ? Times.Once() : Times.Never());
        Assert.HasCount(producesVideo ? 1 : 0, context.Artifacts);
    }

    [TestMethod]
    public async Task Consume_NonOutcomeMessages_DoNotProduceClips()
    {
        var context = new HandlerContext(CreateSegmentDirectory());
        context.Recorder.Setup(recorder => recorder.ReadSegments()).Returns([new VideoSegment("segment.mp4", 0, 4)]);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        await context.Handler.ConsumeAsync(null!, Mock.Of<IData>(), CancellationToken.None);
        await context.Handler.ConsumeAsync(null!, Update(), CancellationToken.None);
        await context.Handler.ConsumeAsync(null!, Update(DiscoveredTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await context.Handler.ConsumeAsync(null!, Update(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        await context.Handler.ConsumeAsync(null!, Update(TestNodeExecutionCompletedProperty.CachedInstance), CancellationToken.None);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        context.Recorder.Verify(
            recorder => recorder.ConcatAsync(
                It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
        Assert.IsEmpty(context.Artifacts);
    }

    private string CreateSegmentDirectory()
    {
        string directory = Path.Combine(TestContext.TestTempDirectory!, "owned-segments");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "segment.mp4"), "segment");
        return directory;
    }

    private static TimingProperty Timing(double start, double end)
        => new(new TimingInfo(RecordingStart.AddSeconds(start), RecordingStart.AddSeconds(end), TimeSpan.FromSeconds(end - start)));

    private static TestNodeUpdateMessage Update(params IProperty[] properties)
        => new(new SessionUid("video-session"), new TestNode { Uid = "test", DisplayName = "Test clip", Properties = new PropertyBag(properties) });

    private static async Task AwaitWithinAsync(Task task, TimeSpan timeout)
    {
        using var deadlineCancellation = new CancellationTokenSource();
        var deadline = Task.Delay(timeout, deadlineCancellation.Token);
        try
        {
            Assert.AreSame(task, await Task.WhenAny(task, deadline), "Video lifecycle did not terminate within its bound.");
            await task;
        }
        finally
        {
            deadlineCancellation.Cancel();
        }
    }

    private static void ReportCleanupFailure(Exception testFailure, Exception cleanupFailure)
        => throw new AggregateException("Video finishing and cleanup both failed.", testFailure, cleanupFailure);

    private sealed class HandlerContext
    {
        public HandlerContext(string directory, VideoRecorderPersistenceMode persistMode = VideoRecorderPersistenceMode.Always)
        {
            Recorder.SetupGet(recorder => recorder.IsAvailable).Returns(true);
            Recorder.SetupGet(recorder => recorder.RecordingStartUtc).Returns(RecordingStart);
            Recorder.SetupGet(recorder => recorder.SegmentDirectory).Returns(directory);
            Recorder.SetupGet(recorder => recorder.SegmentExtension).Returns("mp4");
            Recorder.Setup(recorder => recorder.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Recorder.Setup(recorder => recorder.ReadSegments()).Returns([]);
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(() => Now);
            var bus = new Mock<IMessageBus>();
            bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, data) => Artifacts.Add((SessionFileArtifact)data))
                .Returns(Task.CompletedTask);
            Session.SetupGet(value => value.CancellationToken).Returns(CancellationToken.None);
            Session.SetupGet(value => value.SessionUid).Returns(new SessionUid("video-session"));
            Handler = new VideoRecorderSessionHandler(
                new VideoRecorderOptions { PersistMode = persistMode, OutputDirectory = directory },
                Mock.Of<IConfiguration>(),
                new TestCommandLineOptions(new() { [VideoRecorderCommandLineProvider.EnableOptionName] = [] }),
                bus.Object, Mock.Of<IOutputDevice>(), clock.Object, Mock.Of<ILogger<VideoRecorderSessionHandler>>(), Recorder.Object);
        }

        public DateTimeOffset Now { get; set; } = RecordingStart;

        public Mock<IVideoRecorder> Recorder { get; } = new();

        public Mock<ITestSessionContext> Session { get; } = new();

        public List<SessionFileArtifact> Artifacts { get; } = [];

        public VideoRecorderSessionHandler Handler { get; }
    }
}
