// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Extensions.VideoRecorder;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
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
    public async Task OnTestSessionStartingAsync_RecorderFailsToStart_DoesNotDisplayReadyMessage()
    {
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        recorder.Setup(instance => instance.StartAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var outputDevice = new Mock<IOutputDevice>();
        VideoRecorderSessionHandler handler = CreateEnabledHandler(recorder.Object, outputDevice.Object);

        await handler.OnTestSessionStartingAsync(new TestSessionContextStub());

        outputDevice.Verify(
            instance => instance.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<FormattedTextOutputDeviceData>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [TestMethod]
    public async Task OnTestSessionStartingAsync_RecorderStarts_DisplaysReadyMessage()
    {
        const string Backend = "native backend";
        var recorder = new Mock<IVideoRecorder>();
        recorder.SetupGet(instance => instance.IsAvailable).Returns(true);
        recorder.SetupGet(instance => instance.FfmpegPath).Returns(Backend);
        recorder.Setup(instance => instance.StartAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        FormattedTextOutputDeviceData? displayed = null;
        var outputDevice = new Mock<IOutputDevice>();
        outputDevice
            .Setup(instance => instance.DisplayAsync(
                It.IsAny<IOutputDeviceDataProducer>(),
                It.IsAny<FormattedTextOutputDeviceData>(),
                It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (_, data, _) => displayed = (FormattedTextOutputDeviceData)data)
            .Returns(Task.CompletedTask);
        VideoRecorderSessionHandler handler = CreateEnabledHandler(recorder.Object, outputDevice.Object);

        await handler.OnTestSessionStartingAsync(new TestSessionContextStub());

        Assert.IsNotNull(displayed);
        Assert.Contains(Backend, displayed.Text);
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

    private static VideoRecorderSessionHandler CreateEnabledHandler(IVideoRecorder recorder, IOutputDevice outputDevice)
    {
        var options = new VideoRecorderOptions
        {
            OutputDirectory = Path.GetTempPath(),
        };
        var commandLineOptions = new TestCommandLineOptions(new()
        {
            [VideoRecorderCommandLineProvider.EnableOptionName] = [],
        });
        return new VideoRecorderSessionHandler(
            options,
            Mock.Of<IConfiguration>(),
            commandLineOptions,
            Mock.Of<IMessageBus>(),
            outputDevice,
            Mock.Of<IClock>(),
            Mock.Of<ILogger<VideoRecorderSessionHandler>>(),
            recorder);
    }

    private sealed class TestSessionContextStub : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken => CancellationToken.None;
    }
}
