# Experimental native Bazel build

This branch provides a **staged development build, not a drop-in replacement for
the shipping MSBuild/Arcade pipeline**. The portable graph compiles 35 projects
under `src` for .NET 8/9/10/11 without running MSBuild or consuming its outputs.
Separate compatibility targets provide 26 netstandard2.0 and five net462 source
variants. Native packaging now produces ten experimental MTP/MSTest/analyzer/SDK
NuGets, alongside self-contained test hosts. Existing release, signing-service,
and official packing workflows remain unchanged.

## Getting started

Install PowerShell 7.2 or newer. On Windows, use an environment that already
permits symbolic links (Developer Mode or an appropriately privileged shell).
The bootstrap does not change machine policy or install a global SDK.

From the repository root:

```powershell
.\eng\bazel.ps1 build //:managed_all
.\eng\bazel.ps1 test //:portable_tests
.\eng\bazel\validate.ps1
.\eng\bazel\validate-packages.ps1
```

`validate.ps1` also checks discovery, real TRX/HTML/JUnit/CTRF output, paths
containing spaces and `!`, failing-test exits, zero-result exits, mixed-module
failure aggregation, and the native action graph. Evidence is written under a
unique `artifacts\bazel-validation` directory. Its success is scoped to the
portable suite, not the complete repository. `validate-packages.ps1` separately
builds and inspects native NuGets, compiles the existing platform explorer against
them for all four frameworks, runs the net11 consumer, and executes isolated
self-contained publishers on their actual .NET 8/9/10/11 runtimes.

The wrapper pins and verifies Bazel **9.1.1**. Bzlmod pins `rules_dotnet` commit
**add5c92b30888a70aa951de1744a770b8f8ef436**, with an integrity-checked archive and
a set of scoped compatibility patches. Its default toolchain downloads SDK
**11.0.100-rc.3.26522.148** (runtime **11.0.0-rc.3.26522.148**). Neither the
machine SDK nor `global.json` selects this experimental compiler.

The exact `global.json` SDK, **12.0.100-alpha.1.26509.101**, is also available as
an explicit, checksum-pinned compiler/runtime choice:

```powershell
.\eng\bazel.ps1 build //:managed_all --extra_toolchains=@dotnet_engineering_toolchains//:all
```

This opt-in uses SDK12's real compiler and execution runtime while retaining
the published SDK11 reference/RID-pack policy for net8.0 through net11.0.
It does not claim SDK12 bundled-pack defaults or net12.0 targeting.
`eng/bazel/verify-engineering-sdk.ps1` audits an actual action-graph capture;
the decision record distinguishes successful native builds/consumers from
synthetic verifier checks. The default remains SDK11.

The bootstrap supports Windows x64, Linux x64, and macOS arm64. Windows was
executed locally; the added Windows/Linux CI job is configuration, not evidence
that CI or Linux has run. Other architectures can use Bazelisk with
`.bazelversion`, subject to the rules' platform support.

On Windows, the default output root is
`<checkout-drive>:\tfb\<hash-of-worktree-path>` to avoid batch-file path limits.
Set `TESTFX_BAZEL_OUTPUT_ROOT` to override it with another short, writable path.
It must not be shared by simultaneously running worktrees. Non-Windows hosts
default to a worktree-specific temporary directory. The executable itself lives
under `artifacts\bazel-tools`.

When several worktrees share a nearly full volume, choose a unique output root
on a volume with capacity and pass an absolute evidence root to the validators.
Their consumer feeds and restore caches stay under that evidence root:

```powershell
$env:TESTFX_BAZEL_OUTPUT_ROOT = 'C:\tfb\my-bazel-worktree'
.\eng\bazel\validate.ps1 -Jobs 2 -EvidenceRoot 'C:\tfb\my-native-evidence'
.\eng\bazel\validate-packages.ps1 -Jobs 2 -EvidenceRoot 'C:\tfb\my-package-evidence'
```

The override is process-local; set it in every new shell rather than changing
machine-global settings. `-Jobs` limits Bazel build concurrency, not the tests'
existing assembly worker settings.

Bazelisk users can invoke `bazel` directly, but must provide a suitably short
Windows output root themselves. The checked-in `.bazelrc` supplies the required
runfiles settings.

