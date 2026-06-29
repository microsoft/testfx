// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
