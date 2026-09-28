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
                return nativeRecorder;
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
            warn?.Invoke($"The WinAppCLI native video recorder could not be initialized; falling back to ffmpeg. {ex.Message}");
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
}