## Targets and current scope

| Target | Contents |
| --- | --- |
| `//:managed` | 35 source projects in the selected framework configuration (net10.0 by default). |
| `//:managed_all` | The same graph built for net8.0, net9.0, net10.0, and net11.0, retaining fixed-framework analyzers/generators and server-client validation. |
| `//:managed_compatibility` | All 26 netstandard2.0 and five net462 source variants, with exact-framework inventories. |
| `//:managed_netstandard2_0`, `//:managed_net462` | The respective compatibility subgraphs; individual labels use `_netstandard2_0` or `_net462` suffixes. |
| `//:explorer_released` | Existing platform explorer compiled against its released MTP 2.5.2 packages. |
| `//:explorer` | The same sample compiled against this checkout's MTP and TRX assemblies. |
| `//:simple1` | Existing `samples/public/mstest-runner/Simple1`, using the source-built MSTest runner and reporters. |
| `//:baseline_tests` | Seven Windows test targets: the three sample smoke runs, existing framework/adapter/platform-services suites, and build-tool regression cases. |
| `//:portable_tests` | The Windows baseline plus protocol-contract, source-generation, and VSTestBridge suites: ten targets. |
| `//:portable_non_windows_tests` | The same portable contracts without the Windows-filesystem platform-services suite: nine targets. |
| `//:all_unit_tests` | All thirteen original unit-test projects, including the full MTP suite and its known Windows host failures. No cases or original skips are filtered out. |
| `//:framework_tests` | Existing framework tests, including the assertion-generator tests, using the existing sequential `TestContainer` engine. |
| `//:adapter_tests` | Existing adapter tests, using `TestContainer` and a framework-dependent layout for child-process tests. |
| `//:platform_services_tests` | Windows-only platform-services tests, using `TestContainer` and an isolated writable layout for filesystem/child-process fixtures. |
| `//:build_tools_tests` | Thirteen cases for RESX/XLF handling, version/build-stamp generation, and experimental package-version validation. |
| `//:reference_alias_tests` | Nine cases for aliases, signed friend access, emitted public keys, and satellite identity. |
| `//:sdk_package_tests` | Twelve cases for native SDK/meta-package generation and dependency/asset flow. |
| `//:compatibility_reference_tests` | Four analysis tests for package version/TFM precedence and preservation of native/independent references. |
| `//:protocol_contract_tests` | All 27 existing protocol-contract cases, compiling the shared wire sources independently. |
| `//:source_generation_tests` | All 197 existing source-generator cases, retaining method parallelism with four workers. |
| `//:vstest_bridge_tests` | All 202 existing bridge cases, retaining method-level parallelism. |
| `//:terminal_contract_tests`, `//:self_real_tests` | The existing 15 terminal-contract and 24 self-real cases, with their original method/class scheduling. |
| `//:msbuild_tests` | All 388 existing cases, including the real native netstandard task fixture and reference aliases. |
| `//:server_client_tests` | Native self-contained host and original child-process bootstraps; 305 passes and eight original Unix-only skips on Windows. |
| `//:extension_tests` | Original extension sources, aliases and annotation fixtures; 2,863 passes and seven original skips on Windows. |
| `//:analyzer_tests` | All 2,102 existing analyzer/code-fix cases with ordinary test-subject references, generated Recommended policy and isolated reference-pack caches. |
| `//:packages` | Experimental `Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.TrxReport.Abstractions`, and `Microsoft.Testing.Extensions.TrxReport` NuGets, each with four portable TFMs. |
| `//:analyzers_package` | Native MSTest.Analyzers package, including C#/VB analyzers, C# fixes, satellites and generated analysis-mode configs. |
| `//:mstest_packages` | Native framework, adapter, Telemetry and Platform.MSBuild packages; real netstandard task assemblies are bundled at their required paths. |
| `//:mstest_package_closure` | Those four packages plus the original three MTP/TRX packages and analyzers: eight native archives. |
| `//:mstest_package`, `//:mstest_sdk_package` | Native MSTest meta-package and MSTest.Sdk; together with the closure above, ten packages for isolated consumers. |
| `//:published_samples` | Self-contained platform explorer layouts for .NET 8/9/10/11 on the selected host RID. |
| `//:windows_libraries` | Four WinRT PackagedApp and four WPF-reference UIAutomation variants, plus four modern-UWP and four WinUI library variants. |
| `//:windows_probes` | Seven native self-contained library consumers; no XAML application compilation. |
| `//:windows_controllers` | Both original net8/net9 win-x64 framework-dependent apphost layouts. |
| `//:windows_app_model` | A native console host for real loose-MSIX registration/activation and controller checks. |
| `//:platform_tests` | Full 3,380-case MTP suite, preserving method-level parallelization. Two real Windows impersonation cases are host-blocked in local validation; this target is deliberately not part of `baseline_tests`. |

