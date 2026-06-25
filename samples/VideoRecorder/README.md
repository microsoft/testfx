# Video Recorder extension (sample)

A Microsoft.Testing.Platform (MTP) extension that **records the screen** while tests run.
Recording is **automatic and declarative** — enable it with `--capture-video` and the extension
captures the screen for you (one video per test by default, or one chaptered video per session).
Recording is performed by an external **ffmpeg** process (the only engine that is cross-platform
*and* covers screen capture). Each kept video is attached to the test session as a file artifact.

The screen is recorded **continuously** for the whole run as a rolling sequence of short segments,
and the per-test clips (or the chaptered session video) are cut from those segments afterwards using
each test's timing. This avoids the start/stop race a per-test recorder has — MTP delivers data to
consumers asynchronously, so by the time a test's `InProgress` message is observed the test may
already be finishing — and it lets recorded tests run **in parallel**.

This is a local sample (not a shipping package) wired into the `Playground` sample.
See [DESIGN.md](DESIGN.md) for the engine choice, alternatives considered (Playwright,
Windows.Graphics.Capture, Game Bar), the segmented architecture, and the licensing rationale.

![A frame from a recording produced by the extension (window-capture mode targeting a test-pattern window).](docs/demo-recording.png)

## How to play with it

1. Make `ffmpeg` available — either install it and add it to `PATH`, or pass an explicit path
   (see options below). On Windows the recorder uses `gdigrab`, on macOS `avfoundation`, on
   Linux `x11grab`.
2. Build and run the `Playground` sample **with `--capture-video`** (recording is opt-in). It
   contains demo tests (`VideoRecorderDemoTests`) that simulate a couple of seconds of UI work,
   including one that fails on purpose so you can see on-failure retention and the `[Failed]`
   chapter.
3. Find the videos under `<TestResults>/VideoRecordings/` (also reported as session artifacts at
   the end of the run). With the default per-test granularity you get **one video per test**, named
   after the test. With the default `on-failure` retention, a passing test's video is discarded —
   use `--capture-video always` to keep it.

## How it works

Recording is **automatic and declarative** — there's no API to call from test code. Enable it with
`--capture-video` and the extension records the screen for you:

- **per test** (default): the run is recorded continuously and each test's clip is cut from that
  recording using the test's timing, producing one video per test named after the test;
- **per session** (`--capture-video-granularity session`): the whole run is stitched into a single
  video with **one chapter bookmark per test** (titled `<test> [Outcome]`) so you can jump straight
  to a failing test.

Because recording is continuous, tests can run in parallel; each clip is sliced by time, not by a
live start/stop. On long runs you can bound disk usage with `--capture-video-max-duration` (a
rolling buffer that prunes old footage no running test needs).

Configure it on the command line and/or programmatically at registration time (see below).

## Registering the extension

```csharp
testApplicationBuilder.AddVideoRecorderProvider(options =>
{
    options.FfmpegPath = @"C:\tools\ffmpeg\bin\ffmpeg.exe"; // optional; defaults to PATH lookup
    options.FrameRate = 15;
    options.Format = VideoRecorderFormat.Mp4H264;           // or WebMVp9 (royalty-free)
    options.PersistMode = VideoRecorderPersistenceMode.OnFailure; // or Always
    options.Granularity = VideoCaptureGranularity.PerTest;        // PerTest (default) / PerSession
    options.Source = VideoCaptureSource.Screen;                  // or VideoCaptureSource.Window (capture only this process's window; Windows)
    options.SegmentLengthSeconds = 4;                           // rolling segment length
    options.IncludeChapters = true;                            // chapter bookmarks in the session video
    // options.MaxRetainedDuration = TimeSpan.FromMinutes(10); // rolling buffer to bound disk on long runs
    // options.OutputDirectory = ...;                       // defaults to <TestResults>/VideoRecordings
    // options.InputArgumentsOverride = ...;                // capture a window/region instead of the full screen
    // options.ExtraRecorderArguments = "-vf scale=1280:-1"; // extra args for the underlying recorder (ffmpeg)
});
```

