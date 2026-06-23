// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// An <see cref="IVideoRecorder"/> that captures the screen by driving an external ffmpeg
/// process. Recording is started/stopped by the caller; stopping sends <c>q</c> to ffmpeg's
/// stdin so the container is finalized cleanly (killing the process can corrupt the file).
/// </summary>
internal sealed class FfmpegVideoRecorder : IVideoRecorder
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private readonly VideoRecorderOptions _options;
    private readonly string _outputDirectory;
    private readonly string? _ffmpegPath;
    private readonly Action<string>? _log;
    private readonly Action<string>? _warn;
    private readonly ConcurrentBag<string> _producedFiles = new();
    private readonly ConcurrentQueue<string> _recentFfmpegOutput = new();
    private readonly object _gate = new();

    private Process? _process;
    private string? _currentOutputPath;

    public FfmpegVideoRecorder(VideoRecorderOptions options, string outputDirectory, Action<string>? log, Action<string>? warn)
    {
        _options = options;
        _outputDirectory = outputDirectory;
        _log = log;
        _warn = warn;
        _ffmpegPath = FindFfmpeg(options.FfmpegPath);
    }

    public bool IsAvailable => _ffmpegPath is not null;

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _process is { HasExited: false };
            }
        }
    }

    /// <summary>
    /// Gets the path to the resolved ffmpeg executable, or <see langword="null"/> if none was found.
    /// </summary>
    public string? FfmpegPath => _ffmpegPath;

    /// <summary>
    /// Gets the video files produced so far during the session.
    /// </summary>
    public IReadOnlyCollection<string> ProducedFiles => _producedFiles;

    public void Start(string? name = null)
    {
        if (_ffmpegPath is null)
        {
            _warn?.Invoke("Video recording requested but ffmpeg was not found (set VideoRecorderOptions.FfmpegPath or add ffmpeg to PATH). Recording skipped.");
            return;
        }

        lock (_gate)
        {
            if (_process is { HasExited: false })
            {
                // Recording is a session-global, serial resource. Rather than throw (which would
                // fail an unrelated test), skip the new request.
                _warn?.Invoke("A recording is already in progress; ignoring the new Start() request.");
                return;
            }

            // A previous recording that self-terminated (e.g. ffmpeg crashed) was never disposed;
            // tear it down now so its handle and output pump don't leak into this recording.
            if (_process is not null)
            {
                DetachAndDispose(_process);
                _process = null;
                _currentOutputPath = null;
            }

            Process? process = null;
            try
            {
                Directory.CreateDirectory(_outputDirectory);
                string outputPath = Path.Combine(_outputDirectory, BuildFileName(name));
                string arguments = BuildArguments(outputPath);

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
                _currentOutputPath = outputPath;
            }
            catch (Exception ex)
            {
                // Best-effort: anything from creating the output directory, building arguments, or
                // launching ffmpeg simply means no recording. Never surface as a test failure.
                _warn?.Invoke($"Failed to start recording: {ex.Message}. Recording skipped.");
                if (process is not null)
                {
                    // ffmpeg may already be running if the failure happened after Start();
                    // Dispose() alone doesn't terminate it, so kill it first.
                    TryKill(process);
                    DetachAndDispose(process);
                }

                _process = null;
                _currentOutputPath = null;
            }
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

    public async Task<string?> StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        string? outputPath;
        lock (_gate)
        {
            process = _process;
            outputPath = _currentOutputPath;
            _process = null;
            _currentOutputPath = null;
        }

        if (process is null)
        {
            return null;
        }

        int exitCode = -1;
        try
        {
            if (!process.HasExited)
            {
                // Ask ffmpeg to stop gracefully so it finalizes the container.
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
                    _warn?.Invoke("ffmpeg did not stop within the timeout; killing the process. The video file may be incomplete.");
                    TryKill(process);
                }
            }

            try
            {
                if (process.HasExited)
                {
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception)
            {
                // ExitCode may be unavailable if the process was killed; leave it as -1.
            }
        }
        finally
        {
            DetachAndDispose(process);
        }

        if (outputPath is not null && File.Exists(outputPath))
        {
            var outputFile = new FileInfo(outputPath);
            if (outputFile.Length > 0)
            {
                _producedFiles.Add(outputPath);
                _log?.Invoke($"Screen recording saved to '{outputPath}'.");
                return outputPath;
            }

            // ffmpeg created the output file but captured nothing (e.g. a locked/headless
            // desktop). Remove the empty file so it isn't mistaken for a valid recording.
            try
            {
                outputFile.Delete();
            }
            catch (Exception)
            {
                // Best effort.
            }
        }

        _warn?.Invoke($"Screen recording produced no usable file (ffmpeg exit code {exitCode}). {DescribeLastFfmpegError()}");
        return null;
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
        while (_recentFfmpegOutput.Count > 6 && _recentFfmpegOutput.TryDequeue(out _))
        {
        }
    }

    private string DescribeLastFfmpegError()
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

    // Resolves the screen rectangle of the current process's window so gdigrab can capture just
    // that region. Returns false when there is no usable visible window (headless runs, or
    // terminals like Windows Terminal whose visible window is not owned by this process), in
    // which case the caller falls back to full-screen capture.
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

    private string BuildArguments(string outputPath)
    {
        string input = _options.InputArgumentsOverride ?? BuildDefaultInput();
        string encoder = _options.Format == VideoRecorderFormat.WebMVp9
            ? "-c:v libvpx-vp9 -b:v 0 -crf 30"
            : "-c:v libx264 -preset ultrafast -crf 23";

        string extra = string.IsNullOrWhiteSpace(_options.ExtraRecorderArguments)
            ? string.Empty
            : $"{_options.ExtraRecorderArguments!.Trim()} ";

        return $"-y {input} {encoder} -pix_fmt yuv420p {extra}\"{outputPath}\"";
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

                _log?.Invoke("Could not resolve a visible current-process window (headless run, or a terminal whose window is not owned by this process such as Windows Terminal); capturing the full screen instead.");
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

    private string BuildFileName(string? name)
    {
        string extension = _options.Format == VideoRecorderFormat.WebMVp9 ? "webm" : "mp4";
        string sanitized = Sanitize(name);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        // A short random suffix guarantees uniqueness even if two recordings share a name and
        // start within the same millisecond.
        string unique = Guid.NewGuid().ToString("N").Substring(0, 4);
        return sanitized.Length == 0
            ? $"recording_{timestamp}_{unique}.{extension}"
            : $"{sanitized}_{timestamp}_{unique}.{extension}";
    }

    private static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name!.Length);
        foreach (char c in name!)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        }

        return builder.ToString();
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
        // On .NET Framework the token only prevents the wait task from starting; once
        // WaitForExit is blocking it cannot be interrupted, so a cancelled stop still waits up to
        // the timeout. That bound is acceptable here.
        return await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds), cancellationToken).ConfigureAwait(false);
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