The default configuration remains `net10.0`; `managed_all` explicitly builds the
four-framework matrix. The assertion generator, MSTest
analyzers/code fixes, and MSTest source generator retain `netstandard2.0`;
the server-client's default validation assembly retains `net8.0`.
`managed_compatibility` explicitly selects the additional non-Windows-qualified
project variants rather than relying on compatible-framework fallback.
Windows-qualified targets are opt-in; full workload/runtime parity remains a
separate gate.
The modern-UWP (`net10.0-windows10.0.17763.0`) and WinUI
(`net10.0-windows10.0.18362.0`) closures use their matching `framework_uwp` and
`framework_winui` core variants, not ordinary `framework`. `TestContext` is
defined in core for every flavor; the UI variants retain the exclusions of
`DeploymentItemAttribute` and the desktop `TestContext` directory properties.
The desktop library probe checks that ordinary core still exposes those APIs.
`validate.ps1` selects the ten-target suite on Windows and the nine-target suite
elsewhere, and records the selected suite and unsupported targets in its result.
`MSTestAdapter.PlatformServices.UnitTests` has Windows path/filename semantics
and is also absent from the repository's `NonWindowsTests.slnf`; an explicit
request for its Bazel target on another OS fails compatibility analysis rather
than reporting those tests as passed. Windows still executes its full suite.

The native Roslyn producer/test pins must match `Directory.Packages.props`.
`verify-roslyn-dependencies.ps1 -SelfTest` checks both Paket declarations and
locks before workflow compilation; regenerate them with the existing dependency
updaters when the central analyzer baseline changes. Analyzer package validation
uses that supported baseline rather than assuming an older compiler host.
Workflow text assertions remove ANSI styling from their in-memory view only;
raw command logs remain unchanged. Intentional failure probes force ANSI output
so hosted-console formatting is covered even during plain-console local runs.
Ordinary test launches use the selected SDK's .NET 11 runtime; the separate
self-contained publisher checks prove execution on each actual runtime.

The six source projects outside `managed` are:

| Project | Dedicated target or remaining work |
| --- | --- |
| `MSTest.Analyzers.Package` | `//:analyzers_package`. |
| `MSTest` | `//:mstest_package`. |
| `MSTest.Sdk` | `//:mstest_sdk_package`. |
| `Microsoft.Testing.Extensions.PackagedApp.MSBuild` | Windows NuGet/SDK routing and complete packaged build assets remain outstanding. |
| `Microsoft.Testing.Extensions.PackagedApp.Controller` | `//:windows_controllers`. |
| `TestFramework.Windows.UIAutomation` | `//:windows_ui_automation_all`. |

Native targets now exist for all thirteen repository unit-test projects; this
does not turn the known full-MTP host failures into a passing suite.
Not yet migrated or established: the complete integration/acceptance suites,
every public example, complete legacy runtime behavior, native XAML/UI-thread
and classic-UWP/AppContainer execution, NativeAOT execution,
SourceLink/API-audit parity, official package layout/signing/publishing,
and downstream servicing. Native assemblies and satellites use the same
public-only Microsoft signing key as the repository's Arcade development build
(`PublicKeyToken=b03f5f7f11d50a3a`). This preserves assembly and friend identities
needed by separately shipped extensions; it is not official signature completion
or package publication, and requires no private key or machine verification change.

Building portable `PackagedApp` alone does not prove activation; the separate
Windows gate below exercises that boundary. Building the server-client
validation DLL likewise does not validate the source-only package's consumer
transformation.

### Running applications and selecting tests

PowerShell consumes an unquoted `--` when invoking a script. Quote it when using
the wrapper:

