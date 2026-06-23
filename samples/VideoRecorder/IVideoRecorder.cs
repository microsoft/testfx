// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// A test-facing service that lets a test (or another extension) start and stop video
/// recording of the screen while the test runs.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IVideoRecorder
{
    /// <summary>
    /// Gets a value indicating whether a recording backend (ffmpeg) was found and recording
    /// is possible. When <see langword="false"/>, <see cref="Start"/> is a no-op and
    /// <see cref="StopAsync"/> returns <see langword="null"/>.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether a recording is currently in progress.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts recording the screen. The produced file is attached to the test session when
    /// the recording is stopped (or when the session finishes if it is still running).
    /// </summary>
    /// <param name="name">
    /// An optional friendly name used to build the output file name (for example the test name).
    /// </param>
    /// <remarks>
    /// Recording is a single, session-global, serial resource: only one recording can be active
    /// at a time across the whole test process. This call is best-effort and never throws — if no
    /// recorder is available, ffmpeg is missing, or a recording is already in progress, it is a
    /// no-op. Because of the single-recording constraint, tests that record should not run in
    /// parallel with one another (mark them <c>[DoNotParallelize]</c>).
    /// </remarks>
    void Start(string? name = null);

    /// <summary>
    /// Stops the current recording and finalizes the video file.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the wait for ffmpeg to finalize the file.</param>
    /// <returns>
    /// The full path of the produced video file, or <see langword="null"/> if nothing was being
    /// recorded or no recording backend is available.
    /// </returns>
    Task<string?> StopAsync(CancellationToken cancellationToken = default);
}
