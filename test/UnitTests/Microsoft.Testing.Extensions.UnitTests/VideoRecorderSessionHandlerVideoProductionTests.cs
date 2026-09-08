// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderSessionHandlerVideoProductionTests
{
    private static readonly MethodInfo ProduceVideosAsyncMethod =
        typeof(VideoRecorderSessionHandler).GetMethod(
            "ProduceVideosAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve ProduceVideosAsync.");

    private static readonly FieldInfo SessionUidField =
        typeof(VideoRecorderSessionHandler).GetField(
            "_sessionUid",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve _sessionUid.");

    private static readonly DateTimeOffset RecordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ProduceVideosAsync_RecordingHasNotStarted_DoesNothing()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            recordingStartUtc: null);

        await InvokeProduceVideosAsync(context.Handler);

        context.Recorder.Verify(instance => instance.ReadSegments(), Times.Never());
        VerifyNoConcat(context.Recorder);
        context.OutputDevice.Verify(
            instance => instance.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<IOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
        context.MessageBus.Verify(
            instance => instance.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()),
            Times.Never());
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerTestOnFailurePassedRecord_DoesNotConcat()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            "Passed test",
            PassedTestNodeStateProperty.CachedInstance,
            RecordingStart.AddSeconds(15),
            RecordingStart.AddSeconds(25));
        context.CurrentSegments = [new VideoSegment("segment.mp4", 10, 20)];

        await InvokeProduceVideosAsync(context.Handler);

        VerifyNoConcat(context.Recorder);
        Assert.IsEmpty(context.PublishedData);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerTestOnFailureFailedRecord_ConcatsOnlyOverlappingSegmentsInSourceOrder()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            "Failed test",
            new FailedTestNodeStateProperty("failure"),
            RecordingStart.AddSeconds(15),
            RecordingStart.AddSeconds(25));
        var before = new VideoSegment("before.mp4", 0, 10);
        var overlapsStart = new VideoSegment("overlaps-start.mp4", 10, 20);
        var overlapsEnd = new VideoSegment("overlaps-end.mp4", 20, 30);
        var after = new VideoSegment("after.mp4", 30, 40);
        context.CurrentSegments = [before, overlapsStart, overlapsEnd, after];
        IReadOnlyList<VideoSegment>? capturedSegments = null;
        SetupConcat(context.Recorder, segments => capturedSegments = [.. segments]);

        await InvokeProduceVideosAsync(context.Handler);

        AssertSegments(capturedSegments, overlapsStart, overlapsEnd);
        context.Recorder.Verify(
            instance => instance.ConcatAsync(
                It.IsAny<IReadOnlyList<VideoSegment>>(),
                It.IsAny<string>(),
                null,
                CancellationToken.None),
            Times.Once());
        Assert.IsEmpty(context.PublishedData);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerTestStartBeforeRecording_ClampsStartToZero()
    {
        DateTimeOffset recordingStart = RecordingStart.AddSeconds(10);
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            recordingStart);
        await AddRecordAsync(
            context.Handler,
            "Pre-recording test",
            new FailedTestNodeStateProperty("failure"),
            RecordingStart.AddSeconds(5),
            RecordingStart.AddSeconds(15));
        var beforeRecording = new VideoSegment("before-recording.mp4", -5, 0);
        var afterRecording = new VideoSegment("after-recording.mp4", 0, 5);
        context.CurrentSegments = [beforeRecording, afterRecording];
        IReadOnlyList<VideoSegment>? capturedSegments = null;
        SetupConcat(context.Recorder, segments => capturedSegments = [.. segments]);

        await InvokeProduceVideosAsync(context.Handler);

        AssertSegments(capturedSegments, afterRecording);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerTestWithNoSurvivingOverlap_SkipsConcatAndTraces()
    {
        const string DisplayName = "Pruned failed test";
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            DisplayName,
            new FailedTestNodeStateProperty("failure"),
            RecordingStart.AddSeconds(15),
            RecordingStart.AddSeconds(25));
        context.CurrentSegments = [new VideoSegment("non-overlapping.mp4", 30, 40)];

        await InvokeProduceVideosAsync(context.Handler);

        VerifyNoConcat(context.Recorder);
        Assert.IsEmpty(context.PublishedData);
        context.Logger.Verify(
            instance => instance.LogAsync(
                LogLevel.Trace,
                It.Is<string>(message => message.Contains(DisplayName, StringComparison.Ordinal)),
                null,
                It.IsAny<Func<string, Exception?, string>>()),
            Times.Once());
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerSessionOnFailurePassedOnly_DoesNotConcat()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerSession,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            "Passed test",
            PassedTestNodeStateProperty.CachedInstance,
            RecordingStart.AddSeconds(10),
            RecordingStart.AddSeconds(20));
        context.CurrentSegments = [new VideoSegment("segment.mp4", 0, 30)];

        await InvokeProduceVideosAsync(context.Handler);

        VerifyNoConcat(context.Recorder);
        Assert.IsEmpty(context.PublishedData);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerSessionOnFailureWithFailure_ConcatsAllSegments()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerSession,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            "Failed test",
            new FailedTestNodeStateProperty("failure"),
            RecordingStart.AddSeconds(15),
            RecordingStart.AddSeconds(25));
        var first = new VideoSegment("first.mp4", 0, 10);
        var second = new VideoSegment("second.mp4", 10, 20);
        var third = new VideoSegment("third.mp4", 20, 30);
        context.CurrentSegments = [first, second, third];
        IReadOnlyList<VideoSegment>? capturedSegments = null;
        SetupConcat(context.Recorder, segments => capturedSegments = [.. segments]);

        await InvokeProduceVideosAsync(context.Handler);

        AssertSegments(capturedSegments, first, second, third);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerSessionAlwaysPassedOnly_ConcatsAllSegments()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.Always,
            VideoCaptureGranularity.PerSession,
            RecordingStart);
        await AddRecordAsync(
            context.Handler,
            "Passed test",
            PassedTestNodeStateProperty.CachedInstance,
            RecordingStart.AddSeconds(15),
            RecordingStart.AddSeconds(25));
        var first = new VideoSegment("first.mp4", 0, 10);
        var second = new VideoSegment("second.mp4", 10, 20);
        context.CurrentSegments = [first, second];
        IReadOnlyList<VideoSegment>? capturedSegments = null;
        SetupConcat(context.Recorder, segments => capturedSegments = [.. segments]);

        await InvokeProduceVideosAsync(context.Handler);

        AssertSegments(capturedSegments, first, second);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_EmptySegmentSnapshot_DisplaysDiagnosticWarning()
    {
        var context = new HandlerContext(
            VideoRecorderPersistenceMode.OnFailure,
            VideoCaptureGranularity.PerTest,
            RecordingStart);

        await InvokeProduceVideosAsync(context.Handler);

        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(
            Assert.ContainsSingle(context.OutputData));
        string expected = string.Format(
            CultureInfo.CurrentCulture,
            VideoRecorderResources.NoUsableVideoProduced,
            -1,
            HandlerContext.DiagnosticTail);
        Assert.AreEqual(expected, warning.Message);
        Assert.Contains(HandlerContext.DiagnosticTail, warning.Message);
        VerifyNoConcat(context.Recorder);
        Assert.IsEmpty(context.PublishedData);
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerTestConcatSucceeds_PublishesPerTestArtifact()
    {
        const string DisplayName = "Published failed test";
        string directory = CreateTemporaryDirectory();
        try
        {
            string outputPath = Path.Combine(directory, "per-test.mp4");
            File.WriteAllText(outputPath, string.Empty);
            var context = new HandlerContext(
                VideoRecorderPersistenceMode.OnFailure,
                VideoCaptureGranularity.PerTest,
                RecordingStart);
            SetSessionUid(context.Handler, new SessionUid("per-test-session"));
            await AddRecordAsync(
                context.Handler,
                DisplayName,
                new FailedTestNodeStateProperty("failure"),
                RecordingStart.AddSeconds(15),
                RecordingStart.AddSeconds(25));
            context.CurrentSegments = [new VideoSegment("segment.mp4", 10, 30)];
            SetupConcat(context.Recorder, _ => { }, outputPath);

            await InvokeProduceVideosAsync(context.Handler);

            SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(
                Assert.ContainsSingle(context.PublishedData));
            Assert.AreEqual(Path.GetFullPath(outputPath), artifact.FileInfo.FullName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDisplayName, DisplayName),
                artifact.DisplayName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDescription, DisplayName),
                artifact.Description);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [TestMethod]
    public async Task ProduceVideosAsync_PerSessionConcatSucceeds_PublishesSessionArtifact()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string outputPath = Path.Combine(directory, "session.mp4");
            File.WriteAllText(outputPath, string.Empty);
            var context = new HandlerContext(
                VideoRecorderPersistenceMode.Always,
                VideoCaptureGranularity.PerSession,
                RecordingStart);
            SetSessionUid(context.Handler, new SessionUid("video-session"));
            context.CurrentSegments = [new VideoSegment("segment.mp4", 0, 30)];
            SetupConcat(context.Recorder, _ => { }, outputPath);

            await InvokeProduceVideosAsync(context.Handler);

            SessionFileArtifact artifact = Assert.IsInstanceOfType<SessionFileArtifact>(
                Assert.ContainsSingle(context.PublishedData));
            Assert.AreEqual(Path.GetFullPath(outputPath), artifact.FileInfo.FullName);
            Assert.AreEqual(VideoRecorderResources.ArtifactSessionDisplayName, artifact.DisplayName);
            Assert.AreEqual(VideoRecorderResources.ArtifactSessionDescription, artifact.Description);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static Task AddRecordAsync(
        VideoRecorderSessionHandler handler,
        string displayName,
        TestNodeStateProperty state,
        DateTimeOffset start,
        DateTimeOffset end)
        => handler.ConsumeAsync(
            null!,
            new TestNodeUpdateMessage(
                new SessionUid("session"),
                new TestNode
                {
                    Uid = displayName,
                    DisplayName = displayName,
                    Properties = new PropertyBag(
                        state,
                        new TimingProperty(new TimingInfo(start, end, end - start))),
                }),
            CancellationToken.None);

    private static async Task InvokeProduceVideosAsync(VideoRecorderSessionHandler handler)
        => await (Task)ProduceVideosAsyncMethod.Invoke(handler, [CancellationToken.None])!;

    private static void SetupConcat(
        Mock<IVideoRecorder> recorder,
        Action<IReadOnlyList<VideoSegment>> capture,
        string? result = null)
        => recorder
            .Setup(instance => instance.ConcatAsync(
                It.IsAny<IReadOnlyList<VideoSegment>>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>(
                (segments, _, _, _) => capture(segments))
            .ReturnsAsync(result);

    private static void VerifyNoConcat(Mock<IVideoRecorder> recorder)
        => recorder.Verify(
            instance => instance.ConcatAsync(
                It.IsAny<IReadOnlyList<VideoSegment>>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never());

    private static void AssertSegments(IReadOnlyList<VideoSegment>? actual, params VideoSegment[] expected)
    {
        Assert.IsNotNull(actual);
        Assert.HasCount(expected.Length, actual);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.AreEqual(expected[i].Path, actual[i].Path);
            Assert.AreEqual(expected[i].StartSeconds, actual[i].StartSeconds);
            Assert.AreEqual(expected[i].EndSeconds, actual[i].EndSeconds);
        }
    }

    private static void SetSessionUid(VideoRecorderSessionHandler handler, SessionUid sessionUid)
        => SessionUidField.SetValue(handler, sessionUid);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(VideoRecorderSessionHandlerVideoProductionTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class HandlerContext
    {
        public const string DiagnosticTail = "diagnostic-tail";

        public HandlerContext(
            VideoRecorderPersistenceMode persistMode,
            VideoCaptureGranularity granularity,
            DateTimeOffset? recordingStartUtc)
        {
            var options = new VideoRecorderOptions
            {
                OutputDirectory = Path.GetTempPath(),
                PersistMode = persistMode,
                Granularity = granularity,
                IncludeChapters = false,
            };
            var commandLineOptions = new TestCommandLineOptions(new()
            {
                [VideoRecorderCommandLineProvider.EnableOptionName] = [],
            });
            var clock = new Mock<IClock>();
            clock.SetupGet(instance => instance.UtcNow).Returns(RecordingStart);
            Recorder.SetupGet(instance => instance.RecordingStartUtc).Returns(recordingStartUtc);
            Recorder.SetupGet(instance => instance.SegmentExtension).Returns("mp4");
            Recorder.Setup(instance => instance.ReadSegments()).Returns(() => CurrentSegments);
            Recorder.Setup(instance => instance.DescribeLastFfmpegError()).Returns(DiagnosticTail);
            OutputDevice
                .Setup(instance => instance.DisplayAsync(
                    It.IsAny<IOutputDeviceDataProducer>(),
                    It.IsAny<IOutputDeviceData>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                    (_, data, _) => OutputData.Add(data))
                .Returns(Task.CompletedTask);
            MessageBus
                .Setup(instance => instance.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, data) => PublishedData.Add(data))
                .Returns(Task.CompletedTask);

            Handler = new VideoRecorderSessionHandler(
                options,
                Mock.Of<IConfiguration>(),
                commandLineOptions,
                MessageBus.Object,
                OutputDevice.Object,
                clock.Object,
                Logger.Object,
                Recorder.Object);
        }

        public VideoRecorderSessionHandler Handler { get; }

        public Mock<IVideoRecorder> Recorder { get; } = new();

        public Mock<IOutputDevice> OutputDevice { get; } = new();

        public Mock<IMessageBus> MessageBus { get; } = new();

        public Mock<ILogger<VideoRecorderSessionHandler>> Logger { get; } = new();

        public List<IOutputDeviceData> OutputData { get; } = [];

        public List<IData> PublishedData { get; } = [];

        public IReadOnlyList<VideoSegment> CurrentSegments { get; set; } = [];
    }
}
