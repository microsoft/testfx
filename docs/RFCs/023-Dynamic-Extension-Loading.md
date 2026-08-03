# RFC 023 - Dynamically resolved MTP extensions

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Several internal infrastructure teams have asked for **dynamically resolved Microsoft.Testing.Platform
(MTP) extensions**: the ability to change how a test application runs by having the platform load
extension assemblies at run time, without the test project referencing those extensions at build time.

Deliberate late binding of arbitrary in-process plugins is one of the pillars MTP set out to avoid,
because it is the mechanism behind a large share of VSTest's historical failure modes (assembly/
dependency conflicts, silent version drift, unversioned extension-point contracts). This RFC does
**not** propose that we simply add a plugin loader. It:

1. separates the *requests* we receive from the *solution* the requesters proposed;
2. documents what already satisfies most of those requests today (and is being overlooked);
3. records precisely why the naive form is unsafe, in terms of MTP's own architecture and promises;
4. lays out the option space with trade-offs; and
5. defines the non-negotiable constraints and prerequisites should we decide to build a narrow,
   supported form of dynamic loading.

## Motivation

The recurring request, paraphrased: *"we want to change how tests run — add reporting, collect
artifacts, publish to our internal systems, enforce policy — but we do not want to modify the build
of every test project."*

This is a legitimate need. Central infrastructure teams own the CI pipeline, not the hundreds of test
projects that flow through it. Asking every team to add a `PackageReference` is a coordination problem
that they cannot solve, and the platform should have an answer.

The **proposed** solution — "let the platform load a DLL we point it at" — is what is contentious. The
need and the proposed solution must be evaluated separately, because most of the need can be met
without late binding at all.

## Terminology

- **Static registration** — extensions referenced by the test project (directly or transitively) and
  wired into the generated `SelfRegisteredExtensions.AddSelfRegisteredExtensions(builder, args)` at
  compile time. This is the only mechanism that exists today.
- **Dynamic / late-bound registration** — the platform resolves an assembly path and type name at run
  time (from configuration, an environment variable, or a probing directory), loads it, and registers
  the resulting extension.
- **Contract assembly** — the assembly carrying the extension-point types (`IExtension`,
  `IDataConsumer`, `ITestSessionLifetimeHandler`, …). Today that is `Microsoft.Testing.Platform`
  itself.

## What people are actually asking for

Nearly every request maps to one of four buckets. They differ enormously in risk, and conflating them
is what makes this conversation hard.

| Bucket | Typical ask | Needs in-process access to test code? | Risk |
| --- | --- | --- | --- |
| **A. Observe & report** | ship results to an internal dashboard, custom report format, flaky-test tracking, telemetry | No | Low |
| **B. Shape the run** | retry policy, sharding, filtering, ordering, timeouts | No | Low |
| **C. Environment & diagnostics around the host** | env vars, dumps, video, profilers, tracing | No — runs in the controller, not the test host | Medium |
| **D. In-process interception** | per-test hooks, instrumenting user code, injecting services into the test framework | **Yes** | High |

Buckets A and B are already fully served out-of-process (see below). Bucket C is served by the
test host controller extension points. Only bucket D genuinely requires loading foreign code into the
process that runs user tests — and that is exactly the bucket that caused VSTest's pain.

**Action item:** before designing anything, we should classify the concrete requests we have received
into this table. My expectation is that A/B/C covers the large majority, and that we are being asked
for a plugin loader because the alternatives are undiscoverable, not because they are insufficient.

## What already works today (and is likely the answer for most requesters)

### 1. Central injection via MSBuild — "without modifying the build" is usually already true

Extension packages register themselves through a `TestingPlatformBuilderHook` item declared in the
package's MSBuild assets — for example
`src/Platform/Microsoft.Testing.Extensions.CrashDump/buildMultiTargeting/Microsoft.Testing.Extensions.CrashDump.props`,
which the sibling `build/` and `buildTransitive/` props simply forward to:

```xml
<ItemGroup>
  <TestingPlatformBuilderHook Include="EC3971EE-91B7-4C77-B4E1-DF606F118FAB">
    <DisplayName>Microsoft.Testing.Extensions.CrashDump</DisplayName>
    <TypeFullName>Microsoft.Testing.Extensions.CrashDump.TestingPlatformBuilderHook</TypeFullName>
  </TestingPlatformBuilderHook>
</ItemGroup>
```

`TestingPlatformSelfRegisteredExtensions` then emits `SelfRegisteredExtensions.cs`, and the generated
entry point calls it. Because the hook flows **transitively**, an infrastructure team can:

