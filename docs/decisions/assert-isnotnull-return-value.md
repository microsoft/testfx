# Assert.IsNotNull: research and decisions

## Problem and contract

[Issue #10918](https://github.com/microsoft/testfx/issues/10918) requests an
expression-form null assertion that evaluates its argument once and returns the
checked value with its original type. The issue discussion explicitly selects
the next breaking release instead of retaining non-generic `void` compatibility
overloads. This checkout targets MSTest 5.0 and Microsoft.Testing.Platform 3.0
(`eng/Versions.props`).

Replace the two `Assert.IsNotNull(object?, ...)` methods with unconstrained
generic methods accepting `T?` and returning `T`. Preserve reference identity,
value types (including nullable value types), parameter null-state narrowing,
caller-argument expressions, failure messages, assertion telemetry, and lazy
interpolated messages. Annotate the returned value as non-null, including when a
caller explicitly supplies a nullable type argument. Do not unwrap nullable
structs or add separate reference/value-type overload families.

Null still throws `AssertFailedException` outside an assertion scope. Inside
`Assert.Scope()`, preserve the existing soft-failure contract: collect the
failure, return the original null value, and continue until scope disposal.
Nullability postconditions are not guaranteed inside a scope, as documented in
[RFC 011](../RFCs/011-Soft-Assertions-Nullability-Design.md).

The changed consumer boundary is the public `MSTest.TestFramework` package,
which bundles `MSTest.TestFramework.dll` from the framework project through
`TestFramework.Extensions.csproj`. Verify packed C# 12 consumers on .NET 10 and
.NET 11, and an F# consumer using the repository-pinned SDK. This synchronous
library API introduces no process, handshake, cancellation, fallback, reporter,
or UI contract. Media and rendered-surface checks are therefore inapplicable.

This is intentionally binary-breaking. Untyped C# `null`/`default` calls need
an explicit type argument or typed value; delegates targeting the old `void`
signature must be adapted. F# statement-form calls may need `|> ignore` now
that a value is returned. Existing typed statement-form C# calls remain valid.
This change belongs to the MSTest 5.0 line, not a 4.x servicing release. No
downstream enablement, feature flag, backport, or publication is part of this
change; the implementing contributor owns the package-consumer checks.

## Research and experiments

- **2026-11-05, issue research:** The issue's final maintainer decision rejects
  permanent `void` shims and identifies the existing value-returning positive
  type assertions as prior art. `IsNull` remains `void`.
- **2026-11-05, source inspection:** `Assert.IsNull.cs` contains string and
  interpolated-handler overloads. Their common failure reporter preserves
  caller expressions and supports soft assertion scopes. Reuse these paths
  rather than introduce new failure formatting or handlers.
- **2026-11-05, test inspection:** Framework unit tests use the sequential
  `TestContainer` engine and AwesomeAssertions. Acceptance tests use MSTest
  method-level parallelism with CPU-count workers. New package consumers must
  have per-test assets/caches; no scheduling changes or shared-state locks are
  needed.
- **2026-11-05, baseline setup:** Base revision is
  `92e83edafb54811009aae479caf29a763f19a99a`. The pinned SDK installed locally,
  but the initial framework-test build stopped before compilation because the
  runtime-install subprocess could not resolve `powershell`. This is setup
  failure, not a test result. Installing the pinned .NET 10/11 runtimes directly,
  selecting dotnet MSBuild, and retaining each project's own target frameworks
  resolved setup. Framework-test builds exclude unrelated Windows app-model
  targets and downgrade only the existing NuGet pruning advisory `NU1510`.
- **2026-11-05, executed baseline:** The focused
  `/*/*/AssertTests/*IsNotNull*` discovery and execution selected the same seven
  existing tests on .NET 10. All seven executed and passed, with no failures or
  skips (exit 0). Both TRX and JUnit reports parsed successfully.
- **2026-11-05, shipping validation:** Scoped pack attempts exposed the Windows
  pack hook's requirement for the complete MSTest package set. Used the normal
  full Windows build/pack with the already-installed Windows SDK, an explicit
  child-process PowerShell path, and a unique binlog filename. It completed with
  zero warnings/errors and passed package-content verification; no machine-wide
  SDK installation or repository build-configuration change was needed.

## Alternatives and decision

- **No change / local variable plus assertion:** Already evaluates once and is
  safe, but does not support the requested expression form.
- **Add generic methods beside the existing methods:** Avoids a binary break,
  but retains competing overload families permanently. Rejected by the issue's
  agreed major-release direction.
- **Separate class and nullable-struct overloads:** Could additionally unwrap
  nullable structs, but expands the overload surface and changes the checked
  value's type. Not needed for this request; not implemented.
- **Chosen:** Replace each existing overload with one unconstrained generic
  method, returning its argument after the existing assertion path. This keeps
  all existing input categories and message behavior without compatibility
  shims or additional handler types.

## Validation and demonstration

The pinned SDK is `12.0.100-alpha.1.26509.101`. Unit evidence uses Debug builds
and the unchanged selector `/*/*/AssertTests/*IsNotNull*`.
The initial reviewed validation base is
`6876cf113dce6e1db55092779e73840697a0af73`; the earlier baseline entry above
records the original starting point.

### Review passes

1. **API and compatibility:** Reviewed both generic overloads, nullable
   inference, reference/value inputs, interpolated handlers, caller expressions,
   telemetry, scoped continuation, and API removals. No production defect found;
   the intentional MSTest 5 compatibility breaks remain documented.
2. **Tests and integration:** Reproduced an unquoted-path defect in the new F#
   consumer: both target-framework cases failed with `MSB1008` when the asset
   path contained spaces. Both consumer builds now use their explicit working
   directory without a redundant path argument, and both asset names include
   spaces permanently. All eight language/host cases passed after the fix.
   This is a harness setup-failure repro, not the product's behavioral red phase.
3. **Revised delivery:** Integrated the current `main` by fast-forward, preserving
   its `DataTestMethodAttribute` API removals alongside this feature's entries.
   Re-reviewed the combined diff with no further findings, reran the full
   Windows build/pack and all 61 focused cases, and refreshed the isolated
   behavioral mutation/restore proof on the final base.

| Requirement / risk | Automated evidence | Surface |
| --- | --- | --- |
| Typed return, reference identity, and single evaluation | `IsNotNull_WithReferenceType_ReturnsSameInstance`, `IsNotNull_WithNullableTypeArgument_ReturnsNonNullableValue`, the two value-type cases, and `IsNotNull_EvaluatesArgumentOnce` passed. | Nonvisual public API. |
| Null failures and nullable flow analysis | Existing exact-message tests and `IsNotNull_WithNullNullableValueType_ShouldFail` passed. Packed C# 12 warnings-as-errors compilation passed on .NET 10 and .NET 11, including nullable type arguments and unconstrained generic forwarders. | Exceptions and compiler diagnostics. |
| Lazy messages and soft scopes | Existing interpolated-message checks and both `Scope_AssertIsNotNull_*` cases passed, including returning null and collecting a following failure. | Existing assertion behavior, unchanged formatting. |
| Major-release API and package compatibility | Full Windows Release build/pack passed. Both `PublicAssertCallShapes_CompileWithCSharp12` and `IsNotNullCallShapes_CompileAndRunWithFSharp` passed on .NET 10 and .NET 11. | Actual packed framework assembly; the F# case asserts the exact loaded assembly path and success output. |
| Analyzer and existing F# callers | All 11 selected `WhenAssertIsNotNull*` analyzer cases passed, including five value-use contexts where replacing the call with `Assert.Fail` would not compile. `RunsNamedNestedExplicitAndCustomModulesAlongsideExistingClass` passed through MTP and VSTest on both target frameworks. | Existing diagnostics/code fixes and four real packaged test-host runs, each asserting six associated test names/outcomes. |
| Test sensitivity | Executed a buildable behavior mutation with the API and all 14 tests retained: eight intended assertion failures, six passes, no skips, exit 2. Restoring correct behavior and rebuilding produced 14 passes, no skips, exit 0. | Behavioral red/green, not an old-API compilation failure. |

The initial correct implementation passed all 14 cases separately on .NET
Framework 4.8, .NET 10, and .NET 11. The paired comparison ran on .NET 10 in an
isolated checkout with separate outputs. Its source and test files were checked
against the implementation before mutation and again after restoration. The
mutation returned `default` for non-null inputs without changing failure
reporting, telemetry, tests, selection, or scheduling. All eight failures were
the expected value/identity assertions, not compiler, setup, launch, or timeout
failures. TRX and JUnit reports were parsed and reconciled with the 14
source-declared identities.

The retained production/API/test patch for the paired comparison has SHA-256
`98BF6C9FD3E7316AFC73B274140B3328CFD468C6CDA22933D31EA5C59B9A3583`.
The isolated behavior-only mutation patch has SHA-256
`08373505424D107614A964D4EC0D3DA0F27F5B51027AA46A599475D7F353A8E6`.
The initial reviewed code/test patch, excluding this decision record, has SHA-256
`ADA786C6210A00CE218BDADFCC938C016AD38E019DF4E653EBE98A5069E482E9`.
Exact commands, binary logs, reports, and a revision-bound evidence manifest are
retained in task-owned session artifacts.

Initial reviewed Release execution selected and passed 61 cases: 14 framework cases on each
of .NET Framework 4.8, .NET 10, and .NET 11; 11 analyzer cases; and eight acceptance
cases. There were no skips. The repository execution verifier reconciled all
eight planned acceptance identities with a fresh TRX and exit 0; configured
JUnit reports also parsed successfully. Unit tests retained their sequential
`TestContainer` scheduling; analyzer/acceptance runs retained method-level
parallelism with 32 workers. Per-test assets and caches required no new locks
or serialization.

Reproducible selections from the repository root are below; executed runs also
enabled TRX/JUnit reporting and used task-owned result/binlog destinations:

```powershell
$binlog = Join-Path (Get-Location) ("artifacts\log\isnotnull-" + [Guid]::NewGuid().ToString("N") + ".binlog")
if (Test-Path -LiteralPath $binlog) { throw "Refusing to overwrite an existing binary log." }
.\build.cmd -c Release -pack -verbosity quiet /p:SkipRuntimesInstall=true /p:EnableCodeCoverage=false /p:WarningsNotAsErrors=NU1510 /nr:false "/bl:$binlog"
.\.dotnet\dotnet.exe artifacts\bin\TestFramework.UnitTests\Release\net10.0\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll --treenode-filter "/*/*/AssertTests/*IsNotNull*"
.\.dotnet\dotnet.exe artifacts\bin\TestFramework.UnitTests\Release\net11.0\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.dll --treenode-filter "/*/*/AssertTests/*IsNotNull*"
.\artifacts\bin\TestFramework.UnitTests\Release\net48\Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.exe --treenode-filter "/*/*/AssertTests/*IsNotNull*"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Release\net10.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~PreferAssertFailOverAlwaysFalseConditionsAnalyzerTests.WhenAssertIsNotNull"
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Acceptance.IntegrationTests\Release\net12.0\MSTest.Acceptance.IntegrationTests.dll --filter "FullyQualifiedName~AssertSourceCompatibilityTests|FullyQualifiedName~FSharpModuleTests.RunsNamedNestedExplicitAndCustomModulesAlongsideExistingClass"
```

The consumed package was `MSTest.TestFramework.5.0.0-dev.nupkg`, SHA-256
`25106BFD07654C9E3486BC8F514D980AA2A35892DD4F4D5F0F00D520B2B43868`.
All four final language consumers restored byte-identical copies into separate
per-test caches. Their resolved framework DLLs matched the corresponding packed
.NET 10/.NET 11 payloads; the F# consumers' actually loaded binaries matched too.
The composed F# module runs used aligned `MSTest.TestAdapter` 5.0.0-dev and MTP
3.0.0-dev packages.

The isolated comparison checkout was removed after restoring and checking its
source/test identity. All 18 inspected task-owned acceptance asset/cache/marker
directories were also removed after retaining the evidence. Four SDK/runtime
download archives remain in Windows Temp because Windows denied their deletion;
no permissions were changed or bypassed, and shared caches were not cleared.
Reproducible source, package artifacts, patches, binary logs, result reports,
and provenance/cleanup manifests remain available locally. Publication is limited
to the explicitly requested pull request; no external-service result is used as
local validation evidence.

## PR feedback: retain code-fix target typing

The review of the initial head
`13fac566b11e8de037ece87887a83a1df5ae01c2` identified an in-scope consumer
regression: MSTEST0037 rewrote `Assert.AreNotEqual<object>(null, new())` to
`Assert.IsNotNull(new())`, discarding the type that target-typed `new()` needs.
The same defect applies to target-typed `default`, collection and conditional
expressions, including calls with a message.

The fixer now retains existing explicit type arguments when the target
`IsNotNull` API is generic. It detects the API in the consumer compilation so
MSTest 4's nongeneric `IsNotNull` fix remains valid. The assertion API and
diagnostic behavior are otherwise unchanged.

`WhenAssertAreNotEqualAndExpectedIsNull_PreservesExplicitTypeArgument` executed
five C# rows with the same expected fixed source before and after the correction.
Before the fix, all five failed at the code-fix verifier's exact output
assertion because the generated invocation omitted its required type argument
(exit 2). After the correction, the complete MSTEST0037 class passed 139 cases
on .NET 10; seven relevant cases also passed on each of .NET 11 and .NET
Framework 4.7.2, including
`WhenAssertAreNotEqualAndExpectedIsNull_MSTestV4RemovesTypeArgument`.
All runs retained method-level parallelism; there were no skips. TRX and JUnit
reports were parsed and checked against the selected cases.

The correction is bound to the initial PR head plus code/test patch SHA-256
`BAA630CF848B95B2B4F8BC6C0CE057C454ECBD083833A1CB5A16707A4670A668`.
The rebuilt `MSTest.Analyzers.5.0.0-dev.nupkg` has SHA-256
`52745219DC4B5263416557C433573505556406B1B073BD827510204C7A4A85C1`;
its code-fix DLL was verified against the fixed build. Project build and pack
completed with zero warnings/errors; the existing complete Windows package set
was retained for the repository's package-validation hook.

```powershell
.\.dotnet\dotnet.exe artifacts\bin\MSTest.Analyzers.UnitTests\Release\net10.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~UseProperAssertMethodsAnalyzerTests" --report-trx --report-trx-filename mstest0037-green.trx --report-junit --report-junit-filename mstest0037-green.xml --results-directory artifacts\TestResults\Release\IsNotNullMstest0037Final --show-test-results failed
```

The complete command exited 0 with 139 passes. The two other runtime checks
used `FullyQualifiedName~UseProperAssertMethodsAnalyzerTests.WhenAssertAreNotEqualAndExpectedIsNull`
and exited 0 with seven passes each. This follow-up evidence does not relabel
the initial head's CI result as verification of the follow-up commit.

## Open questions

None in the requested API contract. Validation is local Windows evidence, not a
CI claim; Linux/macOS execution remains outside this validation.
