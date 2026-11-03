# Experimental native Bazel build

This branch provides a **staged development build, not a drop-in replacement for
the shipping MSBuild/Arcade pipeline**. Bazel compiles 35 of the 41 projects under
`src`, including a .NET 8/9/10/11 matrix for the portable assemblies, without
running MSBuild or consuming its outputs. Three experimental MTP/TRX NuGets and
four self-contained test hosts are also produced natively. Existing release,
signing, and official packing workflows remain unchanged.

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
a scoped compatibility patch. Its toolchain downloads SDK
**11.0.100-rc.3.26522.148** (runtime **11.0.0-rc.3.26522.148**). Neither the
machine SDK nor `global.json` selects this experimental compiler.

The repository's exact .NET 12 engineering SDK archive is now available on
`ci.dot.net`, but its public release-metadata endpoint is absent. Integrating that
SDK is still required for compiler parity; this build does not silently replace
the repository pin or claim to use it.

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
| `//:explorer_released` | Existing platform explorer compiled against its released MTP 2.5.2 packages. |
| `//:explorer` | The same sample compiled against this checkout's MTP and TRX assemblies. |
| `//:simple1` | Existing `samples/public/mstest-runner/Simple1`, using the source-built MSTest runner and reporters. |
| `//:baseline_tests` | Seven test targets: the three sample smoke runs, existing framework/adapter/platform-services suites, and build-tool regression cases. |
| `//:portable_tests` | The baseline plus protocol-contract, source-generation, and VSTestBridge suites: 3,443 cases across ten targets. |
| `//:framework_tests` | All 1,634 existing framework tests, using the existing sequential `TestContainer` engine. |
| `//:adapter_tests` | All 152 existing adapter tests, using `TestContainer`. |
| `//:platform_services_tests` | All 1,215 existing platform-services tests, using `TestContainer`. |
| `//:build_tools_tests` | Thirteen cases for RESX/XLF handling, version/build-stamp generation, and experimental package-version validation. |
| `//:protocol_contract_tests` | All 27 existing protocol-contract cases, compiling the shared wire sources independently. |
| `//:source_generation_tests` | All 197 existing source-generator cases, retaining method parallelism with four workers. |
| `//:vstest_bridge_tests` | All 202 existing bridge cases, retaining method-level parallelism. |
| `//:packages` | Experimental `Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.TrxReport.Abstractions`, and `Microsoft.Testing.Extensions.TrxReport` NuGets, each with four portable TFMs. |
| `//:published_samples` | Self-contained platform explorer layouts for .NET 8/9/10/11 on the selected host RID. |
| `//:platform_tests` | Full 3,380-case MTP suite, preserving method-level parallelization. Two real Windows impersonation cases are host-blocked in local validation; this target is deliberately not part of `baseline_tests`. |

The default configuration remains `net10.0`; `managed_all` explicitly builds the
four-framework matrix. The assertion generator, MSTest
analyzers/code fixes, and MSTest source generator retain `netstandard2.0`;
the server-client validation assembly retains `net8.0`. This is **not** the
full matrix of their project files: netstandard and .NET Framework variants of
other assemblies and Windows-qualified TFMs remain outside the current graph.
Ordinary test launches use the selected SDK's .NET 11 runtime; the separate
self-contained publisher checks prove execution on each actual runtime.

The six source projects outside `managed` are:

| Project | Remaining work |
| --- | --- |
| `MSTest.Analyzers.Package` | Analyzer package assembly/layout orchestration. |
| `MSTest` | Meta-package dependencies and packed assets. |
| `MSTest.Sdk` | SDK templates, package metadata, and consumer build hooks. |
| `Microsoft.Testing.Extensions.PackagedApp.MSBuild` | Exact controller/package layout and cross-TFM assets. |
| `Microsoft.Testing.Extensions.PackagedApp.Controller` | Windows-targeting packs and controller apphost deployment. |
| `TestFramework.Windows.UIAutomation` | Windows/WPF targeting and runtime validation. |

Not yet migrated or established: the other unit-test projects, integration and
package-consuming acceptance suites, every public example, the complete
.NET Framework/netstandard matrix, WinRT/UWP/WinUI/WPF workloads, AOT, strong-name
identity, SourceLink/API-audit parity, official package layout/signing/publishing,
and downstream servicing. The assemblies are unsigned development outputs.

The portable `PackagedApp` assembly is included; its Windows register/activate
flavor and real packaged-app execution are **not** implied by that compilation.
Likewise, building the server-client validation DLL does not validate the
source-only package's consumer transformation.

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

`eng/bazel/TestMain.cs` replaces only the selected suites' registration/entrypoint
plumbing. It keeps their test bodies and scheduling, registers the real source
runner and TRX reporter, and uses each Bazel test's undeclared-output directory.
It does not register the Arcade-only affected-test/coverage collectors or
dogfood every extension. Suites with special child-process bootstraps or extern
aliases need their own faithful migration, not this generic entrypoint.

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

Packages carry an explicit `-preview.bazel` prerelease version derived from
`eng/Versions.props`; they are not official shipping packages. They include the
portable DLLs, XML documentation, PDBs, satellite resources, readme, and relevant
`build`/`buildTransitive` assets. MSTest, analyzer, SDK/meta-package, Windows,
and netstandard package parity is still outstanding.

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

## Evidence and next migration gates

See the maintained [research and decision record](decisions/bazel-build.md) for
experiments, exact commands, red/green evidence, and remaining limitations.
`validate.ps1` produces `validation.json`, command logs, copied TRX files, reports,
and `actions.json`. Its portable suite has 3,443 passing cases across ten targets;
the inspected managed action graph contains no MSBuild execution.

Before replacing an existing shipping leg, add the missing framework/workload
toolchains and test harnesses, reproduce every NuGet/SDK layout, preserve signing
identity and audits, then run actual packed consumers and release-branch
acceptance. A successful `managed` or `baseline_tests` target does not waive
those gates.
