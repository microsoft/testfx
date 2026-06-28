// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VideoRecorder;

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
[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal sealed partial class FfmpegVideoRecorder
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly VideoRecorderOptions _options;
    private readonly string _outputDirectory;
    private readonly IClock _clock;
    private readonly Action<string>? _log;
    private readonly Action<string>? _warn;
    private readonly ConcurrentQueue<string> _recentFfmpegOutput = new();
#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif

    private Process? _process;
    private string? _segmentListPath;

    public FfmpegVideoRecorder(VideoRecorderOptions options, string outputDirectory, IClock clock, Action<string>? log, Action<string>? warn)
    {
        _options = options;
        _outputDirectory = outputDirectory;
        _clock = clock;
        _log = log;
        _warn = warn;
        FfmpegPath = FindFfmpeg(options.FfmpegPath);
    }

    public bool IsAvailable => FfmpegPath is not null;

    /// <summary>
    /// Gets the path to the resolved ffmpeg executable, or <see langword="null"/> if none was found.
    /// </summary>
    public string? FfmpegPath { get; }

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
    public string? SegmentDirectory { get; private set; }

    /// <summary>
    /// Gets the file extension used for produced videos (without the dot).
    /// </summary>
    public string SegmentExtension { get; private set; } = "mp4";

    /// <summary>
    /// Starts the continuous segmented recording. Best-effort: never throws.
    /// </summary>
    public void Start()
    {
        if (FfmpegPath is null)
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
                SegmentExtension = _options.Format == VideoRecorderFormat.WebMVp9 ? "webm" : "mp4";
                string segmentDirectory = Path.Combine(_outputDirectory, "segments_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(segmentDirectory);
                string segmentListPath = Path.Combine(segmentDirectory, "segments.csv");
                string arguments = BuildSegmentArguments(segmentDirectory, segmentListPath);

                var startInfo = new ProcessStartInfo(FfmpegPath, arguments)
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                _log?.Invoke($"Starting screen recording: \"{FfmpegPath}\" {arguments}");
                ClearRecentFfmpegOutput();

                process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += OnFfmpegOutput;
                process.ErrorDataReceived += OnFfmpegOutput;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _process = process;
                SegmentDirectory = segmentDirectory;
                _segmentListPath = segmentListPath;
                RecordingStartUtc = _clock.UtcNow;
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
                SegmentDirectory = null;
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
#if NET
                    await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
#else
                    await process.StandardInput.FlushAsync().ConfigureAwait(false);
#endif
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
        string? directory = SegmentDirectory;
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
    public static void PruneSegments(IEnumerable<VideoSegment> segments)
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
        if (FfmpegPath is null || segments.Count == 0 || SegmentDirectory is null)
        {
            return null;
        }

        string outputPath = Path.Combine(_outputDirectory, outputFileName);
        string listPath = Path.Combine(SegmentDirectory, "concat_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");

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
}