```powershell
.\eng\bazel.ps1 run //:explorer '--' --list-tests
.\eng\bazel.ps1 run //:explorer '--' --filter-uid TestingPlatformExplorer.UnitTests.SomeTests.TestMethod1
.\eng\bazel.ps1 run //:framework_tests_app '--' --treenode-filter '/*/*/AssertTests/*'
.\eng\bazel.ps1 run //:platform_tests_app '--' --filter 'FullyQualifiedName~PlatformInformationTests'
```

Use the test host's options after the separator; Bazel's `--test_filter` is not
translated by these targets. The explorer intentionally includes failures:
selecting `TestMethod2` returns **2**, and an empty selection with
`--minimum-expected-tests 1` returns **9**. The manual
`//:explorer_failure_probe` is an intentional negative fixture used by
`validate.ps1` to verify Bazel's aggregate exit **3**.

### Test host differences

The additive `//:terminal_contract_tests` and `//:self_real_tests` targets run
the existing terminal-reporter contract and MSTest self-real-example suites.
Their first Windows x64 validation compiled `net10.0` and executed on the pinned
.NET 11 runtime: **15** and **24** cases passed, respectively, with no skips.
They are not yet included in `portable_tests`. The terminal target also requires
a standalone `net8.0` compilation of the reporter and its coverage/retry model
closure; this does not claim that the existing `.props` manifest contains that
entire standalone closure.

These two targets use `eng/bazel/NativeUnitTestMain.cs`, preserving method-level
and class-level scheduling respectively, with the original CPU-count worker
setting. Their native hosts retain the normal source-built reporters, dump
providers, and OpenTelemetry registration, but not the CI-only affected-test and
coverage collectors. Explicit framework metadata identifies their compile TFM
even when the test host runs on a newer runtime.

Three further opt-in targets retain their original method-level, CPU-count
scheduling: `//:msbuild_tests` passes 388 existing cases;
`//:server_client_tests` passes 305 with eight original Unix-only skips;
`//:extension_tests` passes 2,863 with seven original OS/filesystem skips on
Windows. Their native test hosts and source dependencies are public-signed.
The server suite uses a real self-contained .NET 10 publish so its child
process fixtures can launch the same assembly outside Bazel's application
shim. The other two compile net10 and execute on the pinned .NET 11 host.
The MSBuild suite stages the real native netstandard task DLL, not a reference
assembly. Extension source-location checks use declared original source files
and the repository's `/_/` pathmap. These targets remain outside
`portable_tests`.

`//:analyzer_tests` runs all 2,102 existing analyzer cases in a native
self-contained net10 host, with zero failures or skips. Its generated
`mstest-recommended.globalconfig` comes from the native analyzer-package
producer. The analyzer/codefix assemblies remain ordinary test-subject
references, as in the original project, not compiler plugins running against
their own tests. NuGet reference-pack downloads, HTTP cache, temporary files,
and user-profile settings stay in Bazel's per-test temporary directory.

`eng/bazel/TestMain.cs` replaces only the selected suites' registration/entrypoint
plumbing. It keeps their test bodies and scheduling, registers the real source
runner and TRX reporter, and uses each Bazel test's undeclared-output directory.
It does not register the Arcade-only affected-test/coverage collectors or
dogfood every extension. Suites with special child-process bootstraps or extern
aliases need their own faithful migration, not this generic entrypoint.

Native C# targets now accept consumer-local aliases by assembly simple name:

```starlark
reference_aliases = {
    "Microsoft.Testing.Platform.MSBuild": ["global", "PlatformMSBuild"],
    "Microsoft.Testing.Extensions.GitHubActionsReport": ["ghactions"],
}
```

The list replaces implicit global access. Include `global` explicitly when both
access paths are needed. The runtime dependency identity is unchanged. The
`//:reference_alias_tests` target exercises global-plus-named, named-only, and
key-qualified friend references through every C# compiler pass. It also checks
the emitted public keys and localized satellite identity. Unknown assemblies and
empty/duplicate alias lists are rejected during analysis.

Local full MTP execution reported 3,378 passes and these two failures:

- `CreateServerStream_AnUnauthorizedIdentityCannotConnect`
- `CreateServerStream_AnAppContainerConnectsOnlyWhenItsOwnPackageIsAuthorized`

