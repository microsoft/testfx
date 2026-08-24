# RFC 021 - Per-test temporary directory (`TestContext.TestTempDirectory`)

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation (included in this change set, behind the proposed API)
- [ ] Shipped

## Summary

Add a new, **additive** `TestContext` property, `TestTempDirectory`, that returns a filesystem
directory **unique to the currently executing test**, created lazily on first access. Its contents
are deleted automatically when the test passes and retained when the test fails, with an
environment-variable escape hatch to retain everything for debugging.

This closes a genuine gap: every other major test ecosystem provides a per-test temporary
directory, and no .NET test framework does. MSTest itself used to — MSTest V1 gave every test a
unique results directory, and V2 removed it. This RFC brings the capability back as a dedicated,
clearly-scoped property rather than by repurposing any existing (and already confusing) directory
property.

## Motivation

Tests that touch the filesystem need somewhere private to write. Today MSTest users have two bad
options:

1. **Hardcode a shared path** (`Path.Combine(Path.GetTempPath(), "my-test-data")`). Under parallel
   execution multiple tests then race on the same files, producing intermittent
   `IOException: file is being used by another process` failures. The usual "fix" is to disable
   parallelism with `[DoNotParallelize]`, trading correctness for speed.
2. **Hand-roll a helper** that combines `Path.GetTempPath()`, a `Guid`, creation, and best-effort
   recursive cleanup behind `IDisposable`. This boilerplate is copy-pasted across the ecosystem —
   including in this very repository, at
   `test/Utilities/Microsoft.Testing.TestInfrastructure/TempDirectory.cs`. That helper already
   embodies everything this feature needs: unique creation, best-effort recursive cleanup, and an
   environment-variable switch (`Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup`) to
   keep contents for debugging.

A framework-provided per-test directory removes the sharing entirely: each test owns its own path,
so there is nothing to coordinate and nothing to race on.

### Cross-ecosystem survey (evidence)

Per-test temporary directories are table stakes everywhere except .NET:

| Ecosystem     | API                                         | Auto-cleanup                   |
| ------------- | ------------------------------------------- | ------------------------------ |
| Go 1.15+      | `t.TempDir()`                               | yes                            |
| pytest        | `tmp_path` fixture                          | yes (keeps last 3 runs)        |
| JUnit 5       | `@TempDir` (+ `TempDirFactory` SPI, `CleanupMode`) | yes                     |
| Rust          | `tempfile::TempDir` (community crate, not the built-in test harness) | yes (RAII)     |
| Playwright    | `testInfo.outputDir` / `outputPath()`       | yes                            |
| **MSTest / xUnit / NUnit / TUnit** | **none**               | —                              |

