// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET10_0_OR_GREATER && WINDOWS

using System.Reflection;

using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Windows.SDK.BuildTools.WinApp.UIAutomation.Recording;

using Moq;

namespace Microsoft.Testing.Extensions.VideoRecorder.UnitTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class WinAppCliVideoRecorderTests
{
    private static readonly DateTimeOffset RecordingStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsync_WaitsForStartupCallback_ThenStopFinalizesRecordingAfterSessionCancellation()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            var recordingCompletion = new TaskCompletionSource<RecordCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<bool>? onRecordingStarted = null;
            var recordingService = new Mock<IUiRecordingService>();
            recordingService
                .Setup(instance => instance.RecordDesktopAsync(
                    It.IsAny<RecordOptions>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<bool>>()))
                .Returns((RecordOptions options, CancellationToken cancellationToken, Action<bool> callback) =>
                {
                    File.WriteAllText(options.OutputPath, "video");
                    onRecordingStarted = callback;
                    cancellationToken.Register(() => recordingCompletion.TrySetResult(CreateResult("cancelled")));
                    return recordingCompletion.Task;
                });
            using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
            IVideoRecorder recorder = CreateRecorder(outputDirectory, recordingService.Object);

            Task<bool> startTask = recorder.StartAsync(sessionCancellation.Token);
            await Task.Yield();

            Assert.IsFalse(startTask.IsCompleted);
            Assert.IsNotNull(onRecordingStarted);

            onRecordingStarted!(true);
            Assert.IsTrue(await startTask);
            Assert.AreEqual(RecordingStart, recorder.RecordingStartUtc);

            await sessionCancellation.CancelAsync();
            await recorder.StopAsync(sessionCancellation.Token);

            IReadOnlyList<VideoSegment> segments = recorder.ReadSegments();
            Assert.HasCount(1, segments);
            Assert.IsTrue(File.Exists(segments[0].Path));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartAsync_RecordingCompletesBeforeStartupCallback_ReturnsFalseAndCleansUp()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            var recordingService = new Mock<IUiRecordingService>();
            recordingService
                .Setup(instance => instance.RecordDesktopAsync(
                    It.IsAny<RecordOptions>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<bool>>()))
                .ReturnsAsync(CreateResult("capture_unavailable"));
            var owner = new Mock<IAsyncDisposable>();
            owner.Setup(instance => instance.DisposeAsync()).Returns(ValueTask.CompletedTask);
            IVideoRecorder recorder = CreateRecorder(outputDirectory, recordingService.Object, owner.Object);

            bool started = await recorder.StartAsync(TestContext.CancellationToken);

            Assert.IsFalse(started);
            Assert.IsNull(recorder.SegmentDirectory);
            Assert.IsEmpty(Directory.GetDirectories(outputDirectory));
            owner.Verify(instance => instance.DisposeAsync(), Times.Once());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartAsync_PartialOutputAfterStartupCallback_PreservesUsableRecording()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            var recordingService = new Mock<IUiRecordingService>();
            recordingService
                .Setup(instance => instance.RecordDesktopAsync(
                    It.IsAny<RecordOptions>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<bool>>()))
                .Returns((RecordOptions options, CancellationToken _, Action<bool> callback) =>
                {
                    File.WriteAllText(options.OutputPath, "partial video");
                    callback(true);
                    return Task.FromException<RecordCaptureResult>(
                        new RecordPartialOutputException(
                            "partial",
                            options.OutputPath,
                            null,
                            "The finalized video is usable.",
                            new IOException("capture failed")));
                });
            IVideoRecorder recorder = CreateRecorder(outputDirectory, recordingService.Object);

            Assert.IsTrue(await recorder.StartAsync(TestContext.CancellationToken));
            await recorder.StopAsync(TestContext.CancellationToken);

            Assert.HasCount(1, recorder.ReadSegments());
            Assert.Contains("partial", recorder.DescribeLastFfmpegError());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartAsync_FinalizationFailureAfterStartupCallback_DoesNotExposeUnusableRecording()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            var recordingService = new Mock<IUiRecordingService>();
            recordingService
                .Setup(instance => instance.RecordDesktopAsync(
                    It.IsAny<RecordOptions>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<bool>>()))
                .Returns((RecordOptions options, CancellationToken _, Action<bool> callback) =>
                {
                    File.WriteAllText(options.OutputPath, "unfinalized video");
                    callback(true);
                    return Task.FromException<RecordCaptureResult>(new IOException("MP4 finalization failed"));
                });
            IVideoRecorder recorder = CreateRecorder(outputDirectory, recordingService.Object);