Windows returned `ERROR_BAD_IMPERSONATION_LEVEL` (**1346 / 0x542**).
The same error occurred outside Bazel's test wrapper, and a standalone
`NamedPipeServerStream`/anonymous-token probe with no TestFx or Bazel code
reproduced it. The tests and their assertions remain unchanged. A separate,
explicit selection excluding those two executed and passed the other 3,378
cases; this is not a full-suite pass. Do not change machine security policy or
weaken the assertions to obtain a green result.

## How the build works

`BUILD.bazel` and `eng/bazel/*.bzl` declare the graph independently of MSBuild.
When changing a project, keep its native declaration aligned with the project
file: references, linked/excluded sources, constants, usings, resource names,
version overrides, friend assemblies, and runtime files are all significant.
Do not treat a successful C# glob as project parity.

Compatibility wrappers also expose a `runtime_files` output group. Before
staging an external consumer, materialize its exact runtime closure, for example
`.\eng\bazel.ps1 build //:framework_net462 --output_groups=+runtime_files`.
Library default outputs alone do not force every satellite/runfile to be built.
Use the inventory's `satellites` and runtime-dependency paths, and verify staged
hashes; an old file left in the output tree is not evidence of the current build.

The native build explicitly provides:

- Repository implicit usings and scoped `.editorconfig` diagnostics.
- SDK-compatible `NETCOREAPP` selection and transitive compilation references.
  Friend-only transitive references are also declared directly where required.
- RESX accessors and binary resources, all existing XLF satellite cultures,
  logical resource names, and the HTML template.
- Existing version templates, assembly/file versions, neutral-resource metadata,
  the source generator's assembly-version override, and MTP's UTC build date
  from Bazel's volatile build stamp. Cached artifacts retain their build stamp.
- Real Roslyn generators, analyzer release files, and the MSTest diagnostic
  suppressor rather than blanket nullable-warning suppression.
- Declared runfiles/PDBs and native process exit propagation.

NuGet resolution uses checksum-locked generated repositories, not the
machine's NuGet cache during ordinary builds. Update them explicitly:

```powershell
# Re-resolve the declared package graph and regenerate Bazel repositories.
.\eng\bazel\update-dependencies.ps1 -Resolve

# Regenerate from the committed lock without selecting new versions.
.\eng\bazel\update-dependencies.ps1
```

The updater uses pinned Paket 11.0.1 through the Bazel toolchain and then refreshes
native Bzlmod dependency metadata. Review and commit `paket.dependencies`,
`paket.lock`, and `MODULE.bazel.lock` together. The old generated
`nuget/paket.*.bzl` repositories are no longer used. The source graph's
AI/resilience/hosting closure requires
shared 10.0.13 abstractions; these deliberate development-build convergence pins
are documented in the dependency file and do not change `Directory.Packages.props`.

`eng/bazel/patches/testfx-compatibility.patch` is rebased against the pinned commit.
It retains quoted source paths, generated resource names, satellites, short
Windows launchers, argument/manifest handling, PDB runfiles, netstandard facade
selection, and the workaround for Roslyn crashing in the extra reference-only
analyzer pass. Diagnostic analyzers still run on implementation compilation.
The old duplicate-analyzer patch is no longer needed.

The new matrix also exposed upstream dropping newer transitive package references
in favor of older framework assemblies. The patch preserves package-version
precedence for those references. Native packing exposed a Bazel crash when a
transition is attached to a label-keyed file dictionary; explicit package files
now retain their incoming configuration. Reassess these changes on an upgrade.

## Native package and runtime validation

### Additional existing portable samples

`eng/bazel/samples.bzl` adds opt-in targets without changing sample test bodies:

