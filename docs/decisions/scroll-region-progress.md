# Scroll-region progress: research and decisions

Status: **experimental prototype, opt-in, draft**. Tracking issue:
[#4425](https://github.com/microsoft/testfx/issues/4425). Implementation baseline:
`be3cf477a83f9b94e416b0401163a48580e389aa`.

## Problem and contract

With the interactive (cursor-capable) progress on, Microsoft.Testing.Platform (MTP)
redraws progress in place: it erases the previous frame by moving the cursor up and
clearing lines, writes queued output, then draws the new frame. Tests that write to
`Console.Out` directly do not go through that queue. When such a write lands between
a frame being drawn and the next erase, the next erase moves the cursor up over the
user's text and clears it, or the next frame overwrites it.

Reproduction (TUnit 0.6.43 on MTP 1.4.3, five tests that each `Console.WriteLine`
one line): with progress on, 2 of 5 user lines remained visible; with
`--no-progress` (now `--progress off`), all 5 did. The reply on the issue is
[comment 7127075336](https://github.com/microsoft/testfx/issues/4425#issuecomment-7127075336).

The contract for a fix:

- Every line a test writes to the console must remain visible, in order, in the
  scrollback, including lines written while progress is drawn.
- Progress keeps updating in place on cursor-capable terminals.
- No new lock or interception on the user's console write path: a test writing to
  `Console.Out`, possibly from many threads or from native code, must never block on
  MTP, and MTP must never block on it.
- After the run, including `Environment.Exit` in the middle of a run, the terminal
  must be left as it was found: full-window scrolling and no stale progress rows.
- Non-cursor output (CI, redirected, `--ansi off`/simple ANSI), `--progress off`,
  server mode, `dotnet test` pipe mode, `--list-tests`, and the test-host controller
  are unchanged.

### Producer-consumer boundary

TestFx owns `Microsoft.Testing.Platform` and the terminal reporter source under
`src/MTP/Microsoft.Testing.Platform/OutputDevice/Terminal`. `dotnet/sdk` vendors that
source into the outer `dotnet test` process, which has its own console and its own
`IConsole` implementation. This prototype only changes the test host: the opt-in
environment variable is read by `TerminalOutputDevice`, which the SDK does not vendor.
The vendored files compile unchanged in the SDK only if its `IConsole` implementations
add `WriteAtomically`; porting that, and deciding whether the outer `dotnet test`
console should use a scroll region, is separate SDK work.

## Research and experiments

1. **Intercept `Console.Out` early (rejected).** A probe that called `Console.SetOut`
   before `CreateBuilderAsync` and routed writes through the output device kept 5 of 5
   lines. Four rounds of multi-model review (GPT, Gemini, Sonnet) converged on a
   guarded writer that erases progress before each user write. Every variant needed
   MTP's progress lock and the console's own locks on the same path:
   - on Unix, `Console.WindowWidth` and console stream writes both take
     `lock (Console.Out)` (`ConsolePal.Unix.cs` in dotnet/runtime v10.0.0), so geometry
     reads under MTP's lock invert with a cached user writer;
   - on Windows, write callbacks run under the progress lock, so a callback that
     writes to the console while another thread waits in the guard deadlocks;
   - writers cached before installation, native writes, and partial lines are not
     covered.

   The deadlock risk was judged too high for a default-on change.
2. **Pin progress outside a scroll region (chosen).** DECSTBM (`CSI top;bottom r`)
   restricts scrolling to the rows above the progress. Output written anywhere in the
   region scrolls inside it and can never reach the reserved rows, so the renderer
   never needs to move the cursor over user output and no coordination with user
   writes is needed.
3. **Pending wrap on xterm.js (limitation, executed).** When user output ends exactly
   at the last column without a newline, the terminal is in the "pending wrap" state.
   A frame saves and restores the cursor (DECSC/DECRC). `@xterm/headless` 5.5.0 clamps
   the restored column, so the next character overwrites the last cell instead of
   wrapping. On a 10x6 terminal with region `1;4`: writing `AAAAAAAAAA`, a frame, then
   `B` produced `AAAAAAAAAB` on row 0; without the frame, `B` went to row 1. Windows
   conhost preserves the pending wrap: on a 120-column window the same sequence put
   `B` at the start of row 1 with and without a frame. This affects xterm.js-based
   terminals such as the VS Code terminal and cannot be fixed without knowing the
   user's cursor column, which requires interception.

## Alternatives and decision

| Option | Outcome |
| --- | --- |
| Document `--progress off` / `--ansi off` | Works today; loses in-place progress. Remains the recommendation until this ships. |
| Intercept `Console.Out` and erase before user writes | Rejected: lock inversions with the console's own locks, uncovered writers. |
| Scroll region, opt-in (`MTP_PROGRESS_SCROLL_REGION`) | **Chosen for the prototype.** No new lock on the user write path. |
| Scroll region by default | Deferred until the live terminal matrix and reporter feedback are in. |

Design of the chosen option:

- `MTP_PROGRESS_SCROLL_REGION` accepts `on`, `true`, `enable`, or `1`
  (case-insensitive, trimmed). It only has an effect when the cursor renderer is used.
- `AnsiTerminal` reserves rows at the bottom by scrolling the content up (`ESC D`
  repeated), then sets the region above them. Each frame saves the cursor, disables
  origin mode, draws in the reserved rows, and restores the cursor. Releasing resets
  the region (`CSI r`) and clears the reserved rows. The reserved height follows the
  frame and is recomputed on resize; a window too small for the frame falls back to
  not pinning.
- Each batch (`StartUpdate`/`StopUpdate`) is written with one `IConsole.WriteAtomically`
  call, so another writer cannot land between the cursor save and restore. This is
  best effort: the OS can split a write (Unix partial writes).
- `CursorProgressRenderer` does not erase progress before writes in scroll-region mode.
- A `ProcessExit` handler releases the rows when the process exits during a run. It
  takes no lock. The physical state (rows and height) is published only after a batch
  is written. The handler marks the process as exiting, waits up to 250 ms for another
  thread's in-flight batch, then claims the state with `Interlocked.Exchange`. The
  batch's `StopUpdate` re-checks the exiting flag and releases rows it wrote itself.
  Exactly one path writes each release. On the batching thread, the handler does not
  wait. The 250 ms bound keeps .NET Framework's total `ProcessExit` budget (about 2 s)
  available for other handlers.
- Default-mode change: once teardown has erased progress, later writes no longer draw
  it again (`_progressErased` gate). The teardown erase now runs under the progress
  terminal's existing lock. Without the gate, a late write would redraw progress below
  the final output; in scroll-region mode it would also reserve rows that are never
  released.

Accepted residual risks for the prototype: SIGKILL or a crash leaves the margins set
(`reset`/`tput reset` restores them); if the exit wait times out and the owning
thread never finishes its batch, a stale frame can remain; there is one DECSC save
slot, so user code that saves the cursor itself, positions the cursor absolutely, or
switches to the alternate screen can be disturbed; terminals without DECSTBM are not
detected; many active workers are truncated to the reserved rows; a resize between
measuring and writing a frame can draw one frame at stale coordinates.

## Validation and demonstration

Unit tests (`Microsoft.Testing.Platform.UnitTests`, net10.0):

```powershell
dotnet run --project test\UnitTests\Microsoft.Testing.Platform.UnitTests -f net10.0 --no-build -- --filter "FullyQualifiedName~ProgressReportingConfigurationTests|FullyQualifiedName~Terminal"
```

Result: 558 total, 558 succeeded. The terminal-reporter contract tests passed 16 of 16
on net10.0 and net11.0. `Microsoft.Testing.Platform.csproj` builds for all target
frameworks with 0 warnings.

### Revision-bound regression sensitivity

Each mutation was applied to the production source, rebuilt, and run against the
scroll-region tests. Every one turned tests red, and the source was restored.

| Mutation | Failing tests |
| --- | --- |
| M1: exit handler does nothing | `ProcessExitReleasesPinnedRowsAtomically`, `FrameThatReservesRowsWhileProcessExitsReleasesThemAgain` |
| M2: batch does not release again after exit | `FrameThatReservesRowsWhileProcessExitsReleasesThemAgain` |
| M3: remove the `_progressErased` gate | `TestProgressStateAwareTerminal_ScrollRegion_WritesAfterProgressStoppedDoNotPinProgressAgain` |
| M4: env var accepts only `1`/`true` | `IsScrollRegionEnabled_AcceptsOnlyOnValues` (`" TRUE "`, `enable`, `on`) |
| M5: exit handler does not wait for another thread's batch | `ProcessExitWaitsForBatchOfAnotherThread` |
| M6: claim the state before the release is written | `ProcessExitReleasesRowsWhoseReleaseIsNotWritten` |

### Real-terminal demonstration

A probe test host (3 workers writing lines directly to `Console.Out` while progress
renders, then 45 lines after the run) was run with the repository-built
`Microsoft.Testing.Platform.dll` (hash-checked against `artifacts`) in a fresh
`conhost.exe`. The screen buffer was read back with `ReadConsoleOutputCharacterW`:

| Mode | Expected lines | Missing | Progress residue rows |
| --- | --- | --- | --- |
| Classic progress | 106 | 27 | 3 |
| Scroll region | 106 | 0 | 0 |
| Scroll region, `Environment.Exit(7)` mid-run | 106 | 0 | 0 |

A WSL Debian pty (120x30, `TERM=xterm-256color`) recorded and replayed through
`@xterm/headless` 5.5.0 showed the same pattern before the latest review fixes:
classic mode lost 28 lines, scroll-region mode lost none. It was not re-run after
those fixes.

### Review

Three review rounds were run. Round 1 found seven issues and a batch race, all fixed
with tests (atomic batch writes, re-release after exit, the teardown gate, resize,
small windows, env var parsing). Round 2 found the xterm.js pending wrap and that
teardown retired exit cleanup before the release was written; the latter is fixed
(state is published only after the write). Round 3 found no significant issues.

## Open questions

Before this can leave draft:

- Live end-to-end on Windows Terminal, conhost, the VS Code terminal, macOS Terminal
  and a Linux tty: concurrent console writes, Ctrl+C once and twice, resize during a
  run, the shell prompt after exit, `--output detailed`, multiple test assemblies, and
  hot reload. Assert terminal cells, not only the captured byte stream.
- Decide whether the xterm.js pending-wrap overwrite is acceptable.
- Handle or bound Unix partial writes for `WriteAtomically`.
- Add an acceptance test asserting that the final output resets the region (`CSI r`).
- Decide between the environment variable and a `--progress` value; update
  `--help`/`--info` expectations if an option is added.
- Port `IConsole.WriteAtomically` to the vendored copy in dotnet/sdk.
- Gather feedback from the #4425 reporters.
- Decide whether the scroll region becomes the default.