            Assert.IsTrue(await recorder.StartAsync(TestContext.CancellationToken));
            await recorder.StopAsync(TestContext.CancellationToken);

            Assert.IsEmpty(recorder.ReadSegments());
            Assert.Contains("MP4 finalization failed", recorder.DescribeLastFfmpegError());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task StopAsync_RecordingDoesNotStopBeforeTimeout_ReturnsAndCleansUpWhenRecordingCompletes()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            var recordingCompletion = new TaskCompletionSource<RecordCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recordingService = new Mock<IUiRecordingService>();
            recordingService
                .Setup(instance => instance.RecordDesktopAsync(
                    It.IsAny<RecordOptions>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<bool>>()))
                .Returns((RecordOptions options, CancellationToken _, Action<bool> callback) =>
                {
                    File.WriteAllText(options.OutputPath, "video");
                    callback(true);
                    return recordingCompletion.Task;
                });
            var ownerDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var owner = new Mock<IAsyncDisposable>();
            owner
                .Setup(instance => instance.DisposeAsync())
                .Returns(() =>
                {
                    ownerDisposed.TrySetResult();
                    return ValueTask.CompletedTask;
                });
            string? warning = null;
            IVideoRecorder recorder = CreateRecorder(
                outputDirectory,
                recordingService.Object,
                owner.Object,
                warningSink: message => warning = message,
                stopTimeout: TimeSpan.FromMilliseconds(10));

            Assert.IsTrue(await recorder.StartAsync(TestContext.CancellationToken));
            string segmentDirectory = recorder.SegmentDirectory!;

            await recorder.StopAsync(TestContext.CancellationToken);

            Assert.IsNull(recorder.SegmentDirectory);
            Assert.IsFalse(string.IsNullOrWhiteSpace(warning));
            recordingCompletion.SetResult(CreateResult("cancelled"));
            await ownerDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            await WaitForAsync(() => !Directory.Exists(segmentDirectory), TestContext.CancellationToken);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ConcatAsync_CanceledSessionToken_MovesFinalizedRecording()
    {
        string outputDirectory = CreateTemporaryDirectory();
        try
        {
            string sourcePath = Path.Combine(outputDirectory, "source.mp4");
            File.WriteAllText(sourcePath, "video");
            IVideoRecorder recorder = CreateRecorder(outputDirectory, Mock.Of<IUiRecordingService>());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            string? outputPath = await recorder.ConcatAsync(
                [new VideoSegment(sourcePath, 0, 1)],
                "published.mp4",
                ffmetadataPath: null,
                cancellation.Token);

            Assert.AreEqual(Path.Combine(outputDirectory, "published.mp4"), outputPath);
            Assert.IsFalse(File.Exists(sourcePath));
            Assert.IsTrue(File.Exists(outputPath));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static IVideoRecorder CreateRecorder(
        string outputDirectory,
        IUiRecordingService recordingService,
        IAsyncDisposable? recordingServiceOwner = null,
        Action<string>? warningSink = null,
        TimeSpan? stopTimeout = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(RecordingStart);
        Type recorderType = typeof(VideoRecorderFactory).Assembly.GetType(
            "Microsoft.Testing.Extensions.VideoRecorder.WinAppCliVideoRecorder",
            throwOnError: true)!;
        return (IVideoRecorder)Activator.CreateInstance(
            recorderType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            [
                new VideoRecorderOptions
                {
                    FrameRate = 10,
                    Format = VideoRecorderFormat.Mp4H264,
                    Granularity = VideoCaptureGranularity.PerSession,
                    Source = VideoCaptureSource.Screen,
                },
                outputDirectory,
                clock.Object,
                null,
                warningSink,
                recordingService,
                recordingServiceOwner,
                stopTimeout,
            ],
            culture: null)!;
    }

    private static RecordCaptureResult CreateResult(string stopReason)
        => new()
        {
            Coordinates = null,
            Frames = 1,
            Width = 640,
            Height = 480,
            FileSize = 5,
            Mode = "screen",
            ElapsedMs = 1000,
            AchievedFps = 1,
            CadenceRatio = 0.1,
            StopReason = stopReason,
            FrameArtifacts = null,
            Warnings = [],
        };

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WinAppCliVideoRecorderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}

#endif