The consistent shape across ecosystems is: a directory *unique per test*, *created on demand*, and
*cleaned up automatically*, with an escape hatch to keep artifacts for investigation (pytest's
retention of the last N runs, JUnit's `CleanupMode.NEVER`).

### Repository history: this feature already existed and was removed

MSTest **V1 gave every test a unique `TestResultsDirectory`** of the form `In\<guid>\<machine>`.
MSTest **V2 collapsed that to a single shared `In\` directory**, removing per-test uniqueness. The
regression was reported as [testfx#502](https://github.com/microsoft/testfx/issues/502) and closed
as won't-fix on low upvotes. The reporter's justification is exactly the use case this RFC serves:

> A lot of our tests rely on `TestContext.TestResultsDirectory` being unique to provide test
> isolation. And because most of our tests run in parallel, if `TestResultsDirectory` is not
> unique, we have a lot of race conditions in terms of file access issues.

In hindsight, that was a request for this feature, and its removal is the strongest available
evidence of demand. Rather than silently changing `TestResultsDirectory` back to being per-test
(which would be a breaking change for everyone who now depends on its shared semantics — see the
Compatibility section), we add a **new** property with the per-test guarantee.

### Why the BCL will not hand us this

- `Directory.CreateTempSubdirectory(prefix)` shipped in **.NET 7**, so the low-level unique-creation
  primitive exists on modern TFMs (but not on `net462`/`netstandard2.0`, which MSTest still ships).
- A self-cleaning `TemporaryDirectory` type has been requested in
  [`dotnet/runtime#2048`](https://github.com/dotnet/runtime/issues/2048) since 2020 and is still
  open, so lifetime management is not coming from the BCL. The test framework is the right layer to
  own it, because only the framework knows the test boundary and outcome.

## Goals

1. A `TestContext` property returning a directory unique to the currently executing test.
2. Created **lazily** — tests that never touch it pay nothing (no create/delete per test).
3. Correct under all parallelization settings, including many `[DataRow]`/`[DynamicData]` cases of
   the same method executing concurrently.
4. Automatic cleanup that is useful by default (retain a *failed* test's artifacts) and never throws.
   Cleanup runs synchronously when the per-test `TestContext` is disposed — i.e. *after* the test
   has finished — so it neither interrupts nor extends the test itself, and it is best-effort so an
   aborted or timed-out test is never blocked or failed by it.

## Non-goals

- **Do not** redirect `Environment.CurrentDirectory` (or call `Directory.SetCurrentDirectory`) to
  the per-test directory. The current working directory is **process-global**; mutating it under
  parallel execution corrupts every other concurrently running test. Go makes this explicit:
  `t.Chdir()` **panics** if the test is marked parallel. `TestTempDirectory` is a *scratch path you
  pass to your code*, not an ambient CWD. A follow-up analyzer that flags
  `Environment.CurrentDirectory` / `Directory.SetCurrentDirectory` in an assembly with
  parallelization enabled is worth considering as a **separate** proposal, not part of this work.
- **No class-level or assembly-level shared temp directory** (the equivalent of JUnit's `@TempDir`
  on a `static` field). That is a different lifetime and a different sharing story; noted as
  possible future work.
- **No configurable retention of the last N runs** (pytest keeps 3). Evaluated and rejected for the
  first iteration; see design question 4.

## Design

`TestContext` gains:

```csharp
/// <summary>
/// Gets a temporary directory unique to the currently executing test. The directory is
/// created on first access and is deleted automatically when the test passes; when the test
/// fails, it is retained so its contents can be inspected.
/// </summary>
public virtual string? TestTempDirectory { get; }
```

- **Type** `string?`, matching the existing directory properties (see design question 2).
- **Lazy**: the directory is created the first time the getter is called for a given test and
  cached for the remainder of that test.
- **Unique per test execution**: each executing test — including each data-driven case — observes
  its own directory (see design question 8).

The property is exposed only for the non-UWP / non-WinUI target frameworks, exactly like the
existing directory properties (`#if !WINDOWS_UWP && !WIN_UI`), so it surfaces on `net462`,
`netstandard2.0`, `net8.0`, and `net9.0`.

### How it differs from the existing directory properties

Being crisp about "which directory do I use for what" is a required part of this RFC; the existing
set is already confusing to users (see
[testfx#7589](https://github.com/microsoft/testfx/discussions/7589),
"TestRunResultsDirectory vs. TestResultsDirectory"). Recent work has clarified the XML docs for
these ("Clarify XML docs for TestContext result/deployment directories") and added copying of
per-test result files to the results directory ("Copy per-test result files to results directory"),
but none of them is per-test scratch space.

| Property                    | Scope                     | Shared across tests?          | Auto-created per test? | Auto-cleaned? | Intended use                                  |
| --------------------------- | ------------------------- | ----------------------------- | ---------------------- | ------------- | --------------------------------------------- |
| `TestRunDirectory`          | Whole test run            | Yes (one per run)             | No                     | No            | Root of the deployment/results layout          |
| `DeploymentDirectory`       | Whole test run (`Out`)    | Yes                           | No                     | No            | Where `[DeploymentItem]` files are copied      |
| `ResultsDirectory`          | Whole test run (`In`)     | Yes                           | No                     | No            | Base directory for run result files            |
| `TestRunResultsDirectory`   | Per machine (`In\<machine>`) | Yes (per machine)          | No                     | No            | Per-machine result files                       |
| `TestResultsDirectory`      | Whole test run (`In`)     | Yes (same path as `ResultsDirectory`) | No             | No            | Result files (add via `AddResultFile`)         |
| **`TestTempDirectory`** *(new)* | **Per test execution** | **No — unique per test/case** | **Yes (lazy)**         | **Yes (delete on pass, retain on fail)** | **Private scratch space for the test** |

The critical distinction: all five existing properties are **run-scoped or machine-scoped and
shared**; `TestTempDirectory` is **test-scoped and private**. None of the five is safe to write to
concurrently from parallel tests without coordination; `TestTempDirectory` is safe by construction.

## Design questions

These were genuinely open; each is answered with a proposal and rationale. Items marked **(sign-off)**
are the decisions the author is asking reviewers to **ratify** before this RFC is approved — they are
proposals, not yet-confirmed conclusions. The same three items (name, cleanup policy, location) are
surfaced in the PR description for ratification.

### 1. Name — `TestTempDirectory` **(sign-off)**

Candidates considered: `TestTempDirectory`, `TempDirectory`, `TestDirectory`, `ScratchDirectory`.
The name must not be confusable with the five existing directory properties.

**Chosen: `TestTempDirectory`.** It reads as "the temp directory for this test", pairs naturally
with the existing `Test*` naming (`TestName`, `TestResultsDirectory`, `TestRunDirectory`), and the
"Temp" token clearly signals *disposable scratch space* rather than durable results. `TempDirectory`
alone loses the per-test connotation; `TestDirectory` is ambiguous with the test *source* directory;
`ScratchDirectory` has no precedent in the API surface.

### 2. Type — `string?`

`string` vs `DirectoryInfo`. **Chosen: `string?`**, for consistency with all five existing
`TestContext` directory properties. A `DirectoryInfo` would be marginally more convenient but would
make `TestTempDirectory` the odd one out and complicate the "which directory property do I use"
story this RFC is trying to simplify. `string` also composes directly with `Path.Combine`, which is
how the existing properties are used. Nullable to match the existing properties' signatures (they
are `string?` because the underlying property bag can be empty in edge cases).

### 3. Lazy vs eager creation — lazy

**Chosen: lazy.** The directory is created on first access to the getter. Rationale: eager creation
would add a directory create **and** a directory delete to *every test in every suite*, including
the overwhelming majority that never touch the filesystem — a measurable, pure-overhead cost at
scale (a suite of 50,000 tests would perform 100,000 extra filesystem operations for nothing).
Lazy creation means the cost is paid only by tests that actually opt in by reading the property.
Creation is guarded so concurrent reads on the same context (rare, but possible via async) create
exactly one directory.

### 4. Cleanup policy — delete on pass, retain on failure **(sign-off)**

**Chosen default: delete on pass, retain on failure**, matching pytest's most useful behavior — a
failed test's artifacts are exactly what you want to inspect, and a passed test's are noise.

- **Escape hatch (retain everything):** an environment variable (working name
  `MSTEST_TEST_TEMP_DIRECTORY_RETAIN`, `1`/`true` to enable) forces retention regardless of
  outcome, mirroring `TempDirectory.cs`'s existing `..._Cleanup=0` switch. This is the debugging
  affordance.
- **Result-attachment exception:** a passing test's directory is also retained when the test
  registers a file inside it as a result attachment via `TestContext.AddResultFile`. The host
  collects registered result files *after* the per-test `TestContext` is disposed (the VSTest
  attachment URI and the MTP `FileArtifactProperty` both reference the original path), so deleting
  the directory on pass would leave the attachment pointing at a missing file. The framework
  consumes the result-file list once per execution attempt, so the retention decision is recomputed
  per attempt — a file registered only by an earlier retry attempt does not keep an otherwise
  passing final attempt's directory alive.
- **Keep-last-N-runs (pytest):** evaluated and **rejected for v1**. pytest keeps directories under a
  stable per-user root (`/tmp/pytest-of-<user>/pytest-<N>/`) and prunes older *sessions*. MSTest's
  results directory is already per-run (a fresh GUID each run), so old runs are naturally distinct;
  layering an N-run pruner on top adds cross-run bookkeeping and a global root with its own cleanup
  and concurrency concerns for little benefit. Can be revisited if demand appears.
- **Configurability:** retention-on-failure is the fixed default plus the env-var escape hatch for
  v1. A runsettings knob (e.g. always-delete / always-retain / retain-on-failure) is a natural
  future extension but is intentionally deferred to keep the initial surface minimal.

Retention keys off the outcome recorded on the per-test `TestContext` at disposal time. This relies
on the framework contract that a test's **final** outcome is set before its `TestContext` is
disposed — which holds on every path, including each folded/unfolded data-driven iteration (the
iteration's outcome is set on its own context before that context is disposed). Any non-passing
outcome (Failed, Timeout, Aborted, Inconclusive, …) retains; the `UnitTestOutcome` default is
`Failed`, so the fail-safe direction is *retain*, never an accidental delete of a not-yet-finalized
test.

### 5. Location — under the results directory **(sign-off)**

`Path.GetTempPath()` vs under the run's results directory.

**Chosen: under the results directory** (`TestResultsDirectory` — i.e. the run's `In` directory),
so the artifacts are discoverable next to other run output and can be picked up by artifact
collection. The trade-off is **Windows `MAX_PATH` (260)**: results directories are already deep
(`...\TestResults\<run-guid>\In`) and test display names can be long, so a naive
`<results>\<full-test-name>\<guid>` scheme could overflow `MAX_PATH` and break tests on Windows.

This is treated as a first-class constraint and mitigated by an **adaptive** naming scheme (design
question 6): the readable-name budget is not a fixed number but is computed from how much room the
actual base path leaves under `MAX_PATH`, always reserving a fixed **headroom (working value 80
characters)** for the files the test itself writes inside the directory. Crucially:

- The headroom is a **guarantee to the caller**: on Windows the returned path is short enough that
  ordinary relative writes inside it (`Path.Combine(TestTempDirectory, "sub", "result.json")`) will
  not overflow `MAX_PATH`. This guarantee is stated in the property's XML doc so users know roughly
  how much path they have.
- If the results directory is so deep that even a minimal readable name (floor **8 characters**)
  cannot preserve that headroom, the implementation **falls back to `Path.GetTempPath()`** — a short
  root (`C:\Users\<u>\AppData\Local\Temp\`, ~30 chars) that restores plenty of room. This is a
  **documented behavior**, not merely the availability fallback below.
- The 50-character value is the **cap** on the readable portion, not a fixed size.

When the results directory is entirely unavailable (a host that does not populate it), the
implementation likewise **falls back to `Path.GetTempPath()`** so the property always returns a
usable path. Note that in the normal .NET path `TestResultsDirectory` is rarely empty — when no
results directory is configured, MSTest maps it to the **test assembly's output directory** (the
`bin` folder), so by default the per-test directory is created there, next to the binaries. As a
final safety net, if the chosen base directory **cannot be written to** (for example a read-only
output directory), directory creation falls back to `Path.GetTempPath()` as well. Only if the
system temporary directory is *also* unusable does the underlying `Directory.CreateDirectory`
exception surface from the getter — in that degenerate environment there is genuinely nowhere to
create a scratch directory, so the error is allowed to propagate rather than returning an unusable
path. In every normal environment (writable results directory *or* writable temp), the getter does
not throw a creation error.

**Long-path support:** the feature targets the classic 260-character `MAX_PATH` and **does not rely
on long-path opt-in** (`LongPathsEnabled` / the `\\?\` prefix). Long paths are not guaranteed to be
enabled and are frequently not honored by external tools that end-to-end tests shell out to, so
relying on them would make the headroom guarantee unsafe.

### 6. Directory naming — sanitized + adaptively-truncated name + GUID uniqueness suffix

A readable directory name aids debugging, but arbitrary test names (data-driven tests especially)
contain characters illegal in paths and can be very long. Scheme:

1. Start from the test display name (falling back to the method name).
2. **Sanitize**: replace every character that is invalid in a path segment (via
   `Path.GetInvalidFileNameChars()` plus platform reserved characters) with `_`; collapse runs of
   `_`.
3. **Truncate** the sanitized name to the **adaptive budget** from design question 5 (capped at 50
   characters, floored at 8 before falling back to temp), never slicing through the middle of a
   surrogate pair (which would leave a lone surrogate and an invalid segment).
4. **Append a unique suffix** (`_` + a full 32-hex-char GUID, i.e. `Guid.ToString("N")`) so that
   collisions across cases and across the (truncated, hence potentially colliding) readable names
   are cryptographically negligible — a full 128-bit GUID makes two contexts choosing the same
   suffix effectively impossible even at very large scales (millions of contexts).
5. **Create with collision retry**: attempt `Directory.CreateDirectory` for the candidate; if the
   directory already exists, regenerate the suffix and retry a bounded number of times.
   `Directory.Exists` + `CreateDirectory` is not an atomic exclusive create, so the retry is only a
   belt-and-braces guard — the actual uniqueness guarantee comes from the 128-bit suffix, whose
   birthday-collision probability is negligible, not from the pre-check.

If the test name is empty, whitespace-only, made up entirely of invalid characters, or the adaptive
budget has collapsed to zero, the sanitized prefix is empty and the directory name is **just the
suffix** (no leading `_`). This is the one case where the shape is `<uniq>` rather than
`<sanitized-truncated-name>_<uniq>`.

Result shape: `...\TestResults\<run-guid>\In\<sanitized-truncated-name>_<uniq>`. This is correct for
`[DataRow]`/`[DynamicData]`, where many *cases* share one method name: each case runs on its own
`TestContext` (see design question 8) and therefore gets its own directory, distinguished by the
unique suffix (and, where the display name includes the arguments, by a differing readable prefix).

### 7. Cleanup failure handling — best-effort with swallow

Deleting a directory can fail if the test left a file handle open, or on Windows if antivirus or
the indexer holds a transient lock. **Chosen: best-effort recursive delete that swallows
exceptions** (exactly like `TempDirectory.cs`). A failed cleanup must never fail an otherwise
passing test. A cleanup failure is **not** surfaced as a test warning (which would be noise on flaky
AV/indexer locks); instead it is recorded through the adapter's diagnostic trace logger, so it is
observable when diagnostics are enabled but invisible otherwise. The trace call is itself guarded so
that a misbehaving logger cannot let an exception escape disposal.

### 8. Concurrency — correct under all execution scopes

`TestTempDirectory` must be correct under every `ExecutionScope` and under `[Parallelize]`,
including several data-driven cases of the same method running concurrently. This falls out of the
implementation model: the backing field is a **per-instance lazy field on the per-test
`TestContext`**. MSTest already creates a distinct `TestContext` per executing test, and — crucially
— a **fresh `TestContext` per data-driven iteration** (the folded path clones a sibling context per
row; the unfolded path allocates one per row). Because each executing test/case has its own context
object, each gets its own lazily-created directory with no shared mutable state and no cross-test
locking. Uniqueness is structural, not coordinated.

The same `TestContext` type is also handed to **fixture methods** (`[AssemblyInitialize]`,
`[ClassInitialize]`, `[ClassCleanup]`, `[AssemblyCleanup]`). Those contexts are not per-test — their
outcome/disposal semantics differ (e.g. `ClassCleanupManager.ForceCleanup` contexts are never
disposed), so lazily creating a directory for them would leak it. The property therefore returns
`null` for any non-test context; a directory is only ever created for an executing test (or a
data-driven case of one).

### 9. Timeouts / aborted tests — cleanup must not extend the test or throw

Cleanup runs when the per-test `TestContext` is disposed, which happens after the test's outcome
has been recorded — including when the test timed out or the run was cancelled. Because it runs
*after* the test rather than as part of it, it cannot extend the test's own execution or trip its
timeout. Cleanup therefore:

- performs a **synchronous, best-effort** recursive delete (no waiting on handles, no retries beyond
  the swallow),
- never observes or blocks on the cancellation token, and
- swallows all exceptions,

so an aborted or timed-out test is never blocked or failed by cleanup. The delete itself is an
ordinary synchronous `Directory.Delete`, so a pathologically stalled filesystem (a hung network
share, a wedged FUSE mount) could in principle make the *disposal* slow; bounding that with a
watchdog thread is deliberately out of scope, matching every other cleanup path in the adapter and
the reference `TempDirectory.cs`.

## Relationship to RFC 020 (`[ResourceLock]`)

RFC 020 ([`020-Resource-Lock-Attribute.md`](020-Resource-Lock-Attribute.md)) proposes a
`[ResourceLock]` attribute for declaring contended resources so that tests touching the same
resource do not run concurrently. The two features are **complements**, and the guidance is
*eliminate before you lock*:

- **`TestTempDirectory` (this RFC) — eliminate the sharing.** When the contended resource is "a
  place to write files", give each test its own directory and there is nothing left to coordinate.
  This is the **preferred** fix and should be recommended first.
- **`[ResourceLock]` (RFC 020) — coordinate access** to state that genuinely *cannot* be made
  per-test: environment variables, the current working directory, the console, a database, or a
  fixed external path. There, isolation is impossible, so serialization is the answer.

RFC 020 already contains an "eliminate before you lock" section; this RFC aligns with it and both
documents cross-reference each other.

## Public API

```diff
 namespace Microsoft.VisualStudio.TestTools.UnitTesting;

 public abstract class TestContext
 {
+    public virtual string? TestTempDirectory { get; }
 }
```

Added to `PublicAPI.Unshipped.txt` for `net462`, `netstandard2.0`, `net8.0`, and `net9.0`. The
property does **not** use an `init` accessor, per repo policy.

## Compatibility

Purely additive. No existing property's value or semantics change, so no consumer of
`TestRunDirectory`, `DeploymentDirectory`, `ResultsDirectory`, `TestRunResultsDirectory`, or
`TestResultsDirectory` is affected. Subclasses of `TestContext` that do not override
`TestTempDirectory` inherit the base behavior. Because creation is lazy, suites that never read the
property see no behavioral or performance change whatsoever.

## Example

```csharp
[TestClass]
public class FileWritingTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void WritesToItsOwnPrivateDirectory()
    {
        string dir = TestContext.TestTempDirectory!;   // created here, on first access
        string file = Path.Combine(dir, "data.json");
        File.WriteAllText(file, "{}");

        Assert.IsTrue(File.Exists(file));
        // On pass, `dir` is deleted automatically. On failure, it is kept for inspection.
    }

    [DataTestMethod]
    [DataRow("alpha")]
    [DataRow("beta")]
    public void EachDataRowGetsItsOwnDirectory(string name)
    {
        // The two rows observe two different TestTempDirectory paths, so they never collide
        // even when executed in parallel.
        File.WriteAllText(Path.Combine(TestContext.TestTempDirectory!, name + ".txt"), name);
    }
}
```

## Unresolved questions

The items below were open during drafting and are now **settled in the initial implementation**;
they are recorded here as the decisions taken (each is still open for reviewer sign-off, but the
code reflects these values, not placeholders):

- **Environment-variable name** for the retain-all escape hatch: **`MSTEST_TEST_TEMP_DIRECTORY_RETAIN`**
  (`1`/`true`, case-insensitive, to enable).
- **Warn on cleanup failure** (design question 7): **trace-only, no test warning** — the failure is
  logged through the adapter diagnostic trace logger and is otherwise silent.
- **Truncation budget and unique-suffix length** (design questions 5–6): the readable name is
  **adaptive** — sized from the base-path length, **capped at 50 chars**, **floored at 8** (below
  which the implementation falls back to system temp), with a full **32 hex-char (128-bit) GUID**
  suffix and a reserved **80-char headroom** under Windows `MAX_PATH`. These are the shipped
  constants; they can be tuned if headroom testing on Windows shows a reason to.

Genuinely still open (deferred, not part of v1):

- **Future runsettings knob** for cleanup policy (design question 4) — always-delete / always-retain
  / retain-on-failure. Deferred to keep the initial surface minimal.
- **Class-/assembly-level shared temp directory** and **keep-last-N-runs** — see Non-goals.