Registration only makes the option available — recording is still **opt-in** and happens only
when the run is started with `--capture-video`.

## Command-line options

Recording is enabled per-run with a single flag, mirroring the platform's `--report-<kind>`
grouping. The `--capture-<kind>-…` prefix leaves room for a future capture kind — for example
screenshots — under the same `--capture-` umbrella (e.g. `--capture-screenshot`).

| Option | Values | Description |
| --- | --- | --- |
| `--capture-video` | *(none)*, `on-failure`, `always` | Enables screen recording for the run. The optional argument controls retention: `on-failure` (**default** — keep only failing tests' footage) or `always`. |
| `--capture-video-granularity` | `test` (default), `session` | How recordings are split: **one video per test** (default — named after the test) or one chaptered video for the whole run. |
| `--capture-video-source` | `screen` (default), `window` | What to capture: the full screen, or only the current process window (Windows; falls back to full screen elsewhere). Requires `--capture-video`. |
| `--capture-video-max-duration` | seconds | Limit retained footage to roughly the last N seconds (a rolling buffer) to bound disk usage on long runs. Footage no running test needs is pruned. Requires `--capture-video`. |
| `--capture-video-chapters` | `on` (default), `off` | Whether the per-session video includes one chapter bookmark per test. Only applies with `--capture-video-granularity session`. Requires `--capture-video`. |
| `--capture-video-args` | any string | Extra arguments passed to the underlying recorder (currently ffmpeg), as output/encoding options. Requires `--capture-video`. |

Examples:

```bash
# Record, keeping the video only if a test fails (default). Each test gets its own video.
yourtests --capture-video

# Record the whole run into a single chaptered video instead of one per test
yourtests --capture-video always --capture-video-granularity session

# Record and always keep the video
yourtests --capture-video always

# Record only the current process window (e.g. your terminal) instead of the whole screen
yourtests --capture-video always --capture-video-source window

# Long run: keep only the last 10 minutes of footage to bound disk usage
yourtests --capture-video always --capture-video-max-duration 600

# Record and pass extra recorder args. NOTE: because the value starts with '-', use the '=' (or ':')
# delimiter form so MTP binds it to the option instead of parsing it as a new option.
yourtests --capture-video --capture-video-args="-vf scale=1280:-1"
```

> **Window capture** records the screen rectangle of a window resolved in this order: the process
> **main window** (a GUI app under test owns it), then the **foreground window** (the terminal you
> launched from — this is what lets it capture **Windows Terminal**, whose window isn't owned by
> the test process), then the **console window** (classic conhost). If none is a usable visible
> window (e.g. a headless/CI run), it **falls back to full-screen capture** with a logged note.
> Because the foreground window is read when recording starts, keep your terminal focused as the
> run begins.
>
> Screen capture also requires an **accessible interactive desktop**. On a locked screen, a
> disconnected RDP session, or a session-0 service, `gdigrab` fails with "access denied" and no
> video is produced — the recorder logs this and the run continues.
>
> Retention is decided per recording: in `on-failure` mode a **per-test** video is kept only if
> *that* test failed; a **per-session** video is kept if *any* test in the run failed.
>
> **Per-test granularity** (the default) records each test into its own video automatically — no
> code in the test body. Because recording is continuous and each clip is cut by time, tests can
> run **in parallel**; each clip is segment-aligned (it may include a fraction of a second of
> padding around the test). Use `--capture-video-granularity session` for a single chaptered video
> of the whole run.

## A note on ffmpeg licensing / codecs

- `Mp4H264` uses `libx264`, which is GPL in most ffmpeg builds and H.264 carries patent fees —
  fine when you bring your own ffmpeg on `PATH`, but not something to bundle/redistribute.
- `WebMVp9` uses `libvpx-vp9`, which is royalty-free and LGPL/BSD-clean — the safe choice if an
  ffmpeg binary is ever shipped alongside the extension.
