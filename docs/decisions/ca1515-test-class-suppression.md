# CA1515 suppression for MSTest test classes

## Problem and contract

[TestFx #12528](https://github.com/microsoft/testfx/issues/12528) requests that
public MSTest test classes not receive CA1515 when that rule is enabled.
Default MSTest discovery requires public test classes. Internal discovery is
opt-in, not a reason to require existing projects to change accessibility.

Suppress only CA1515 on types directly marked with `TestClassAttribute` or an
attribute derived from it, using semantic identity rather than the short name.
Keep diagnostics on helpers, unrelated attributes, and unannotated types.
Partial declarations share the same type symbol. No framework reference means
no suppression. Opting into `DiscoverInternals` does not require making existing
public test classes internal.

The producer is the netstandard2.0 MSTest.Analyzers assembly, distributed through
the existing C# and VB analyzer assets in MSTest.Analyzers. The consumer is the
Roslyn compiler/IDE running CA1515. No runtime discovery, SDK dependency, public
framework API, or project-wide diagnostic configuration changes are required.

## Research and alternatives

- 2026-11-19: The SDK's `MakeTypesInternal` checks the output kind and reports
  public types without a test-class exception. The earlier cross-framework
  discussion is [dotnet/roslyn-analyzers#7192](https://github.com/dotnet/roslyn-analyzers/issues/7192).
- [dotnet/sdk#57223](https://github.com/dotnet/sdk/issues/57223) asks about upstream
  handling and fairness across frameworks. Local suppression is independent of
  that decision and can benefit existing SDK users.
- Disabling CA1515 for the entire project also hides useful helper diagnostics.
  Changing discovery/accessibility imposes an unrelated migration on consumers.
- Reuse the existing language-independent suppressor pattern, `IsTestClass`
  helper, localized justification, and suppression release catalog. MSTEST0091
  is the next available ID. No new dependency or configuration switch is needed.

## Validation

Focused C# tests cover ordinary and custom test attributes, partial/nested types,
lookalike attributes, missing framework references, and retained helper warnings.
They compare diagnostic suppression flags, including a run without the suppressor.
Test state and compilations are per invocation; no shared mutable resource or
scheduling change is introduced.

`CA1515_OnlyPublicTestClassesAreSuppressed(false/true)` exercises the real SDK
CA1515 analyzer with the packed MSTest.Analyzers and MSTest.TestFramework assets.
The test-only executable must build successfully; adding a public helper must
fail with CA1515 on `Helper.cs`, never on `TestCases.cs`.
Each case owns a generated test asset; the existing acceptance fixture isolates
its package cache. The unit and acceptance assemblies remain method-parallel
with CPU-count workers. No locks or scheduling overrides are added.

### Executed unit-test evidence

Base: `7c711e326166bb1fc0f0f80b3e8811f725ba2817`, plus this change.
Pinned SDK: `12.0.100-alpha.1.26562.108`; Windows x64, Debug.
The suppressor source SHA-256 is
`46B2817B3C4CC0DA50CC3ED1C1E93444954B52F08F42041CE477FE129932339C`;
the unit-test source SHA-256 is
`15444E3CF05284AFA65DD04012A18A41A5640D756A8D0130671C2A9FBB58F0FD`.

Build (exit 0, no warnings):

```powershell
.\build.cmd -msbuildEngine dotnet -projects test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj -bl
```

For `net10.0` and `net11.0`, invoke the matching built DLL using the pinned
`.dotnet\dotnet.exe`; for `net472`, invoke the built EXE. Each invocation used:

```text
--filter "FullyQualifiedName~PublicTestClassSuppressorTests|FullyQualifiedName~AnalyzerReleaseTrackingTests" --report-trx --results-directory <distinct-run-directory>
```

All three runs exited 0 with 10 executed/passed and zero skipped:
seven new cases and three existing suppression/release-catalog cases.
Results are retained under `artifacts\TestResults\ca1515`.

A separately compiled comparison retained the same suppressor ID and descriptor
but made `ReportSuppressions` a no-op. Build intermediates and binaries were
isolated under `artifacts\obj\ca1515-mutation` and `artifacts\ca1515-mutation`;
only that assembly replaced the analyzer in a copied net10.0 test host. The
unchanged tests executed with the identical filter and parallel settings:
exit 2, five failures, five passes, zero skips. Each failure was
`Expected diagnostic suppression state to match`, expecting suppressed CA1515
and receiving unsuppressed CA1515. The comparison source SHA-256 was
`321F57A2D48C99FD6C56FDF805C488D89D821DDADB0E0F176613BF0E77C7C0E5`.
The original host was then rerun: exit 0, all 10 passed. This is executed
behavior-removal evidence, not a compilation failure or a pristine-base run.

The five detecting cases are `PublicTestClass_DiagnosticIsSuppressed` (both
DiscoverInternals settings), `DerivedTestClassAttribute_DiagnosticIsSuppressed`,
`PartialTestClass_AttributeOnAnotherDeclaration_DiagnosticIsSuppressed`, and
`NestedTypes_OnlyAnnotatedTestClassIsSuppressed`.

Localization was regenerated by the owning project build.
`python .github\scripts\check_localization.py` exited 0 (364 catalogs).

### Executed packaged-consumer evidence

The full Windows `.\build.cmd -pack -bl` completed successfully (exit 0,
zero warnings/errors), including the shipping-package checks. The acceptance
host was rebuilt with:

```powershell
.\build.cmd -msbuildEngine dotnet -projects test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj -bl
```

The host build exited 0. With `DOTNET_ROOT` and `PATH` pointing to the
repository's `.dotnet`, the execution command was:

```powershell
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Debug\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter 'FullyQualifiedName~CA1515_OnlyPublicTestClassesAreSuppressed' --report-trx --results-directory artifacts\TestResults\ca1515\acceptance-passed
```

Both data rows executed and passed, with zero skipped and outer exit 0.
The source-planned identities were
`CA1515_OnlyPublicTestClassesAreSuppressed (False)` and
`CA1515_OnlyPublicTestClassesAreSuppressed (True)`. The execution verifier
confirmed planned/selected/executed/passed counts of 2/2/2/2 in the fresh TRX.
The first child's build exited 0; the second exited 1 with CA1515 on
`Helper.cs(1,14)` and no diagnostic on `TestCases.cs`.
The generated consumers target net11.0 using the pinned SDK; the outer host
targets net12.0. The final acceptance source SHA-256 is
`C725E39BCB2A7A2B2BB29CC3C69C206B124716E25F4BBA7F5BF0E577FE57B191`.

The generated consumers' `obj\project.assets.json` files resolved both packages
at 5.0.0-dev in the task-owned `artifacts\ca1515-consumer-cache`, supplied through
`TESTFX_COLLECTION_NUGET_CACHE`. Cached nupkg hashes exactly matched the current
`artifacts\packages\Debug\Shipping` files:

| Package | SHA-256 |
| --- | --- |
| MSTest.Analyzers.5.0.0-dev.nupkg | `98629BE372BAC73D4ADFBAFD622A9D52826EDC7D9AEDBD009A228044D9EB2E38` |
| MSTest.TestFramework.5.0.0-dev.nupkg | `F205F62CC75DA241BD0536565E4AAE0E5A70DA67EB8D820BD931AC0C423AE10C` |

The cached `analyzers\dotnet\cs\MSTest.Analyzers.dll` also matched the built
netstandard2.0 analyzer:
`5FCC4D567F424A31591565C4EBCF2D54E1E9C8B91FAFA1E51C5D9FD69553F0EE`.
No production changes followed the successful pack. The behavior-removal
comparison above was unit-level; no separate mutated-package run was performed.
CLI compilation is validated; Visual Studio rendering and CI execution are not
claimed. The acceptance command was rerun after quoting the generated project path (2/2 passed).

After hash verification, the task-owned consumer cache and isolated comparison
outputs were removed. Final generated consumers remain under
`artifacts\tmp\Debug\testsuite\d4Foo` and `artifacts\tmp\Debug\testsuite\g8V8t`;
shipping packages, logs, binlogs, and TRX evidence are retained.

### Build experiments

Overriding `TargetFrameworks` globally incorrectly changed netstandard restores;
using the project's declared frameworks fixed this. The focused package build
produced the analyzer but the repository-wide package guard expected all
shipping packages. Direct pinned-SDK packs of the analyzer and framework
succeeded after building framework compatibility facades. The shared acceptance
fixture also requires unrelated package versions at initialization, so a full
package build is needed for its execution. The dotnet MSBuild engine cannot
build the legacy UWP targets; the normal Windows build uses desktop MSBuild.

The first acceptance attempt exposed the fixture's inherited
`RunAnalyzers=false`, not a suppressor defect: neither test classes nor helpers
produced CA1515. The generated project now explicitly enables analyzers.
The negative case also disables the command helper's automatic nonzero-exit
exception so its expected exit and diagnostic can be asserted. Neither fix
weakens the negative assertion.

## Open questions

Unannotated base classes and public types exposed through test signatures are
outside this narrowly scoped request. Upstream framework-wide policy remains
with the SDK issue. No servicing/backport or downstream SDK rollout is proposed.