| Target | Verified behavior |
| --- | --- |
| `sdk_runner_sample_test` | Existing net8 SDK-runner example: one passing case, using native source assemblies rather than proving SDK-package behavior. |
| `reliable_suite_test` | All 11 existing net8 reliability cases pass, including retry/cancellation and the real pinned-dotnet executable condition. |
| `scale_10c_test`, `scale_100c_test`, `scale_1000c_test` | Existing method-parallel net8 scale examples: 1,000, 10,000 and 100,000 passing cases. These are scalability fixtures, not newly generated coverage. |
| `custom_report_test` | The existing passing custom-report example; the separate manual failure probe retains the intentional failing example and exit 2. |
| `ensure_sample` | Existing custom framework: discovery finds its one case; real execution intentionally fails with exit 2. |
| `otel_sample_test` | Existing net9 host-composition example: five passes and real source-built OpenTelemetry output. |
| `browser_publish` | Native upstream WebAssembly bundle; unchanged Node runner passes one browser-wasm case and propagates minimum-count exit 9. |
| `benchmark_runner` | Native self-contained net10 layout; build with `-c opt`. Twelve existing benchmarks are discoverable and an in-process Dry execution runs without an MSBuild-generated host. |
| `aot_sample` | Existing SDK AOT sample compiles with source-generated reflection metadata and passes its one managed case. This alone is not NativeAOT execution. |

The manual `aot_publish` invokes upstream's native AOT publisher, but the pinned
upstream does not supply NativeAOT for this existing sample's net8.0 target:
an isolated Linux attempt also rejects `net8.0 on linux-x64`. Windows native
linking is separately unsupported. The sample was not retargeted to hide this
boundary, and its managed pass is not AOT coverage. None of these targets enlarges
the previously verified `portable_tests` aggregate.

Packages carry an explicit `-preview.bazel` prerelease version derived from
`eng/Versions.props`; they are not official shipping packages. They include the
portable DLLs, XML documentation, PDBs, satellite resources, readme, and relevant
`build`/`buildTransitive` assets. Dedicated analyzer, MSTest and SDK/meta-package
validators exercise the native layouts; complete official package/workload and
release parity remains outstanding.

```powershell
.\eng\bazel.ps1 build //:mstest_package_closure //:mstest_package //:mstest_sdk_package
.\eng\bazel\validate-analyzers-package.ps1 -Jobs 2 -EvidenceRoot 'C:\tfb\analyzer-evidence'
.\eng\bazel\validate-mstest-packages.ps1 -OutputRoot 'C:\tfb\mstest-evidence' -VerifySatelliteSensitivity
.\eng\bazel\validate-sdk-packages.ps1 -Jobs 2 -EvidenceRoot 'C:\tfb\sdk-evidence'
```

Use unique writable evidence roots for concurrent worktrees. The analyzer gate
checks actual C#/VB compiler diagnostics, generated configs, fixes and an older
Roslyn host. The MSTest gate checks loaded native hashes, registration,
localization, actual four-runtime execution and MSBuild parent exits; its optional
sensitivity check repacks the adapter without one required satellite. The SDK
gate distinguishes layout/evaluation from execution of default/None SDK and
meta-package consumers, including the pinned external CodeCoverage dependency.
`-LayoutOnly` deliberately omits composed execution and is not a consumer pass.

`validate-packages.ps1` stages the three NuGets in an isolated local feed and
restores into a fresh cache. Source mapping prevents first-party packages from
coming from nuget.org. It compiles the unmodified explorer sample against each
TFM, verifies loaded net11 assembly hashes and native-build metadata, and checks
TRX output and exits 0, 2, and 9. MSBuild is used only for these external package
consumers, never to build the TestFx producer.

The same script copies each self-contained publisher outside Bazel's output
tree, points `DOTNET_ROOT` at a nonexistent directory, checks the actual runtime
major and loaded CoreLib location, and repeats pass/failure/zero-result checks.
Evidence is written to `artifacts/bazel-package-validation/<run>`.

## Native Windows validation

On an existing AppX-capable Windows session:

```powershell
.\eng\bazel\windows\validate.ps1 -ArtifactsDirectory 'C:\tfb\windows-evidence'
.\eng\bazel\windows\validate-app-model.ps1 -ArtifactsDirectory 'C:\tfb\app-model-evidence'
```

The first gate builds all fourteen library variants and runs seven copied
self-contained consumers, checking actual runtimes, loaded hashes, public-key
identity, Windows minimums and projection selection. The second builds both
controllers and the native console host, then exercises unpackaged and
suppressed execution, real registration/activation, 0/2/9 results, malformed
bootstrap exit 5 and discovery without the execution handshake. It verifies
the run-owned package/install identity and reads back empty registration after
cleanup. It does not alter Developer Mode, ACLs, global SDKs or verification
policy.

