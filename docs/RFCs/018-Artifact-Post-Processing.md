# RFC 018 - Artifact post-processing for `dotnet test` (MTP)

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

> **Renumber note.** This design was originally drafted as "RFC 017" in [microsoft/testfx#9187](https://github.com/microsoft/testfx/pull/9187). `017` has since been taken by `017-TestHost-Launcher.md` (merged in [#9349](https://github.com/microsoft/testfx/pull/9349)), so this document is renumbered to **018**.

## Summary

Introduce an **artifact post-processing** mechanism for Microsoft.Testing.Platform (MTP) so that, after a multi-module `dotnet test` run, well-known artifacts of the same kind (TRX reports, `.coverage` files, Cobertura, custom formats) can be **consolidated into single files** instead of being listed one-per-module.

The design has two layers that ship in order:

1. **A typed, invocation-agnostic engine contract `IArtifactPostProcessor`** plus a **user-facing `ITool`** per well-known kind (e.g. `merge-trx`). Both delegate to one shared merge engine. This layer delivers value immediately, is discoverable (listed under "Registered tools:" by `--info`), user-runnable from a shell, and needs **zero SDK or protocol changes** (it does need a small platform API change — promoting `ITool` to public, see §7.2).
2. **SDK auto-orchestration in `dotnet test`.** After all modules finish, the SDK elects an already-built app per artifact kind and relaunches it once to perform the merge, then swaps the merged output into the run summary. The election is computed **in-memory from data the SDK already has** (handshake-advertised capabilities plus the artifacts that already streamed live over the `dotnet-test` pipe), and the merged outputs flow **back over that same existing pipe** so they re-enter the normal reporter path.

It addresses [dotnet/sdk#47613](https://github.com/dotnet/sdk/issues/47613) and is related to [#7345](https://github.com/microsoft/testfx/issues/7345), [#7471](https://github.com/microsoft/testfx/issues/7471), and [#6586](https://github.com/microsoft/testfx/issues/6586).

> **Why this shape.** Two facts about the current codebase drive the recommendation and distinguish it from earlier drafts:
>
> - MTP already has a sanctioned non-test execution path — `--tool` — with a defined lifecycle, exit-code convention, and shipping precedent (`TrxCompareTool`/`ms-trxcompare`). Post-processing is itself a non-test path, so a tool is a natural fit and doubles as the user-facing escape hatch.
> - `dotnet test` already holds a **persistent named pipe** to every MTP host for the whole run (`DotnetTestConnection`), and artifacts **already stream live** to the SDK over it (`FileArtifactMessages`). The SDK therefore already has the full, attributed input list before post-processing starts — so election needs no dedicated "collect" phase, and merged outputs can re-enter through the same channel rather than a bespoke result-file swap.

## 1. Problem statement

When `dotnet test` runs N test modules under MTP, each module process produces its own artifacts (TRX files, `.coverage` files, attachments, custom files). The SDK's terminal output currently just lists every artifact path it received via `FileArtifactMessages`. On multi-module solutions (e.g. the `microsoft/testfx` repo itself) this produces dozens of TRX paths and zero consolidated view, and downstream consumers (Azure DevOps `PublishTestResults`, `ReportGenerator`, CI dashboards) have to either glob every file or rely on out-of-band merging tools. This is exactly the pain reported in [#7345](https://github.com/microsoft/testfx/issues/7345).

VSTest historically solved this with a **two-phase** flow driven by `dotnet test`:

1. **Collect** — `dotnet test` spawns `vstest.console` with `--artifactsProcessingMode-collect --testSessionCorrelationId:<guid>`. Each invocation drops its artifacts into a temp folder tagged with that correlation id. (See `microsoft/vstest`: `ArtifactProcessingCollectModeProcessor.cs`.)
2. **Post-process** — after all collect invocations finish, `dotnet test` invokes `vstest.console` **a second time** with `--artifactsProcessingMode-postprocess --testSessionCorrelationId:<same-guid>`. That triggers `ArtifactProcessingPostProcessModeProcessor`, which discovers the tagged temp folder and runs the merge. (See `dotnet/sdk`: `src/Cli/dotnet/Commands/Test/VSTest/TestCommand.cs` — `artifactsPostProcessArgs`.)

MTP has no equivalent today, and it does **not** need to reproduce VSTest's disk-tagging plumbing, because the MTP `dotnet test` orchestrator is a single long-lived process that already receives every artifact live over the pipe. The design below keeps the useful VSTest idea (relaunch a process that has the right extensions loaded to do the merge) and drops the parts MTP renders unnecessary (correlation ids, temp-folder tagging, implicit disk-layout coupling).

## 2. Goals

1. **Consolidate artifacts** of well-known formats (TRX, code-coverage, Cobertura, custom) into single files at the end of a `dotnet test` run.
2. **Extension-agnostic SDK**: SDK must not link or special-case any specific extension (TRX, Microsoft.CodeCoverage, Coverlet, AltCover, future formats).
3. **A user-runnable manual path**: merging must also be possible from a shell, outside a `dotnet test` run, as a discoverable tool.
4. **No new user-visible CLI option** for the automatic path. Merging "just happens" when >= 2 mergeable artifacts of the same kind are produced and at least one running app advertises a processor.
5. **Backward compatible**: older test hosts (without the new contract) keep today's behavior. New SDK on old hosts and old SDK on new hosts both work unchanged.
6. **Non-destructive**: per-module artifacts remain on disk where the producing host wrote them. Merged output is additional, not a replacement.
7. **Never fail the run** because of a post-processing failure.

## 3. Non-goals

- Real-time merging during the run. Post-processing happens after all module runs complete.
- Cross-`dotnet test` invocation merging (correlating artifacts across separate `dotnet test` calls). VSTest needed this because the orchestrator process was short-lived per invocation; MTP `dotnet test` owns the full session.
- IDE integration. This design is for the CLI orchestrator; IDE adapters can layer on it later if useful.
- Defining a new artifact format. We merge existing formats; we do not invent one.

## 4. Glossary

| Term | Meaning |
| --- | --- |
| **Test app** | An MTP host process (the user's test project after build/publish). |
| **Module** | The test app binary the user references (`*.dll` or AOT exe). |
| **Artifact** | Any file produced by an extension and reported via `SessionFileArtifact` / `FileArtifactMessages`. |
| **Orchestrator** | The `dotnet test` process in the SDK that spawns test apps and consumes their IPC. |
| **Engine** | The format-specific merge implementation (e.g. a new `TrxReportEngine` file-merge entry point). Invocation-agnostic. |
| **Post-processor** | An `IArtifactPostProcessor` extension that wraps an engine for one or more artifact kinds. |
| **Dispatcher tool** | A platform-owned internal `ITool` that routes manifest inputs to the registered post-processors in a relaunched host. |
| **User tool** | A per-extension `ITool` (e.g. `merge-trx`) that lets a user run the same engine from a shell. Requires `ITool` to be promoted to public (§7.2). |
| **Election** | The orchestrator's decision of which test app to relaunch to perform a given post-processing job. |
| **Kind** | A producer-asserted, reverse-DNS identifier for an artifact format (e.g. `microsoft.testing.trx`). Primary matching key. |

## 5. Recommended design at a glance

The recommendation is a **layered** design. The layers are independent, so the parts that are unambiguously right can ship first, and the one genuinely contested decision (how the SDK spawns the merge) is deferred until the shared engine exists.

| Piece | What it is | Depends on protocol change? | Depends on SDK change? | Ships in |
| --- | --- | --- | --- | --- |
| `IArtifactPostProcessor` engine contract | Typed, invocation-agnostic merge contract (experimental-gated) | No | No | Phase 1 |
| Shared engine (new `TrxReportEngine` file-merge path) | The actual merge implementation (new logic) | No | No | Phase 1 |
| User tool (`merge-trx`) | `ITool` (needs `ITool` promoted to public), discoverable via `--info`, runnable from a shell | No | No | Phase 1 |
| `Kind` tagging on `SessionFileArtifact` / `FileArtifactMessages` | Optional producer metadata | Additive (skip-unknown) | No | Phase 2 |
| Handshake capability advertisement | Two extra semicolon-joined strings in `HandshakeMessage` | Additive | No | Phase 2 |
| In-memory election | SDK computes plan from data it already has | No | Yes | Phase 2 |
| Dispatcher tool + pipe re-entry | SDK relaunches elected host, merged artifacts flow back over the existing pipe | Reuses existing messages | Yes | Phase 2 |

**Phase 1 is the recommended starting point for implementation.** It delivers the user-visible value in [#7345](https://github.com/microsoft/testfx/issues/7345) (a real, single merged TRX), is fully testable inside testfx with no SDK dependency, is the discoverable/non-hidden path users asked for, and builds the shared engine that every later layer reuses.

**Why a tool as the orchestration primitive (over a new reserved switch).** Post-processing is already a non-test execution path. `--tool` is the platform's existing, documented non-test path — it has a defined lifecycle, an exit-code convention, and shipping precedent (`TrxCompareTool` registers `ms-trxcompare`). Reusing it means the manual and automated routes are the *same mechanism*, we avoid inventing `--internal-post-process-artifacts` and threading it through the mutually-exclusive-modes table, and we keep one code path to test. Note that `ITool`, `IToolsManager.AddTool`, and `TestApplicationBuilder.Tools` are `internal` today (the `ms-trxcompare` precedent only compiles via an `InternalsVisibleTo` grant to the first-party TRX assembly); Phase 1a must promote them to public (experimental-gated) so third-party extensions (Phase 3/4) can ship tools at all. The only real drawback — a registered tool is listed under "Registered tools:" by `--info` — is solved by an "internal tool" flag mirroring the existing `IsHidden` used for hidden command-line options.

**Why results flow back over the existing pipe (over a bespoke result JSON swap).** `dotnet test` already keeps a persistent `DotnetTestConnection` to each host and already receives `FileArtifactMessages` live. If the relaunched dispatcher reports the merged artifact via the *same* `FileArtifactMessage`, it re-enters the normal reporter/summary pipeline. The SDK still collapses the consumed originals and surfaces the merged file (via `RemoveArtifacts`/`ArtifactAdded`), but through that normal path — what the pipe removes is the *bespoke result-JSON parse/swap*, not those reporter calls. Reusing the pipe is also the natural hook for future UI (live "merging..." progress) without inventing a new channel.

## 6. High-level architecture

```text
+--------------------------------------------------------------------------+
|                         dotnet test (SDK orchestrator)                   |
|                                                                          |
|   1. Spawn test app A (net10.0)  --> artifacts stream live over pipe:    |
|        a.trx (kind microsoft.testing.trx), a.coverage (microsoft.cc)     |
|   2. Spawn test app B (net10.0)  --> b.trx, b.coverage                   |
|   3. Spawn test app C (net8.0)   --> c.trx                               |
|                                                                          |
|   Each host's handshake advertised: SupportedPostProcessorKinds.         |
|   Each artifact arrived attributed (kind + producing module + tfm).      |
|                                                                          |
|   After all runs complete (election is pure in-memory, no new call):     |
|                                                                          |
|   4. Elect the fewest apps that cover all mergeable kinds:               |
|        {trx, coverage} both covered by app A  -->  relaunch A once       |
|                                                                          |
|   5. Relaunch app A once:                                                |
|        A.dll --tool internal-merge-artifacts --manifest <m.json>         |
|             --server dotnettestcli --dotnet-test-pipe <same-style pipe>  |
|                                                                          |
|         +------------------------------------------------+               |
|         |   Test app A, dispatcher tool mode             |               |
|         |     reads manifest -> routes by Kind ->        |               |
|         |     TrxArtifactPostProcessor.ProcessAsync      |               |
|         |     CodeCoverageArtifactPostProcessor.Process  |               |
|         |   reports merged.trx / merged.coverage back    |               |
|         |   over the pipe as FileArtifactMessage, exits  |               |
|         +------------------------------------------------+               |
|                                                                          |
|   6. Merged artifacts re-enter the reporter; originals of the merged     |
|      groups are collapsed in the summary.                                |
|                                                                          |
|   Final summary:                                                         |
|     Merged TRX report: TestResults/merged.trx                            |
|     Merged coverage:   TestResults/merged.coverage                       |
+--------------------------------------------------------------------------+
```

## 7. Detailed design

### 7.1 Typed engine contract (testfx)

Package: `Microsoft.Testing.Platform` (or a new abstractions package if we want to ship without forcing a platform major).
Namespace: `Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing`.

The contract is **invocation-agnostic**: nothing in it assumes whether a user tool, a dispatcher tool, or (a future) live-channel host called it. That independence is what lets the orchestration decision evolve without reworking the contract, and it is why the contract can ship in Phase 1.

The contract is introduced under MTP's **experimental** diagnostic-id mechanism (like `ITestHostLauncher` in the TestHost Launcher RFC, 017) so it is not frozen before the SDK consumer in Phase 2 lands.

```csharp
[Experimental("MTPEXP", UrlFormat = "https://aka.ms/mtp/experimental/{0}")]
public interface IArtifactPostProcessor : IExtension
{
    // Inherited from IExtension: Uid, Version, DisplayName, Description, Task<bool> IsEnabledAsync().

    /// <summary>
    /// Reverse-DNS identifiers of the artifact kinds this processor can consume.
    /// Open vocabulary; recommended convention matches MTP extension Uids
    /// (e.g. "microsoft.testing.trx", "microsoft.codecoverage", "coverlet.cobertura").
    /// This is the primary matching key used by the orchestrator and the dispatcher.
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
    /// Called once with all matching input artifacts. Implementations must:
    ///  - Treat <paramref name="inputs"/> as read-only (do not delete the source files).
    ///  - Produce zero or one merged output written under <paramref name="outputDirectory"/>.
    ///  - Be deterministic and idempotent -- the orchestrator may retry on transient failures.
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

> **Public-API note (no `init`).** testfx bans `init` accessors on *new* public API. C# positional records synthesize `{ get; init; }`, so if `InputArtifact` / `ProcessedArtifact` are public they must instead be declared with get-only auto-properties and an explicit constructor (the positional form above is shorthand for readability only). Alternatively keep the DTOs `internal` and expose only the interface. This must be settled at API-review time, not discovered there.

#### Why a Kind, not just file extension?

- **File extensions collide across formats.** `.xml` is JUnit, NUnit3, custom; `.json` is everywhere. The orchestrator should never need to inspect content to disambiguate.
- **Producer-asserted identity** is more reliable than orchestrator-guessed. The TRX writer knows it produced a TRX; the orchestrator shouldn't have to encode that knowledge.
- **Extension-less / compound artifacts.** A future processor might consume a folder of Playwright traces, a `.zip` of dumps, or a sqlite DB with a custom suffix — none of those round-trip cleanly through extension matching.
- **Versioning.** If a format evolves incompatibly, the producer can switch from `microsoft.codecoverage` to `microsoft.codecoverage.v2` without changing file naming, and the old processor stops matching cleanly.

#### Notes

- **Kind is primary, file extension is fallback.** During the transition period (older producers don't tag Kind yet) the orchestrator *and* the dispatcher match by Kind first, then by file extension for any remaining untagged inputs. Long-term, file-extension fallback can be deprecated.
- **Open vocabulary, namespaced.** No central registry. Convention: reverse-DNS strings owned by the producing component (e.g. `microsoft.testing.trx`, `microsoft.codecoverage`, `coverlet.cobertura`, `playwright.trace`). Same hygiene rule as extension `Uid`s.
- **Returning `null`** means "I looked but there's nothing to do" (e.g. < 2 inputs). The orchestrator then leaves the originals visible.
- **Idempotent / deterministic.** The contract is intentionally pure-functional from `inputs` -> `output`. Implementations must not stash state across calls.
- **`IExtension` base** gives us `Uid`, `Version`, `DisplayName`, `Description`, and `IsEnabledAsync()` for free, plus integration with the existing extension manifest tooling. (`IsEnabledAsync()` is therefore *not* a bespoke member of this contract; the sketch in Appendix A.1 implements the inherited member.)

#### The producer/post-processor co-location invariant

The election algorithm (§7.5) leans on a structural fact worth stating explicitly: **the producer and the post-processor for a given Kind ship in the same extension assembly.** `Microsoft.Testing.Extensions.TrxReport` contains both `TrxReportGenerator` (producer) and `TrxArtifactPostProcessor` (post-processor). Consequently, *any app that produced an artifact of a Kind necessarily has the post-processor for that Kind loaded* (once upgraded). This is why "elect an app that advertises the needed kinds" always has a valid candidate set — producing implies capability. The handshake advertisement (§7.4) is what makes this explicit to the orchestrator rather than inferred.

### 7.2 Shared engine and the user-facing tool (Phase 1)

> **Platform prerequisite.** `ITool`, `IToolsManager.AddTool`, and `TestApplicationBuilder.Tools` are `internal` today. The `ms-trxcompare` precedent only works because `Microsoft.Testing.Extensions.TrxReport` has an `InternalsVisibleTo` grant from the platform. First-party TRX (Phase 1b) can therefore ship immediately, but the user-tool story for **third-party** extensions (Phase 3 code coverage, Phase 4 coverlet) and Goal 2/3 require promoting these members to **public** (experimental-gated, recorded in `PublicAPI.Unshipped.txt`). This is a Phase 1a platform work item.

Every extension that ships an `IArtifactPostProcessor` factors the actual merge into a **shared engine** and ships two thin wrappers over it:

- `TrxArtifactPostProcessor : IArtifactPostProcessor` — used by SDK auto-orchestration.
- `TrxMergeTool : ITool` — user-callable from a shell:

```text
dotnet run --project A.Tests.csproj -- --tool merge-trx \
    --input "./results/**/*.trx" --output ./merged.trx
```

Both delegate to the same `TrxReportEngine` file-merge core, so the manual and automated paths produce byte-identical output. Note this merge core is **new logic**: today `TrxReportEngine` is an instance class whose only entry point (`GenerateReportAsync`) *builds* a TRX from in-memory results — it does not merge existing TRX *files*. Phase 1a adds a file-parse-and-merge path (shared by both wrappers).

This is the piece that satisfies Goal 3 and the reason Phase 1 is worth shipping on its own:

- **Users can do it themselves.** It is a real tool, listed under "Registered tools:" by `--info` (there is no `--list-tools` switch today), not a hidden feature. CI scripts that reassemble artifacts from multiple agents, `--no-build` download-then-merge pipelines, and "re-merge after hand-editing one TRX" all work without re-running tests.
- **No SDK and no protocol dependency.** Beyond the Phase 1a `ITool`-visibility change, it compiles, tests, and ships entirely inside testfx.
- **It is the foundation.** Every later layer (the dispatcher tool, the SDK election) reuses the exact same engine, so building the tool first de-risks everything downstream.

Recommendation: ship a user tool for every well-known kind (TRX first, then code coverage, Cobertura). An extension may ship the processor without the tool, or vice-versa — the two wrappers are decoupled.

### 7.3 Producer-side Kind tagging (Phase 2)

`SessionFileArtifact` (and the `FileArtifactMessages` IPC contract) gain an optional `Kind` string. Today's producers (`TrxReportGenerator`, MS CC, etc.) start tagging their outputs when they upgrade — fully backward compatible: a missing Kind just means "use the fallback rules".

Concretely, the `FileArtifactMessage` IPC record gains a `Kind` field (a new field id in `FileArtifactMessageFieldsId`, e.g. `Kind = 7`). The existing serializer already skips unknown field ids on read, so an old SDK reading a new host, or a new SDK reading an old host, both degrade cleanly.

### 7.4 Handshake capability advertisement (Phase 2)

Each test app advertises the kinds (and legacy file extensions) it can post-process so the orchestrator can elect. `HandshakeMessage` is already a `Dictionary<byte, string>` whose values are frequently semicolon-joined lists (e.g. `SupportedProtocolVersions` = `"1.0.0;1.1.0;1.2.0;1.3.0"`), so this is additive and cheap:

```text
SupportedPostProcessorKinds:            "microsoft.testing.trx;microsoft.codecoverage"
SupportedPostProcessorExtensionsLegacy: ".trx;.coverage"
```

Two new optional handshake properties. Reverse-DNS kinds never contain `;`, so the separator is safe. Missing fields = empty sets = "this app has no post-processors"; the orchestrator handles that gracefully. This is preferred over a new dedicated message because it piggybacks on the handshake that already arrives first and avoids an extra round-trip.

### 7.5 Election is computed in-memory (no collection phase)

A key simplification over earlier drafts: **the orchestrator needs no dedicated round-trip to plan post-processing.** By the time the last module exits, the SDK already holds both election inputs, both gathered during the normal run:

- **Capabilities** — the per-module handshake (§7.4) arrives first on each `DotnetTestConnection` and carries `ModulePath`, `ExecutionId`, `InstanceId`, `Architecture`, `Framework`.
- **Artifacts** — `FileArtifactMessages` already stream live during the run and correlate to the producing module via `ExecutionId`/`InstanceId`. With the `Kind` field from §7.3, every artifact is fully attributed.

So election is a pure local computation; the only remaining process spawn is the merge itself. Merging cannot be folded into the initial run: each host sees only its own artifacts, and the SDK's global view exists only *after* the hosts have exited.

Election objective: **relaunch the fewest apps that cover all mergeable kinds** (a tiny set-cover), preferring apps that produced the artifacts in question.

```text
plan = []
groups = []

# 1. Group artifacts that have a Kind by their Kind.
for kind in unique(a.kind for a in artifacts if a.kind != null):
    groups.append(("kind", kind, [a for a in artifacts if a.kind == kind]))

# 2. Group remaining (untagged) artifacts by file extension as a fallback.
untagged = [a for a in artifacts if a.kind == null]
for ext in unique(Path.GetExtension(a.path) for a in untagged):
    groups.append(("ext", ext, [a for a in untagged if Path.GetExtension(a.path) == ext]))

# 3. Only groups with >= 2 inputs are worth merging.
groups = [g for g in groups if len(g.items) >= 2]

# 4. Candidate apps per group (must advertise the key AND be arch-compatible
#    for binary kinds -- see section 7.12).
for g in groups:
    g.candidates = [app for app in apps if advertises(app, g) and arch_ok(app, g)]
groups = [g for g in groups if g.candidates]

# 5. Greedy minimal set-cover: repeatedly pick the app that covers the most
#    still-uncovered groups; tie-break by "produced the most inputs across
#    those groups", then highest TFM, then deterministic path.
while any(not g.covered for g in groups):
    app = argmax(apps, key=lambda a: (
        covered_group_count(a, groups),
        produced_input_count(a, groups),
        tfm_order(a.tfm),
        lexical(a.path)))   # ascending, deterministic (matches Appendix A.3)
    assign(app, [g for g in groups if not g.covered and app in g.candidates])
return plan
```

#### Why single-winner-preferred (and not per-kind always)

An earlier draft elected independently per kind, which relaunches one app *per kind* even when a single app advertises all of them. That was justified by "the per-kind winner is more likely to have the exact extension version loaded, lowering format-drift risk." In practice, within a single `dotnet test` run, Central Package Management usually pins one extension version across all modules, so the drift risk is largely theoretical, while the extra relaunches are real process-startup cost. Minimising relaunches is the better default. Per-kind election remains a legitimate *fallback* if we ever observe real hosts where a single app cannot safely merge a kind it advertises — and it needs no contract change to layer in.

Worked example:

| Test app | TFM | Advertises kinds | Produced this run |
| --- | --- | --- | --- |
| A.dll | net10.0 | `microsoft.testing.trx`, `microsoft.codecoverage` | 4x trx, 4x coverage |
| B.dll | net10.0 | `microsoft.testing.trx`, `microsoft.codecoverage` | 1x trx, 1x coverage |
| C.dll | net8.0 | `microsoft.testing.trx` only | 1x trx |

- Groups: `microsoft.testing.trx` (6 inputs), `microsoft.codecoverage` (5 inputs).
- A covers both groups and produced the most in each -> **A wins both**; the plan relaunches **A once** with the union. (The old per-kind design would still have relaunched A twice or A+B.)

#### Edge cases

- **No app advertises a group's Kind (and no extension fallback matches)** -> no-op for that group; originals listed as today.
- **Group has only 1 input** -> not planned (the `>= 2` filter). Original listed as today; no relaunch.
- **Two processors in the same app advertise the same Kind** -> first-wins inside the dispatcher with a warning surfaced to the SDK. (Packaging bug for the user to fix.)
- **Election succeeds but the relaunch fails** -> warning; originals listed; run exit code unchanged.
- **Same file matches both a Kind and a fallback extension** -> Kind wins; an artifact is never double-routed.

### 7.6 SDK orchestration via the dispatcher tool (Phase 2)

In the dotnet/sdk repo (`src/Cli/dotnet/Commands/Test/MTP/`, not in this repo), after all modules finish and before rendering the final summary:

```csharp
// 1. The SDK already has every artifact (streamed live) and every module's
//    advertised capabilities (from the handshake). No extra round-trip.
PostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
    output.SnapshotArtifacts(),                 // attributed inputs, in memory already
    testHandler.GetAdvertisedCapabilities());   // kinds + legacy extensions, per module

// 2. Execute jobs (sequential is fine; N is tiny).
foreach (PostProcessingJob job in plan.Jobs)
{
    // Relaunch the elected app in dispatcher-tool mode, connected to a fresh
    // dotnet-test pipe so merged artifacts flow back as FileArtifactMessage.
    await PostProcessingHostInvoker.InvokeAsync(
        executableToRelaunch: job.ElectedApp.Path,
        toolName: "internal-merge-artifacts",
        manifestPath: job.WriteManifest(),
        onMergedArtifact: merged =>
        {
            // Re-enters the normal reporter path; collapse the consumed originals.
            output.RemoveArtifacts(job.InputsFor(merged.Kind));
            output.ArtifactAdded(outOfProcess: true, path: merged.Path, /* ... */);
        },
        onError: err => output.Warning($"Post-processing of {err.ProcessorUid}: {err.Message}"),
        cancellationToken);
}

// 3. Render the summary (one TRX, one .coverage, etc.).
output.TestExecutionCompleted(DateTimeOffset.Now, exitCode);
```

The relaunched host runs a **platform-owned dispatcher `ITool`** (`internal-merge-artifacts`), which:

1. Skips discovery and test execution (it is a tool host, not a test host).
2. Resolves all registered `IArtifactPostProcessor` instances exactly as any extension.
3. Reads the manifest and routes inputs to processors **by Kind first, then by file-extension fallback** for untagged inputs. An input is never routed to more than one processor.
4. Calls each matched processor's `ProcessAsync` once.
5. Reports each merged `ProcessedArtifact` back over the connected `dotnet-test` pipe as a `FileArtifactMessage`, and surfaces per-processor errors, then exits.

The dispatcher tool is marked **internal** (a flag mirroring command-line `IsHidden`) so it is not listed under "Registered tools:" by `--info`. A user would never type `--tool internal-merge-artifacts --manifest ...`; the non-hidden manual value comes entirely from the per-extension user tools in §7.2, which ship regardless of this decision.

> **Composition note (main implementation task).** Today `--tool` mode and `--server dotnettestcli --dotnet-test-pipe` mode are built as separate hosts (`ToolsTestHost` vs the normal path that wires `DotnetTestConnection`). Making a tool host *also* establish the `dotnet-test` pipe so it can emit `FileArtifactMessage` is the central platform-side task for Phase 2. If that composition proves costly, the transitional fallback in §7.7 (a manifest-in/result-JSON-out dispatcher with an SDK-side swap) lets Phase 2 land without it, and the pipe re-entry becomes a follow-up.

### 7.7 Manifest (transitional) and why it is largely redundant

The dispatcher receives its inputs as a JSON manifest passed as a tool argument. The manifest is a **transitional convenience**, not new state: the SDK already has this exact data in memory (§7.5), so the manifest is a serialization of information the SDK received live. It exists only because passing a file path as a tool argument is the simplest way to hand a batch of inputs to a relaunched process without adding an SDK->host request capability to the pipe (which does not exist today — the pipe is host->SDK for data, with only the handshake carrying SDK->host response data).

Manifest (orchestrator -> dispatcher):

```jsonc
{
  "schemaVersion": 1,
  "outputDirectory": "C:/path/to/TestResults",
  "inputs": [
    {
      "path": "C:/.../A/TestResults/A_net10.0_...trx",
      "kind": "microsoft.testing.trx",
      "producingTestModule": "A.dll",
      "targetFramework": "net10.0",
      "architecture": "x64",
      "executionId": "8c5f..."
    },
    {
      "path": "C:/.../C/TestResults/legacy.trx",
      "kind": null,                       // legacy producer didn't tag -- routed via extension fallback
      "producingTestModule": "C.dll",
      "targetFramework": "net8.0",
      "architecture": "x64",
      "executionId": "1f02..."
    }
  ]
}
```

**Preferred result path: over the pipe.** In the recommended design the dispatcher reports each merged artifact back as a `FileArtifactMessage`, so there is no separate result file and no SDK-side result-JSON parsing. The SDK correlates the incoming merged artifact to the job it dispatched (it knows which inputs it sent) and collapses the originals.

**Transitional result path: result JSON.** If the pipe-composition task in §7.6 is deferred, the dispatcher writes a result JSON (path supplied in the manifest) and the SDK performs the swap. This keeps the same typed contract and the same manifest; only the *return channel* changes. Keeping the manifest/result schema `schemaVersion`-versioned means the return channel can switch from files to the pipe without touching `IArtifactPostProcessor`.

Dispatcher exit codes (final values to be finalized so they don't overlap existing MTP `ExitCode` values — see §12 Q11):

| Code | Meaning |
| --- | --- |
| 0 | All processors completed (individual processors may have returned `null`). |
| (tbd) | Manifest invalid / unreadable. |
| (tbd) | One or more processors threw. Details surfaced as warnings. |
| (tbd) | Host couldn't load extensions. |

### 7.8 Behavior matrix

| Scenario | Outcome |
| --- | --- |
| 1 module, 1 TRX | No relaunch (only 1 input; group filtered out by the `>= 2` rule). |
| 5 modules, 5 TRX, all tag `microsoft.testing.trx` | 1 relaunch, 1 merged TRX, 5 originals on disk, 1 summary line. |
| 5 modules, mixed TRX + coverage, one app covers both | 1 relaunch (set-cover), 1 merged TRX + 1 merged coverage. |
| `.xml` artifacts: 3 JUnit (kind `junit.report`) + 2 NUnit3 (kind `nunit.report`) | Two distinct groups, two merges, no cross-contamination (would have collided under pure extension matching). |
| Module lacks a processor for `playwright.trace` | Those files listed individually (today's behavior). |
| Older testfx with no `IArtifactPostProcessor` | No advertisements -> no relaunch -> today's behavior end-to-end. |
| New SDK + new platform + producer not tagging Kind yet | Orchestrator falls back to file extension; merge still happens. |
| Post-process child times out / crashes | Warning printed; originals listed; run exit code preserved. |
| Binary `.coverage`, elected app arch-incompatible | Group not planned for that app; see §7.12. |
| User runs `--tool merge-trx` manually | Works standalone; identical output to the automated path (shared engine). |

### 7.9 Failure handling

- **Manifest write failure** -> orchestrator skips that job, logs warning.
- **Child startup failure (non-zero exit, no merged artifact / no result)** -> orchestrator surfaces stderr tail as warning; originals stay listed.
- **Result parse failure (transitional path)** -> warning; originals stay listed.
- **Processor exception** -> surfaced as a warning; other processors still run.
- **Cancellation** (`Ctrl+C`) during post-processing -> propagate `CancellationToken`; child honors it; orchestrator stops without merging.
- **Disk full / permission denied on output** -> warning; originals stay listed.

**Invariant:** post-processing never changes the test run exit code.

### 7.10 Security considerations

- The manifest is created by the orchestrator in a process-private temp directory (`Path.GetTempPath()/dotnet-test-postproc-<guid>/manifest.json`), cleaned up after use.
- The manifest contains only file paths under directories the user already had write access to — no privilege-escalation surface.
- The dispatcher tool is local-only. There is no network path; we do not download or evaluate scripts.
- Hosts validate inputs. A local attacker who can tamper with the manifest can equally tamper with the test binaries, so no new trust boundary is crossed.
- The dispatcher tool name is namespaced (`internal-merge-artifacts`) so a reader of a process command line can tell this is platform infrastructure, not user invocation.

### 7.11 Telemetry

The orchestrator emits one telemetry event per `dotnet test` run summarizing: count of jobs planned / executed / failed, distinct kinds (and fallback extensions) processed, and total wall-time in post-processing. No file paths, no artifact contents. Standard `dotnet test` telemetry envelope.

### 7.12 Cross-TFM and cross-architecture constraints

- **TRX is data (XML).** Cross-TFM and cross-architecture merges are fine; election may pick any advertising app.
- **`.coverage` is a binary format.** Merging across architectures (x64 host merging arm64 coverage) is not obviously safe. For binary kinds, election **constrains candidates to architecture-compatible apps** (the `arch_ok` predicate in §7.5). Whether coverage should also merge per-TFM or cross-TFM is a policy the coverage processor decides from the input metadata (`TargetFramework`, `Architecture`), which is why those fields are on `InputArtifact`.
- If no arch-compatible app can merge a binary group, that group is left un-merged (originals listed) rather than merged unsafely.

## 8. Implementation phasing

| Phase | Repo | Deliverable | Blocked by |
| --- | --- | --- | --- |
| 1a | testfx | `IArtifactPostProcessor` contract (experimental); new `TrxReportEngine` file-merge path; **promote `ITool`/`AddTool`/`Tools` to public** (experimental, `PublicAPI.Unshipped.txt`). | — |
| 1b | testfx | `TrxArtifactPostProcessor` + user tool `merge-trx` in `Microsoft.Testing.Extensions.TrxReport`. | 1a |
| 2a | testfx | `Kind` on `SessionFileArtifact` / `FileArtifactMessages`; handshake advertisement; producers tag their outputs. | 1a |
| 2b | testfx | Platform-owned dispatcher tool `internal-merge-artifacts` + tool-host-over-pipe composition. | 2a |
| 2c | dotnet/sdk | Election + relaunch + merged-artifact re-entry. Gated on advertisement presence and host version. | 2a, 2b |
| 3 | microsoft/codecoverage | `CodeCoverageArtifactPostProcessor` + `merge-coverage` tool. | 1a |
| 4 | (optional) coverlet | `CoverletArtifactPostProcessor` + tool. | 1a |

**Phase 1 (1a + 1b) is the recommended starting point** — it ships user-visible value with no cross-repo dependency and builds the engine everything else reuses. Phases 2a–2c together deliver the automatic experience.

## 9. Testing strategy

### testfx

- Unit tests for `TrxArtifactPostProcessor` / the new `TrxReportEngine` file-merge path:
  - 0 inputs -> `null`; 1 input -> `null` (no merge needed).
  - N inputs same TFM -> merged TRX schema-valid, counters summed, attachment URIs preserved.
  - Inputs with duplicate test ids -> dedup (or namespace) deterministically.
  - Inputs with `<UriAttachment>` relative paths -> rewritten relative to merged output.
- Acceptance test for the user tool: `--tool merge-trx --input <glob> --output <path>` produces a valid merged TRX. (No SDK dependency — validates Phase 1 alone.)
- Integration test for the dispatcher tool using a test app that registers a noop processor.
- Acceptance test: end-to-end `dotnet test` against a multi-project solution, asserting `merged.trx` exists and counters match the sum. Use `AcceptanceAssert.DurationPattern` for any rendered durations.

### SDK

- Unit tests for `ArtifactPostProcessingPlanner`: single app covering everything (1 relaunch); split capabilities; no-op when nothing advertises; set-cover minimality; arch-constrained binary kinds.
- Integration tests covering the §7.8 behavior matrix.
- Negative: dispatcher non-zero exit -> run still succeeds, originals appear. Malformed return -> warning + graceful degrade.

## 10. Compatibility & migration

| SDK version | testfx version | Behavior |
| --- | --- | --- |
| Old SDK | New testfx | Apps advertise capabilities; SDK ignores them. User tools still work. Behavior otherwise unchanged. |
| New SDK | Old testfx | Apps don't advertise. SDK plans nothing. Behavior unchanged. |
| New SDK | Mixed (some new, some old apps) | Only new apps participate in election. Per-kind degradation if a kind is only produced by old apps. |
| New SDK + new testfx | no MS CC processor yet | TRX merging works; `.coverage` files listed individually. |

The relaunch is **gated on version**: the orchestrator inspects the advertised `Version` and only relaunches hosts >= the first supporting version. The `IArtifactPostProcessor` contract stays behind the experimental diagnostic id until the SDK consumer ships, so it is not frozen prematurely.

## 11. Alternatives considered

### 11.1 Reserved host switch `--internal-post-process-artifacts` (the original primary proposal)

The first draft invoked the merge via a new reserved, hidden command-line switch that put the host into a bespoke non-test "merge" mode. It works, but compared with the dispatcher-tool recommendation it is strictly more surface: it invents a new mode that must be threaded through the mutually-exclusive-modes validation, re-derives exit-code semantics that `ITool` already defines, and does not double as the user-facing manual path. Because post-processing is already a non-test path, the existing `--tool` route is the cleaner primitive. Kept here as the runner-up; if tool/pipe composition (§7.6) proves too costly, this switch is the natural fallback for the *invocation* half while the typed contract and manifest stay unchanged.

### 11.2 Fire-and-forget manifest/result JSON vs. reusing the existing pipe

The original design was fully fire-and-forget: write a manifest, launch a child, child writes a result JSON, exits; no live channel. But `dotnet test` already keeps a **persistent bidirectional-capable named pipe** to every host for the whole run (`DotnetTestConnection`), and artifacts **already stream over it live** (`FileArtifactMessages`). So:

- The SDK already has the input list; the manifest re-serializes data it already received (§7.7).
- Reporting merged artifacts back over that pipe as `FileArtifactMessage` removes the bespoke **result-JSON parse/swap** on the SDK side, because merged outputs re-enter the same reporter pipeline as in-run artifacts. (The SDK still collapses the consumed originals and surfaces the merged file via `RemoveArtifacts`/`ArtifactAdded`, but through that normal path.)
- It is the natural home for future UI (live "merging... (3/12)" progress, incremental/partial results) without inventing a new channel.

The one genuine gap: the pipe is host->SDK for *data* today; the SDK cannot push a request to the host. But the handshake **response** already carries SDK->host data (`IsIDE`, negotiated versions), so the plumbing to extend it is precedented. For v1 we still hand inputs to the child as a manifest argument (no new SDK->host request needed) and only use the pipe for the *return* of merged artifacts. Net: reusing the pipe is **less** total surface than a parallel result-file swap, not more — the opposite of how the original draft framed it.

### 11.3 Keep an elected host alive instead of relaunching (non-option)

One might avoid a relaunch by keeping an elected host process alive at end-of-run and pushing it the manifest over the live pipe. This is worse, not better: hosts finish at different times and the SDK cannot pick the winner until the last one is done, so it would have to hold **all** N host processes' memory alive to the end just in case. Relaunching one well-targeted app is cheaper. The value of §7.5 (election inputs pre-gathered during the run) is precisely what makes that single relaunch a well-aimed, one-shot call.

### 11.4 Other rejected approaches

1. **Untyped, per-extension `ITool` with no typed contract.** Rejected: tool names must be globally unique across all loaded extensions, the CLI surface is string-typed, and there is no structured way to return per-input/per-output results without re-introducing a manifest/result schema. We keep the typed `IArtifactPostProcessor` and only use a tool as the *invocation* primitive — the tool wraps the typed engine, it does not replace it.
2. **Reuse `--server` (long-lived JSON-RPC) mode.** Rejected for v1: lifecycle complexity, extensions untested in server mode, holding a process across siblings is fragile. §11.2 is the lighter, post-run-only version of this idea.
3. **Standalone "merger testhost" binary shipped by MTP.** Rejected: nowhere to source extension assemblies dynamically — the same footgun as a `dotnet-coverage merge` global-tool install. Relaunching an already-built test app (which has its own extensions) sidesteps it.
4. **SDK-internal TRX merger.** Rejected: forces two homes for the same code (SDK + testfx), creates drift risk, and cannot extend to `microsoft.codecoverage` / `coverlet.cobertura` for the extension-agnostic story.

## 12. Open questions

1. **Tool/pipe composition cost (§7.6).** How much work is it to let a tool host also establish the `dotnet-test` pipe and emit `FileArtifactMessage`? If large, ship the transitional result-JSON return first (§7.7) and follow up. This is the main implementation unknown, not a design fork.
2. **Should processors be allowed to consume and delete their inputs?** Current design: no (non-destructive). Revisit only if we ever want opt-in garbage collection.
3. **Where do `IArtifactPostProcessor` registrations live?** Recommend the same `TestApplicationBuilder.RegisterTestApplicationExtensions` flow as everything else, for consistency.
4. **Per-TFM vs cross-TFM merge for coverage.** Likely a per-Kind policy the processor decides from input metadata (§7.12).
5. **TRX attachment paths.** TRX attachments use absolute paths today, so cross-process merge keeps them resolvable. If MTP later switches to relative paths, the merger must copy attachments alongside the merged TRX. Out of scope for v1.
6. **Diagnostic flag pass-through.** Should the orchestrator pass `--diagnostic-output-directory` to the relaunched child? Likely yes for debuggability.
7. **Two extensions advertising the same Kind** — first-wins or fail-fast? Recommend warn-and-first-wins to preserve the never-fail-the-run invariant.
8. **Kind vocabulary governance.** Open string with reverse-DNS convention is enough to ship, but document a short well-known list (TRX, MS CC, Cobertura, JUnit, NUnit3) so producers don't fork on capitalization or punctuation.
9. **Typed `Kind` wrapper** (`readonly record struct ArtifactKind(string Value)`) vs. plain `string`. Lean plain `string` initially.
10. **Versioning a Kind.** On incompatible format change, bump the Kind (`microsoft.codecoverage.v2`) vs. add a version field. Recommend bumping the Kind — keeps matching trivial.
11. **Final dispatcher exit-code values** so they don't overlap existing MTP exit codes.
12. **`--no-build` with stale binaries.** A relaunch uses the on-disk binary; if that binary no longer advertises the kind, the relaunch is a silent no-op. Document as expected.

## 13. Decisions summary (what this RFC currently proposes)

- **Engine contract:** typed, invocation-agnostic `IArtifactPostProcessor` keyed on `SupportedKinds` (reverse-DNS) with `SupportedFileExtensionsFallback`, shipped experimental.
- **Manual path (Phase 1, ship first):** a user-facing `ITool` per well-known kind (`merge-trx`, ...) sharing the same engine. Discoverable, non-hidden, zero SDK/protocol dependency.
- **Producer side:** `SessionFileArtifact` / `FileArtifactMessages` gain an optional `Kind` string.
- **Discovery:** capabilities advertised in `HandshakeMessage` (two semicolon-separated lists: kinds + legacy extensions).
- **Election:** computed in-memory from data the SDK already has (handshake + live artifacts); minimal set-cover, relaunching the fewest apps that cover all mergeable kinds; arch-constrained for binary kinds.
- **Invocation (Phase 2):** a platform-owned **internal dispatcher `ITool`** relaunched by the SDK, routing by Kind then extension fallback; the reserved switch (§11.1) is the runner-up.
- **Results:** merged artifacts flow back over the existing `dotnet-test` pipe as `FileArtifactMessage` (transitional result-JSON fallback if pipe composition is deferred).
- **CLI surface:** no new public flags on `dotnet test`. Automatic path is implicit, non-destructive, never-fails-the-run.
- **Phasing:** contract + TRX engine + TRX user tool first (testfx-only); Kind/handshake/dispatcher next; SDK consumer after; MS CC and others on their own cadence.

## 14. Appendix A — illustrative code sketches

> These are **illustrative**. In particular, `TrxReportEngine.MergeAsync(string[], string, CancellationToken)` is shown as a static helper for brevity; the real Phase 1a work adds a file-parse-and-merge entry point to `TrxReportEngine` (an instance class today whose only method builds a TRX from in-memory results). The exact signature/shape is an implementation detail. If `InputArtifact`/`ProcessedArtifact` are made public, they must avoid synthesized `init` accessors (see the note in §7.1).

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

### A.2 User tool sharing the same engine (in Microsoft.Testing.Extensions.TrxReport)

```csharp
internal sealed class TrxMergeTool : ITool
{
    public const string ToolName = "merge-trx";

    public string Name => ToolName;
    public string Uid => "Microsoft.Testing.Extensions.TrxReport.MergeTool";
    public string DisplayName => "TRX report merge tool";
    public string Description => "Merges multiple TRX files into one from the command line.";
    public string Version => "1.0.0";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        // Resolve --input globs and --output from the command line, then reuse the
        // SAME engine the SDK-orchestrated processor uses, guaranteeing identical output.
        string[] inputs = ResolveInputGlobs();
        string output = ResolveOutput();
        await TrxReportEngine.MergeAsync(inputs, output, cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
```

### A.3 Orchestrator planner (in dotnet/sdk)

```csharp
internal static class ArtifactPostProcessingPlanner
{
    public static PostProcessingPlan Plan(
        IReadOnlyList<TestRunArtifact> artifacts,
        IReadOnlyDictionary<string, AppCapabilities> apps)
    {
        // 1. Primary grouping by Kind; 2. fallback grouping of untagged by extension.
        var byKind = artifacts
            .Where(a => a.Kind is not null)
            .GroupBy(a => a.Kind!, StringComparer.Ordinal)
            .Select(g => (matchType: "kind", key: g.Key, items: g.ToList()));

        var byExt = artifacts
            .Where(a => a.Kind is null)
            .GroupBy(a => Path.GetExtension(a.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => (matchType: "ext", key: g.Key, items: g.ToList()));

        // 3. Only groups with >= 2 inputs; 4. candidates must advertise AND be arch-compatible.
        var groups = byKind.Concat(byExt)
            .Where(g => g.items.Count >= 2)
            .Select(g => new Group(g.matchType, g.key, g.items, Candidates(g, apps)))
            .Where(g => g.Candidates.Count > 0)
            .ToList();

        // 5. Greedy minimal set-cover, preferring producers, then TFM, then path.
        var jobsByApp = new Dictionary<string, PostProcessingJob>();
        while (groups.Any(g => !g.Covered))
        {
            AppCapabilities app = groups
                .Where(g => !g.Covered)
                .SelectMany(g => g.Candidates)
                .Distinct()
                .OrderByDescending(a => groups.Count(g => !g.Covered && g.Candidates.Contains(a)))
                .ThenByDescending(a => groups.Where(g => !g.Covered && g.Candidates.Contains(a))
                    .Sum(g => g.items.Count(i =>
                        string.Equals(i.ProducingTestModule, Path.GetFileName(a.ModulePath), StringComparison.OrdinalIgnoreCase))))
                .ThenByDescending(a => TfmOrder(a.TargetFramework))
                .ThenBy(a => a.ModulePath, StringComparer.Ordinal)
                .First();

            foreach (Group g in groups.Where(g => !g.Covered && g.Candidates.Contains(app)).ToList())
            {
                if (!jobsByApp.TryGetValue(app.ModulePath, out PostProcessingJob? job))
                {
                    job = new PostProcessingJob(app, new List<InputArtifact>());
                    jobsByApp[app.ModulePath] = job;
                }

                job.Inputs.AddRange(g.items.Select(ToInputArtifact));
                g.Covered = true;
            }
        }

        return new PostProcessingPlan(jobsByApp.Values.ToList());
    }
}
```

---

*End of RFC.*
