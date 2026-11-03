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
| `//:baseline_tests` | Seven test targets: the three sample smoke runs, existing framework/adapter/platform-services suites, and build-tool regression cases. |
| `//:portable_tests` | The baseline plus protocol-contract, source-generation, and VSTestBridge suites: 3,443 cases across ten targets. |
| `//:framework_tests` | All 1,634 existing framework tests, using the existing sequential `TestContainer` engine. |
| `//:adapter_tests` | All 152 existing adapter tests, using `TestContainer`. |
| `//:platform_services_tests` | All 1,215 existing platform-services tests, using `TestContainer`. |
| `//:build_tools_tests` | Thirteen cases for RESX/XLF handling, version/build-stamp generation, and experimental package-version validation. |
| `//:reference_alias_tests` | Nine cases for aliases, signed friend access, emitted public keys, and satellite identity. |
| `//:sdk_package_tests` | Twelve cases for native SDK/meta-package generation and dependency/asset flow. |
| `//:compatibility_reference_tests` | Four analysis tests for package version/TFM precedence and preservation of native/independent references. |
| `//:protocol_contract_tests` | All 27 existing protocol-contract cases, compiling the shared wire sources independently. |
| `//:source_generation_tests` | All 197 existing source-generator cases, retaining method parallelism with four workers. |
| `//:vstest_bridge_tests` | All 202 existing bridge cases, retaining method-level parallelism. |
| `//:packages` | Experimental `Microsoft.Testing.Platform`, `Microsoft.Testing.Extensions.TrxReport.Abstractions`, and `Microsoft.Testing.Extensions.TrxReport` NuGets, each with four portable TFMs. |
| `//:analyzers_package` | Native MSTest.Analyzers package, including C#/VB analyzers, C# fixes, satellites and generated analysis-mode configs. |
| `//:mstest_packages` | Native framework, adapter, Telemetry and Platform.MSBuild packages; real netstandard task assemblies are bundled at their required paths. |
| `//:mstest_package_closure` | Those four packages plus the original three MTP/TRX packages and analyzers: eight native archives. |
| `//:mstest_package`, `//:mstest_sdk_package` | Native MSTest meta-package and MSTest.Sdk; together with the closure above, ten packages for isolated consumers. |
| `//:published_samples` | Self-contained platform explorer layouts for .NET 8/9/10/11 on the selected host RID. |
| `//:platform_tests` | Full 3,380-case MTP suite, preserving method-level parallelization. Two real Windows impersonation cases are host-blocked in local validation; this target is deliberately not part of `baseline_tests`. |

The default configuration remains `net10.0`; `managed_all` explicitly builds the
four-framework matrix. The assertion generator, MSTest
analyzers/code fixes, and MSTest source generator retain `netstandard2.0`;
the server-client's default validation assembly retains `net8.0`.
`managed_compatibility` explicitly selects the additional non-Windows-qualified
project variants rather than relying on compatible-framework fallback.
Windows-qualified/workload targets and full runtime parity remain separate gates.
Ordinary test launches use the selected SDK's .NET 11 runtime; the separate
self-contained publisher checks prove execution on each actual runtime.

The six source projects outside `managed` are:

| Project | Dedicated target or remaining work |
| --- | --- |
| `MSTest.Analyzers.Package` | `//:analyzers_package`. |
| `MSTest` | `//:mstest_package`. |
| `MSTest.Sdk` | `//:mstest_sdk_package`. |
| `Microsoft.Testing.Extensions.PackagedApp.MSBuild` | Exact controller/package layout and cross-TFM assets. |
| `Microsoft.Testing.Extensions.PackagedApp.Controller` | Windows-targeting packs and controller apphost deployment. |
| `TestFramework.Windows.UIAutomation` | Windows/WPF targeting and runtime validation. |

Not yet migrated or established: all remaining unit-test projects, the complete
integration/acceptance suites, every public example, complete legacy runtime
behavior, WinRT/UWP/WinUI/WPF workloads, AOT,
SourceLink/API-audit parity, official package layout/signing/publishing,
and downstream servicing. Native assemblies and satellites now use the same
public-only Microsoft signing key as the repository's Arcade development build
(`PublicKeyToken=b03f5f7f11d50a3a`). This preserves assembly and friend identities
needed by separately shipped extensions; it is not official signature completion
or package publication, and requires no private key or machine verification change.

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
