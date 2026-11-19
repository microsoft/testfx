// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

internal interface IVideoRecorder
{
    bool IsAvailable { get; }

    string? FfmpegPath { get; }

    DateTimeOffset? RecordingStartUtc { get; }

    string? SegmentDirectory { get; }

    string SegmentExtension { get; }

    void Start();

    Task StopAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<VideoSegment> ReadSegments();

    string DescribeLastFfmpegError();

    Task<string?> ConcatAsync(
        IReadOnlyList<VideoSegment> segments,
        string outputFileName,
        string? ffmetadataPath,
        CancellationToken cancellationToken);
}
