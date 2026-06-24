# Video Recorder extension — design notes

Status: **sample / experimental** (`[Experimental("TPEXP")]`, `IsPackable=false`).
This document records the design decisions and the alternatives we considered, so we can
"complexify later" deliberately rather than rediscover the trade-offs.

## Goal

Expose a Microsoft.Testing.Platform (MTP) **service** that lets a test (or another extension)
start/stop **video recording** during a test run, and attach the produced video to the test
session as an artifact. Primary scenario: **capture the OS screen / a window** while a UI test
runs (WinForms/WPF/Avalonia/console/browser).

## Decision (ADR-style)

> **We drive an external `ffmpeg` process, require it on `PATH`, and default to H.264/MP4.**
> Start simple; add native backends and/or bundling only if a concrete need appears.

- **Engine: `ffmpeg` as a child process**, behind an internal recorder abstraction.
  - It is the only option that is **cross-platform** *and* covers **OS screen capture**
    (`gdigrab` on Windows, `x11grab` on Linux, `avfoundation` on macOS).
  - Graceful stop by sending `q` to ffmpeg's stdin (killing risks a corrupt/unplayable file).
- **Acquisition: require `ffmpeg` on `PATH`** (or an explicit `FfmpegPath`). We do **not** bundle a
  binary. This keeps the extension dependency-free and sidesteps all redistribution/licensing
  obligations (see *Licensing*).
- **Default format: H.264 / MP4** (`libx264`), best for inline preview in CI dashboards
  (e.g. Azure DevOps test attachments). A royalty-free **VP9 / WebM** option exists for any future
  bundled scenario.
- **Recording is automatic/declarative** (no public test-facing API). Granularity selects
  per-test (default) or per-session capture; the extension drives ffmpeg itself. This keeps the
  public surface minimal (CLI options + a registration callback) — like `--crashdump`/`--hangdump`.
- **Extensibility:** the internal recorder is swappable, so a native backend
  (Windows.Graphics.Capture) or a UI-tool pass-through (Playwright) can be added later without
  changing the option model.

## Alternatives considered (and why deferred)

| Option | Captures | Cross-platform | Avoids ffmpeg | Licensing on us | Verdict |
| --- | --- | --- | --- | --- | --- |
| **ffmpeg child process** (chosen) | OS screen/window | ✅ | ❌ | none (PATH) | ✅ baseline |
| **Playwright-style** (browser screencast → ffmpeg) | browser page only | ✅ (headless) | ❌ (bundles ffmpeg) | n/a here | only browser tests |
| **Windows.Graphics.Capture + Media Foundation** | window/monitor | ❌ Windows-only | ✅ (OS codecs) | clean | future Windows backend |
| **Game Bar / `Windows.Media.AppRecording`** | own app window | ❌ | ✅ | clean | ✗ own-window + packaging only |
| **Bundle a minimal LGPL/VP8 ffmpeg** | OS screen/window | ✅ | ❌ | LGPL notice + source offer | defer; needs legal sign-off |

Notes:

- **Playwright** does **not** record the OS screen. It captures the **browser page** via the
  browser's CDP screencast, then encodes the frames with a **bundled minimal LGPL ffmpeg** to
  **VP8/WebM** (royalty-free). It works headlessly precisely because the browser is the *frame
  source*. This is the "encode caller-provided frames" pattern — not applicable to arbitrary
  desktop UI tests, which have no frame stream. (It does, however, validate two of our choices:
  ffmpeg is the right engine, and VP8/VP9 WebM is the licensing-safe codec when bundling.)
  Borrowable tuning: `-threads 1 -deadline realtime`, `-vf pad/crop` to normalize size, and the
  stdin Matroska-with-timestamps pipe — relevant only if we ever add a frame-source mode.
