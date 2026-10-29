# Experimental native Bazel build

This branch provides a **staged development build, not a drop-in replacement for
the shipping MSBuild/Arcade pipeline**. Bazel compiles 35 of the 41 projects under
`src`, without running MSBuild or consuming its outputs. Existing release,
signing, packing, and acceptance workflows remain unchanged.

## Getting started

Install PowerShell 7.2 or newer. On Windows, use an environment that already
permits symbolic links (Developer Mode or an appropriately privileged shell).
The bootstrap does not change machine policy or install a global SDK.

From the repository root:

```powershell
.\eng\bazel.ps1 build //:managed
.\eng\bazel.ps1 test //:baseline_tests
.\eng\bazel\validate.ps1
```

The last command also checks discovery, real TRX/HTML/JUnit/CTRF output, paths
containing spaces and `!`, failing-test exits, zero-result exits, mixed-module
failure aggregation, and the native action graph. Evidence is written under a
unique `artifacts\bazel-validation` directory. Its success is scoped to the
baseline, not the complete repository.

The wrapper pins and verifies Bazel **8.8.2**. Bzlmod pins `rules_dotnet`
**0.22.2**, and its toolchain downloads and verifies SDK **10.0.400**
(runtime **10.0.11**). Neither the machine SDK nor the repository's
`global.json` selects this experimental compiler.

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

Bazelisk users can invoke `bazel` directly, but must provide a suitably short
Windows output root themselves. The checked-in `.bazelrc` supplies the required
runfiles settings.

## Targets and current scope

| Target | Contents |
| --- | --- |
| `//:managed` | 35 source projects: MTP, its portable extensions, MSTest framework and adapter, analyzers/code fixes, generators, and the server-client validation assembly. |
| `//:explorer_released` | Existing platform explorer compiled against its released MTP 2.5.2 packages. |
| `//:explorer` | The same sample compiled against this checkout's MTP and TRX assemblies. |
| `//:simple1` | Existing `samples/public/mstest-runner/Simple1`, using the source-built MSTest runner and reporters. |
| `//:baseline_tests` | Seven test targets: the three sample smoke runs, existing framework/adapter/platform-services suites, and build-tool regression cases. |
| `//:framework_tests` | All 1,634 existing framework tests, using the existing sequential `TestContainer` engine. |
| `//:adapter_tests` | All 152 existing adapter tests, using `TestContainer`. |
| `//:platform_services_tests` | All 1,215 existing platform-services tests, using `TestContainer`. |
| `//:build_tools_tests` | Nine focused cases for RESX text/BOM preservation, XLF application/validation, and version/build-stamp generation. |
| `//:platform_tests` | Full 3,380-case MTP suite, preserving method-level parallelization. Two real Windows impersonation cases are host-blocked in local validation; this target is deliberately not part of `baseline_tests`. |

Most native assemblies target `net10.0`. The assertion generator, MSTest
analyzers/code fixes, and MSTest source generator retain `netstandard2.0`;
the server-client validation assembly retains `net8.0`. This is **not** the
full multi-targeting matrix of their project files.

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
package-consuming acceptance suites, every public example, .NET 11, the complete
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

The updater uses pinned Paket 11.0.1 and upstream `paket2bazel`, both launched
through the Bazel toolchain. Review and commit `paket.dependencies`, `paket.lock`,
generated `nuget/paket.*.bzl`, and `MODULE.bazel.lock` together. Do not hand-edit
generated repositories. The source graph's AI/resilience/hosting closure requires
shared 10.0.13 abstractions; these deliberate development-build convergence pins
are documented in the dependency file and do not change `Directory.Packages.props`.

Nine small, version-scoped patches under `eng/bazel/patches` address demonstrated
gaps in the pinned rules: quoted source paths, logical resources, satellites,
short Windows launchers, Windows argument/manifest handling, PDB runfiles,
duplicate framework/package generators, canonical framework references, and a
Roslyn crash in the extra reference-only analyzer pass. Diagnostic analyzers still
run on implementation compilation. Reassess every patch on a rules upgrade;
none has been submitted upstream by this work.

## Evidence and next migration gates

See the maintained [research and decision record](decisions/bazel-build.md) for
experiments, exact commands, red/green evidence, and remaining limitations.
`validate.ps1` produces `validation.json`, command logs, copied TRX files, reports,
and `actions.json`. Its baseline has 3,013 passing cases across seven targets;
the action graph contains no MSBuild execution.

Before replacing an existing shipping leg, add the missing framework/workload
toolchains and test harnesses, reproduce every NuGet/SDK layout, preserve signing
identity and audits, then run actual packed consumers and release-branch
acceptance. A successful `managed` or `baseline_tests` target does not waive
those gates.
