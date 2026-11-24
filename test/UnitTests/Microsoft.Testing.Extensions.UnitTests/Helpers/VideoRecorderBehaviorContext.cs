// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class VideoRecorderBehaviorContext
{
    internal static readonly DateTimeOffset Epoch = new(2026, 10, 12, 13, 14, 15, 123, TimeSpan.Zero);
    internal static readonly SessionUid SessionId = new("owned-video-session");

    public VideoRecorderBehaviorContext(
        VideoRecorderOptions? options = null,
        Dictionary<string, string[]>? commandLine = null,
        CancellationToken cancellationToken = default)
    {
        Options = options ?? new VideoRecorderOptions
        {
            PersistMode = VideoRecorderPersistenceMode.Always,
            IncludeChapters = false,
        };
        Configuration.SetupGet(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory])
            .Returns("configured-video-results");
        Clock.SetupGet(clock => clock.UtcNow).Returns(() => Now);
        Session.SetupGet(session => session.SessionUid).Returns(SessionId);
        Session.SetupGet(session => session.CancellationToken).Returns(cancellationToken);

        Recorder.SetupGet(recorder => recorder.IsAvailable).Returns(() => Available);
        Recorder.SetupGet(recorder => recorder.FfmpegPath).Returns("mock-ffmpeg");
        Recorder.SetupGet(recorder => recorder.RecordingStartUtc).Returns(() => RecordingStartUtc);
        Recorder.SetupGet(recorder => recorder.SegmentDirectory).Returns(() => SegmentDirectory);
        Recorder.SetupGet(recorder => recorder.SegmentExtension).Returns(() => SegmentExtension);
        Recorder.Setup(recorder => recorder.Start()).Callback(() =>
        {
            Events.Add("start");
            RecordingStartUtc = Epoch;
        });
        Recorder.Setup(recorder => recorder.StopAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(_ => Events.Add("stop"))
            .Returns(Task.CompletedTask);
        Recorder.Setup(recorder => recorder.ReadSegments())
            .Callback(() => Events.Add("read"))
            .Returns(() => Segments);
        Recorder.Setup(recorder => recorder.DescribeLastFfmpegError()).Returns("permission denied by mock input");
        Recorder.Setup(recorder => recorder.ConcatAsync(
            It.IsAny<IReadOnlyList<VideoSegment>>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<VideoSegment>, string, string?, CancellationToken>((segments, fileName, metadata, token) =>
            {
                Events.Add("concat");
                Concats.Add(new ConcatCall([.. segments], fileName, metadata, token));
            })
            .ReturnsAsync(() => ConcatResult);

        Output.Setup(output => output.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(),
            It.IsAny<IOutputDeviceData>(),
            It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>(
                (producer, data, token) => OutputMessages.Add((producer, data, token)))
            .Returns(Task.CompletedTask);
        Bus.Setup(bus => bus.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback<IDataProducer, IData>((producer, data) =>
            {
                Events.Add("publish");
                Published.Add((producer, (SessionFileArtifact)data));
            })
            .Returns(Task.CompletedTask);
        Logger.Setup(logger => logger.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, state, exception, formatter) => Logs.Add((level, formatter(state, exception))));
        Logger.Setup(logger => logger.LogAsync(
            It.IsAny<LogLevel>(),
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (level, state, exception, formatter) => Logs.Add((level, formatter(state, exception))))
            .Returns(Task.CompletedTask);

        Handler = new VideoRecorderSessionHandler(
            Options,
            Configuration.Object,
            new TestCommandLineOptions(commandLine ?? new() { [VideoRecorderCommandLineProvider.EnableOptionName] = [] }),
            Bus.Object,
            Output.Object,
            Clock.Object,
            Logger.Object,
            Recorder.Object);
    }

    public VideoRecorderOptions Options { get; }

    public VideoRecorderSessionHandler Handler { get; }

    public Mock<IVideoRecorder> Recorder { get; } = new(MockBehavior.Strict);

    public Mock<IConfiguration> Configuration { get; } = new(MockBehavior.Strict);

    public Mock<IClock> Clock { get; } = new(MockBehavior.Strict);

    public Mock<ITestSessionContext> Session { get; } = new(MockBehavior.Strict);

    public Mock<IOutputDevice> Output { get; } = new(MockBehavior.Strict);

    public Mock<IMessageBus> Bus { get; } = new(MockBehavior.Strict);

    public Mock<ILogger<VideoRecorderSessionHandler>> Logger { get; } = new(MockBehavior.Strict);

    public bool Available { get; set; } = true;

    public DateTimeOffset Now { get; set; } = Epoch;

    public DateTimeOffset? RecordingStartUtc { get; set; }

    // Null avoids real directory access during finishing; no recording is ever written by this fixture.
    public string? SegmentDirectory { get; set; }

    public string SegmentExtension { get; set; } = "mp4";

    public string? ConcatResult { get; set; }

    public IReadOnlyList<VideoSegment> Segments { get; set; } = [];

    public List<string> Events { get; } = [];

    public List<ConcatCall> Concats { get; } = [];

    public List<(IOutputDeviceDataProducer Producer, IOutputDeviceData Data, CancellationToken Token)> OutputMessages { get; } = [];

    public List<(IDataProducer Producer, SessionFileArtifact Artifact)> Published { get; } = [];

    public List<(LogLevel Level, string Text)> Logs { get; } = [];

    public static TimingProperty Timing(double startSeconds, double endSeconds)
        => new(new TimingInfo(Epoch.AddSeconds(startSeconds), Epoch.AddSeconds(endSeconds), TimeSpan.FromSeconds(endSeconds - startSeconds)));

    public static TestNodeUpdateMessage Update(string uid, string? displayName, params IProperty[] properties)
        => new(
            new SessionUid("framework-update-session"),
            new TestNode { Uid = uid, DisplayName = displayName!, Properties = new PropertyBag(properties) });

    public Task ConsumeAsync(string uid, string? displayName, params IProperty[] properties)
        => Handler.ConsumeAsync(null!, Update(uid, displayName, properties), CancellationToken.None);

    public static string ArtifactPath(string fileName)
        => Path.Combine(Path.GetPathRoot(typeof(VideoRecorderBehaviorContext).Assembly.Location)!, "mock-video-artifacts", fileName);

    internal sealed class ConcatCall(VideoSegment[] segments, string fileName, string? metadataPath, CancellationToken token)
    {
        public VideoSegment[] Segments { get; } = segments;

        public string FileName { get; } = fileName;

        public string? MetadataPath { get; } = metadataPath;

        public CancellationToken Token { get; } = token;
    }
}
