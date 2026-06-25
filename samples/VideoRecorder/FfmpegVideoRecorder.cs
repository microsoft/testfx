// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// A single finalized recording segment and the time range (in seconds, relative to the start of
/// the recording) that it covers.
/// </summary>
internal readonly struct VideoSegment
{
    public VideoSegment(string path, double startSeconds, double endSeconds)
    {
        Path = path;
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
    }

    public string Path { get; }

    public double StartSeconds { get; }

    public double EndSeconds { get; }

    public bool Overlaps(double fromSeconds, double toSeconds)
        => EndSeconds > fromSeconds && StartSeconds < toSeconds;
}

/// <summary>
/// Captures the screen by driving a single long-lived ffmpeg process that writes the recording as a
/// rolling sequence of short, independently playable segments (the ffmpeg <c>segment</c> muxer).
/// Recording runs continuously for the whole session, which removes the start/stop race that a
/// per-test recorder suffers (by the time an asynchronous data consumer observes that a test
/// started, the test may already be finishing). Per-test clips and the chaptered session video are
/// produced afterwards by losslessly concatenating (<c>-c copy</c>) the relevant segments.
/// </summary>
/// <remarks>
/// <see cref="Start"/> is best-effort and never throws — if ffmpeg is unavailable or the launch
/// fails, it warns and the recorder simply produces nothing.
/// </remarks>
internal sealed class FfmpegVideoRecorder
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly VideoRecorderOptions _options;
    private readonly string _outputDirectory;
    private readonly string? _ffmpegPath;
    private readonly Action<string>? _log;
    private readonly Action<string>? _warn;
    private readonly ConcurrentQueue<string> _recentFfmpegOutput = new();
    private readonly object _gate = new();

    private Process? _process;
    private string? _segmentDirectory;
    private string? _segmentListPath;
    private string _segmentExtension = "mp4";

    public FfmpegVideoRecorder(VideoRecorderOptions options, string outputDirectory, Action<string>? log, Action<string>? warn)
    {
        _options = options;
        _outputDirectory = outputDirectory;
        _log = log;
        _warn = warn;
        _ffmpegPath = FindFfmpeg(options.FfmpegPath);
    }

    public bool IsAvailable => _ffmpegPath is not null;

    /// <summary>
    /// Gets the path to the resolved ffmpeg executable, or <see langword="null"/> if none was found.
    /// </summary>
    public string? FfmpegPath => _ffmpegPath;

    /// <summary>
    /// Gets the wall-clock time at which continuous recording started, used to map a test's
    /// timestamps to an offset within the recording. <see langword="null"/> until <see cref="Start"/>
    /// succeeds.
    /// </summary>
    public DateTimeOffset? RecordingStartUtc { get; private set; }

    /// <summary>
    /// Gets the directory the segments are written to, or <see langword="null"/> if recording never
    /// started.
    /// </summary>
    public string? SegmentDirectory => _segmentDirectory;

    /// <summary>
    /// Gets the file extension used for produced videos (without the dot).
    /// </summary>
    public string SegmentExtension => _segmentExtension;

    /// <summary>
    /// Starts the continuous segmented recording. Best-effort: never throws.
    /// </summary>
    public void Start()
    {
        if (_ffmpegPath is null)
        {
            _warn?.Invoke(VideoRecorderResources.FfmpegNotFound);
            return;
        }

        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            Process? process = null;
            try
            {
                _segmentExtension = _options.Format == VideoRecorderFormat.WebMVp9 ? "webm" : "mp4";
                string segmentDirectory = Path.Combine(_outputDirectory, "segments_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(segmentDirectory);
                string segmentListPath = Path.Combine(segmentDirectory, "segments.csv");
                string arguments = BuildSegmentArguments(segmentDirectory, segmentListPath);

                var startInfo = new ProcessStartInfo(_ffmpegPath, arguments)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                _log?.Invoke($"Starting screen recording: \"{_ffmpegPath}\" {arguments}");
                ClearRecentFfmpegOutput();

                process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += OnFfmpegOutput;
                process.ErrorDataReceived += OnFfmpegOutput;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _process = process;
                _segmentDirectory = segmentDirectory;
                _segmentListPath = segmentListPath;
                RecordingStartUtc = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                // Best-effort: anything from creating the output directory, building arguments, or
                // launching ffmpeg simply means no recording. Never surface as a test failure.
                _warn?.Invoke(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.FailedToStartRecording, ex.Message));
                if (process is not null)
                {
                    TryKill(process);
                    DetachAndDispose(process);
                }

                _process = null;
                _segmentDirectory = null;
                _segmentListPath = null;
                RecordingStartUtc = null;
            }
        }
    }

    /// <summary>
    /// Stops the continuous recording, asking ffmpeg to finalize the current segment cleanly.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        lock (_gate)
        {
            process = _process;
            _process = null;
        }

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    await process.StandardInput.WriteLineAsync("q").ConfigureAwait(false);
                    await process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // stdin already closed; fall through to wait/kill.
                }

                if (!await WaitForExitAsync(process, StopTimeout, cancellationToken).ConfigureAwait(false))
                {
                    _warn?.Invoke(VideoRecorderResources.StopTimeoutKilled);
                    TryKill(process);
                }
            }
        }
        finally
        {
            DetachAndDispose(process);
        }
    }

    /// <summary>
    /// Reads the finalized segments from the segment list, ordered by their position in the
    /// recording timeline. The segment currently being written is not listed until it is finalized,
    /// so it is naturally excluded.
    /// </summary>
    public IReadOnlyList<VideoSegment> ReadSegments()
    {
        string? listPath = _segmentListPath;
        string? directory = _segmentDirectory;
        if (listPath is null || directory is null || !File.Exists(listPath))
        {
            return [];
        }

        var segments = new List<VideoSegment>();
        string[] lines;
        try
        {
            lines = File.ReadAllLines(listPath);
        }
        catch (IOException)
        {
            return segments;
        }

        foreach (string line in lines)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 3
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double start)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double end))
            {
                continue;
            }

            string path = Path.IsPathRooted(parts[0]) ? parts[0] : Path.Combine(directory, parts[0]);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                segments.Add(new VideoSegment(path, start, end));
            }
        }

        return segments;
    }

    /// <summary>
    /// Deletes the given finalized segments from disk (used by the rolling-buffer pruning to bound
    /// disk usage on long runs). Best-effort.
    /// </summary>
    public void PruneSegments(IEnumerable<VideoSegment> segments)
    {
        foreach (VideoSegment segment in segments)
        {
            try
            {
                if (File.Exists(segment.Path))
                {
                    File.Delete(segment.Path);
                }
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }

    /// <summary>
    /// Losslessly concatenates the given segments (and optional chapter metadata) into a single
    /// output file using a short-lived ffmpeg <c>concat</c> invocation. Returns the output path on
    /// success, or <see langword="null"/> if nothing usable was produced.
    /// </summary>
    public async Task<string?> ConcatAsync(IReadOnlyList<VideoSegment> segments, string outputFileName, string? ffmetadataPath, CancellationToken cancellationToken)
    {
        if (_ffmpegPath is null || segments.Count == 0 || _segmentDirectory is null)
        {
            return null;
        }

        string outputPath = Path.Combine(_outputDirectory, outputFileName);
        string listPath = Path.Combine(_segmentDirectory, "concat_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");

        try
        {
            var builder = new StringBuilder();
            foreach (VideoSegment segment in segments)
            {
                // The concat demuxer requires single quotes around the path with embedded quotes escaped.
                builder.Append("file '").Append(segment.Path.Replace("'", @"'\''")).AppendLine("'");
            }

            File.WriteAllText(listPath, builder.ToString());

            var arguments = new StringBuilder();
            arguments.Append("-y -f concat -safe 0 -i \"").Append(listPath).Append('"');
            if (ffmetadataPath is not null)
            {
                arguments.Append(" -i \"").Append(ffmetadataPath).Append("\" -map 0 -map_chapters 1");
            }

            arguments.Append(" -c copy \"").Append(outputPath).Append('"');

            if (!await RunFfmpegAsync(arguments.ToString(), cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Failed to concatenate segments into '{outputFileName}': {ex.Message}");
            return null;
        }
        finally
        {
            try
            {
                if (File.Exists(listPath))
                {
                    File.Delete(listPath);
                }
            }
            catch (Exception)
            {
                // Best effort.
            }
        }

        return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0
            ? outputPath
            : null;
    }

    /// <summary>
    /// Returns the tail of recent ffmpeg output filtered to error-like lines, for diagnostics.
    /// </summary>
    public string DescribeLastFfmpegError()
    {
        string[] lines = _recentFfmpegOutput.ToArray();
        if (lines.Length == 0)
        {
            return "No ffmpeg output was captured.";
        }

        string[] errors = Array.FindAll(
            lines,
            line => line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("Could not", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("Failed", StringComparison.OrdinalIgnoreCase) >= 0);

        return errors.Length > 0 ? string.Join(" | ", errors) : lines[lines.Length - 1];
    }

    private async Task<bool> RunFfmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_ffmpegPath!, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _log?.Invoke($"Running ffmpeg: \"{_ffmpegPath}\" {arguments}");

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OnFfmpegOutput;
        process.ErrorDataReceived += OnFfmpegOutput;
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await WaitForExitAsync(process, StopTimeout, cancellationToken).ConfigureAwait(false);
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

        string extra = string.IsNullOrWhiteSpace(_options.ExtraRecorderArguments)
            ? string.Empty
            : $"{_options.ExtraRecorderArguments!.Trim()} ";

        string segmentPattern = Path.Combine(segmentDirectory, $"seg_%05d.{_segmentExtension}");

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

    // Resolves the screen rectangle of the window to capture so gdigrab can record just that
    // region. Candidates are tried in order: the process main window (a GUI app under test owns
    // it), then the foreground window (the terminal you launched from — this is what makes
    // Windows Terminal work, since its window isn't owned by the test process), then the console
    // window (classic conhost). Returns false when none is a usable visible window, in which case
    // the caller falls back to full-screen capture.
    private bool TryGetCurrentProcessWindowRegion(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;

#if NETCOREAPP
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
#endif

        // gdigrab captures physical pixels, so we must read window rectangles in physical
        // coordinates too. Make this thread Per-Monitor-V2 DPI aware while querying; otherwise a
        // DPI-unaware process gets virtualized (logical) coordinates and the region is wrong on
        // any scaled display. Restore the previous context afterwards.
        IntPtr previousDpiContext = TrySetPerMonitorDpiAwareThread();
        try
        {
            int screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
            int screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

            foreach (IntPtr handle in EnumerateCandidateWindows())
            {
                if (handle == IntPtr.Zero
                    || !NativeMethods.IsWindowVisible(handle)
                    || !NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect))
                {
                    continue;
                }

                int left = Math.Max(0, rect.Left);
                int top = Math.Max(0, rect.Top);
                int right = screenWidth > 0 ? Math.Min(rect.Right, screenWidth) : rect.Right;
                int bottom = screenHeight > 0 ? Math.Min(rect.Bottom, screenHeight) : rect.Bottom;

                // gdigrab requires even dimensions for the yuv420p pixel format.
                int candidateWidth = (right - left) & ~1;
                int candidateHeight = (bottom - top) & ~1;
                if (candidateWidth <= 0 || candidateHeight <= 0)
                {
                    continue;
                }

                x = left;
                y = top;
                width = candidateWidth;
                height = candidateHeight;
                return true;
            }
        }
        finally
        {
            if (previousDpiContext != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.SetThreadDpiAwarenessContext(previousDpiContext);
                }
                catch (Exception)
                {
                    // Best effort; the override is per-thread and short-lived.
                }
            }
        }

        return false;
    }

    private static IntPtr TrySetPerMonitorDpiAwareThread()
    {
        try
        {
            return NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch (Exception)
        {
            // SetThreadDpiAwarenessContext is unavailable before Windows 10 1607; proceed without it.
            return IntPtr.Zero;
        }
    }

    private static IEnumerable<IntPtr> EnumerateCandidateWindows()
    {
        // A GUI app under test owns its main window.
        using (var current = Process.GetCurrentProcess())
        {
            yield return current.MainWindowHandle;
        }

        // The window you launched from (e.g. Windows Terminal), whose window is not owned by the
        // test process. Captured at record time.
        yield return NativeMethods.GetForegroundWindow();

        // A classic console (conhost) window owned by the console host.
        yield return NativeMethods.GetConsoleWindow();
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
#if NETCOREAPP
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
#else
        // On .NET Framework, WaitForExit can't be interrupted once it's blocking, so the token
        // can't shorten the wait anyway. Intentionally don't pass it to Task.Run: a token that is
        // already cancelled would make Task.Run return a cancelled task and throw
        // OperationCanceledException, which would break the best-effort stop on a cancelled run.
        // The timeout bounds the wait.
        _ = cancellationToken;
        return await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds)).ConfigureAwait(false);
#endif
    }

    private static string? FindFfmpeg(string? configuredPath)
    {
        if (!string.IsNullOrEmpty(configuredPath))
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
            if (string.IsNullOrWhiteSpace(directory))
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

    private static class NativeMethods
    {
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