- publish one internal NuGet package containing their extension plus a `buildTransitive/*.props`
  declaring a `TestingPlatformBuilderHook`; and
- inject that single `PackageReference` from one central location they already own — a repo-root
  `Directory.Build.targets`, `Directory.Packages.props`, a company MSBuild SDK, or
  `CustomAfterMicrosoftCommonTargets`.

No individual test project changes. NuGet resolves the dependency graph, so there is no conflict
problem. It works under NativeAOT and single-file. The types are checked at compile time.

When a requester says "we don't want to modify the build", it is worth asking whether they mean *"we
cannot touch N test projects"* (solved above) or *"we cannot touch any MSBuild file at all"* (much
rarer, and usually means they only own the pipeline YAML — see the next option).

**Gap to close:** this is essentially undocumented as a *scenario*. We should write it up as
"injecting an MTP extension organisation-wide" and point requesters at it first.

### 2. Out-of-process consumption — for buckets A and B

For anything that only needs to *observe* a run, the test application is already a well-behaved
process with machine-readable output:

- report extensions (`--report-trx`, CTRF, JUnit, HTML) write standard artifacts a pipeline step can
  post-process, with zero in-process coupling;
- server mode (`docs/mstest-runner-protocol/`) exposes a documented JSON-RPC protocol, which is a
  *wire* contract — it has no assembly identity, no dependency graph, and no type-compat problem at
  all;
- `IArtifactPostProcessor` / the artifact post-processing dispatcher provides a defined post-run hook.

A pipeline-owned tool that launches the test app and consumes the protocol or the artifacts has none
of the failure modes we are worried about. If a requester's scenario is "collect results and send them
somewhere", this is strictly better than a plugin and we should say so.

### 3. Ship-always, enable-conditionally — for "we want it off by default"

`IExtension.IsEnabledAsync()` already lets a statically registered extension decide at run time
whether to activate, based on `IConfiguration`, `testconfig.json`, an environment variable, or a
command-line option. "Statically referenced, dynamically enabled" covers a surprising number of
requests that get phrased as "dynamically loaded".

## Why the naive form is unsafe

These are not abstract concerns; each maps to something concrete in the current codebase.

### The contract surface is not currently a supportable public contract

`src/Platform/Microsoft.Testing.Platform/Microsoft.Testing.Platform.csproj` grants
`InternalsVisibleTo` to **every first-party extension** — CrashDump, HangDump, TrxReport, Retry,
Telemetry, VSTestBridge, AzureDevOpsReport, OpenTelemetry, and more. First-party extensions are
therefore written against a surface a third party cannot use, and that surface is free to change in
any release.

