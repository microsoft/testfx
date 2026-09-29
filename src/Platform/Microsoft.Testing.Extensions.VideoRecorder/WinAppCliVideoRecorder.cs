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
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(15);

    private readonly VideoRecorderOptions _options;
    private readonly string _outputDirectory;
    private readonly IClock _clock;
    private readonly Action<string>? _log;
    private readonly Action<string>? _warn;
    private readonly IAsyncDisposable? _recordingServiceOwner;
    private readonly IUiRecordingService _recordingService;
    private readonly TimeSpan _stopTimeout;
    private readonly Lock _gate = new();

    private CancellationTokenSource? _recordingCancellation;
    private Task? _recordingTask;
    private RecordCaptureResult? _captureResult;
    private DateTimeOffset? _recordingEndUtc;
    private string? _recordingPath;
    private string? _lastError;
    private bool _hasUsableOutput;
    private bool _stopCompleted;
    private int _recordingServiceOwnerDisposed;

    public WinAppCliVideoRecorder(
        VideoRecorderOptions options,
        string outputDirectory,
        IClock clock,
        Action<string>? log,
        Action<string>? warn)
        : this(options, outputDirectory, clock, log, warn, CreateRecordingServiceProvider(), DefaultStopTimeout)
    {
    }

    private WinAppCliVideoRecorder(
        VideoRecorderOptions options,
        string outputDirectory,
        IClock clock,
        Action<string>? log,
        Action<string>? warn,
        ServiceProvider serviceProvider,
        TimeSpan stopTimeout)
        : this(
            options,
            outputDirectory,
            clock,
            log,
            warn,
            serviceProvider.GetRequiredService<IUiRecordingService>(),
            serviceProvider,
            stopTimeout)
    {
    }

    internal WinAppCliVideoRecorder(
        VideoRecorderOptions options,
        string outputDirectory,
        IClock clock,
        Action<string>? log,
        Action<string>? warn,
        IUiRecordingService recordingService,
        IAsyncDisposable? recordingServiceOwner = null,
        TimeSpan? stopTimeout = null)
    {
        _options = options;
        _outputDirectory = outputDirectory;
        _clock = clock;
        _log = log;
        _warn = warn;
        _recordingService = recordingService;
        _recordingServiceOwner = recordingServiceOwner;
        _stopTimeout = stopTimeout ?? DefaultStopTimeout;
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

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        Task recordingTask;
        var recordingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_recordingTask is not null)
            {
                return RecordingStartUtc is not null;
            }

            try
            {
                string segmentDirectory = Path.Combine(_outputDirectory, "native_recording_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(segmentDirectory);
                string recordingPath = Path.Combine(segmentDirectory, "session.mp4");
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                SegmentDirectory = segmentDirectory;
                _recordingPath = recordingPath;
                _recordingCancellation = cancellation;
                _log?.Invoke($"Starting native Windows screen recording with WinAppCLI: \"{recordingPath}\"");
                recordingTask = RecordAsync(recordingPath, recordingStarted, cancellation.Token);
                _recordingTask = recordingTask;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _warn?.Invoke(string.Format(CultureInfo.CurrentCulture, Resources.VideoRecorderResources.FailedToStartRecording, ex.Message));
                ResetFailedStart();
                return false;
            }
        }

        await Task.WhenAny(recordingStarted.Task, recordingTask).ConfigureAwait(false);
        bool started = await recordingStarted.Task.ConfigureAwait(false);
        if (!started)
        {
            ResetFailedStart();
            await DisposeRecordingServiceOwnerQuietlyAsync().ConfigureAwait(false);
        }

        return started;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? recordingTask;
        CancellationTokenSource? recordingCancellation;
        string? segmentDirectory;
        lock (_gate)
        {
            if (_stopCompleted)
            {
                return;
            }

            recordingTask = _recordingTask;
            recordingCancellation = _recordingCancellation;
            segmentDirectory = SegmentDirectory;
        }

        if (recordingTask is null)
        {
            lock (_gate)
            {
                _stopCompleted = true;
            }

            await DisposeRecordingServiceOwnerQuietlyAsync().ConfigureAwait(false);
            return;
        }

        if (recordingCancellation is not null)
        {
            try
            {
                await recordingCancellation.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Failed to signal native recording cancellation: {ex.Message}");
            }
        }

        Task completedTask = await Task.WhenAny(recordingTask, Task.Delay(_stopTimeout, CancellationToken.None)).ConfigureAwait(false);
        if (completedTask != recordingTask && !recordingTask.IsCompleted)
        {
            _lastError = string.Format(CultureInfo.CurrentCulture, Resources.VideoRecorderResources.NativeStopTimeout, _stopTimeout.TotalSeconds);
            _warn?.Invoke(_lastError);
            lock (_gate)
            {
                _stopCompleted = true;
                _recordingTask = null;
                _recordingCancellation = null;
                SegmentDirectory = null;
            }

            _ = CompleteTimedOutStopAsync(recordingTask, recordingCancellation, segmentDirectory);
            return;
        }

        // WinAppCLI treats cancellation as a graceful stop and finalizes already captured MP4
        // evidence. Do not abandon that finalization when the outer test session is cancelled.
        await recordingTask.ConfigureAwait(false);
        _recordingEndUtc = _clock.UtcNow;
        recordingCancellation?.Dispose();
        lock (_gate)
        {
            _stopCompleted = true;
            _recordingCancellation = null;
        }

        await DisposeRecordingServiceOwnerQuietlyAsync().ConfigureAwait(false);
    }

    public IReadOnlyList<VideoSegment> ReadSegments()
    {
        string? recordingPath = _recordingPath;
        DateTimeOffset? recordingStart = RecordingStartUtc;
        if (_recordingTask?.IsCompleted != true
            || !_hasUsableOutput
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
            _warn?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.VideoRecorderResources.NativePublishFailed,
                ex.Message));
            return Task.FromResult<string?>(null);
        }
    }

    private async Task RecordAsync(
        string recordingPath,
        TaskCompletionSource<bool> recordingStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            _captureResult = await _recordingService.RecordDesktopAsync(
                new RecordOptions
                {
                    OutputPath = recordingPath,
                    DurationSec = 0,
                    Fps = _options.FrameRate,
                },
                cancellationToken,
                _ =>
                {
                    lock (_gate)
                    {
                        RecordingStartUtc ??= _clock.UtcNow;
                    }

                    recordingStarted.TrySetResult(true);
                }).ConfigureAwait(false);

            _hasUsableOutput = IsUsableRecording(recordingPath, recordingPath);
            _log?.Invoke(
                $"Native Windows recording completed: {_captureResult.Frames} frames, "
                + $"{_captureResult.Width}x{_captureResult.Height}, mode {_captureResult.Mode}, "
                + $"stop reason {_captureResult.StopReason}.");

            if (_captureResult.Warnings is { Length: > 0 })
            {
                _warn?.Invoke(string.Join(" ", _captureResult.Warnings));
            }

            if (_captureResult.StopReason is not ("cancelled" or "duration_elapsed"))
            {
                _warn?.Invoke(string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.VideoRecorderResources.NativeStoppedEarly,
                    _captureResult.StopReason));
            }
        }
        catch (RecordPartialOutputException ex)
        {
            _lastError = ex.Message;
            _hasUsableOutput = IsUsableRecording(ex.VideoPath, recordingPath);
            _warn?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.VideoRecorderResources.NativePartialOutput,
                ex.Message,
                ex.RecoveryHint));
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
            _warn?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.VideoRecorderResources.NativeRecordingFailed,
                ex.Message));
        }
        finally
        {
            recordingStarted.TrySetResult(false);
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
        _hasUsableOutput = false;

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

    private static ServiceProvider CreateRecordingServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddWinAppUiAutomation();
        services.AddWinAppUiRecording();
        return services.BuildServiceProvider();
    }

    private static class NativeMethods
    {
        [DllImport("shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr processHandle, out int awareness);
    }

    private static bool IsUsableRecording(string? candidatePath, string expectedPath)
    {
        try
        {
            return candidatePath is not null
                && string.Equals(Path.GetFullPath(candidatePath), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase)
                && File.Exists(expectedPath)
                && new FileInfo(expectedPath).Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task CompleteTimedOutStopAsync(
        Task recordingTask,
        CancellationTokenSource? recordingCancellation,
        string? segmentDirectory)
    {
        try
        {
            await recordingTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Native recording failed while completing timed-out cleanup: {ex.Message}");
        }
        finally
        {
            _recordingEndUtc = _clock.UtcNow;
            recordingCancellation?.Dispose();
            await DisposeRecordingServiceOwnerQuietlyAsync().ConfigureAwait(false);
            DeleteDirectoryQuietly(segmentDirectory);
        }
    }

    private async ValueTask DisposeRecordingServiceOwnerQuietlyAsync()
    {
        if (Interlocked.Exchange(ref _recordingServiceOwnerDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_recordingServiceOwner is not null)
            {
                await _recordingServiceOwner.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Failed to dispose the native recorder service provider: {ex.Message}");
        }
    }

    private void DeleteDirectoryQuietly(string? directory)
    {
        try
        {
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Failed to delete native recording directory '{directory}': {ex.Message}");
        }
    }
}

#endif
