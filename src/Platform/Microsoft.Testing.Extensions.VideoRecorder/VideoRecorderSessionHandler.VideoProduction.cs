// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal sealed partial class VideoRecorderSessionHandler
{
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

            double startOffset = (record.Start - recordingStart).TotalSeconds;
            if (startOffset < 0)
            {
                startOffset = 0;
            }

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
                // Stryker disable once Boolean: continuation scheduling does not change the trace diagnostic.
                await _logger.LogTraceAsync($"No segments survived for test '{record.DisplayName}' (pruned by the rolling buffer); skipping its clip.").ConfigureAwait(false);
                continue;
            }

            string fileName = BuildFileName(record.DisplayName);
            // Stryker disable once Boolean: continuation scheduling does not change video production.
            string? file = await _recorder!.ConcatAsync(overlapping, fileName, ffmetadataPath: null, cancellationToken).ConfigureAwait(false);
            if (file is not null)
            {
                // Stryker disable once Boolean: continuation scheduling does not change artifact publication.
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
        // Stryker disable once Boolean: continuation scheduling does not change video production.
        string? file = await _recorder!.ConcatAsync(segments, fileName, metadataPath, cancellationToken).ConfigureAwait(false);
        if (file is not null)
        {
            // Stryker disable once Boolean: continuation scheduling does not change artifact publication.
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
            .Where(item => item.End > 0 && item.Start < videoEnd)
            .OrderBy(item => item.Start)
            .ToArray();

        if (ordered.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(';').AppendLine("FFMETADATA1");
        foreach ((TestRecord record, double start, double end) in ordered)
        {
            long startMs = (long)(Math.Max(0, start) * 1000);
            long endMs = (long)(Math.Min(videoEnd, end) * 1000);
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
            // Stryker disable once String: "N" and the default GUID format have the same first eight hexadecimal characters.
            string unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            string metadataPath = Path.Combine(directory, $"chapters_{unique}.txt");
            File.WriteAllText(metadataPath, builder.ToString());
            return metadataPath;
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Failed to write chapter metadata: {ex.Message}");
            return null;
        }
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
}
