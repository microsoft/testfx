// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VideoRecorder.Resources;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal sealed partial class VideoRecorderSessionHandler
{
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

        double nowOffset = (_clock.UtcNow - recordingStart).TotalSeconds;
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
            FfmpegVideoRecorder.PruneSegments(prunable);
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
}
