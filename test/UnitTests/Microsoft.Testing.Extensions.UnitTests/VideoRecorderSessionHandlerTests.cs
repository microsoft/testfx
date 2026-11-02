// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
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
public sealed class VideoRecorderSessionHandlerTests
{
    [TestMethod]
    public void VideoRecorderOptions_DefaultValues_AreDocumentedDefaults()
    {
        var options = new VideoRecorderOptions();

        Assert.AreEqual("1920x1080", options.X11CaptureSize);
        Assert.IsTrue(options.IncludeChapters);
    }

    [TestMethod]
    public async Task Constructor_WhenEnabled_AppliesAllCommandLineOverridesAndUsesRecorder()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [VideoRecorderCommandLineProvider.ModeAlways],
            [VideoRecorderCommandLineProvider.SourceOptionName] = [VideoRecorderCommandLineProvider.SourceWindow],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = [VideoRecorderCommandLineProvider.GranularitySession],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = ["-b:v 2M"],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = ["42"],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = [VideoRecorderCommandLineProvider.ChaptersOff],
        });
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        recorder.SetupGet(instance => instance.RecordingStartUtc).Returns((DateTimeOffset?)null);
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            recorder.Object);
        var testSessionContext = new Mock<ITestSessionContext>();
        testSessionContext.SetupGet(instance => instance.CancellationToken).Returns(CancellationToken.None);
        testSessionContext.SetupGet(instance => instance.SessionUid).Returns(new SessionUid("session"));

        bool isEnabled = await handler.IsEnabledAsync();
        await handler.OnTestSessionStartingAsync(testSessionContext.Object);

        Assert.IsTrue(isEnabled);
        Assert.AreEqual(VideoRecorderPersistenceMode.Always, options.PersistMode);
        Assert.AreEqual(VideoCaptureSource.Window, options.Source);
        Assert.AreEqual(VideoCaptureGranularity.PerSession, options.Granularity);
        Assert.AreEqual("-b:v 2M", options.ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(42), options.MaxRetainedDuration);
        Assert.IsFalse(options.IncludeChapters);
        recorder.Verify(instance => instance.Start(), Times.Once());
    }

    [TestMethod]
    public async Task Constructor_WhenEnabled_AppliesFallbackCommandLineValues()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
            PersistMode = VideoRecorderPersistenceMode.Always,
            Source = VideoCaptureSource.Window,
            Granularity = VideoCaptureGranularity.PerSession,
            MaxRetainedDuration = TimeSpan.FromSeconds(42),
            IncludeChapters = false,
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [VideoRecorderCommandLineProvider.ModeOnFailure],
            [VideoRecorderCommandLineProvider.SourceOptionName] = [VideoRecorderCommandLineProvider.SourceScreen],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = [VideoRecorderCommandLineProvider.GranularityTest],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = ["-an"],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = ["0"],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = [VideoRecorderCommandLineProvider.ChaptersOn],
        });
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            Mock.Of<IVideoRecorder>());

        bool isEnabled = await handler.IsEnabledAsync();

        Assert.IsTrue(isEnabled);
        Assert.AreEqual(VideoRecorderPersistenceMode.OnFailure, options.PersistMode);
        Assert.AreEqual(VideoCaptureSource.Screen, options.Source);
        Assert.AreEqual(VideoCaptureGranularity.PerTest, options.Granularity);
        Assert.AreEqual("-an", options.ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(42), options.MaxRetainedDuration);
        Assert.IsTrue(options.IncludeChapters);
    }

    [TestMethod]
    public void Constructor_WhenSubOptionArgumentsAreEmpty_LeavesOptionsUnchanged()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
            PersistMode = VideoRecorderPersistenceMode.Always,
            Source = VideoCaptureSource.Window,
            Granularity = VideoCaptureGranularity.PerSession,
            ExtraRecorderArguments = "-an",
            MaxRetainedDuration = TimeSpan.FromSeconds(42),
            IncludeChapters = false,
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
            [VideoRecorderCommandLineProvider.SourceOptionName] = [],
            [VideoRecorderCommandLineProvider.GranularityOptionName] = [],
            [VideoRecorderCommandLineProvider.ArgsOptionName] = [],
            [VideoRecorderCommandLineProvider.MaxDurationOptionName] = [],
            [VideoRecorderCommandLineProvider.ChaptersOptionName] = [],
        });

        _ = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            Mock.Of<IVideoRecorder>());

        Assert.AreEqual(VideoRecorderPersistenceMode.Always, options.PersistMode);
        Assert.AreEqual(VideoCaptureSource.Window, options.Source);
        Assert.AreEqual(VideoCaptureGranularity.PerSession, options.Granularity);
        Assert.AreEqual("-an", options.ExtraRecorderArguments);
        Assert.AreEqual(TimeSpan.FromSeconds(42), options.MaxRetainedDuration);
        Assert.IsFalse(options.IncludeChapters);
    }

    [TestMethod]
    public async Task Constructor_WhenDisabled_DoesNotApplyOverridesOrUseRecorder()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.SourceOptionName] = [VideoRecorderCommandLineProvider.SourceWindow],
        });
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            recorder.Object);
        var testSessionContext = new Mock<ITestSessionContext>();
        testSessionContext.SetupGet(instance => instance.CancellationToken).Returns(CancellationToken.None);
        testSessionContext.SetupGet(instance => instance.SessionUid).Returns(new SessionUid("session"));

        bool isEnabled = await handler.IsEnabledAsync();
        await handler.OnTestSessionStartingAsync(testSessionContext.Object);

        Assert.IsFalse(isEnabled);
        Assert.AreEqual(VideoCaptureSource.Screen, options.Source);
        recorder.Verify(instance => instance.Start(), Times.Never());
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_WhenCanceled_ThrowsBeforeStartingRecorder()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            recorder.Object);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var testSessionContext = new Mock<ITestSessionContext>();
        testSessionContext.SetupGet(instance => instance.CancellationToken).Returns(cancellationTokenSource.Token);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.OnTestSessionStartingAsync(testSessionContext.Object));

        recorder.Verify(instance => instance.Start(), Times.Never());
    }

    [TestMethod]
    public async Task ConsumeAsync_WhenCanceled_ThrowsBeforeConsumingUpdate()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            Mock.Of<IVideoRecorder>());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ConsumeAsync(
                null!,
                CreateUpdate(InProgressTestNodeStateProperty.CachedInstance),
                cancellationTokenSource.Token));

        Assert.AreEqual(0, GetCollectionCount(handler, "_inFlight"));
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_WhenRecorderDoesNotStart_DoesNotDisplayReadyMessage()
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        recorder.SetupGet(instance => instance.RecordingStartUtc).Returns((DateTimeOffset?)null);
        var outputDevice = new Mock<IOutputDevice>();
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            outputDevice.Object,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            recorder.Object);
        var testSessionContext = new Mock<ITestSessionContext>();
        testSessionContext.SetupGet(instance => instance.CancellationToken).Returns(CancellationToken.None);
        testSessionContext.SetupGet(instance => instance.SessionUid).Returns(new SessionUid("session"));

        await handler.OnTestSessionStartingAsync(testSessionContext.Object);

        recorder.Verify(instance => instance.Start(), Times.Once);
        outputDevice.Verify(
            instance => instance.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<FormattedTextOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ConsumeAsync_ExecutionCompleted_RemovesInFlightTestWithoutRecordingOutcome()
    {
        var options = new VideoRecorderOptions
        {
            FfmpegPath = Path.Combine(Path.GetTempPath(), "missing-ffmpeg"),
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>());

        await handler.ConsumeAsync(null!, CreateUpdate(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);
        Assert.AreEqual(1, GetCollectionCount(handler, "_inFlight"));

        await handler.ConsumeAsync(null!, CreateUpdate(TestNodeExecutionCompletedProperty.CachedInstance), CancellationToken.None);

        Assert.AreEqual(0, GetCollectionCount(handler, "_inFlight"));
        Assert.AreEqual(0, GetCollectionCount(handler, "_testRecords"));
    }

    // A test framework that retries in-process reports every attempt under the same uid. A superseded failed
    // attempt is not the test's outcome, so it must not be recorded as a failure - otherwise a fail-then-pass
    // [Retry] test would retain failure-only video artifacts despite passing.
    [TestMethod]
    public async Task ConsumeAsync_SupersededRetryAttempt_IsNotRecordedAsFailure()
    {
        var options = new VideoRecorderOptions
        {
            FfmpegPath = Path.Combine(Path.GetTempPath(), "missing-ffmpeg"),
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        var handler = new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            Mock.Of<IOutputDevice>(),
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>());

        await handler.ConsumeAsync(null!, CreateUpdate(InProgressTestNodeStateProperty.CachedInstance), CancellationToken.None);

        // Attempt 1 failed but a later attempt supersedes it: no record, and the session is not marked failed.
        await handler.ConsumeAsync(
            null!,
            CreateUpdate(new FailedTestNodeStateProperty("boom"), new RetryAttemptProperty(1, isSuperseded: true)),
            CancellationToken.None);

        Assert.AreEqual(0, GetCollectionCount(handler, "_testRecords"));
        Assert.IsFalse(GetBooleanField(handler, "_anyTestFailed"), "a superseded attempt must not mark the session as failed");

        // Attempt 2 passed and is the final outcome: exactly one record, still not a failure.
        await handler.ConsumeAsync(
            null!,
            CreateUpdate(PassedTestNodeStateProperty.CachedInstance, new RetryAttemptProperty(2, isSuperseded: false)),
            CancellationToken.None);

        Assert.AreEqual(1, GetCollectionCount(handler, "_testRecords"));
        Assert.IsFalse(GetBooleanField(handler, "_anyTestFailed"));
    }

    private static TestNodeUpdateMessage CreateUpdate(params IProperty[] properties)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "uid",
                DisplayName = "DroppedTest",
                Properties = new PropertyBag(properties),
            });

    private static bool GetBooleanField(VideoRecorderSessionHandler handler, string fieldName)
        => (bool)typeof(VideoRecorderSessionHandler)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(handler)!;

    private static int GetCollectionCount(VideoRecorderSessionHandler handler, string fieldName)
    {
        object collection = typeof(VideoRecorderSessionHandler)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(handler)!;
        return (int)collection.GetType().GetProperty("Count")!.GetValue(collection)!;
    }
}