- **Windows.Graphics.Capture (WGC)** is what Game Bar/OBS use under the hood. It captures a window
  (HWND) or monitor as Direct3D11 textures (Win10 1803+, `Direct3D11CaptureFramePool.CreateFreeThreaded`
  works from a console app); encode via Media Foundation `IMFSinkWriter` (H.264 MP4, hardware
  accelerated). **Pros vs ffmpeg:** no external binary, no GPL/patent exposure (OS codecs),
  GPU-accelerated, **DPI-correct by construction**, captures occluded/moving windows. **Cons:**
  **Windows-only**, heavy D3D11 + WGC + Media Foundation interop, Win10 1803+ floor. A library such
  as `ScreenRecorderLib` wraps this if we don't want to hand-roll it.
- **Game Bar / `AppRecordingManager`** still ships in Windows 11 but only records *your own app's*
  window and requires a packaged/UWP app — no public API to record arbitrary windows. Not a fit for
  a console test runner.

## Licensing analysis (summary)

The licensing risk is **not "ffmpeg"** — it is the **GPL/patented codecs**:

- ffmpeg **core is LGPL**. Building with **`libx264`/`libx265` makes the whole binary GPL**; H.264/HEVC
  also carry **MPEG-LA patent** royalties regardless of license.
- **Requiring ffmpeg on PATH** (our default) means we **distribute nothing** → no obligations; the
  user's ffmpeg can be H.264/MP4.
- **If we ever bundle:** ship a **minimal LGPL** build (`--disable-everything --enable-libvpx
  --enable-encoder=libvpx_vp8 --enable-muxer=webm …`) and default to **VP8/VP9 WebM** (royalty-free).
  LGPL is **weak copyleft** and does **not** affect our MIT source; because we invoke ffmpeg as a
  **separate executable** (replaceable), the LGPL "user can replace the component" requirement is
  trivially met — we'd just carry the LGPL notice + a source offer for that binary. (Apache-2.0
  Playwright bundling an LGPL ffmpeg is precedent.) **This path needs OSS/legal sign-off and is
  explicitly out of scope for now.**

(Not legal advice; final compliance goes through the standard OSS review.)

## Architecture

- The internal `FfmpegVideoRecorder` — the current backend. Per-OS capture input, graceful stop,
  best-effort (never throws), prunes empty output files, warns on capture failure.
- `VideoRecorderSessionHandler` — MTP wiring: drives recording per the **granularity** (per-test
  by default, or per-session), tracks failures, persists/discards/attaches videos, applies CLI
  overrides.
- `VideoRecorderCommandLineProvider` / `AddVideoRecorderProvider` — opt-in CLI + registration.

### Capture-source resolution (Windows window mode)

`--capture-video-source window` records the rectangle of a window resolved as **process main window
→ foreground window → console window**, read under a **Per-Monitor-V2** DPI context (so the rect is in
physical pixels matching gdigrab) and clamped to the screen. Falls back to full screen (with a logged
note) when no usable window exists — e.g. headless/CI, or Windows Terminal whose visible window is
owned by the terminal (ConPTY), not the test process.

## Known limitations

- Screen capture needs an **accessible interactive desktop** (locked/disconnected/session-0 → ffmpeg
  "access denied", no video; logged and the run continues).
- Window capture is best with a classic console window or a GUI app under test; Windows Terminal and
  headless runs fall back to full screen.
- Single, **session-global, serial** recorder. **Per-test** granularity (the default) records each
  test into its own video and so expects **serial** execution (mark recording tests
  `[DoNotParallelize]`); with parallel execution only one overlapping test is captured. Use
  `session` for one video per run.
- macOS `avfoundation` device index may need overriding via `InputArgumentsOverride`.

## Future work ("complexify later")

1. **Native Windows backend** via Windows.Graphics.Capture + Media Foundation (no binary, no GPL,
   DPI-correct, GPU-accelerated) selected automatically on Windows, ffmpeg elsewhere.
2. **Optional bundled minimal LGPL/VP8 ffmpeg** for a zero-prerequisite experience — pending legal.
3. **Frame-source mode** (Playwright-style) for callers that can emit frames/screenshots.
4. **Audio** capture/mux (currently video-only).
5. **Per-test-node attachment**: attach each per-test video to its test node (today per-test videos
   are attached as session artifacts named after the test).
6. **Programmatic API** (a test-facing `Start`/`Stop` service) — intentionally omitted for now to
   keep the public surface minimal; add back only if a concrete framework-driven need appears.
