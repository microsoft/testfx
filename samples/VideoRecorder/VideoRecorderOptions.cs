// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// The output video format produced by the recorder.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum VideoRecorderFormat
{
    /// <summary>
    /// H.264 video in an MP4 container (<c>libx264</c>). Broadest playability, but the H.264
    /// encoder shipped with most ffmpeg builds is GPL-licensed and H.264 carries patent fees,
    /// so this is best suited to a "bring your own ffmpeg on PATH" scenario.
    /// </summary>
    Mp4H264,

    /// <summary>
    /// VP9 video in a WebM container (<c>libvpx-vp9</c>). Royalty-free and LGPL/BSD-clean,
    /// which makes it the safe choice when an ffmpeg binary is bundled/redistributed.
    /// </summary>
    WebMVp9,
}

/// <summary>
/// Controls when a recorded video is persisted (kept and attached to the test session).
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum VideoRecorderPersistenceMode
{
    /// <summary>
    /// Always keep the recorded videos.
    /// </summary>
    Always,

    /// <summary>
    /// Record every test, but only keep the videos when at least one test failed during the
    /// session. When all tests pass, the recordings are deleted at the end of the run.
    /// </summary>
    OnFailure,
}

/// <summary>
/// Controls how recordings are split across a test run.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum VideoCaptureGranularity
{
    /// <summary>
    /// Automatically record each test individually, producing one video per test (named after the
    /// test). This is what most people expect. Because a screen recorder is a single, serial
    /// resource, tests being recorded should run serially (mark them <c>[DoNotParallelize]</c>);
    /// with parallel execution only one overlapping test is recorded.
    /// </summary>
    PerTest,

    /// <summary>
    /// Record the whole run as a single video (from session start to session end).
    /// </summary>
    PerSession,

    /// <summary>
    /// Don't record automatically; tests drive recording explicitly through
    /// <see cref="VideoRecorder.Current"/> (<see cref="IVideoRecorder.Start"/> /
    /// <see cref="IVideoRecorder.StopAsync"/>).
    /// </summary>
    Manual,
}

/// <summary>
/// What the recorder captures.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum VideoCaptureSource
{
    /// <summary>
    /// The full screen / desktop.
    /// </summary>
    Screen,

    /// <summary>
    /// Only the current process window. Supported on Windows (via gdigrab <c>title=</c>);
    /// falls back to full-screen capture on other platforms.
    /// </summary>
    Window,
}

/// <summary>
/// Options controlling how <see cref="IVideoRecorder"/> captures and encodes video.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class VideoRecorderOptions
{
    /// <summary>
    /// Gets or sets the full path to the ffmpeg executable. When <see langword="null"/> (the
    /// default) the recorder looks ffmpeg up on the <c>PATH</c> environment variable.
    /// </summary>
    public string? FfmpegPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where video files are written. When <see langword="null"/>
    /// (the default) the test result directory is used.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets the capture frame rate in frames per second. Defaults to <c>15</c>, which
    /// keeps CPU overhead low while a test runs.
    /// </summary>
    public int FrameRate { get; set; } = 15;

    /// <summary>
    /// Gets or sets the output video format. Defaults to <see cref="VideoRecorderFormat.Mp4H264"/>.
    /// </summary>
    public VideoRecorderFormat Format { get; set; } = VideoRecorderFormat.Mp4H264;

    /// <summary>
    /// Gets or sets how recordings are split across a run: one video per test (default), one for
    /// the whole session, or manual (tests drive recording themselves). Can be overridden on the
    /// command line with <c>--capture-video-granularity</c>.
    /// </summary>
    public VideoCaptureGranularity Granularity { get; set; } = VideoCaptureGranularity.PerTest;

    /// <summary>
    /// Gets or sets when recorded videos are persisted. Defaults to
    /// <see cref="VideoRecorderPersistenceMode.OnFailure"/>. Can be overridden on the command line
    /// by the argument of <c>--capture-video</c> (e.g. <c>--capture-video always</c>).
    /// </summary>
    public VideoRecorderPersistenceMode PersistMode { get; set; } = VideoRecorderPersistenceMode.OnFailure;

    /// <summary>
    /// Gets or sets what is captured: the full screen (default) or only the current process
    /// window. Window capture is supported on Windows (via gdigrab <c>title=</c>) and falls back
    /// to full-screen capture elsewhere. Can be set on the command line with
    /// <c>--capture-video-source</c>.
    /// </summary>
    public VideoCaptureSource Source { get; set; } = VideoCaptureSource.Screen;

    /// <summary>
    /// Gets or sets extra arguments passed to the underlying recorder. With the default ffmpeg
    /// recorder these are appended to the command line just before the output file (i.e. as
    /// output/encoding options, for example <c>-vf scale=1280:-1</c> or <c>-b:v 2M</c>). Can be
    /// overridden on the command line with <c>--capture-video-args</c>. To fully control the
    /// capture input instead, use <see cref="InputArgumentsOverride"/>.
    /// </summary>
    public string? ExtraRecorderArguments { get; set; }

    /// <summary>
    /// Gets or sets ffmpeg <em>input</em> arguments overriding the default per-OS screen capture
    /// (for example to capture a single window or a region). When set, this replaces the whole
    /// <c>-f &lt;backend&gt; -framerate N -i &lt;target&gt;</c> portion of the command line.
    /// </summary>
    public string? InputArgumentsOverride { get; set; }

    /// <summary>
    /// Gets or sets the capture size (<c>"WIDTHxHEIGHT"</c>) used by the Linux <c>x11grab</c>
    /// backend, which requires an explicit size. Ignored on Windows and macOS. Defaults to
    /// <c>"1920x1080"</c>.
    /// </summary>
    public string X11CaptureSize { get; set; } = "1920x1080";
}