This is already tracked as [#7739 (IVT story for MTP and extensions)](https://github.com/microsoft/testfx/issues/7739)
and [#7708](https://github.com/microsoft/testfx/issues/7708). Shipping a *dynamic* plugin model on top
of an internals-coupled surface would institutionalise a two-tier extension model — "real" extensions
that use internals and plugin extensions that get a strict subset — and we would immediately owe
binary compatibility on a surface we have not designed for it.

**Nothing here can proceed until the extension contract is IVT-free and versioned.**

### There is no API version to negotiate against

`IExtension` exposes a `Version` string describing *the extension*, and `ICapabilities<T>` /
`ICapability` provide feature negotiation between the framework and the platform. Neither answers the
question a loader must answer: *"was this plugin compiled against a contract this host can satisfy?"*
Static registration never needed it, because the compiler answered it. A loader needs an explicit,
monotonic contract version and a documented compatibility policy.

### Dependency conflicts are real, and the controller process does not save us

The obvious hope is that test host controller extensions are safe because they run "in the other
process". They do run in a different process from the test host — but `TestHostControllersTestHost`
launches the child by re-running **the same executable**:

```csharp
ProcessStartInfo processStartInfo = new(executableInfo.FilePath, arguments) { … };
```

So the controller process is the test application, in controller mode, with the test application's
`.deps.json` governing resolution. Loading a plugin with its own dependency closure into it has the
same conflict surface as loading it into the test host. The process boundary only helps if we
introduce a **dedicated extension host process** that is not the test app.

### Trimming, single-file and NativeAOT are first-class MTP scenarios

`MSTest.Sdk` ships a NativeAOT runner mode (`src/Package/MSTest.Sdk/Sdk/Runner/NativeAOT.targets`),
with samples and acceptance tests (`test/IntegrationTests/*/NativeAotTests.cs`). Dynamic loading is
fundamentally incompatible with NativeAOT and hostile to trimming and single-file.

This is the strongest structural argument: **dynamic extension loading can never be a pillar of MTP.**
At best it is an opt-in mode that is mutually exclusive with AOT/single-file, which means every
requester must be told their tests can no longer be published that way.

### Half our target surface has no isolation primitive

`Microsoft.Testing.Platform` targets `net8.0;net9.0;netstandard2.0`. The `netstandard2.0` asset is
what .NET Framework consumers get, and it has no `AssemblyLoadContext`. Any isolation-based design is
.NET (Core) only; the .NET Framework story would be `AppDomain` (which we will not do) or nothing.

### Supportability

Today, if a run misbehaves, the set of extensions is a function of the project file — reproducible
from source. With machine- or pipeline-scoped dynamic loading, a run's behaviour depends on ambient
state that is not in the repo. Every bug report becomes "which plugins were loaded, from where, built
against what?" This is a large share of why VSTest issue triage was expensive, independent of the
technical conflicts.

## Options

### Option 0 — Say no; invest in discoverability of what exists

Document organisation-wide static injection (§1), out-of-process consumption (§2), and conditional
enablement (§3). Close requests by routing them to the right one.

- **Pros:** no new surface, no new risk, keeps AOT/trim/single-file intact, zero compat burden.
- **Cons:** does not serve the genuine bucket-D cases; risks teams forking or hacking around us
  (start-up hooks, profilers, hand-patched entry points) in ways we cannot support either.

### Option 1 — Make static injection a first-class, documented, ergonomic scenario

Option 0 plus deliberate investment: a documented "org-wide extension injection" guide, a template or
sample for an internal extension package, possibly a small MSBuild-side helper so an infra team can
inject a hook without authoring a full package, and diagnostics (`--info`) that make it obvious which
hooks were registered and from where.

- **Pros:** solves the stated problem ("without modifying each test project") completely and safely.
- **Cons:** still requires the ability to influence *one* MSBuild file, and requires a build of the
  test project after the extension changes.

### Option 2 — Out-of-process extension host

Introduce a dedicated extension-host process that loads plugins and communicates with the test host
over the existing IPC/protocol. Plugins never share a process with test code or with the test app's
dependency graph.

- **Pros:** eliminates the dependency-conflict class outright; contract becomes a wire protocol
  (versionable, language-agnostic, already partially exists via server mode); a crashing plugin cannot
  take down the run; compatible with an AOT test app, since only the *extension host* is dynamic.
- **Cons:** significant new infrastructure; serialisation cost; only supports extension points whose
  semantics survive a process boundary (fine for A/B/C, impossible for D); another process to ship,
  version and diagnose.

### Option 3 — Narrow, isolated, opt-in in-process loading

Load plugins in-process, but under strict constraints (see next section): explicit manifest only,
`AssemblyLoadContext` isolation driven by the plugin's own `.deps.json`, a versioned IVT-free
contract, a restricted set of extension points, .NET-only, incompatible with AOT/single-file, and
marked experimental.

- **Pros:** serves bucket D; smallest conceptual leap from today's model.
- **Cons:** highest risk; reintroduces the failure class we designed against; permanent compat
  obligation on the contract assembly; forces the AOT trade-off onto users.

### Option 4 — Unrestricted plugin directory probing

Scan a directory, `Assembly.LoadFrom` everything, register whatever implements a known interface.

**Explicitly rejected.** This is the VSTest model that motivated MTP. Recorded only so the RFC is
unambiguous about it.

## Recommendation (for discussion)

A tiered response rather than a single answer:

1. **Default response: Option 1.** Treat "change how tests run without touching each test project" as
   a solved problem and close the discoverability gap. I expect this to absorb most requests.
2. **For observe/report/orchestrate: Option 2's cheap half — out-of-process consumption of what we
   already emit** (artifacts + server-mode protocol). No new platform work needed for many teams.
3. **Only if concrete, classified bucket-D requests remain**, consider Option 3 under the constraints
   below, gated behind the prerequisites, and shipped as `[TPEXP]`. Option 2's full form (dedicated
   extension host) is the better long-term shape if the volume justifies the investment.

Crucially: we should not design this from paraphrased requests. The next step is to collect the actual
scenarios and classify them.

## Non-negotiable constraints if we build in-process dynamic loading

These are what would distinguish the design from VSTest's. If we cannot commit to all of them, we
should not ship the feature.

1. **Explicit manifest, never probing.** Extensions are declared by assembly path + type full name +
   expected contract version, in `testconfig.json` and/or an explicit CLI option. No directory scan,
   no "drop a DLL and it activates". A run's extension set must be reproducible from declared inputs.
2. **Opt-in at two levels.** The test application must opt in to permitting dynamic extensions at all
   (MSBuild property / config), and the run must name them. A test app that has not opted in ignores
   the configuration entirely. Plus a `--no-dynamic-extensions` kill switch for triage.
3. **Isolation is mandatory.** Load into a dedicated `AssemblyLoadContext` driven by
   `AssemblyDependencyResolver` over the plugin's own `.deps.json`. `Assembly.LoadFrom` into the
   default context is banned. This implies **plugins ship as published output, not loose DLLs** —
   which should be stated as a requirement, not an implementation detail.
4. **Exactly one shared contract.** Only the contract assembly (and the BCL) unify with the host; the
   plugin ALC delegates those to the default context and resolves everything else itself. Anything a
   plugin exchanges with the platform must live in the contract assembly.
5. **A real, versioned, IVT-free contract.** Extract the extension-point surface into a contract that
   third parties can implement without `InternalsVisibleTo`, give it a monotonic contract version,
   and have the loader refuse plugins built against a newer version with a clear error. Resolves the
   prerequisite in [#7739](https://github.com/microsoft/testfx/issues/7739).
6. **A restricted extension-point set.** Start with observation-shaped points only — `IDataConsumer`,
   `ITestSessionLifetimeHandler`, `ITestHostApplicationLifetime`, `ICommandLineOptionsProvider`,
   `ITestHostEnvironmentVariableProvider`, `ITestHostProcessLifetimeHandler`, and (once it ships)
   `IArtifactPostProcessor`. Explicitly **not** the test framework, execution filter
   factories/providers, or orchestrators: nothing that can silently change which tests run or what
   result they report.
7. **Honest platform-support boundaries.** .NET only (no `netstandard2.0`/.NET Framework asset).
   Mutually exclusive with NativeAOT and single-file, with a build-time error rather than a run-time
   surprise.
8. **Provenance everywhere.** Every dynamically loaded extension appears in `--info`, in diagnostic
   logs, in the TRX/report metadata, and in crash artifacts, with its path, resolved version and
   contract version. Triage must start from the artifact, not from asking the reporter.
9. **A written support policy.** Loading a third-party plugin puts the run in a supported-with-caveats
   state; the first triage step is re-running with `--no-dynamic-extensions`. Say this up front rather
   than discovering it per-incident.

## Prerequisites

Ordered; each is independently valuable even if we never ship dynamic loading:

1. Classify the real requests against the bucket table above.
2. Document organisation-wide static extension injection (Option 1). Cheapest, highest immediate value.
3. Resolve the IVT story ([#7739](https://github.com/microsoft/testfx/issues/7739),
   [#7708](https://github.com/microsoft/testfx/issues/7708)) — a third-party-authorable extension
   surface is a hard prerequisite for any plugin model.
4. Define a contract version and compatibility policy for that surface.
5. Only then, prototype Option 2 or Option 3 behind `[TPEXP]`.

## Open questions

- Which of the four buckets do our actual requests fall into? (Blocking.)
- Do requesters control *any* central MSBuild file, or genuinely only the pipeline definition?
- Is a rebuild of the test project acceptable after an infra-side extension change? If yes, Option 1
  is sufficient and this RFC largely resolves to a documentation task.
- If not, is the requirement "no rebuild" or "no source change"? Those have different answers.
- Are requesters willing to give up NativeAOT/single-file publishing for their test apps?
- Should the contract surface move to a separate `Microsoft.Testing.Platform.Extensions.Abstractions`
  package, or stay in `Microsoft.Testing.Platform` with a documented contract version?
- For Option 2, can the existing server-mode protocol carry extension traffic, or does an extension
  host need its own channel?

## References

- [#7739 — IVT story for MTP and extensions](https://github.com/microsoft/testfx/issues/7739)
- [#7708 — MTP AppVersion shouldn't be used via IVT by extensions](https://github.com/microsoft/testfx/issues/7708)
- [#7639 — MTP is generating a non-static SelfRegisteredExtensions class incompatible with extensions](https://github.com/microsoft/testfx/issues/7639)
- [#3494 / #3525 — Platform.MSBuild should allow generating a helper for registration of extensions](https://github.com/microsoft/testfx/issues/3494)
- [#6334 — MTP 2.0: Allow to register & resolve test framework dependencies](https://github.com/microsoft/testfx/issues/6334)
- [RFC 017 — Custom test host launcher](./017-TestHost-Launcher.md)
- [RFC 018 — Artifact post-processing](./018-Artifact-Post-Processing.md)
- [MSTest runner protocol](../mstest-runner-protocol/001-protocol-intro.md)
