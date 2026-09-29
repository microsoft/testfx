# Microsoft.Testing.Extensions.VideoRecorder

Microsoft.Testing.Extensions.VideoRecorder is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that **records the screen** while your tests run and attaches the captured videos to the test session as artifacts.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.VideoRecorder` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.VideoRecorder
```

## About

> **⚠️ Experimental:** This extension is currently experimental. The API, CLI options and on-disk format may change in future releases without notice.

This package extends Microsoft.Testing.Platform with continuous screen recording. The default and cross-platform backend drives an external **ffmpeg** process, records a rolling sequence of short segments, and cuts per-test clips (or a chaptered session video) from those segments afterwards using each test's timing — so recorded tests can run in parallel and there is no per-test start/stop race.

On Windows 10 build 19041 or later, a per-monitor DPI-aware `net10.0-windows10.0.19041.0` test application can use the published WinAppCLI recording package for a native Media Foundation H.264 session recording. The proof-of-concept native backend is selected only for full-desktop, MP4, unbounded, chapterless per-session recording. Every other configuration keeps the ffmpeg backend so per-test slicing, chapters, rolling-buffer pruning, window capture, custom recorder arguments, and VP9/WebM retain their existing behavior.

## Prerequisites

`ffmpeg` must be available — either on the `PATH` or via an explicit path (`VideoRecorderOptions.FfmpegPath`) — unless the test application and options meet the native Windows constraints above. On Windows the ffmpeg recorder uses `gdigrab`, on macOS `avfoundation`, on Linux `x11grab`.

## Usage

Recording is **opt-in** per run. Enable it with `--capture-video`:

```dotnetcli
# Record, keeping the video only if a test fails (default). One video per test.
yourtests --capture-video

# Record the whole run into a single chaptered video instead of one per test
yourtests --capture-video always --capture-video-granularity session

# Native Windows POC on a compatible net10.0-windows test application
yourtests --capture-video always --capture-video-granularity session --capture-video-chapters off

# Long run: keep only the last 10 minutes of footage to bound disk usage
yourtests --capture-video always --capture-video-max-duration 600
```

### Command-line options

| Option | Values | Description |
| --- | --- | --- |
| `--capture-video` | *(none)*, `on-failure`, `always` | Enable recording. Retention: `on-failure` (default — keep only failing tests' footage) or `always`. |
| `--capture-video-granularity` | `test` (default), `session` | One video per test, or one chaptered video for the whole run. |
| `--capture-video-source` | `screen` (default), `window` | Full screen, or just the current process window (Windows; falls back to full screen elsewhere). |
| `--capture-video-max-duration` | seconds | Rolling buffer: keep ~the last N seconds to bound disk usage on long runs. |
| `--capture-video-chapters` | `on` (default), `off` | Chapter bookmarks in the per-session video. |
| `--capture-video-args` | any string | Extra arguments passed to the underlying recorder (ffmpeg), as output/encoding options. |

### Programmatic configuration

```csharp
using Microsoft.Testing.Extensions;

testApplicationBuilder.AddVideoRecorderProvider(options =>
{
    options.FfmpegPath = @"C:\tools\ffmpeg\bin\ffmpeg.exe"; // optional; defaults to PATH lookup
    options.Format = VideoRecorderFormat.Mp4H264;           // or WebMVp9 (royalty-free)
    options.Granularity = VideoCaptureGranularity.PerTest;  // PerTest (default) / PerSession
    options.PersistMode = VideoRecorderPersistenceMode.OnFailure; // or Always
});
```

## Licensing note

The native Windows backend uses the operating system's Media Foundation H.264 encoder through the MIT-licensed WinAppCLI recording package. The ffmpeg backend's default `Mp4H264` format uses `libx264`, which is GPL in most ffmpeg builds and H.264 carries patent fees — fine when you bring your own ffmpeg on `PATH`. A royalty-free `WebMVp9` format is provided for any scenario where an ffmpeg binary is bundled/redistributed.
