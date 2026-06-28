// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// ffmpeg process lifecycle (launch, monitor, tear down), output capture, command-line argument
/// construction, and executable discovery for <see cref="FfmpegVideoRecorder"/>.
/// </summary>
internal sealed partial class FfmpegVideoRecorder
{
    private async Task<bool> RunFfmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(FfmpegPath!, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _log?.Invoke($"Running ffmpeg: \"{FfmpegPath}\" {arguments}");

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OnFfmpegOutput;
        process.ErrorDataReceived += OnFfmpegOutput;
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!await WaitForExitAsync(process, StopTimeout, cancellationToken).ConfigureAwait(false))
            {
                // ffmpeg didn't finish within the timeout (or the run was cancelled). Disposing the
                // Process below does not terminate the underlying OS process, so kill it explicitly
                // to avoid leaving an orphaned ffmpeg behind on slow machines or long sessions.
                TryKill(process);
                return false;
            }

            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _log?.Invoke($"ffmpeg invocation failed: {ex.Message}");
            return false;
        }
        finally
        {
            process.OutputDataReceived -= OnFfmpegOutput;
            process.ErrorDataReceived -= OnFfmpegOutput;
        }
    }

#if NETCOREAPP
    private void ClearRecentFfmpegOutput() => _recentFfmpegOutput.Clear();
#else
    private void ClearRecentFfmpegOutput()
    {
        while (_recentFfmpegOutput.TryDequeue(out _))
        {
        }
    }
#endif

    private void DetachAndDispose(Process process)
    {
        process.OutputDataReceived -= OnFfmpegOutput;
        process.ErrorDataReceived -= OnFfmpegOutput;
        process.Dispose();
    }

    private void OnFfmpegOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null)
        {
            return;
        }

        _log?.Invoke($"[ffmpeg] {e.Data}");

        // Keep a small tail of ffmpeg output so capture failures can be reported with context.
        _recentFfmpegOutput.Enqueue(e.Data);
        while (_recentFfmpegOutput.Count > 8 && _recentFfmpegOutput.TryDequeue(out _))
        {
        }
    }

    private string BuildSegmentArguments(string segmentDirectory, string segmentListPath)
    {
        int segmentSeconds = Math.Max(1, _options.SegmentLengthSeconds);
        string input = _options.InputArgumentsOverride ?? BuildDefaultInput();
        string encoder = _options.Format == VideoRecorderFormat.WebMVp9
            ? "-c:v libvpx-vp9 -b:v 0 -crf 32 -deadline realtime -cpu-used 5"
            : "-c:v libx264 -preset veryfast -crf 28";
        string segmentFormat = _options.Format == VideoRecorderFormat.WebMVp9 ? "webm" : "mp4";

        string extra = RoslynString.IsNullOrWhiteSpace(_options.ExtraRecorderArguments)
            ? string.Empty
            : $"{_options.ExtraRecorderArguments!.Trim()} ";

        string segmentPattern = Path.Combine(segmentDirectory, $"seg_%05d.{SegmentExtension}");

        // gdigrab (and some grabbers) can capture an odd width/height, but yuv420p requires both to
        // be even. Crop to the nearest even dimensions so the encoder's format conversion can't fail
        // with "width/height not divisible by 2". Skipped when the caller fully overrides the input.
        string evenCrop = _options.InputArgumentsOverride is null
            ? "-vf \"crop=trunc(iw/2)*2:trunc(ih/2)*2\" "
            : string.Empty;

        // Force a keyframe at every segment boundary so each segment starts on an IDR frame; that
        // makes the segments independently decodable and lets us concatenate them later with
        // stream copy (-c copy) instead of a slow, lossy re-encode.
        return $"-y {input} {evenCrop}{encoder} -pix_fmt yuv420p -force_key_frames \"expr:gte(t,n_forced*{segmentSeconds})\" "
            + $"-f segment -segment_time {segmentSeconds} -segment_format {segmentFormat} -reset_timestamps 1 "
            + $"-segment_list \"{segmentListPath}\" -segment_list_type csv {extra}\"{segmentPattern}\"";
    }

    private string BuildDefaultInput()
    {
        int fps = _options.FrameRate;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (_options.Source == VideoCaptureSource.Window)
            {
                if (TryGetCurrentProcessWindowRegion(out int x, out int y, out int width, out int height))
                {
                    return $"-f gdigrab -framerate {fps} -offset_x {x} -offset_y {y} -video_size {width}x{height} -i desktop";
                }

                _log?.Invoke(VideoRecorderResources.WindowCaptureFallback);
            }

            return $"-f gdigrab -framerate {fps} -i desktop";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // The avfoundation screen device index can vary; override via InputArgumentsOverride if needed.
            return $"-f avfoundation -capture_cursor 1 -framerate {fps} -i \"Capture screen 0\"";
        }

        // Assume Linux/X11.
        string display = Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 } d ? d : ":0.0";
        return $"-f x11grab -framerate {fps} -video_size {_options.X11CaptureSize} -i {display}";
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (Exception)
        {
            // Best effort.
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // Process.WaitForExitAsync(CancellationToken) is available on modern .NET and provided by a
        // polyfill on netstandard2.0, so a single token-based implementation works on every target.
        // A linked CTS bounds the wait by the timeout; an already-cancelled token simply means we
        // report whether the process happened to have exited (best-effort stop on a cancelled run).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }

    private static string? FindFfmpeg(string? configuredPath)
    {
        if (!RoslynString.IsNullOrEmpty(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        string executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return null;
        }

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (RoslynString.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            try
            {
                string candidate = Path.Combine(directory.Trim(), executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }
}