This is a non-XAML loose-package validation, not a signed MSIX archive or a
WinUI/XAML application pipeline. The bounded standalone XAML compiler
experiments did not produce C# or XBF; no MSBuild hybrid bridge is presented
as a native build. These Windows gates are separate from the portable CI job.

## GitHub Actions build cache

The experimental `.github/workflows/bazel.yml` workflow restores two separate
GitHub Actions caches on Windows and Ubuntu:

| Store | Contents |
| --- | --- |
| `artifacts/bazel-cache/downloads` | Content-addressed downloaded SDK, rules and NuGet archives (`--repository_cache`). |
| `artifacts/bazel-cache/actions` | Compiled/generated action outputs (`--disk_cache`). |

Successful non-PR runs on `main` save new immutable snapshots after stopping
their Bazel server. The workflow now runs on relevant `main` pushes to seed
these stores. PRs only restore; they do not run the save actions. OS/architecture
and Bazel/toolchain settings partition compatible snapshots. Per-run keys with
compatible restore prefixes allow reuse across commits, while Bazel still
checks each action's declared inputs. A cache miss or eviction runs the same
full validation rather than skipping work.
PRs remain cold until the first successful compatible `main` seed is available.

Before a successful main save, the stopped job trims oldest cache files to
1,536 MiB of download payload and 512 MiB of action payload: at most 2 GiB
uncompressed per platform for a new snapshot pair. Missing blobs are ordinary
cache misses and are downloaded/rebuilt when needed. This bounds accumulated
store contents, not repository-wide archive history: multiple immutable
generations, archive overhead and other workflows still share GitHub's quota.

The output base, server state and extracted repositories are **not** cached.
In particular, Bazel 9's repository-content cache is redirected to
`<output-root>/repositories`, outside both the source checkout and the download
archive store. Consumer NuGet restores remain isolated, and both test commands
retain `--nocache_test_results`; this enables build reuse, not cached test passes.

For local use, the wrapper supports two independent, absolute single-line paths:

```powershell
$env:TESTFX_BAZEL_CACHE_ROOT = Join-Path $PWD 'artifacts\my-bazel-cache'
$env:TESTFX_BAZEL_PROFILE_ROOT = Join-Path $PWD 'artifacts\my-bazel-profiles'
.\eng\bazel.ps1 test //:protocol_contract_tests --nocache_test_results
```

Without these environment variables, existing local behavior is unchanged.
Do not share an output root between concurrent invocations/worktrees or put
credentials in either cache. The wrapper removes its per-invocation rc file;
profiles and BEP records have distinct names and remain outside the stores.

CI uploads `artifacts/bazel-profiles` with the existing evidence artifacts.
The separately named restore/save steps expose cache-transfer durations,
compressed sizes and matched keys in the GitHub job timeline/logs. Compare
the **whole job**, including transfers and artifact handling, and inspect BEP
cache-hit statistics before reporting a speedup. Whole-archive snapshots incur
transfer/extraction overhead and GitHub cache quota/eviction; a remote action
cache remains a possible later optimization, not part of this change.

## Build-and-test cache dogfood

`azure-pipelines-bazel.yml` is an independent experimental pipeline. Register it
against this repository in the public dnceng project and keep its GitHub status
**non-required**. It is not imported by the required or official pipelines and
does not supply their binaries, tests or coverage. Keep fork secrets disabled;
only the summary job maps the job token for build-timeline reads.

Ordinary PR/main runs select `Cached`. The nightly schedule and the manual
`runBenchmarks` parameter also select the three controls:

| Mode | Work |
| --- | --- |
| `MSBuild` | Fresh MSBuild outputs and execution of the matching unit assemblies. |
| `Cold` | Fresh Bazel outputs and compiled/test cache; tests execute. |
| `BuildCache` | Fresh outputs restored from the action cache; tests must execute. |
| `Cached` | Fresh outputs, build-cache and eligible test-result reuse. |

The bounded workload is all thirteen original unit projects at net10.0, using
the exact SDK12 compiler, Release/optimized compilation, eight build workers and
at most two test processes. Existing in-process scheduling is unchanged. The
analyzer/server-client suites retain their self-contained runtime; MSBuild
explicitly uses the matching runtime for those assemblies. Other suites use
the SDK runtime. Runtime drift, missing targets, empty/partial TRXs and different
canonical test identities/outcomes prevent a comparable result.

