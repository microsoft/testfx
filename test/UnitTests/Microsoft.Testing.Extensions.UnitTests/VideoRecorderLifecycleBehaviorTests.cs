// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using static Microsoft.Testing.Extensions.UnitTests.Helpers.VideoRecorderBehaviorContext;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class VideoRecorderLifecycleBehaviorTests
{
    [TestMethod]
    [DataRow(VideoCaptureGranularity.PerTest, VideoRecorderPersistenceMode.Always)]
    [DataRow(VideoCaptureGranularity.PerTest, VideoRecorderPersistenceMode.OnFailure)]
    [DataRow(VideoCaptureGranularity.PerSession, VideoRecorderPersistenceMode.Always)]
    [DataRow(VideoCaptureGranularity.PerSession, VideoRecorderPersistenceMode.OnFailure)]
    public async Task Start_AvailableRecorder_DisplaysModeSpecificReadyMessage(VideoCaptureGranularity granularity, VideoRecorderPersistenceMode mode)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(
            new VideoRecorderOptions { Granularity = granularity, PersistMode = mode },
            cancellationToken: cancellation.Token);

        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);

        (IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(context.OutputMessages);
        FormattedTextOutputDeviceData ready = Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(data);
        string hint = granularity == VideoCaptureGranularity.PerSession
            ? VideoRecorderResources.HintPerSession
            : VideoRecorderResources.HintPerTest;
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.RecorderReady, "mock-ffmpeg", granularity, mode, hint),
            ready.Text);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(cancellation.Token, token);
        context.Recorder.Verify(recorder => recorder.Start(), Times.Once());
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Start_UnavailableRecorder_WarnsAndLeavesFinishingInert()

    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token) { Available = false };

        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        (IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(context.OutputMessages);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data);
        Assert.AreEqual(VideoRecorderResources.FfmpegNotFound, warning.Message);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(cancellation.Token, token);
        context.Recorder.Verify(recorder => recorder.Start(), Times.Never());
        context.Recorder.Verify(recorder => recorder.StopAsync(It.IsAny<CancellationToken>()), Times.Never());
        context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());
        VerifyNoConcat(context);
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Start_WithoutRecordingTimestamp_DoesNotAnnounceOrProduceVideo()
    {
        var context = new VideoRecorderBehaviorContext();
        context.Recorder.Setup(recorder => recorder.Start()).Callback(() => context.Events.Add("start"));

        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreSequenceEqual(["start", "stop"], context.Events);
        Assert.IsEmpty(context.OutputMessages);
        Assert.IsEmpty(context.Published);
        context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());
        VerifyNoConcat(context);
    }

    [TestMethod]
    public async Task DisabledHandler_IgnoresOverridesAndEveryLifecycleCallback()
    {
        var options = new VideoRecorderOptions
        {
            PersistMode = VideoRecorderPersistenceMode.Always,
            Source = VideoCaptureSource.Window,
            Granularity = VideoCaptureGranularity.PerSession,
            ExtraRecorderArguments = "-an",
            IncludeChapters = false,
            MaxRetainedDuration = TimeSpan.FromSeconds(37),
        };
        var context = new VideoRecorderBehaviorContext(options, new()
        {
            [VideoRecorderCommandLineProvider.SourceOptionName] = ["screen"],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = ["test"],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = ["-different"],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = ["42"],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = ["on"],
        });

        Assert.IsFalse(await context.Handler.IsEnabledAsync());
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("disabled", "Disabled test", new FailedTestNodeStateProperty("ignored"), Timing(1, 7));
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreEqual(VideoRecorderPersistenceMode.Always, options.PersistMode);
        Assert.AreEqual(VideoCaptureSource.Window, options.Source);
        Assert.AreEqual(VideoCaptureGranularity.PerSession, options.Granularity);
        Assert.AreEqual("-an", options.ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(37), options.MaxRetainedDuration);
        Assert.IsFalse(options.IncludeChapters);
        context.Recorder.VerifyNoOtherCalls();
        context.Clock.VerifyNoOtherCalls();
        context.Configuration.VerifyNoOtherCalls();
        context.Output.VerifyNoOtherCalls();
        context.Bus.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Start_CanceledReplacement_PreservesRecordingAndArtifactSession()
    {
        var context = new VideoRecorderBehaviorContext
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            ConcatResult = ArtifactPath("preserved.mp4"),
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var replacement = new Mock<ITestSessionContext>(MockBehavior.Strict);
        replacement.SetupGet(session => session.CancellationToken).Returns(cancellation.Token);
        replacement.SetupGet(session => session.SessionUid).Returns(new SessionUid("rejected-video-session"));

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => context.Handler.OnTestSessionStartingAsync(replacement.Object));
        await context.ConsumeAsync("test", "Preserved clip", PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));
        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        replacement.VerifyGet(session => session.SessionUid, Times.Never());
        context.Recorder.Verify(recorder => recorder.Start(), Times.Once());
        Assert.HasCount(1, context.OutputMessages);
        (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(SessionId, artifact.SessionUid);
        Assert.AreEqual(ArtifactPath("preserved.mp4"), artifact.FileInfo.FullName);
    }

    [TestMethod]
    [DataRow(true, DisplayName = "Ready notification failure")]
    [DataRow(false, DisplayName = "Unavailable warning failure")]
    public async Task Start_OutputFailure_PropagatesWithoutInventingRecorderWork(bool available)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token) { Available = available };
        var expected = new InvalidOperationException("output rejected");
        context.Output.Setup(output => output.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(),
            It.IsAny<IOutputDeviceData>(),
            It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (producer, data, token) => context.OutputMessages.Add((producer, data, token)))
            .Returns(Task.FromException(expected));

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => context.Handler.OnTestSessionStartingAsync(context.Session.Object));

        Assert.AreSame(expected, actual);
        (IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token) = Assert.ContainsSingle(context.OutputMessages);
        Assert.AreSame(context.Handler, Producer);
        Assert.AreEqual(cancellation.Token, Token);
        if (available)
        {
            Assert.IsInstanceOfType<FormattedTextOutputDeviceData>(Data);
        }
        else
        {
            Assert.AreEqual(VideoRecorderResources.FfmpegNotFound, Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Data).Message);
        }

        context.Recorder.Verify(recorder => recorder.Start(), available ? Times.Once() : Times.Never());
        context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());
        VerifyNoConcat(context);
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Finish_AwaitsStopAndConcatBeforePublishingAndReleasingSegments()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token)
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
        };
        var stop = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var concatEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var concat = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        string outputPath = ArtifactPath("awaited.mp4");
        context.Recorder.Setup(recorder => recorder.StopAsync(cancellation.Token))
            .Callback(() => context.Events.Add("stop"))
            .Returns(stop.Task);
        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, cancellation.Token))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, fileName, metadata, token) =>
            {
                context.Events.Add("concat");
                context.Concats.Add(new ConcatCall([.. segments], fileName, metadata, token));
                concatEntered.SetResult(true);
            })
            .Returns(concat.Task);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("test", "Awaited clip", PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        Task finishing = context.Handler.OnTestSessionFinishingAsync(context.Session.Object);
        try
        {
            Assert.IsFalse(finishing.IsCompleted);
            context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());
            VerifyNoConcat(context);
            context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Never());

            stop.SetResult(true);
            Assert.AreSame(concatEntered.Task, await Task.WhenAny(concatEntered.Task, finishing), "Finishing must reach concat after stop completes.");
            await concatEntered.Task;
            Assert.IsFalse(finishing.IsCompleted);
            Assert.IsEmpty(context.Published);
            context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Never());

            concat.SetResult(outputPath);
            await finishing;

            Assert.AreSequenceEqual(["start", "stop", "read", "concat", "publish"], context.Events);
            ConcatCall call = Assert.ContainsSingle(context.Concats);
            Assert.AreSequenceEqual(context.Segments, call.Segments);
            Assert.AreEqual(cancellation.Token, call.Token);
            Assert.IsNull(call.MetadataPath);
            (IDataProducer producer, SessionFileArtifact artifact) = Assert.ContainsSingle(context.Published);
            Assert.AreSame(context.Handler, producer);
            Assert.AreEqual(SessionId, artifact.SessionUid);
            Assert.AreEqual(outputPath, artifact.FileInfo.FullName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDisplayName, "Awaited clip"),
                artifact.DisplayName);
            Assert.AreEqual(
                string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDescription, "Awaited clip"),
                artifact.Description);
            context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once());
        }
        finally
        {
            stop.TrySetResult(true);
            concat.TrySetResult(outputPath);
            await finishing;
        }
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Stop fault prevents production")]
    [DataRow(true, DisplayName = "Stop cancellation prevents production")]
    public async Task Finish_StopFailure_DoesNotReadProduceOrReleaseSegments(bool canceled)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        var expected = new InvalidOperationException("stop rejected");
        if (canceled)
        {
            cancellation.Cancel();
            context.Recorder.Setup(recorder => recorder.StopAsync(cancellation.Token)).Returns(Task.FromCanceled(cancellation.Token));
            TaskCanceledException actual = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                () => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));
            Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        }
        else
        {
            context.Recorder.Setup(recorder => recorder.StopAsync(cancellation.Token)).Returns(Task.FromException(expected));
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));
            Assert.AreSame(expected, actual);
        }

        context.Recorder.Verify(recorder => recorder.StopAsync(cancellation.Token), Times.Once());
        context.Recorder.Verify(recorder => recorder.ReadSegments(), Times.Never());
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Never());
        VerifyNoConcat(context);
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Finish_EmptyRecording_DisplaysExactDiagnosticInsteadOfArtifact()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token);
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        context.OutputMessages.Clear();

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        (IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(context.OutputMessages);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data);
        Assert.AreEqual(
            string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.NoUsableVideoProduced, -1, "permission denied by mock input"),
            warning.Message);
        Assert.AreSame(context.Handler, producer);
        Assert.AreEqual(cancellation.Token, token);
        Assert.AreSequenceEqual(["start", "stop", "read"], context.Events);
        context.Recorder.Verify(recorder => recorder.DescribeLastFfmpegError(), Times.Once());
        VerifyNoConcat(context);
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Concat fault releases segments")]
    [DataRow(true, DisplayName = "Concat cancellation releases segments")]
    public async Task Finish_ConcatFailure_PropagatesAndReleasesSegmentsWithoutPublishing(bool canceled)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new VideoRecorderBehaviorContext(cancellationToken: cancellation.Token)
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
        };
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("first", "First clip", PassedTestNodeStateProperty.CachedInstance, Timing(0, 3));
        await context.ConsumeAsync("second", "Second clip", PassedTestNodeStateProperty.CachedInstance, Timing(4, 7));
        var expected = new InvalidOperationException("concat rejected");
        Task<string?> result;
        if (canceled)
        {
            cancellation.Cancel();
            result = Task.FromCanceled<string?>(cancellation.Token);
        }
        else
        {
            result = Task.FromException<string?>(expected);
        }

        context.Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, cancellation.Token)).Returns(result);

        if (canceled)
        {
            TaskCanceledException actual = await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                () => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));
            Assert.AreEqual(cancellation.Token, actual.CancellationToken);
        }
        else
        {
            InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));
            Assert.AreSame(expected, actual);
        }

        context.Recorder.Verify(recorder => recorder.StopAsync(cancellation.Token), Times.Once());
        context.Recorder.Verify(
            recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), null, cancellation.Token), Times.Once());
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once());
        Assert.IsEmpty(context.Published);
    }

    [TestMethod]
    public async Task Finish_PublicationFailure_StopsFurtherProductionAndReleasesSegments()
    {
        var context = new VideoRecorderBehaviorContext
        {
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            ConcatResult = ArtifactPath("unpublished.mp4"),
        };
        var expected = new InvalidOperationException("bus rejected");
        context.Bus.Setup(bus => bus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>())).Returns(Task.FromException(expected));
        await context.Handler.OnTestSessionStartingAsync(context.Session.Object);
        await context.ConsumeAsync("first", "First clip", PassedTestNodeStateProperty.CachedInstance, Timing(0, 3));
        await context.ConsumeAsync("second", "Second clip", PassedTestNodeStateProperty.CachedInstance, Timing(4, 7));

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => context.Handler.OnTestSessionFinishingAsync(context.Session.Object));

        Assert.AreSame(expected, actual);
        ConcatCall call = Assert.ContainsSingle(context.Concats);
        Assert.AreSequenceEqual([context.Segments[0]], call.Segments);
        context.Bus.Verify(bus => bus.PublishAsync(context.Handler, It.IsAny<SessionFileArtifact>()), Times.Once());
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once());
    }

    [TestMethod]
    public async Task Finish_WithoutSessionStart_DoesNotPublishUnownedArtifact()
    {
        var context = new VideoRecorderBehaviorContext
        {
            RecordingStartUtc = Epoch,
            Segments = [new VideoSegment("first.mp4", 0, 4), new VideoSegment("second.mp4", 4, 8)],
            ConcatResult = ArtifactPath("unowned.mp4"),
        };
        await context.ConsumeAsync("test", "Unowned clip", PassedTestNodeStateProperty.CachedInstance, Timing(1, 7));

        await context.Handler.OnTestSessionFinishingAsync(context.Session.Object);

        Assert.HasCount(1, context.Concats);
        Assert.IsEmpty(context.Published);
        context.Bus.VerifyNoOtherCalls();
        context.Recorder.Verify(recorder => recorder.Start(), Times.Never());
        context.Recorder.Verify(recorder => recorder.StopAsync(CancellationToken.None), Times.Once());
        context.Recorder.VerifyGet(recorder => recorder.SegmentDirectory, Times.Once());
    }

    private static void VerifyNoConcat(VideoRecorderBehaviorContext context)
        => context.Recorder.Verify(
            recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never());
}
