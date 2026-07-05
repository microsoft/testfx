// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Wires the <see cref="FfmpegVideoRecorder"/> into the platform when recording is enabled via
/// <c>--capture-video</c>. The screen is recorded continuously as a rolling sequence of segments;
/// at the end of the run the per-test clips (or the chaptered session video) are cut from those
/// segments using each test's timing. This avoids the race a per-test recorder has — data consumers
/// are invoked asynchronously, so by the time a test's <c>InProgress</c> message is observed the
/// test may already be finishing.
/// </summary>
[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal sealed partial class VideoRecorderSessionHandler :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly bool _enabled;
    private readonly FfmpegVideoRecorder? _recorder;
    private readonly VideoRecorderOptions _options;
    private readonly VideoRecorderPersistenceMode _persistMode;
    private readonly VideoCaptureGranularity _granularity;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly IClock _clock;
    private readonly ILogger<VideoRecorderSessionHandler> _logger;

#if NET9_0_OR_GREATER
    private readonly Lock _stateGate = new();
#else
    private readonly object _stateGate = new();
#endif
    private readonly List<TestRecord> _testRecords = [];
    private readonly Dictionary<string, DateTimeOffset> _inFlight = [];

    private SessionUid? _sessionUid;
    private bool _anyTestFailed;
    private int _bufferDropWarned;

    public VideoRecorderSessionHandler(
        VideoRecorderOptions options,
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        IClock clock,
        ILogger<VideoRecorderSessionHandler> logger)
    {
        _messageBus = messageBus;
        _outputDevice = outputDevice;
        _clock = clock;
        _logger = logger;
        _options = options;

        _enabled = commandLineOptions.IsOptionSet(VideoRecorderCommandLineProvider.EnableOptionName);
        if (_enabled)
        {
            ApplyCommandLineOverrides(options, commandLineOptions);
        }

        _persistMode = options.PersistMode;
        _granularity = options.Granularity;

        if (!_enabled)
        {
            return;
        }

        string outputDirectory = options.OutputDirectory
            ?? Path.Combine(configuration.GetTestResultDirectory(), "VideoRecordings");

        _recorder = new FfmpegVideoRecorder(
            options,
            outputDirectory,
            clock,
            log: message => logger.LogTrace(message),
            warn: message => logger.LogWarning(message));
    }

    public string Uid => nameof(VideoRecorderSessionHandler);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => VideoRecorderResources.ExtensionDisplayName;

    public string Description => VideoRecorderResources.ExtensionDescription;

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced => [typeof(SessionFileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_enabled);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _sessionUid = testSessionContext.SessionUid;

        if (_recorder is null)
        {
            return;
        }

        if (!_recorder.IsAvailable)
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(VideoRecorderResources.FfmpegNotFound), cancellationToken).ConfigureAwait(false);
            return;
        }

        // Record continuously for the whole session regardless of granularity; both per-test clips
        // and the session video are cut from this single recording afterwards.
        _recorder.Start();

        string hint = _granularity == VideoCaptureGranularity.PerSession
            ? VideoRecorderResources.HintPerSession
            : VideoRecorderResources.HintPerTest;
        string ready = string.Format(
            CultureInfo.CurrentCulture,
            VideoRecorderResources.RecorderReady,
            _recorder.FfmpegPath,
            _granularity,
            _persistMode,
            hint);
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(ready), cancellationToken).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_recorder is null || !_recorder.IsAvailable)
        {
            return;
        }

        CancellationToken cancellationToken = testSessionContext.CancellationToken;

        // The platform drains the data-consumer queue before invoking session-finishing handlers,
        // so every test's terminal message has already been recorded into _testRecords.
        await _recorder.StopAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ProduceVideosAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteDirectoryQuietly(_recorder.SegmentDirectory);
        }
    }
}