This is not the full required pipeline: no affected-test filtering, coverage,
acceptance, packaging, Windows application activation or shipping validation
is included. Those remain on the existing pipeline. The comparison includes
the selected test targets' localization/runtime dependencies, not just the
primary-output matrix.

For a local experiment, keep a dedicated cache and use a **new run directory
for every command**:

```powershell
$cache = Join-Path $PWD 'artifacts\bazel-dogfood-cache'
$runs = Join-Path $PWD 'artifacts\bazel-dogfood-runs'
.\eng\bazel\dogfood.ps1 -Mode Cached -CacheRoot $cache -RunRoot (Join-Path $runs ([guid]::NewGuid().ToString('N')))
.\eng\bazel\dogfood.ps1 -Mode Cached -CacheRoot $cache -RunRoot (Join-Path $runs ([guid]::NewGuid().ToString('N')))
.\eng\bazel\dogfood.ps1 -Mode BuildCache -CacheRoot $cache -RunRoot (Join-Path $runs ([guid]::NewGuid().ToString('N')))
```

`-Targets '//:protocol_contract_tests'` is a smaller local probe. Its selection
gets a different scope fingerprint and must not be compared with the full
workload. Remove any `.bazelrc.local` override in an isolated checkout rather
than letting local options silently change the experiment. Windows x64, the
repository's normal build prerequisites and symlink capability are required.

Azure caches downloaded archives and the action store separately. It does not
cache the output/server tree or extracted repositories with machine-specific
links. Compatible prefix restores allow reuse across commits; per-run keys
allow immutable snapshots to accumulate new entries. PR cache writes cannot
replace the target branch's cache. The MSBuild control caches its private pinned
SDK/runtimes and NuGet packages, not its `bin/obj`.

Each job retains `context.json`, `result.json`, exact commands, logs, BEP/binlogs
and TRXs. Newly executed reports are published to Azure test results; reused
reports remain under `reports/cached` as historical evidence, never as new
passes. A dependent summary reads Azure's completed timeline, including
post-job cache uploads. `comparison.json`, `timings.csv` and the job summary
show build/test/job time, cache-transfer time and executed/reused counts.
Missing timing stays unknown and is not included in a speedup. Compatible
successful history records provide sample/revision counts, median and P95 job
times; a repeated SHA or small sample is not a representative CI estimate.

The original host-dependent failures are not suppressed. Failed or incomplete
runs remain failed and are excluded from successful timing samples. Cached test
results are experimental, target-level evidence; regular forced-execution
controls and input-invalidation checks are required before considering any
replacement of authoritative validation.

## Evidence and next migration gates

See the maintained [research and decision record](decisions/bazel-build.md) for
experiments, exact commands, red/green evidence, and remaining limitations.
`validate.ps1` produces `validation.json`, command logs, copied TRX files, reports,
and `actions.json`. Its portable suite has 3,443 passing cases across ten targets;
the inspected managed action graph contains no MSBuild execution.

The combined native source revision `c910ab89ce7dc95c3ab2666ed62c4172a9950183`
was validated after integrating all seven lanes. All modern, compatibility and
Windows library builds succeeded. The thirteen original unit projects reported
12,502 passes, 15 original platform-dependent skips and exactly the two known
Win32-1346 failures described above; all 34 native helper cases and six
targeting/reference analysis checks passed. The full-unit command correctly
returned failure rather than filtering those host limitations.

On that same integrated source, the SDK/meta gate passed all 40 steps, the
MSTest/runtime/satellite-sensitivity gate all 31 steps, and the analyzer package
gate all 15 cases. Separately recorded parent runs cover real Windows
registration/controllers, all 111,000 scale-example cases, copied browser-wasm
execution with exits 0/9, and opt-in SDK12 compilation with actual 0/2/9 consumer
outcomes. These are local results, not CI or release certification.

Before replacing an existing shipping leg, add the missing framework/workload
toolchains and test harnesses, reproduce every NuGet/SDK layout, preserve signing
identity and audits, then run actual packed consumers and release-branch
acceptance. A successful `managed` or `baseline_tests` target does not waive
those gates.
