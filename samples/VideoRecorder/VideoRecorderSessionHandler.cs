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

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Wires the <see cref="FfmpegVideoRecorder"/> into the platform when recording is enabled via
/// <c>--capture-video</c>: exposes it through <see cref="VideoRecorder.Current"/>, tracks test
/// failures so recordings can be persisted conditionally, stops any in-flight recording when the
/// session finishes, and attaches every kept video file to the test session.
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
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger<VideoRecorderSessionHandler> _logger;

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

        string outputDirectory = options.OutputDirectory
            ?? Path.Combine(configuration.GetTestResultDirectory(), "VideoRecordings");

        _recorder = new FfmpegVideoRecorder(
            options,
            outputDirectory,
            log: message => logger.LogTrace(message),
            warn: message => logger.LogWarning(message));
        VideoRecorder.SetCurrent(_recorder);
    }

    public string Uid => nameof(VideoRecorderSessionHandler);

    public string Version => "1.0.0";

    public string DisplayName => "Video recorder";

    public string Description => "Records the screen during a test run using ffmpeg.";

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced => [typeof(SessionFileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_enabled);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is TestNodeUpdateMessage update
            && update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty)
        {
            _anyTestFailed = true;
        }

        return Task.CompletedTask;
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        if (_recorder is null)
        {
            return;
        }

        IOutputDeviceData message = _recorder.IsAvailable
            ? new FormattedTextOutputDeviceData($"Video recorder ready (ffmpeg: {_recorder.FfmpegPath}, persist: {_persistMode}). Call VideoRecorder.Current.Start()/StopAsync() from your tests.")
            : new WarningMessageOutputDeviceData("Video recording requested but ffmpeg was not found on PATH (set VideoRecorderOptions.FfmpegPath or install ffmpeg). Recordings will be skipped.");

        await _outputDevice.DisplayAsync(this, message, cancellationToken).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_recorder is null)
        {
            return;
        }

        try
        {
            // Stop any recording a test left running (even on a cancelled run) so ffmpeg isn't
            // orphaned and its file is finalized. StopAsync handles an already-cancelled token by
            // killing the process.
            await _recorder.StopAsync(testSessionContext.CancellationToken).ConfigureAwait(false);

            bool keepRecordings = _persistMode == VideoRecorderPersistenceMode.Always || _anyTestFailed;

            foreach (string file in _recorder.ProducedFiles)
            {
                if (keepRecordings)
                {
                    await _messageBus.PublishAsync(
                        this,
                        new SessionFileArtifact(
                            testSessionContext.SessionUid,
                            new FileInfo(file),
                            "Screen recording",
                            "Video captured during the test run.")).ConfigureAwait(false);
                }
                else
                {
                    DeleteQuietly(file);
                }
            }
        }
        finally
        {
            VideoRecorder.ResetCurrent(_recorder);
        }
    }

    private void DeleteQuietly(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                _logger.LogTrace($"Discarded recording '{file}' because all tests passed (persist mode: {_persistMode}).");
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

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.ArgsOptionName, out string[]? recorderArguments)
            && recorderArguments.Length > 0)
        {
            options.ExtraRecorderArguments = recorderArguments[0];
        }
    }
}
