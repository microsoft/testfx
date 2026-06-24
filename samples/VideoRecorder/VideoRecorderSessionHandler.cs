// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Wires the <see cref="FfmpegVideoRecorder"/> into the platform when recording is enabled via
/// <c>--capture-video</c>. Depending on the granularity it records each test individually
/// (default) or records the whole session. Kept videos are attached as session artifacts.
/// </summary>
internal sealed class VideoRecorderSessionHandler :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private readonly bool _enabled;
    private readonly FfmpegVideoRecorder? _recorder;
    private readonly VideoRecorderPersistenceMode _persistMode;
    private readonly VideoCaptureGranularity _granularity;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger<VideoRecorderSessionHandler> _logger;

    private SessionUid? _sessionUid;

    // Only mutated from ConsumeAsync, which the platform calls sequentially per consumer.
    private string? _recordingOwnerTestUid;

    private volatile bool _anyTestFailed;

    public VideoRecorderSessionHandler(
        VideoRecorderOptions options,
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ILogger<VideoRecorderSessionHandler> logger)
    {
        _messageBus = messageBus;
        _outputDevice = outputDevice;
        _logger = logger;

        _enabled = commandLineOptions.IsOptionSet(VideoRecorderCommandLineProvider.EnableOptionName);
        if (!_enabled)
        {
            return;
        }

        ApplyCommandLineOverrides(options, commandLineOptions);
        _persistMode = options.PersistMode;
        _granularity = options.Granularity;

        string outputDirectory = options.OutputDirectory
            ?? Path.Combine(configuration.GetTestResultDirectory(), "VideoRecordings");

        _recorder = new FfmpegVideoRecorder(
            options,
            outputDirectory,
            log: message => logger.LogTrace(message),
            warn: message => logger.LogWarning(message));
    }

    public string Uid => nameof(VideoRecorderSessionHandler);

    public string Version => "1.0.0";

    public string DisplayName => "Video recorder";

    public string Description => "Records the screen during a test run using ffmpeg.";

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced => [typeof(SessionFileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_enabled);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is not TestNodeUpdateMessage update)
        {
            return;
        }

        TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        bool isFailure = state is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty;
        if (isFailure)
        {
            _anyTestFailed = true;
        }

        if (_recorder is null || _granularity != VideoCaptureGranularity.PerTest)
        {
            return;
        }

        string testUid = update.TestNode.Uid.Value;

        if (state is InProgressTestNodeStateProperty)
        {
            // Start recording this test. If one is already running (parallel execution), skip — a
            // single screen recorder can only follow one test at a time.
            if (_recordingOwnerTestUid is null)
            {
                _recordingOwnerTestUid = testUid;
                _recorder.Start(update.TestNode.DisplayName);
            }

            return;
        }

        bool isTerminal = state is PassedTestNodeStateProperty or FailedTestNodeStateProperty
            or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty;
        if (isTerminal && _recordingOwnerTestUid == testUid)
        {
            _recordingOwnerTestUid = null;
            string? file = await _recorder.StopAsync(cancellationToken).ConfigureAwait(false);
            if (file is null)
            {
                return;
            }

            bool keep = _persistMode == VideoRecorderPersistenceMode.Always || isFailure;
            if (keep)
            {
                await PublishArtifactAsync(file, $"Screen recording — {update.TestNode.DisplayName}", $"Video captured for test '{update.TestNode.DisplayName}'.").ConfigureAwait(false);
            }
            else
            {
                DeleteQuietly(file);
            }
        }
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        _sessionUid = testSessionContext.SessionUid;

        if (_recorder is null)
        {
            return;
        }

        IOutputDeviceData message = _recorder.IsAvailable
            ? new FormattedTextOutputDeviceData($"Video recorder ready (ffmpeg: {_recorder.FfmpegPath}, granularity: {_granularity}, persist: {_persistMode}). {GranularityHint()}")
            : new WarningMessageOutputDeviceData("Video recording requested but ffmpeg was not found on PATH (set VideoRecorderOptions.FfmpegPath or install ffmpeg). Recordings will be skipped.");
        await _outputDevice.DisplayAsync(this, message, cancellationToken).ConfigureAwait(false);

        if (_granularity == VideoCaptureGranularity.PerSession && _recorder.IsAvailable)
        {
            _recorder.Start("session");
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_recorder is null)
        {
            return;
        }

        // Stop any recording still running (the session recording, or a per-test recording left
        // by a test that never finished) so ffmpeg isn't orphaned and its file is finalized.
        string? leftover = await _recorder.StopAsync(testSessionContext.CancellationToken).ConfigureAwait(false);

        if (_granularity == VideoCaptureGranularity.PerTest)
        {
            // Per-test recordings were already published as each test finished. Only a leftover
            // from an unfinished test (e.g. a cancelled run) remains.
            if (leftover is not null)
            {
                await PublishArtifactAsync(leftover, "Screen recording (incomplete)", "Recording for a test that did not finish.").ConfigureAwait(false);
            }

            return;
        }

        bool keepRecordings = _persistMode == VideoRecorderPersistenceMode.Always || _anyTestFailed;
        foreach (string file in _recorder.ProducedFiles)
        {
            if (keepRecordings)
            {
                await PublishArtifactAsync(file, "Screen recording", "Video captured during the test run.").ConfigureAwait(false);
            }
            else
            {
                DeleteQuietly(file);
            }
        }
    }

    private string GranularityHint()
        => _granularity == VideoCaptureGranularity.PerSession
            ? "Recording the whole session into one video."
            : "Recording each test automatically.";

    private async Task PublishArtifactAsync(string file, string displayName, string description)
    {
        if (_sessionUid is not { } sessionUid)
        {
            return;
        }

        await _messageBus.PublishAsync(
            this,
            new SessionFileArtifact(sessionUid, new FileInfo(file), displayName, description)).ConfigureAwait(false);
    }

    private void DeleteQuietly(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                _logger.LogTrace($"Discarded recording '{file}' (persist mode: {_persistMode}).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Failed to delete recording '{file}': {ex.Message}");
        }
    }

    private static void ApplyCommandLineOverrides(VideoRecorderOptions options, ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.EnableOptionName, out string[]? modeArguments)
            && modeArguments.Length > 0)
        {
            options.PersistMode = modeArguments[0].Equals(VideoRecorderCommandLineProvider.ModeAlways, StringComparison.OrdinalIgnoreCase)
                ? VideoRecorderPersistenceMode.Always
                : VideoRecorderPersistenceMode.OnFailure;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.SourceOptionName, out string[]? sourceArguments)
            && sourceArguments.Length > 0)
        {
            options.Source = sourceArguments[0].Equals(VideoRecorderCommandLineProvider.SourceWindow, StringComparison.OrdinalIgnoreCase)
                ? VideoCaptureSource.Window
                : VideoCaptureSource.Screen;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.GranularityOptionName, out string[]? granularityArguments)
            && granularityArguments.Length > 0)
        {
            options.Granularity = granularityArguments[0].Equals(VideoRecorderCommandLineProvider.GranularitySession, StringComparison.OrdinalIgnoreCase)
                ? VideoCaptureGranularity.PerSession
                : VideoCaptureGranularity.PerTest;
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.ArgsOptionName, out string[]? recorderArguments)
            && recorderArguments.Length > 0)
        {
            options.ExtraRecorderArguments = recorderArguments[0];
        }
    }
}
