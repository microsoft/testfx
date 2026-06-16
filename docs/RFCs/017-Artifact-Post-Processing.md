# RFC 017 - Artifact post-processing for `dotnet test` (MTP)

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Introduce a **two-phase artifact post-processing** mechanism for Microsoft.Testing.Platform (MTP) so that, after a multi-module `dotnet test` run, well-known artifacts of the same kind (TRX reports, `.coverage` files, Cobertura, custom formats) are **consolidated into single files** instead of being listed one-per-module.

The design adds:

- A new typed extension contract `IArtifactPostProcessor` in testfx.
- A reserved, hidden host switch `--internal-post-process-artifacts <manifest.json>` that runs an MTP host in a non-test "merge" mode.
- A handshake addition so each test app can advertise which artifact kinds it can post-process.
- SDK orchestration in `dotnet test` that elects an already-built app per artifact kind, relaunches it once with a JSON manifest, and swaps the merged output into the run summary.

It addresses [dotnet/sdk#47613](https://github.com/dotnet/sdk/issues/47613) and is related to [#7345](https://github.com/microsoft/testfx/issues/7345), [#7471](https://github.com/microsoft/testfx/issues/7471), and [#6586](https://github.com/microsoft/testfx/issues/6586).

> **Status note.** This is an early RFC opened for discussion. Two parts in particular are explicitly *not* settled and are called out in [§10 Alternatives considered](#10-alternatives-considered) and [§11 Open questions](#11-open-questions): (a) whether the orchestration route should be the reserved host switch, the existing `ITool` route, or a specialized host; and (b) whether a live IPC channel between orchestrator and post-processing host buys us anything over the current fire-and-forget manifest-in/result-out model.

## 1. Problem statement

When `dotnet test` runs N test modules under MTP, each module process produces its own artifacts (TRX files, `.coverage` files, attachments, custom files…). The SDK's terminal output currently just lists every artifact path it received via `FileArtifactMessages`. On multi-module solutions (e.g. the `microsoft/testfx` repo itself) this produces dozens of TRX paths and zero consolidated view, and downstream consumers (Azure DevOps `PublishTestResults`, `ReportGenerator`, CI dashboards) have to either glob every file or rely on out-of-band merging tools.

VSTest historically solved this with a **two-phase** flow driven by `dotnet test`:

1. **Collect** — `dotnet test` spawns `vstest.console` with `--artifactsProcessingMode-collect --testSessionCorrelationId:<guid>`. Each invocation drops its artifacts into a temp folder tagged with that correlation id. (See `microsoft/vstest`: `ArtifactProcessingCollectModeProcessor.cs`.)
2. **Post-process** — after all collect invocations finish, `dotnet test` invokes `vstest.console` **a second time** with `--artifactsProcessingMode-postprocess --testSessionCorrelationId:<same-guid>`. That triggers `ArtifactProcessingPostProcessModeProcessor`, which discovers the tagged temp folder and runs the merge. (See `dotnet/sdk`: `src/Cli/dotnet/Commands/Test/VSTest/TestCommand.cs` — `artifactsPostProcessArgs`.)

MTP has no equivalent today. The design below borrows the **two-phase** idea but differs in three meaningful ways:

- We relaunch **a test app**, not a separate orchestrator like `vstest.console`. That sidesteps the "where do extension assemblies come from in a tool process?" problem.
- We pass an **explicit JSON manifest** of input paths instead of relying on a correlation-id-tagged temp folder. No implicit disk-layout coupling.
- The orchestrator (SDK `dotnet test`) is the **same process** across both phases under MTP, so we don't need a correlation id at all.

## 2. Goals

1. **Consolidate artifacts** of well-known formats (TRX, code-coverage, Cobertura, custom) into single files at the end of a `dotnet test` run.
2. **Extension-agnostic SDK**: SDK must not link or special-case any specific extension (TRX, Microsoft.CodeCoverage, Coverlet, AltCover, future formats).
3. **No new user-visible CLI option** for normal use. Merging "just happens" when ≥ 2 mergeable artifacts of the same kind are produced and at least one running app advertises a processor.
4. **Backward compatible**: older test hosts (without the new contract) keep today's behavior. SDK upgrades on top of older hosts and vice-versa work.
5. **Non-destructive**: per-module artifacts remain on disk where the producing host wrote them. Merged output is additional, not a replacement.
6. **Never fail the run** because of a post-processing failure.

## 3. Non-goals

- Real-time merging during the run. Post-processing happens after all module runs complete.
- Cross-`dotnet test` invocation merging (correlating artifacts across separate `dotnet test` calls). VSTest needed this because the orchestrator process was short-lived per invocation; MTP `dotnet test` owns the full session.
- IDE integration. This design is for the CLI orchestrator; IDE adapters can layer on it later if useful.
- Defining a new artifact format. We merge existing formats; we do not invent one.

## 4. Glossary

| Term | Meaning |
|---|---|
| **Test app** | An MTP host process (the user's test project after build/publish). |
| **Module** | The test app binary the user references (`*.dll` or AOT exe). |
| **Artifact** | Any file produced by an extension and reported via `SessionFileArtifact` / `FileArtifactMessages`. |
| **Orchestrator** | The `dotnet test` process in the SDK that spawns test apps and consumes their IPC. |
| **Post-processor** | An MTP extension that knows how to merge / consolidate one or more artifact kinds. |
| **Election** | The orchestrator's decision of which test app to relaunch to perform a given post-processing job. |
| **Kind** | A producer-asserted, reverse-DNS identifier for an artifact format (e.g. `microsoft.testing.trx`). Primary matching key. |

## 5. High-level architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         dotnet test (SDK orchestrator)                   │
│                                                                          │
│   1. Spawn test app A (net10.0)  ──► artifacts: a.trx, a.coverage        │
│   2. Spawn test app B (net10.0)  ──► artifacts: b.trx, b.coverage        │
│   3. Spawn test app C (net8.0)   ──► artifacts: c.trx                    │
│                                                                          │
│   After all runs complete:                                               │
│                                                                          │
│   4. Elect an app per artifact-kind:                                     │
│        microsoft.testing.trx  → app A  (advertised the kind)             │
│        microsoft.codecoverage → app A  (advertised the kind)             │
│                                                                          │
│   5. Spawn app A once with:                                              │
│        --internal-post-process-artifacts <manifest.json>                 │
│                                                                          │
│         ┌────────────────────────────────────────────────┐               │
│         │       Test app A in post-process mode          │               │
│         │                                                │               │
│         │   IArtifactPostProcessor instances loaded:     │               │
│         │     • TrxArtifactPostProcessor                 │               │
│         │     • CodeCoverageArtifactPostProcessor        │               │
│         │                                                │               │
│         │   Reads manifest → routes by Kind → calls      │               │
│         │   each processor → writes result JSON → exits  │               │
│         └────────────────────────────────────────────────┘               │
│                                                                          │
│   6. Read result JSON, swap originals for merged in summary              │
│                                                                          │
│   Final summary:                                                         │
│     Merged TRX report: TestResults/merged.trx                            │
│     Merged coverage:   TestResults/merged.coverage                       │
└──────────────────────────────────────────────────────────────────────────┘
```

## 6. Detailed design

### 6.1 New extension contract (testfx)

Package: `Microsoft.Testing.Platform` (or a new abstractions package if we want to ship without forcing a platform major)
Namespace: `Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing`

```csharp
public interface IArtifactPostProcessor : IExtension
{
    // Inherited from IExtension: Uid, Version, DisplayName, Description, Task<bool> IsEnabledAsync().

    /// <summary>
    /// Reverse-DNS identifiers of the artifact kinds this processor can consume.
    /// Open vocabulary; recommended convention matches MTP extension Uids
    /// (e.g. "microsoft.testing.trx", "microsoft.codecoverage", "coverlet.cobertura").
    /// This is the primary matching key used by the orchestrator and the host.
    /// </summary>
    IReadOnlyList<string> SupportedKinds { get; }

    /// <summary>
    /// Optional fallback for matching artifacts whose producer did not declare a Kind
    /// (older platform versions, custom data consumers that haven't migrated yet).
    /// Lowercase file extensions including the leading dot, e.g. [".trx"].
    /// May be empty if the processor only operates on Kind-tagged artifacts.
    /// </summary>
    IReadOnlyList<string> SupportedFileExtensionsFallback { get; }

    /// <summary>
    /// Called once with all matching input artifacts after every test module finished.
    /// Implementations must:
    ///  - Treat <paramref name="inputs"/> as read-only (do not delete the source files).
    ///  - Produce zero or one merged output written under <paramref name="outputDirectory"/>.
    ///  - Be deterministic and idempotent — the orchestrator may retry on transient failures.
    /// </summary>
    Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        CancellationToken cancellationToken);
}

public sealed record InputArtifact(
    string Path,
    string? Kind,                       // null when the producer didn't declare one
    string? ProducingTestModule,
    string? TargetFramework,
    string? Architecture,
    string? ExecutionId);

public sealed record ProcessedArtifact(
    string Path,
    string Kind,                        // processor MUST tag its output
    string DisplayName,
    string? Description);
```

#### Why a Kind, not just file extension?

- **File extensions collide across formats.** `.xml` is JUnit, NUnit3, custom; `.json` is everywhere. The orchestrator should never need to inspect content to disambiguate.
- **Producer-asserted identity** is more reliable than orchestrator-guessed. The TRX writer knows it produced a TRX; the orchestrator shouldn't have to encode that knowledge.
- **Extension-less / compound artifacts.** A future processor might consume a folder of Playwright traces, a `.zip` of dumps, or a sqlite DB with a custom suffix — none of those round-trip cleanly through extension matching.
- **Versioning.** If a format evolves incompatibly, the producer can switch from `microsoft.codecoverage` to `microsoft.codecoverage.v2` without changing file naming, and the old processor stops matching cleanly.

#### Notes

- **Kind is primary, file extension is fallback.** During the transition period (older producers don't tag Kind yet) the orchestrator *and the host* match by Kind first, then by file extension for any remaining untagged inputs. Long-term, file-extension fallback can be deprecated.
- **Open vocabulary, namespaced.** No central registry. Convention: reverse-DNS strings owned by the producing component (e.g. `microsoft.testing.trx`, `microsoft.codecoverage`, `coverlet.cobertura`, `playwright.trace`). Same hygiene rule as extension `Uid`s.
- **Returning `null`** means "I looked but there's nothing to do" (e.g., < 2 inputs). The orchestrator then leaves the originals visible.
- **Idempotent / deterministic.** The contract is intentionally pure-functional from `inputs` → `output`. Implementations must not stash state across calls.
- **`IExtension` base** gives us `Uid`, `Version`, `DisplayName`, `Description`, and `IsEnabledAsync()` for free, plus integration with the existing extension manifest tooling. (`IsEnabledAsync()` is therefore *not* a bespoke member of this contract; the sketch in [Appendix A.1](#a1-trxartifactpostprocessor-in-microsofttestingextensionstrxreport) implements the inherited member.)

#### Producer-side change

`SessionFileArtifact` (and the `FileArtifactMessages` IPC contract) gain an optional `Kind` string. Today's producers (TRX consumer, MS CC, etc.) start tagging their outputs when they upgrade — fully backward compatible: a missing Kind just means "use the fallback rules".

#### The producer/post-processor co-location invariant

The election algorithm (§6.7) leans on a structural fact worth stating explicitly: **the producer and the post-processor for a given Kind ship in the same extension assembly.** `Microsoft.Testing.Extensions.TrxReport` contains both `TrxDataConsumer` (producer) and `TrxArtifactPostProcessor` (post-processor). Consequently, *any app that produced an artifact of a Kind necessarily has the post-processor for that Kind loaded* (once upgraded). This is why "elect the app that produced the most of a group" is always a valid candidate set — producing implies capability. The handshake advertisement (§6.5) is what makes this explicit to the orchestrator rather than inferred.

### 6.2 Reserved internal host mode

Every MTP host gains a new reserved switch:

```
--internal-post-process-artifacts <manifest.json>
```

Properties:
- Hidden from `--help` and `--info`. Documented only in the platform spec.
- Mutually exclusive with `--list-tests`, `--server`, `--info`, normal run, and `--tool`.
- When present at startup, the platform:
  1. Skips framework discovery and test execution.
  2. Resolves all registered `IArtifactPostProcessor` instances exactly as it would resolve any extension.
  3. Loads the manifest JSON.
  4. **Routes inputs to processors by Kind first, then by file-extension fallback** (mirroring the orchestrator's grouping in §6.7): for each input, find the processor whose `SupportedKinds` contains the input's `Kind`; for any input whose `Kind` is `null`, find the processor whose `SupportedFileExtensionsFallback` contains the input's file extension (case-insensitive). An input is never routed to more than one processor.
  5. Calls each matched processor's `ProcessAsync` once with the inputs routed to it.
  6. Writes a result JSON file (path supplied in the manifest) and exits 0 on success, non-zero on infrastructure failure.

Exit codes:
| Code | Meaning |
|---|---|
| 0 | All processors completed (individual processors may have returned `null`). |
| 90 | Manifest invalid / unreadable. |
| 91 | One or more processors threw. Details in result JSON. |
| 92 | Host couldn't load extensions. |

(Final values to be chosen so they do not overlap existing MTP exit codes; see [§11 Open questions](#11-open-questions).)

### 6.3 Manifest format (orchestrator → host)

```jsonc
{
  "schemaVersion": 1,
  "resultPath": "C:/path/to/manifest.result.json",
  "outputDirectory": "C:/path/to/TestResults",
  "inputs": [
    {
      "path": "C:/.../A/TestResults/A_net10.0_2026-06-10_10-30-00.trx",
      "kind": "microsoft.testing.trx",
      "producingTestModule": "A.dll",
      "targetFramework": "net10.0",
      "architecture": "x64",
      "executionId": "8c5f..."
    },
    {
      "path": "C:/.../B/TestResults/B_net10.0_2026-06-10_10-30-01.trx",
      "kind": "microsoft.testing.trx",
      "producingTestModule": "B.dll",
      "targetFramework": "net10.0",
      "architecture": "x64",
      "executionId": "9a13..."
    },
    {
      "path": "C:/.../A/TestResults/A.coverage",
      "kind": "microsoft.codecoverage",
      "producingTestModule": "A.dll",
      "targetFramework": "net10.0",
      "architecture": "x64",
      "executionId": "8c5f..."
    },
    {
      "path": "C:/.../C/TestResults/legacy.trx",
      "kind": null,                       // legacy producer didn't tag — orchestrator routed via extension fallback
      "producingTestModule": "C.dll",
      "targetFramework": "net8.0",
      "architecture": "x64",
      "executionId": "1f02..."
    }
  ]
}
```

### 6.4 Result format (host → orchestrator)

```jsonc
{
  "schemaVersion": 1,
  "results": [
    {
      "processorUid": "Microsoft.Testing.Extensions.TrxReport.PostProcessor",
      "consumedInputs": [ "C:/.../A_net10.0_...trx", "C:/.../B_net10.0_...trx" ],
      "output": {
        "path": "C:/.../TestResults/merged.trx",
        "kind": "microsoft.testing.trx",
        "displayName": "TRX report (merged)",
        "description": "2 TRX files merged"
      },
      "errors": []
    },
    {
      "processorUid": "Microsoft.Testing.Extensions.CodeCoverage.PostProcessor",
      "consumedInputs": [ "C:/.../A.coverage" ],
      "output": null,
      "errors": []
    }
  ]
}
```

Multiple `results` entries per host run — one per processor invoked. `output == null` means the processor declined (returned `null` from `ProcessAsync`). `errors` is a non-empty array if `ProcessAsync` threw; the orchestrator surfaces these as warnings.

### 6.5 Handshake addition

Each test app must advertise the kinds (and legacy file extensions) it can post-process so the orchestrator can elect.

Two options for *where* to put this:

**Option A — extend `HandshakeMessage`** with two new optional properties:
```
SupportedPostProcessorKinds:            "microsoft.testing.trx;microsoft.codecoverage"
SupportedPostProcessorExtensionsLegacy: ".trx;.coverage"
```
- Pro: piggybacks on existing handshake message that arrives first.
- Con: handshake is small/perf-sensitive; we shouldn't keep adding fields.

**Option B — new message `RegisteredExtensionsMessage`** sent right after handshake.
- Pro: extensible; future advertisement fits the same channel.
- Con: one more round-trip.

**Recommendation:** Option A. Two optional semicolon-separated strings. Cheap. If it grows further, we revisit. (Reverse-DNS kinds never contain `;`, so the separator is safe.)

Backward compatibility: missing fields = empty sets = "this app has no post-processors". Orchestrator handles gracefully.

The producer side (e.g., `TrxDataConsumer.cs`) also needs to start tagging its emitted `SessionFileArtifact` with the new `Kind` property. Until that lands, the orchestrator's extension-fallback path handles those artifacts.

### 6.6 SDK orchestration flow

In `src/Cli/dotnet/Commands/Test/MTP/`:

```csharp
// MicrosoftTestingPlatformTestCommand.Run, in finally (before TestExecutionCompleted)

// 1. Drain all artifacts from the reporter, keep originals in memory
IReadOnlyList<TestRunArtifact> allArtifacts = output.SnapshotArtifacts();

// 2. Plan post-processing jobs. App capabilities = { modulePath -> (kinds, legacyExtensions) }
PostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
    allArtifacts,
    appCapabilities: testHandler.GetAdvertisedCapabilities());

// 3. Execute jobs (sequential is fine; small N)
foreach (PostProcessingJob job in plan.Jobs)
{
    PostProcessingResult result = await PostProcessingHostInvoker.InvokeAsync(
        executableToRelaunch: job.ElectedApp.Path,
        inputs: job.Inputs,
        outputDirectory: job.OutputDirectory,
        cancellationToken);

    // 4. Swap originals for merged in the reporter
    foreach (var merged in result.Outputs)
    {
        output.RemoveArtifacts(merged.ConsumedInputs);
        output.ArtifactAdded(outOfProcess: false, ..., path: merged.Path);
    }

    foreach (var error in result.Errors)
    {
        output.Warning($"Post-processing of {error.ProcessorUid}: {error.Message}");
    }
}

// 5. Now render the summary (one TRX, one .coverage, etc., instead of N each)
output.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
```

New tiny surface on `TerminalTestReporter`:
```csharp
public IReadOnlyList<TestRunArtifact> SnapshotArtifacts();
public void RemoveArtifacts(IEnumerable<string> paths);
public void Warning(string message);  // already-ish present via _output.WriteWarning if any
```

`GetAdvertisedCapabilities()` returns, per module path, the **kinds** and **legacy extensions** the app advertised in its handshake (§6.5) — not a bare extension list.

### 6.7 Election algorithm

```
plan = []
groups = []

# 1. Group artifacts that have a Kind by their Kind.
for kind in unique(artifact.kind for artifact in artifacts if artifact.kind != null):
    groups.append(("kind", kind, [a for a in artifacts if a.kind == kind]))

# 2. Group remaining (untagged) artifacts by file extension as a fallback.
untagged = [a for a in artifacts if a.kind == null]
for ext in unique(Path.GetExtension(a.path) for a in untagged):
    groups.append(("ext", ext, [a for a in untagged if Path.GetExtension(a.path) == ext]))

# 3. Only groups with >= 2 inputs are worth merging.
groups = [g for g in groups if len(g.items) >= 2]

for (matchType, key, items) in groups:
    if matchType == "kind":
        candidates = [app for app in apps if key in app.AdvertisedKinds]
    else:  # "ext"
        candidates = [app for app in apps if key in app.AdvertisedExtensionsLegacy]

    if not candidates:
        continue  # leave these artifacts alone

    # Tie-break:
    #  1. Prefer apps that produced the most artifacts in this group
    #     (highest chance same extension version is loaded).
    #  2. Then by highest TFM (newer = usually faster startup).
    #  3. Then deterministic by full path for reproducibility.
    elected = sort_by(
        candidates,
        key=lambda a: (
            -count_produced_by(a, items),
            -tfm_order(a.tfm),
            a.path
        )
    )[0]
    plan.add_or_extend_job(elected, items)

# Coalesce: if same app is elected for multiple groups, one relaunch handles all.
return plan
```

#### Multiple elected apps is expected

Election runs **independently per group**, so different kinds can — and often will — elect different test apps. The coalesce step at the end only collapses jobs when the *same* app happens to win multiple groups.

Worked example:

| Test app | TFM | Advertises kinds | Produced this run |
|---|---|---|---|
| A.dll | net10.0 | `microsoft.testing.trx`, `microsoft.codecoverage` | 4× trx, 1× coverage |
| B.dll | net10.0 | `microsoft.testing.trx`, `microsoft.codecoverage` | 1× trx, 4× coverage |
| C.dll | net8.0 | `microsoft.testing.trx` only | 1× trx |

- `microsoft.testing.trx` group: candidates = {A, B, C}; most-produced → **A wins** (4 trx vs 1 vs 1).
- `microsoft.codecoverage` group: candidates = {A, B}; most-produced → **B wins** (4 coverage vs 1).
- Plan: relaunch A once (for the 6 TRX), relaunch B once (for the 5 coverage). **Two relaunches** total.

If instead A had produced all 6 TRX and all 5 coverage, A would win both groups, the coalesce step would merge them into a single plan entry, and we'd relaunch A **once** with the union.

#### Why per-kind, not single-winner?

We could pick one app overall that supports the *union* of needed kinds and always do one relaunch. We don't, because:
- The per-kind winner is more likely to have the *exact extension version* that produced those artifacts loaded (lower risk of format drift between writer and reader).
- N is small (≤ #kinds), so a few extra relaunches are cheap compared to startup cost in normal multi-project solutions.
- Single-winner becomes a fallback worth considering only if we observe real-world hosts where startup is dominant — easy to layer in later without changing the contract.

#### Edge cases
- **No app supports a Kind (and no extension fallback matches)** → no-op for that group. Originals listed as today.
- **Group has only 1 input** → not planned at all (the `>= 2` filter in step 3 removes it). The single original is listed as today; no relaunch happens.
- **Two processors in the same app advertise the same Kind** → first-wins inside the host with a warning logged to the result JSON. SDK surfaces as a warning. (This is a packaging bug for the user to fix.)
- **App election succeeds but the relaunch fails** → warning, originals listed.
- **Same file matches both a Kind and a fallback extension** → Kind wins; orchestrator never double-routes an artifact.

### 6.7a Companion user-facing surface: optional ITool wrapper

The typed `IArtifactPostProcessor` contract is the right shape for **automated** SDK orchestration. But there's a real user need that contract alone doesn't serve: **manual** merging from a shell, outside a `dotnet test` run. Examples:

- CI script reassembles artifacts from multiple agents into one shared folder and wants to merge after the fact, without re-running tests.
- A developer wants to re-merge with different filtering after editing one TRX by hand.
- Pipeline that uses `dotnet test --no-build` from a download step and wants to merge separately.

Each extension that ships an `IArtifactPostProcessor` is **encouraged (not required)** to also ship a thin `ITool` wrapper that delegates to the same implementation. For example, `Microsoft.Testing.Extensions.TrxReport` would ship:

- `TrxArtifactPostProcessor : IArtifactPostProcessor` — used by SDK orchestration.
- `TrxMergeTool : ITool` — user-callable, e.g.:
  ```
  dotnet run --project A.Tests.csproj -- --tool merge-trx \
      --input "./results/**/*.trx" --output ./merged.trx
  ```

Both delegate to the same shared `TrxReportEngine.MergeAsync` core. Sharing the engine guarantees the manual and automated paths produce byte-identical output.

#### Why this is additive, not (necessarily) the design

- ITool is the *user* surface; `IArtifactPostProcessor` is the *orchestration* surface. They serve different consumers.
- The orchestrator, as designed in §6.2–§6.7, never picks an `ITool` — it picks an `IArtifactPostProcessor`. So ITool's tool-name uniqueness, untyped CLI, `--list-tools` visibility etc. are pure pros for the user-facing scenario and irrelevant to automation.
- Optional: an extension can ship the processor without the tool (no manual escape hatch) or vice-versa. The contracts are decoupled.
- Recommendation: ship a tool for every well-known kind (TRX, code coverage, Cobertura). That gives users the manual escape hatch automatically.

> **However** — whether `ITool` should *also* be the orchestration route (and let us avoid inventing a second reserved switch at all) is an open design question, discussed in [§10.1](#10-alternatives-considered). Because post-processing is already a non-test execution path, `--tool` is not as foreign a fit as it first appears.

### 6.8 Behavior matrix

| Scenario | Outcome |
|---|---|
| 1 module, 1 TRX | No relaunch (only 1 input; group filtered out by the `>= 2` rule). |
| 5 modules, 5 TRX, all tag `microsoft.testing.trx` | 1 relaunch, 1 merged TRX, 5 originals on disk, 1 summary line. |
| 5 modules, 4 net10.0 + 1 net8.0, all TRX | All 5 TRX go to 1 elected (most likely net10.0) app. Cross-TFM merge is fine — TRX is data. |
| Multi-TFM solution producing mixed kinds | Election grouped per Kind; 1-2 relaunches total. |
| `.xml` artifacts: 3 JUnit (kind `junit.report`) + 2 NUnit3 (kind `nunit.report`) | Two distinct groups, two separate merges, no cross-contamination. (Would have collided under pure extension matching.) |
| Module that lacks a processor for `playwright.trace` | Those files listed individually (today's behavior). |
| User on an older testfx that has no `IArtifactPostProcessor` | No advertisements → no relaunch → today's behavior end-to-end. |
| New SDK + new platform + producer that doesn't yet tag Kind | Orchestrator falls back to file extension; merge still happens. |
| Post-process child times out / crashes | Warning printed; originals listed; test run exit code preserved. |
| `dotnet test --no-build` with stale binaries | Same election; relaunch uses on-disk binary. No special handling. |

### 6.9 Failure handling

- **Manifest write failure** → orchestrator skips that job, logs warning.
- **Child startup failure (non-zero exit, no result JSON)** → orchestrator surfaces stderr tail as warning, originals stay listed.
- **Result JSON parse failure** → warning, originals stay listed.
- **Processor exception** → captured in `errors[]`, child still exits 0 unless every processor failed (then 91).
- **Cancellation** (`Ctrl+C`) during post-processing → propagate `CancellationToken`, child must honor; orchestrator stops without merging.
- **Disk full / permission denied on output** → warning, originals stay listed.

**Invariant:** post-processing never changes the test run exit code.

### 6.10 Security considerations

- Manifest is created by the orchestrator in a process-private temp directory (`Path.GetTempPath()/dotnet-test-postproc-<guid>/manifest.json`), cleaned up after use.
- Manifest contains only file paths under directories the user already had write access to — no privilege escalation surface.
- The reserved switch is local-only. There is no network path. We do not download or evaluate scripts.
- Hosts must validate inputs (the manifest could be tampered with by a local attacker who already has write access to the temp dir — but they could equally just tamper with the test binaries).
- The reserved switch name is namespaced (`--internal-post-process-…`) so it's clear to anyone reading a process command line that this is platform infrastructure, not user invocation.

### 6.11 Telemetry

The orchestrator emits one telemetry event per `dotnet test` run summarizing:
- count of jobs planned / executed / failed,
- distinct kinds (and fallback extensions) processed,
- total wall-time spent in post-processing.

No file paths, no artifact contents. Standard `dotnet test` telemetry envelope.

## 7. Implementation phasing

| Phase | Repo | Deliverable | Blocked by |
|---|---|---|---|
| 1 | testfx | `IArtifactPostProcessor` + reserved host switch + handshake field. No built-in processors. | — |
| 2 | testfx | `TrxArtifactPostProcessor` in `Microsoft.Testing.Extensions.TrxReport`. | 1 |
| 3 | microsoft/codecoverage | `CodeCoverageArtifactPostProcessor` in `Microsoft.Testing.Extensions.CodeCoverage`. | 1 |
| 4 | dotnet/sdk | Orchestration (plan, relaunch, swap-in summary). Gated on advertisement presence. | 1 |
| 5 | (optional) coverlet | `CoverletArtifactPostProcessor`. | 1 |

Phases 2–4 can ship together for first user-visible value; phase 5 is opt-in by the Coverlet team.

## 8. Testing strategy

### testfx
- Unit tests for `TrxArtifactPostProcessor`:
  - 0 inputs → returns `null`.
  - 1 input → returns `null` (no merge needed; orchestrator will keep the original).
  - N inputs same TFM → merged TRX schema-valid, counters summed, attachment URIs preserved.
  - Inputs with duplicate test ids → dedup (or namespace) deterministically.
  - Inputs containing `<UriAttachment>` with relative paths → rewritten relative to merged output.
- Integration test for the reserved host switch using a test app that registers a noop processor.
- Acceptance test: end-to-end `dotnet test` against a multi-project solution, asserting `merged.trx` exists and counters match the sum.

### SDK
- Unit tests for `ArtifactPostProcessingPlanner`:
  - Election with single app supporting everything.
  - Election with split capabilities across apps.
  - No-op when no app advertises support.
  - Coalescing of multi-kind jobs into one relaunch per app.
- Integration tests in `test/dotnet-test.Tests/MTP/` covering the behavior matrix in §6.8.
- Negative: host returns non-zero exit → run still succeeds and originals appear in summary.
- Negative: host returns malformed JSON → warning + graceful degrade.

## 9. Compatibility & migration

| SDK version | testfx version | Behavior |
|---|---|---|
| Old SDK | New testfx | Apps advertise capabilities; SDK ignores them. Behavior unchanged. |
| New SDK | Old testfx | Apps don't advertise. SDK plans nothing. Behavior unchanged. |
| New SDK | Mixed (some apps new, some old) | Only new apps participate in election. Per-kind degradation if a kind is only produced by old apps. |
| New SDK + new testfx, no MS CC processor yet | TRX merging works; `.coverage` files listed individually. |

The reserved host switch is **gated on version**: orchestrator inspects the advertised `Version` and only invokes the switch on hosts ≥ first supporting version.

## 10. Alternatives considered

The *orchestration route* — i.e. how the SDK actually invokes a process to do the merging — is the most debated part of this RFC. The reserved-switch approach in §6.2 is the current recommendation, but it is **not** obviously the only good answer. Notably, post-processing is already an **abnormal, non-test execution path**: the host starts up, skips discovery and execution entirely, does one batch of work, and exits. Once we accept that, several pre-existing "non-test" routes become legitimate candidates rather than awkward fits.

### 10.1 `ITool` as the orchestration route (not just the user wrapper)

MTP already has a sanctioned non-test execution path: `--tool <name>` runs an `ITool` instead of executing tests. Since our post-processing host *also* runs in a non-test mode, we could orchestrate by having the SDK invoke:

```
A.dll --tool internal-merge-artifacts --manifest <manifest.json>
```

where `internal-merge-artifacts` is an `ITool` shipped by the platform (or by each extension) that reads the same manifest and delegates to the registered `IArtifactPostProcessor` engines.

**Pros**
- **No new reserved host switch.** We reuse an existing, documented execution mode instead of inventing `--internal-post-process-artifacts` and threading it through option validation and the mutually-exclusive-modes table.
- **Symmetry with the user-facing escape hatch** (§6.7a): the manual `--tool merge-trx` and the automated path become the *same* mechanism, differing only in tool name and whether input comes from a manifest or CLI globs. Less surface, one code path to test.
- **Tools already have a defined lifecycle and result convention** in MTP; we'd lean on it rather than re-deriving exit-code semantics.

**Cons**
- **Tool-name uniqueness.** `ITool` names must be globally unique across all loaded extensions in a host. A platform-owned `internal-merge-artifacts` tool that *dispatches* to processors avoids per-extension name collisions, but then the tool is effectively the reserved switch wearing a different hat.
- **Untyped CLI in, file out.** Tools take a string CLI and have no structured return channel back to the parent; we'd still serialize results via the manifest's `resultPath`. So the "structured results" advantage of the typed contract is preserved by the JSON files either way — but it means `ITool` doesn't actually *remove* the manifest/result-JSON plumbing.
- **Discoverability leakage.** A real registered tool can show up in `--list-tools`. A reserved switch is trivially hidden. We'd need a convention for "internal" tools to keep it out of the user-visible tool list.

**Assessment.** This is a genuine contender and arguably the cleaner factoring, precisely because post-processing is already off the test path. The recommendation in this RFC keeps the reserved switch for now (tighter control over visibility and mutual exclusivity), but reviewers should weigh whether a single platform-owned dispatcher `ITool` is the better primitive. **This is an open decision, not a closed one.**

### 10.2 Specialized post-processing host with a live communication channel

Today's design is **fire-and-forget**: the orchestrator writes a manifest, launches a child, the child does everything and writes a result JSON, then exits. There is no live channel between the two while the child runs.

An alternative is a **specialized host** (either a dedicated mode of the test app, or a small platform-shipped host that dynamically loads the elected app's extensions) that keeps an **IPC channel** open to the orchestrator for the duration — the same kind of named-pipe/JSON-RPC channel the platform already uses for `FileArtifactMessages` during a normal run.

What a live channel *could* enable:
- **Streaming progress** for long merges (large `.coverage` sets), so `dotnet test` can render a live "merging… (3/12)" line instead of a dead-air gap.
- **Incremental result reporting** — emit each merged artifact as it completes rather than only at exit, so a crash mid-way still surfaces the artifacts that finished.
- **Mid-flight cancellation** with cooperative draining, richer than killing the process on `Ctrl+C`.
- **Re-using the existing message bus** (`SessionFileArtifact`) so merged outputs flow back through the *same* reporter pipeline as in-run artifacts, rather than a bespoke result-JSON swap in the SDK (§6.6).

Does it matter? Honest assessment:
- For **TRX**, merges are sub-second; the channel buys essentially nothing and costs a persistent connection + handshake.
- For **large coverage** sets it might matter for UX (progress) and for partial-failure resilience.
- The biggest *architectural* attraction is **uniformity**: post-processed artifacts would re-enter through the same IPC artifact pipeline, removing the special-case `SnapshotArtifacts`/`RemoveArtifacts`/`ArtifactAdded` swap on the SDK side. That's a real simplification of the orchestrator, at the cost of a more complex host mode.

**Assessment.** Deferred, not rejected. The fire-and-forget manifest model is the smallest thing that works and is the right v1. A persistent specialized host with a live channel is the natural evolution **if** we find (a) merges long enough that progress/partial-results matter, or (b) that the SDK-side artifact-swap special case becomes a maintenance burden. We should keep the manifest/result-JSON contract versioned (`schemaVersion`) so a future live-channel host can be introduced without breaking the typed `IArtifactPostProcessor` contract — the processor contract is independent of *how* it is hosted.

### 10.3 Other rejected approaches

1. **Use `ITool` as the *only* mechanism with an untyped, per-extension tool (no typed contract).** Rejected: tool names need to be globally unique across all loaded extensions, the CLI surface is string-typed, and there's no structured way to return per-input/per-output results back to the orchestrator without re-introducing the manifest/result JSON. (Contrast with §10.1, which keeps the typed `IArtifactPostProcessor` and only debates the *invocation* primitive.)
2. **Reuse `--server` (long-lived JSON-RPC) mode.** Rejected for v1: lifecycle complexity, untested-in-server-mode extensions, holding a process across siblings is fragile. (§10.2 is the lighter-weight, post-run-only version of this idea.)
3. **Standalone "merger testhost" binary shipped by MTP.** Rejected: nowhere to source extension assemblies dynamically — same footgun as `dotnet-coverage merge` global tool installation. (A platform host that *dynamically loads the elected app's extensions*, per §10.2, is the variant that sidesteps this.)
4. **SDK-internal TRX merger.** Rejected: forces two homes for the same code (SDK + testfx), creates drift risk, can't extend to `microsoft.codecoverage`/`coverlet.cobertura` for the extension-agnostic story.

## 11. Open questions

1. **Orchestration primitive: reserved switch vs. dispatcher `ITool` vs. specialized host** — see §10.1 / §10.2. This is the single biggest open decision and the main reason this RFC is "Under discussion".
2. **Should processors be allowed to *consume and delete* their inputs?** Current design says no (non-destructive). If we ever want to garbage-collect, we'd add an opt-in.
3. **Where do `IArtifactPostProcessor` registrations live?** Same `TestApplicationBuilder.RegisterTestApplicationExtensions` flow as everything else, or a dedicated `RegisterArtifactPostProcessors`? Recommend the former for consistency.
4. **Per-TFM segregation:** for `microsoft.codecoverage`, should we merge cross-TFM or per-TFM? Likely per-Kind policy decided by the processor itself based on the input metadata.
5. **Merging *attachments* referenced by TRX.** TRX attachments use absolute paths today, so cross-process merge keeps them resolvable. If MTP later switches to relative paths, the TRX merger needs to copy attachments alongside the merged TRX. Out of scope for v1.
6. **Diagnostic flags pass-through.** Should the orchestrator pass `--diagnostic-output-directory` or other diagnostic flags through to the post-process child? Likely yes for debuggability.
7. **Two extensions advertising the same Kind** — first-wins or fail-fast? Recommend warn-and-first-wins to keep the "never fail the run" invariant.
8. **Kind vocabulary governance.** Open string with reverse-DNS convention is enough to ship, but we should document a short well-known list (TRX, MS CC, Cobertura, JUnit, NUnit3) in the platform docs so producers don't accidentally fork on capitalisation or punctuation (e.g. `microsoft.codecoverage` vs `microsoft.code-coverage`).
9. **Should `Kind` be a typed wrapper** (e.g. `readonly record struct ArtifactKind(string Value)`) rather than `string`? Trades cosmetic safety for one more type in the public surface. Lean towards plain `string` initially.
10. **Versioning a Kind.** If a format evolves incompatibly, does the producer bump the Kind (`microsoft.codecoverage.v2`) or add a separate version field? Recommend bumping the Kind — keeps matching trivial, removes the need for processors to negotiate versions.
11. **Final exit-code values** for the reserved host mode (§6.2) so they don't overlap existing MTP exit codes.

## 12. Decisions summary (what this RFC currently proposes)

- **Extension contract:** `IArtifactPostProcessor` keyed on `SupportedKinds` (reverse-DNS strings) with `SupportedFileExtensionsFallback` for legacy producers + `ProcessAsync`. *(Stable regardless of how the host is invoked.)*
- **Producer side:** `SessionFileArtifact` / `FileArtifactMessages` gain an optional `Kind` string. Producers tag their outputs when they upgrade.
- **Host mode:** reserved hidden switch `--internal-post-process-artifacts <manifest.json>` with JSON manifest in / JSON result out. *(Under discussion vs. a dispatcher `ITool` or a specialized host with a live channel — §10.1/§10.2.)*
- **Discovery:** capabilities advertised in `HandshakeMessage` (two semicolon-separated lists: kinds + legacy extensions).
- **Routing (host & orchestrator):** Kind first, then file-extension fallback for untagged inputs; an artifact is never double-routed.
- **Election:** group artifacts by Kind first, then by file extension for any untagged remainder; only groups with ≥ 2 inputs are planned; prefer the app that produced the most of that group, tie-break by TFM then path. Multiple elected apps per run is expected and intentional; coalesce only when the same app wins multiple groups.
- **User-facing wrapper:** each extension shipping a processor is encouraged to also ship an `ITool` that delegates to the same engine, giving users a manual escape hatch (e.g. `--tool merge-trx`).
- **CLI surface:** no new public flags on `dotnet test`. Implicit, non-destructive, never-fails-the-run.
- **Phasing:** ship testfx contract + TRX processor (+ optional TRX tool) first; SDK consumer second; MS CC processor on its own cadence; others opt in.

## 13. Appendix A — illustrative code sketches

### A.1 `TrxArtifactPostProcessor` (in Microsoft.Testing.Extensions.TrxReport)

```csharp
internal sealed class TrxArtifactPostProcessor : IArtifactPostProcessor
{
    public string Uid => "Microsoft.Testing.Extensions.TrxReport.PostProcessor";
    public string DisplayName => "TRX report merger";
    public string Description => "Merges TRX reports produced by Microsoft.Testing.Extensions.TrxReport.";
    public string Version => "1.0.0";

    public IReadOnlyList<string> SupportedKinds { get; } = ["microsoft.testing.trx"];
    public IReadOnlyList<string> SupportedFileExtensionsFallback { get; } = [".trx"];

    // Inherited from IExtension.
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (inputs.Count < 2)
        {
            return null;
        }

        string output = Path.Combine(outputDirectory, "merged.trx");
        await TrxReportEngine.MergeAsync(
            inputs.Select(i => i.Path).ToArray(),
            output,
            cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            Path: output,
            Kind: "microsoft.testing.trx",
            DisplayName: "TRX report (merged)",
            Description: $"{inputs.Count} TRX files merged");
    }
}
```

### A.2 Orchestrator planner (in dotnet/sdk)

```csharp
internal static class ArtifactPostProcessingPlanner
{
    public static PostProcessingPlan Plan(
        IReadOnlyList<TestRunArtifact> artifacts,
        IReadOnlyDictionary<string, AppCapabilities> apps)
    {
        // 1. Primary grouping: by Kind.
        var byKind = artifacts
            .Where(a => a.Kind is not null)
            .GroupBy(a => a.Kind!, StringComparer.Ordinal);

        // 2. Fallback grouping: untagged artifacts by file extension.
        var byExtFallback = artifacts
            .Where(a => a.Kind is null)
            .GroupBy(a => Path.GetExtension(a.Path), StringComparer.OrdinalIgnoreCase);

        var groups = byKind.Select(g => (matchType: "kind", key: g.Key, items: g.ToList()))
            .Concat(byExtFallback.Select(g => (matchType: "ext", key: g.Key, items: g.ToList())))
            .Where(g => g.items.Count >= 2);

        var jobsByApp = new Dictionary<string, PostProcessingJob>();
        foreach (var (matchType, key, items) in groups)
        {
            var candidates = matchType == "kind"
                ? apps.Values.Where(a => a.SupportedKinds.Contains(key, StringComparer.Ordinal))
                : apps.Values.Where(a => a.SupportedExtensionsLegacy.Contains(key, StringComparer.OrdinalIgnoreCase));

            var electedList = candidates.ToList();
            if (electedList.Count == 0)
            {
                continue;
            }

            var elected = electedList
                .OrderByDescending(a => items.Count(i => i.Assembly == a.ModulePath))
                .ThenByDescending(a => TfmOrder(a.TargetFramework))
                .ThenBy(a => a.ModulePath, StringComparer.Ordinal)
                .First();

            if (!jobsByApp.TryGetValue(elected.ModulePath, out var job))
            {
                job = new PostProcessingJob(elected, new List<InputArtifact>());
                jobsByApp[elected.ModulePath] = job;
            }

            job.Inputs.AddRange(items.Select(ToInputArtifact));
        }

        return new PostProcessingPlan(jobsByApp.Values.ToList());
    }
}
```

---

*End of RFC.*
