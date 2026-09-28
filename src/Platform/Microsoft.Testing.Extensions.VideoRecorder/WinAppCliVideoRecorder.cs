// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET10_0_OR_GREATER && WINDOWS

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Windows.SDK.BuildTools.WinApp.UIAutomation.Recording;

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Records one continuous, full-desktop H.264 session through WinAppCLI's Windows capture and
/// Media Foundation encoder. The factory selects this backend only when the requested behavior can
/// be represented by that single output without weakening TestFx's segmented ffmpeg semantics.
/// </summary>
[Embedded]
[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class WinAppCliVideoRecorder : IVideoRecorder
{
    private const int ProcessPerMonitorDpiAware = 2;

    private readonly VideoRecorderOptions _options;
    private readonly string _outputDirectory;
    private readonly IClock _clock;
    private readonly Action<string>? _log;
    private readonly Action<string>? _warn;
    private readonly ServiceProvider _serviceProvider;
    private readonly IUiRecordingService _recordingService;
    private readonly Lock _gate = new();

    private CancellationTokenSource? _recordingCancellation;
    private Task? _recordingTask;
    private RecordCaptureResult? _captureResult;
    private DateTimeOffset? _recordingEndUtc;
    private string? _recordingPath;
    private string? _lastError;

    public WinAppCliVideoRecorder(
        VideoRecorderOptions options,
        string outputDirectory,
        IClock clock,
        Action<string>? log,
        Action<string>? warn)
    {
        _options = options;
        _outputDirectory = outputDirectory;
        _clock = clock;
        _log = log;
        _warn = warn;

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddWinAppUiAutomation();
        services.AddWinAppUiRecording();
        _serviceProvider = services.BuildServiceProvider();
        _recordingService = _serviceProvider.GetRequiredService<IUiRecordingService>();
    }

    public bool IsAvailable => true;

    // IVideoRecorder predates multiple backends. For compatibility, this property supplies the
    // backend description consumed by the session-ready message.
    public string FfmpegPath => "WinAppCLI 0.7.0 (Windows capture + Media Foundation H.264)";

    public DateTimeOffset? RecordingStartUtc { get; private set; }

    public string? SegmentDirectory { get; private set; }

    public string SegmentExtension => "mp4";

    internal static bool IsProcessPerMonitorDpiAware()
    {
        try
        {
            int result = NativeMethods.GetProcessDpiAwareness(IntPtr.Zero, out int awareness);
            return result >= 0 && awareness == ProcessPerMonitorDpiAware;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_recordingTask is not null)
            {
                return;
            }

            try
            {
                string segmentDirectory = Path.Combine(_outputDirectory, "native_recording_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(segmentDirectory);
                string recordingPath = Path.Combine(segmentDirectory, "session.mp4");
                var cancellation = new CancellationTokenSource();

                SegmentDirectory = segmentDirectory;
                _recordingPath = recordingPath;
                _recordingCancellation = cancellation;
                RecordingStartUtc = _clock.UtcNow;
                _log?.Invoke($"Starting native Windows screen recording with WinAppCLI: \"{recordingPath}\"");
                _recordingTask = RecordAsync(recordingPath, cancellation.Token);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _warn?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.VideoRecorderResources.FailedToStartRecording, ex.Message));
                ResetFailedStart();
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? recordingTask;
        CancellationTokenSource? recordingCancellation;
        lock (_gate)
        {
            recordingTask = _recordingTask;
            recordingCancellation = _recordingCancellation;
        }

        if (recordingTask is null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
            return;
        }

        if (recordingCancellation is not null)
        {
            await recordingCancellation.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            // WinAppCLI treats cancellation as a graceful stop and finalizes already captured MP4
            // evidence. Do not abandon that finalization when the outer test session is cancelled.
            await recordingTask.ConfigureAwait(false);
        }
        finally
        {
            _recordingEndUtc = _clock.UtcNow;
            recordingCancellation?.Dispose();
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    public IReadOnlyList<VideoSegment> ReadSegments()
    {
        string? recordingPath = _recordingPath;
        DateTimeOffset? recordingStart = RecordingStartUtc;
        if (_recordingTask?.IsCompleted != true
            || recordingPath is null
            || recordingStart is null
            || !File.Exists(recordingPath)
            || new FileInfo(recordingPath).Length == 0)
        {
            return [];
        }

        double endSeconds = _captureResult is { } result
            ? result.ElapsedMs / 1000d
            : Math.Max(0, ((_recordingEndUtc ?? _clock.UtcNow) - recordingStart.Value).TotalSeconds);
        return [new VideoSegment(recordingPath, 0, endSeconds)];
    }

    public string DescribeLastFfmpegError()
        => _lastError ?? "No native recorder error detail was reported.";

    public Task<string?> ConcatAsync(
        IReadOnlyList<VideoSegment> segments,
        string outputFileName,
        string? ffmetadataPath,
        CancellationToken cancellationToken)
    {
        if (segments.Count != 1 || ffmetadataPath is not null)
        {
            _lastError = "The native backend can only publish its single session recording without chapter metadata.";
            return Task.FromResult<string?>(null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        string sourcePath = segments[0].Path;
        string outputPath = Path.Combine(_outputDirectory, outputFileName);
        try
        {
            File.Move(sourcePath, outputPath, overwrite: false);
            return Task.FromResult<string?>(outputPath);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _warn?.Invoke($"Failed to publish the native Windows recording: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }

    private async Task RecordAsync(string recordingPath, CancellationToken cancellationToken)
    {
        try
        {
            _captureResult = await _recordingService.RecordDesktopAsync(
                new RecordOptions
                {
                    OutputPath = recordingPath,
                    DurationSec = 0,
                    Fps = Math.Max(1, _options.FrameRate),
                },
                cancellationToken).ConfigureAwait(false);

            _log?.Invoke(
                $"Native Windows recording completed: {_captureResult.Frames} frames, "
                + $"{_captureResult.Width}x{_captureResult.Height}, mode {_captureResult.Mode}, "
                + $"stop reason {_captureResult.StopReason}.");

            if (_captureResult.Warnings is { Length: > 0 })
            {
                _warn?.Invoke(string.Join(" ", _captureResult.Warnings));
            }
        }
        catch (RecordPartialOutputException ex)
        {
            _lastError = ex.Message;
            _warn?.Invoke($"Native Windows recording completed with partial output: {ex.Message} {ex.RecoveryHint}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // WinAppCLI normally consumes cancellation and returns a finalized result. Preserve a
            // defensive best-effort path in case cancellation occurs before capture initializes.
            _lastError = "Native recording was cancelled before a usable video could be finalized.";
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _warn?.Invoke($"Native Windows recording failed: {ex.Message}");
        }
    }

    private void ResetFailedStart()
    {
        string? segmentDirectory = SegmentDirectory;
        _recordingCancellation?.Dispose();
        _recordingCancellation = null;
        _recordingTask = null;
        _recordingPath = null;
        SegmentDirectory = null;
        RecordingStartUtc = null;

        try
        {
            if (segmentDirectory is not null && Directory.Exists(segmentDirectory))
            {
                Directory.Delete(segmentDirectory, recursive: true);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup after a failed start.
        }
    }

    private static class NativeMethods
    {
        [DllImport("shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr processHandle, out int awareness);
    }
}

#endif
