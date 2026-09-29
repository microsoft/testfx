// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VideoRecorder;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal static class VideoRecorderFactory
{
    public static IVideoRecorder Create(
        VideoRecorderOptions options,
        string outputDirectory,
        IClock clock,
        Action<string>? log,
        Action<string>? warn)
    {
#if NET10_0_OR_GREATER && WINDOWS
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)
            && IsNativeConfigurationSupported(options))
        {
            if (!WinAppCliVideoRecorder.IsProcessPerMonitorDpiAware())
            {
                log?.Invoke("The WinAppCLI native video recorder requires a per-monitor DPI-aware process; falling back to ffmpeg.");
                return new FfmpegVideoRecorder(options, outputDirectory, clock, log, warn);
            }

            IVideoRecorder? nativeRecorder = TryCreateNative(
                options,
                () => new WinAppCliVideoRecorder(options, outputDirectory, clock, log, warn),
                warn);
            if (nativeRecorder is not null)
            {
                return CreateWithStartupFallback(
                    nativeRecorder,
                    new FfmpegVideoRecorder(options, outputDirectory, clock, log, warn),
                    log);
            }
        }
#endif

        return new FfmpegVideoRecorder(options, outputDirectory, clock, log, warn);
    }

    internal static IVideoRecorder? TryCreateNative(
        VideoRecorderOptions options,
        Func<IVideoRecorder> createNativeRecorder,
        Action<string>? warn)
    {
        if (!IsNativeConfigurationSupported(options))
        {
            return null;
        }

        try
        {
            return createNativeRecorder();
        }
        catch (Exception ex)
        {
            warn?.Invoke(string.Format(
                CultureInfo.CurrentCulture,
                Resources.VideoRecorderResources.NativeInitializationFallback,
                ex.Message));
            return null;
        }
    }

    internal static bool IsNativeConfigurationSupported(VideoRecorderOptions options)
        => options.FfmpegPath is null
            && options.Format == VideoRecorderFormat.Mp4H264
            && options.Granularity == VideoCaptureGranularity.PerSession
            && options.Source == VideoCaptureSource.Screen
            && options.MaxRetainedDuration is null
            && !options.IncludeChapters
            && RoslynString.IsNullOrWhiteSpace(options.ExtraRecorderArguments)
            && RoslynString.IsNullOrWhiteSpace(options.InputArgumentsOverride);

    internal static IVideoRecorder CreateWithStartupFallback(
        IVideoRecorder primary,
        IVideoRecorder fallback,
        Action<string>? log)
        => new StartupFallbackVideoRecorder(primary, fallback, log);

    private sealed class StartupFallbackVideoRecorder : IVideoRecorder
    {
        private readonly IVideoRecorder _primary;
        private readonly IVideoRecorder _fallback;
        private readonly Action<string>? _log;
        private IVideoRecorder _active;
        private bool _startAttempted;

        public StartupFallbackVideoRecorder(IVideoRecorder primary, IVideoRecorder fallback, Action<string>? log)
        {
            _primary = primary;
            _fallback = fallback;
            _log = log;
            _active = primary;
        }

        public bool IsAvailable => _primary.IsAvailable || _fallback.IsAvailable;

        public string? FfmpegPath => _active.FfmpegPath;

        public DateTimeOffset? RecordingStartUtc => _active.RecordingStartUtc;

        public string? SegmentDirectory => _active.SegmentDirectory;

        public string SegmentExtension => _active.SegmentExtension;

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_startAttempted)
            {
                return _active.RecordingStartUtc is not null;
            }

            _startAttempted = true;
            if (_primary.IsAvailable)
            {
                try
                {
                    if (await _primary.StartAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"The primary video recorder failed to start: {ex.Message}");
                }

                try
                {
                    await _primary.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"The primary video recorder failed during startup cleanup: {ex.Message}");
                }
            }

            _active = _fallback;
            _log?.Invoke("The native Windows video recorder was unavailable at startup; falling back to ffmpeg.");
            return _fallback.IsAvailable
                && await _fallback.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
            => _active.StopAsync(cancellationToken);

        public IReadOnlyList<VideoSegment> ReadSegments()
            => _active.ReadSegments();

        public string DescribeLastFfmpegError()
            => _active.DescribeLastFfmpegError();

        public Task<string?> ConcatAsync(
            IReadOnlyList<VideoSegment> segments,
            string outputFileName,
            string? ffmetadataPath,
            CancellationToken cancellationToken)
            => _active.ConcatAsync(segments, outputFileName, ffmetadataPath, cancellationToken);
    }
}
