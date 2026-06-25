// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
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
/// <c>--capture-video</c>. The screen is recorded continuously as a rolling sequence of segments;
/// at the end of the run the per-test clips (or the chaptered session video) are cut from those
/// segments using each test's timing. This avoids the race a per-test recorder has — data consumers
/// are invoked asynchronously, so by the time a test's <c>InProgress</c> message is observed the
/// test may already be finishing.
/// </summary>
internal sealed class VideoRecorderSessionHandler :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer
{
    private readonly bool _enabled;
    private readonly FfmpegVideoRecorder? _recorder;
    private readonly VideoRecorderOptions _options;
    private readonly VideoRecorderPersistenceMode _persistMode;
    private readonly VideoCaptureGranularity _granularity;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger<VideoRecorderSessionHandler> _logger;

    private readonly object _stateGate = new();
    private readonly List<TestRecord> _testRecords = new();
    private readonly Dictionary<string, DateTimeOffset> _inFlight = new();

    private SessionUid? _sessionUid;
    private bool _anyTestFailed;
    private int _bufferDropWarned;

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
        _options = options;

        _enabled = commandLineOptions.IsOptionSet(VideoRecorderCommandLineProvider.EnableOptionName);
        if (!_enabled)
        {
            _persistMode = options.PersistMode;
            _granularity = options.Granularity;
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

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_recorder is null || value is not TestNodeUpdateMessage update)
        {
            return Task.CompletedTask;
        }

        // A node carries a single state property in practice; use FirstOrDefault rather than
        // SingleOrDefault so a malformed producer can't throw out of the consumer pump.
        TestNodeStateProperty? state = update.TestNode.Properties.OfType<TestNodeStateProperty>().FirstOrDefault();
        if (state is null)
        {
            return Task.CompletedTask;
        }

        string testUid = update.TestNode.Uid.Value;

        if (state is InProgressTestNodeStateProperty)
        {
            lock (_stateGate)
            {
                // Remember when we first saw the test running, as a fallback start time if the
                // terminal message doesn't carry authoritative timing.
                if (!_inFlight.ContainsKey(testUid))
                {
                    _inFlight[testUid] = DateTimeOffset.UtcNow;
                }
            }

            return Task.CompletedTask;
        }

        if (!IsTerminal(state))
        {
            return Task.CompletedTask;
        }

        bool isFailure = state is FailedTestNodeStateProperty or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty;

        // Prefer the authoritative wall-clock timing the framework publishes on the terminal
        // message; fall back to the observed in-progress timestamp (and now) otherwise.
        (DateTimeOffset start, DateTimeOffset end) = ResolveTiming(update, testUid);

        lock (_stateGate)
        {
            _inFlight.Remove(testUid);
            if (isFailure)
            {
                _anyTestFailed = true;
            }

            _testRecords.Add(new TestRecord(update.TestNode.DisplayName, start, end, isFailure, OutcomeText(state)));
        }

        TryPruneOldSegments();
        return Task.CompletedTask;
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

        if (!_recorder.IsAvailable)
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(VideoRecorderResources.FfmpegNotFound), cancellationToken).ConfigureAwait(false);
            return;
        }

        // Record continuously for the whole session regardless of granularity; both per-test clips
        // and the session video are cut from this single recording afterwards.
        _recorder.Start();

        string hint = _granularity == VideoCaptureGranularity.PerSession
            ? VideoRecorderResources.HintPerSession
            : VideoRecorderResources.HintPerTest;
        string ready = string.Format(
            CultureInfo.CurrentCulture,
            VideoRecorderResources.RecorderReady,
            _recorder.FfmpegPath,
            _granularity,
            _persistMode,
            hint);
        await _outputDevice.DisplayAsync(this, new FormattedTextOutputDeviceData(ready), cancellationToken).ConfigureAwait(false);
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_recorder is null || !_recorder.IsAvailable)
        {
            return;
        }

        CancellationToken cancellationToken = testSessionContext.CancellationToken;

        // The platform drains the data-consumer queue before invoking session-finishing handlers,
        // so every test's terminal message has already been recorded into _testRecords.
        await _recorder.StopAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ProduceVideosAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteDirectoryQuietly(_recorder.SegmentDirectory);
        }
    }

    private async Task ProduceVideosAsync(CancellationToken cancellationToken)
    {
        if (_recorder is null || _recorder.RecordingStartUtc is not { } recordingStart)
        {
            return;
        }

        IReadOnlyList<VideoSegment> segments = _recorder.ReadSegments();
        if (segments.Count == 0)
        {
            await _outputDevice.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.NoUsableVideoProduced, -1, _recorder.DescribeLastFfmpegError())),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        TestRecord[] records;
        bool anyFailed;
        lock (_stateGate)
        {
            records = _testRecords.ToArray();
            anyFailed = _anyTestFailed;
        }

        if (_granularity == VideoCaptureGranularity.PerSession)
        {
            await ProduceSessionVideoAsync(segments, records, anyFailed, recordingStart, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ProducePerTestVideosAsync(segments, records, recordingStart, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProducePerTestVideosAsync(IReadOnlyList<VideoSegment> segments, TestRecord[] records, DateTimeOffset recordingStart, CancellationToken cancellationToken)
    {
        foreach (TestRecord record in records)
        {
            bool keep = _persistMode == VideoRecorderPersistenceMode.Always || record.IsFailure;
            if (!keep)
            {
                continue;
            }

            double startOffset = Math.Max(0, (record.Start - recordingStart).TotalSeconds);
            double endOffset = Math.Max(startOffset, (record.End - recordingStart).TotalSeconds);

            var overlapping = new List<VideoSegment>();
            foreach (VideoSegment segment in segments)
            {
                if (segment.Overlaps(startOffset, endOffset))
                {
                    overlapping.Add(segment);
                }
            }

            if (overlapping.Count == 0)
            {
                _logger.LogTrace($"No segments survived for test '{record.DisplayName}' (pruned by the rolling buffer); skipping its clip.");
                continue;
            }

            string fileName = BuildFileName(record.DisplayName);
            string? file = await _recorder!.ConcatAsync(overlapping, fileName, ffmetadataPath: null, cancellationToken).ConfigureAwait(false);
            if (file is not null)
            {
                await PublishArtifactAsync(
                    file,
                    string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDisplayName, record.DisplayName),
                    string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ArtifactPerTestDescription, record.DisplayName)).ConfigureAwait(false);
            }
        }
    }

    private async Task ProduceSessionVideoAsync(IReadOnlyList<VideoSegment> segments, TestRecord[] records, bool anyFailed, DateTimeOffset recordingStart, CancellationToken cancellationToken)
    {
        bool keep = _persistMode == VideoRecorderPersistenceMode.Always || anyFailed;
        if (!keep)
        {
            return;
        }

        string? metadataPath = null;
        if (_options.IncludeChapters && records.Length > 0)
        {
            metadataPath = TryWriteChapterMetadata(segments, records, recordingStart);
        }

        string fileName = BuildFileName("session");
        string? file = await _recorder!.ConcatAsync(segments, fileName, metadataPath, cancellationToken).ConfigureAwait(false);
        if (file is not null)
        {
            await PublishArtifactAsync(file, VideoRecorderResources.ArtifactSessionDisplayName, VideoRecorderResources.ArtifactSessionDescription).ConfigureAwait(false);
        }
    }

    private string? TryWriteChapterMetadata(IReadOnlyList<VideoSegment> segments, TestRecord[] records, DateTimeOffset recordingStart)
    {
        if (_recorder?.SegmentDirectory is not { } directory)
        {
            return null;
        }

        // Segments may have been pruned from the front of the recording, so the concatenated video
        // starts at the first surviving segment. Express chapter times relative to that.
        double baseOffset = segments[0].StartSeconds;
        double videoEnd = segments[segments.Count - 1].EndSeconds - baseOffset;

        (TestRecord Record, double Start, double End)[] ordered = records
            .Select(record => (Record: record, Start: Math.Max(0, (record.Start - recordingStart).TotalSeconds - baseOffset), End: (record.End - recordingStart).TotalSeconds - baseOffset))
            .Where(item => item.End > 0)
            .OrderBy(item => item.Start)
            .ToArray();

        if (ordered.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(";FFMETADATA1");
        foreach ((TestRecord record, double start, double end) in ordered)
        {
            long startMs = (long)(Math.Max(0, start) * 1000);
            long endMs = (long)(Math.Min(videoEnd, Math.Max(start + 0.001, end)) * 1000);
            if (endMs <= startMs)
            {
                endMs = startMs + 1;
            }

            string title = string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.ChapterTitleFormat, record.DisplayName, record.Outcome);
            builder.AppendLine("[CHAPTER]");
            builder.AppendLine("TIMEBASE=1/1000");
            builder.AppendLine("START=" + startMs.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("END=" + endMs.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("title=" + EscapeMetadata(title));
        }

        try
        {
            string metadataPath = Path.Combine(directory, "chapters_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
            File.WriteAllText(metadataPath, builder.ToString());
            return metadataPath;
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Failed to write chapter metadata: {ex.Message}");
            return null;
        }
    }

    // Bounds disk usage during long runs. Segments that no running test needs are pruned; in
    // on-failure mode passed-only footage is always pruned (we keep failed footage to slice at the
    // end), and when a rolling-buffer length is configured, footage older than that window is
    // dropped too.
    private void TryPruneOldSegments()
    {
        if (_recorder is null || _recorder.RecordingStartUtc is not { } recordingStart)
        {
            return;
        }

        TimeSpan? cap = _options.MaxRetainedDuration;
        if (cap is null && _persistMode != VideoRecorderPersistenceMode.OnFailure)
        {
            // Nothing to prune: Always mode with no cap keeps the full run.
            return;
        }

        double nowOffset = (DateTimeOffset.UtcNow - recordingStart).TotalSeconds;
        double[] failedWindows;
        double watermark;
        lock (_stateGate)
        {
            watermark = _inFlight.Count == 0
                ? nowOffset
                : _inFlight.Values.Min(start => (start - recordingStart).TotalSeconds);

            failedWindows = _testRecords
                .Where(record => record.IsFailure)
                .SelectMany(record => new[] { (record.Start - recordingStart).TotalSeconds, (record.End - recordingStart).TotalSeconds })
                .ToArray();
        }

        double ageCutoff = cap is { } c ? nowOffset - c.TotalSeconds : double.NegativeInfinity;

        var prunable = new List<VideoSegment>();
        foreach (VideoSegment segment in _recorder.ReadSegments())
        {
            // Never prune footage at or after the earliest running test's start.
            if (segment.EndSeconds > watermark)
            {
                break;
            }

            bool overlapsFailed = OverlapsAnyFailedWindow(segment, failedWindows);
            if (_persistMode == VideoRecorderPersistenceMode.OnFailure)
            {
                if (!overlapsFailed)
                {
                    prunable.Add(segment);
                }
                else if (cap is not null && segment.EndSeconds <= ageCutoff)
                {
                    prunable.Add(segment);
                    WarnBufferDropOnce(cap.Value);
                }
            }
            else if (cap is not null && segment.EndSeconds <= ageCutoff)
            {
                prunable.Add(segment);
                WarnBufferDropOnce(cap.Value);
            }
        }

        if (prunable.Count > 0)
        {
            _recorder.PruneSegments(prunable);
        }
    }

    private void WarnBufferDropOnce(TimeSpan cap)
    {
        if (Interlocked.Exchange(ref _bufferDropWarned, 1) == 0)
        {
            string capText = string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.CapDurationDescription, cap);
            _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, VideoRecorderResources.RecordingCapReached, capText));
        }
    }

    private static bool OverlapsAnyFailedWindow(VideoSegment segment, double[] failedWindows)
    {
        for (int i = 0; i + 1 < failedWindows.Length; i += 2)
        {
            if (segment.Overlaps(failedWindows[i], failedWindows[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolveTiming(TestNodeUpdateMessage update, string testUid)
    {
        TimingProperty? timing = update.TestNode.Properties.OfType<TimingProperty>().FirstOrDefault();
        if (timing is not null)
        {
            return (timing.GlobalTiming.StartTime, timing.GlobalTiming.EndTime);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset observedStart;
        lock (_stateGate)
        {
            observedStart = _inFlight.TryGetValue(testUid, out DateTimeOffset value) ? value : now;
        }

        return (observedStart, now);
    }

    private static bool IsTerminal(TestNodeStateProperty state)
        => state is PassedTestNodeStateProperty or FailedTestNodeStateProperty
            or ErrorTestNodeStateProperty or TimeoutTestNodeStateProperty or SkippedTestNodeStateProperty;

    private static string OutcomeText(TestNodeStateProperty state)
        => state switch
        {
            FailedTestNodeStateProperty => VideoRecorderResources.OutcomeFailed,
            ErrorTestNodeStateProperty => VideoRecorderResources.OutcomeError,
            TimeoutTestNodeStateProperty => VideoRecorderResources.OutcomeTimeout,
            SkippedTestNodeStateProperty => VideoRecorderResources.OutcomeSkipped,
            _ => VideoRecorderResources.OutcomePassed,
        };

    private string BuildFileName(string? name)
    {
        string extension = _recorder!.SegmentExtension;
        string sanitized = Sanitize(name);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
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

    private static string EscapeMetadata(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (c is '=' or ';' or '#' or '\\' or '\n')
            {
                builder.Append('\\');
            }

            builder.Append(c == '\n' ? ' ' : c);
        }

        return builder.ToString();
    }

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

    private void DeleteDirectoryQuietly(string? directory)
    {
        if (directory is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Failed to delete segment directory '{directory}': {ex.Message}");
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

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.MaxDurationOptionName, out string[]? maxDurationArguments)
            && maxDurationArguments.Length > 0
            && int.TryParse(maxDurationArguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0)
        {
            options.MaxRetainedDuration = TimeSpan.FromSeconds(seconds);
        }

        if (commandLineOptions.TryGetOptionArgumentList(VideoRecorderCommandLineProvider.ChaptersOptionName, out string[]? chapterArguments)
            && chapterArguments.Length > 0)
        {
            options.IncludeChapters = !chapterArguments[0].Equals(VideoRecorderCommandLineProvider.ChaptersOff, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TestRecord
    {
        public TestRecord(string displayName, DateTimeOffset start, DateTimeOffset end, bool isFailure, string outcome)
        {
            DisplayName = displayName;
            Start = start;
            End = end;
            IsFailure = isFailure;
            Outcome = outcome;
        }

        public string DisplayName { get; }

        public DateTimeOffset Start { get; }

        public DateTimeOffset End { get; }

        public bool IsFailure { get; }

        public string Outcome { get; }
    }
}
