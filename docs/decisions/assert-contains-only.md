# ContainsOnly: research and decisions

## Problem and contract

[#12260](https://github.com/microsoft/testfx/issues/12260) requests assertions that
verify whether every or no collection element satisfies a predicate, with
diagnostics that identify offending elements rather than only a Boolean result.
The implementation follows the
[maintainer reply](https://github.com/microsoft/testfx/issues/12260#issuecomment-6737728610):

- Add `Assert.ContainsOnly`, taking `Func<T, bool>` before the collection, with
  the existing predicate assertion's enumerable, array, span, and memory shapes.
- Reuse `Assert.DoesNotContain` for the negative polarity; do not add `None`.
- Report the first counterexample's zero-based index and value, retaining the
  predicate and collection expressions. An empty collection passes.
- Visit each element at most once and stop at the first violation. Propagate
  predicate and iterator exceptions, including cancellation; dispose the
  enumerator on success, failure, and exceptions.
- Capture the counterexample's display before disposal can mutate a reused
  element. Built-in formatting must not enumerate nested lazy collections.
  Explicit user value formatters retain their existing guarded behavior.
- Preserve existing `DoesNotContain` invalid-argument behavior. New
  `ContainsOnly` argument failures use MSTest's immediate parameter reporting,
  including inside `Assert.Scope`. Only valid predicate failures aggregate.
- Extend MSTEST0037 for positive `Enumerable.All` checks only when the referenced
  framework exposes the new API and argument evaluation can be preserved.
  Preserve explicit element type arguments. Do not change `Queryable.All`,
  negative `IsFalse(All)` checks, or effectful factory/message calls.

Exact failure totals, action inspectors, async assertions, query-provider
execution, and changes to other assertions' rendering are outside this scope.

The target is `main`, **MSTest 5.0 / MTP 3.0**, not the 4.5.3 servicing milestone.
This introduces a new API; no servicing backport or downstream enablement is
planned. The implementation owner retains responsibility for validating the
framework and analyzer together.

### Shipping consumers

| Boundary | Producer and consumer | Required observation |
| --- | --- | --- |
| Framework behavior | `TestFramework.csproj` produces `MSTest.TestFramework.dll`; `TestFramework.Extensions.csproj` bundles it into `MSTest.TestFramework`. | A real package consumer observes both predicate polarities and their counterexamples. |
| Source compatibility | Packed `MSTest.TestFramework`, compiled by the existing acceptance harness with C# 12. | Existing call shapes remain valid; the new family supports implicit array/interface and span/memory calls. |
| Analyzer guidance | `MSTest.Analyzers` and a compilation referencing the capable framework. | The code fix preserves the predicate, collection, message, and short-circuit contract; older frameworks receive no unusable suggestion. |
| Presentation | The actual built consumer's assertion failure text. | The index, escaped value, and predicate belong to the same counterexample and remain readable. |

The repository pins SDK `12.0.100-alpha.1.26509.101`. Validation uses Release
outputs and net10.0 test hosts; packing must cover the framework's declared
targets, including netstandard2.0 and .NET Framework. Exact produced package
versions and hashes will be recorded after packing. There is no new process,
handshake, protocol, fallback, or multi-module aggregation contract.

## Research and experiments

### 2026-11-09: inventory and design review

Source inspection used base
`b3f3c980c480c5d97976d658ac367b1e246502be` and released v4.5.2
(`933377f07ac4bded77e820b0beef7d27e805cd89`).
`Contains`, `DoesNotContain`, and `ContainsSingle` already support predicates;
no positive general-purpose all-match assertion exists.

The existing predicate `DoesNotContain` delegates to `Enumerable.Any` and loses
the counterexample before reporting failure. `Assert.That` can display a
collection but does not identify its violating entries. `IsEmpty(Where(...))`
reports a count, at the cost of enumerating the whole filtered sequence.
These alternatives were evaluated from source, not executed as experiments.

Independent Claude Opus 5.5 and Gemini 4 Argon reviews confirmed the inventory
and supported a positive assertion plus stronger negative-polarity diagnostics.
Their additional findings were checked against the source: MSTEST0037 has no
positive `All` mapping, existing `AreAll*` assertions scan all elements, and
`AssertionValueRenderer` recursively enumerates collection values.

[#9075](https://github.com/microsoft/testfx/issues/9075) rejected an action-based
aggregating `All` in favor of experimental `Assert.Scope` plus a loop. That
remains relevant API-surface precedent, not an equivalent solution to this
predicate and diagnostic contract.

The first baseline build attempt exited 9009 before MSBuild started because
`build.cmd` could not resolve `powershell` on PATH. It produced no binary log and
is not behavioral regression evidence. Running `eng\build.ps1` directly
bootstrapped the pinned SDK but its runtime-install command wrappers encountered
the same shell lookup problem.

The command bridge did not propagate PowerShell PATH edits to native child
processes. A command-local PATH prefix inside `cmd.exe /d /c` restored normal
shell lookup without changing global settings or repository infrastructure.
The next root build's global `TargetFramework=net10.0` restore property produced
incorrect netstandard2.0 generator assets and unrelated package-pruning errors.
Removing that override and using the pinned dotnet MSBuild engine resolved the
graph.

The successful baseline sequence restored the complete graph through
`eng\build.ps1 -restore -c Release -msbuildEngine dotnet -projects
test\UnitTests\TestFramework.UnitTests\TestFramework.UnitTests.csproj`, then built
that project with the pinned `dotnet build -c Release -f net10.0 --no-restore`.
Both exited zero with no warnings. Unique binlogs are retained in
`artifacts\log\Release`.

Independent discovery and direct test-host execution used
`--treenode-filter "/*/*/AssertTests/*Contain*"`. The baseline selected and
executed 209 tests: 209 passed, zero failed, zero skipped, process exit zero.
The TRX names were reconciled with discovery, not inferred from a green badge.
Evidence is retained under
`artifacts\TestResults\Release\contains-only-baseline`.

### 2026-11-09: implementation

Added the eight predicate collection overloads, a shared first-counterexample
path for `ContainsOnly` and `DoesNotContain`, and a non-enumerating formatter
fallback that still consults the existing per-async-flow formatter registry.
Public and internal API baselines were updated. New evidence labels and summary
text live in neutral resources; XLF regeneration used the owning framework
project's `UpdateXlf` target.

Framework and analyzer net10.0/netstandard2.0 compilation succeeded. The first
framework build identified five formatting diagnostics from the edited files;
those edits were corrected before final validation. Regression tests, packed
consumers, and counterfactual execution are still pending.

### 2026-11-09: shipping package validation

The first targeted pack used dotnet MSBuild and failed because classic UWP
requires desktop MSBuild. Switching to desktop MSBuild compiled every framework
target, but separate project builds ran the solution-level package validator
before the complete MSTest package set existed. A semicolon project list did not
provide a single solution-level validation boundary.

A temporary production-only solution filter, derived from the source projects
in `MSTest.slnf`, groups the complete package producers without building unrelated
tests. The filter belongs under ignored `artifacts\validation`, not source
control. This command, run with the command-local shell PATH described above,
exited zero with zero warnings and errors:

```powershell
.\build.cmd -pack -c Release -msbuildEngine vs -projects artifacts\validation\contains-only-production.slnf /bl:artifacts\log\Release\contains-only-pack-solution-b24b66e199f843eb802c74012826fcb6.binlog
```

The package-content and metadata validator passed for all eight MSTest packages.
Core assertion compilation covered netstandard2.0, net462, net10.0, and net11.0;
the package also contains the UWP core payload and the declared Extensions assets.
The new assertions are included in the same core assembly for each target.

| Package | Version | SHA-256 |
| --- | --- | --- |
| MSTest.TestFramework | 5.0.0-dev | `C693B2820FBFA28301FC216F2C17EFCCF55C5D211E69BFD99B2BC03C61EA08A8` |
| MSTest.Analyzers | 5.0.0-dev | `AEF140442EA5F496AAB8625F8CFFBF1F08D64E091AFCBBAD5883E038948AF8BD` |

The 41 localization guard tests passed, and the read-only catalog guard validated
all 364 catalogs. No XLF files were edited manually.

An owned comparison checkout was created outside the repository tree to avoid
ancestor analyzer-configuration collisions. Test-source copying and behavioral
counterfactual execution remain pending; creating a checkout is not test proof.

### 2026-11-09: regression results and draft handoff

The test pipeline completed source implementation and net10.0 Release builds
with zero warnings and errors. Independent discovery was reconciled with TRX,
JUnit, and CTRF identities and outcomes:

| Selection | Executed | Passed | Failed | Skipped | Exit |
| --- | --- | --- | --- | --- | --- |
| Framework containment tests | 241 | 240 | 1 | 0 | 2 |
| Scoped predicate aggregation | 1 | 1 | 0 | 0 | 0 |
| UseProperAssertMethodsAnalyzerTests | 151 | 149 | 2 | 0 | 2 |

The remaining failures are explicit draft blockers, not successful validation:

- `ContainsOnly_InvalidParameters_ScopedFailuresContinueSafely` expects invalid
  input to permit scoped continuation; the current parameter-reporting path
  throws before the continuation marker. Reconcile this contract before marking
  the feature ready.
- `WhenUsingIsTrueWithStaticEnumerableAll_SuggestsContainsOnly` leaves the
  explicit static `Enumerable.All<int>` invocation unchanged during code-fix
  application.
- `WhenUsingIsTrueAllWithReorderedNamedArguments_PreservesArgumentMeaning`
  leaves the reordered named-argument invocation unchanged during code-fix
  application.

The packed C# 12 compatibility additions and
`ContainsOnlyAndDoesNotContain_ExecuteFromPackedFrameworkWithCSharp12` are
implemented but unexecuted. The compiling counterfactual comparison and actual
rendered-surface inspection are also unexecuted. These gaps must remain visible
in the draft pull request; passing builds, packages, and other tests do not
establish completion.

### 2026-11-09: review and CI corrections

The eight path-combination findings were checked against their inputs and
resolved without speculative normalization: components are literals, fixed TFM
rows, or NuGet-generated paths for the asserted package and runtime asset.

Concrete corrections:

- Bind LINQ operands by parameter ordinal, normalize named outer arguments in
  the fixer, and retain messages regardless of their original argument position.
- Retain explicit `All<Base>` type arguments in `ContainsOnly<Base>` so overload
  resolution inside untyped predicates does not change.
- Decline the automatic rewrite when collection/predicate factories or
  nonconstant messages could move observable work across predicate execution.
  Stable locals, parameters, lambdas, method groups, and constant messages remain
  supported.
- Preserve the shared `CheckParameterNotNull` contract. The original newly added
  invalid-input test assumed soft parameter failures incorrectly; its replacement
  requires immediate collection-first failure, zero predicate visits, and zero
  enumeration. Valid scoped assertion aggregation remains covered separately.
- Fix the three introduced IDE0007 errors. Compare the consumer transcript after
  the process harness's existing blank-line filtering; full diagnostic spacing
  remains asserted independently inside the packaged consumer.

Current executed results:

| Requirement | Evidence |
| --- | --- |
| Collection semantics, indexed evidence, lifecycle and formatting | 241/241 containment tests pass, exit 0. |
| Valid scoped aggregation | `PredicateAssertions_ScopedFailures_AggregateAndContinue` passes, exit 0. |
| Analyzer mapping and safe exclusions | 155/155 analyzer cases pass, exit 0, including explicit-base overload binding, named/message-first arguments, and effectful factory/message exclusions. |
| Real packed consumers | All six `AssertSourceCompatibilityTests` rows pass on net10.0/net11.0, including C# 12 compilation/runtime and existing F# controls; the execution gate confirms planned/selected/executed/passed 6, zero skips, exit 0. |
| CI compile/style errors | Framework, analyzer, and net12.0 acceptance builds pass with warnings treated as errors. |

The complete 41-project production graph was packed to satisfy the acceptance
fixture's declared package inventory; the first consumer attempt with only MSTest
packages failed during fixture initialization, not at a feature assertion.
Fresh package SHA-256 values are:

- `MSTest.TestFramework.5.0.0-dev.nupkg`:
  `A9AE56D8559E28F45648AD9E2A79964EF709863F593CBDF13AF35CBEA8D5A8BA`.
- `MSTest.Analyzers.5.0.0-dev.nupkg`:
  `2AE41405D63B65B323BF402FCE9CFA12177E188264AF1773980F072847237E56`.

#### Executed test-value evidence

Comparisons used the owned sibling worktree, isolated outputs, identical tests
and selectors, and the pinned SDK. No compiler/setup failure is counted as red.

| Contract | Counterfactual | Without behavior | Restored correct implementation |
| --- | --- | --- | --- |
| Analyzer type binding, argument order, static/named fixes | Restore the three analyzer/fixer files from actual pre-fix commit `f98cfe9c3afc1ba120a273de7e43170125316563`, retaining current tests. | 155 execute; the six corresponding cases fail at exact diagnostic/fixed-source assertions, exit 2. | Clean rebuild: the same 155 identities pass, zero skips, exit 0. |
| Positive predicate checking and indexed counterexamples | Compiling mutation: invert the required predicate result in the new assertion and shift reported indices by one. | 241 execute; 19 fail, including `ContainsOnly_AllMatchingItems_EvaluatesEachItemOnce`, `ContainsOnly_FirstMismatch_ReportsIndexValueAndExpressions`, and DNC evidence cases, exit 2. | Clean rebuild: the same 241 identities pass, zero skips, exit 0. |

The analyzer test-file SHA-256 was identical in the paired runs:
`B4855B8708309AC09246AC2432282A5933F8EA9C11708C9B1F3DF07B6AA778B9`.
An initial restoration reused timestamp-newer counterfactual outputs; it was
rejected and repeated with `--no-incremental`. Only the clean restoration above
is green evidence.

Retained results include:

- `artifacts\TestResults\Release\agent-merge-12278-77ecc24b47d34acc9302451265d76fee`
  (framework and scope);
- `artifacts\TestResults\Release\agent-merge-12278-final-5a851bcf42044bbb815ecb1b0fd8c714`
  (analyzer and acceptance build);
- `artifacts\TestResults\Release\agent-merge-12278-composed-51b331b56dd54f6e8e3b0dd1a13cc6b3`
  (fresh pack and six consumers);
- comparison-worktree `artifacts\TestResults\counterfactual`, `restored-clean`,
  `feature-ablation`, and `feature-restored` (paired proof).

CI on the eventual pushed head and actual rendered terminal/IDE layout inspection
remain separate, unclaimed evidence.

### 2026-11-09: follow-up shared-fixer regressions

Two fresh review examples exposed regressions introduced by the first correction:
the shared LINQ matcher removed explicit operand casts through conversion
unwrapping, and message-first comparison fixes could move effectful message
evaluation after their operands.

Argument selection still uses bound parameter ordinals, but now retains
`argument.Value.Syntax`, including explicit casts. Unsafe effectful arguments
before the condition suppress the Boolean-assertion mapping rather than
reordering work. This protection covers the shared comparison path as well as
the new `All` path.

The analyzer class now passes 158/158 cases, including
`WhenUsingLinqWithCastedPredicate_PreservesExplicitConversion` for both `Any` and
`All`, and
`WhenUsingIsTrueWithEffectfulMessageBeforeComparison_NoDiagnostic`.

An isolated comparison retained the same 158 test identities and restored the
two affected production files from actual pre-fix commit
`ca81cf9feccf1c4b357a6a9012dba95b96cca211`. Exactly the three new rows failed at
diagnostic/fixed-source assertions, exit 2. After clean restoration the identical
158 cases passed, zero skips, exit 0. The test source SHA-256 was identical:
`604CBBEE4770538F399282C3791F05A087EAE1C60C6745356D8B912EA3E7812B`.

Retained evidence: main-worktree
`artifacts\TestResults\Release\agent-merge-12278-followup-861d5eb384204e3f9a0ed6b1477e5aea`,
and comparison-worktree `artifacts\TestResults\followup-red` and `followup-green`.
The earlier 241 framework, one scope, and six packaged consumer checks remain
unchanged, making 406 passing targeted cases across the current selections.

### 2026-11-09: .NET Framework CI compatibility

[Azure build 1670988](https://dev.azure.com/dnceng-public/cbb18261-c48f-4abb-8651-8cdcb5474649/_build/results?buildId=1670988)
tested source head `88a2a62c52c4e45a2da6df4142cb89efa54c8dad`. Windows Debug,
Windows Release, and app-model dependency packing all failed on the same
introduced CS1501 error in the named-argument test: the
`string.Replace(string, string, StringComparison)` overload is unavailable on
the test project's declared net472 target.

The test now uses the equivalent ordinal two-string overload, without changing
the input, expected fixed source, target frameworks, assertions, or CI gates.
Pinned-SDK net472 Release and Debug builds pass with warnings treated as errors
and zero warnings/errors. The net472 analyzer class executes 149/149 cases,
including both named-argument order rows, with zero skips and exit 0. The same
two rows also pass on net10.0.

Evidence: `artifacts\TestResults\Release\agent-merge-net472-05712680da22425d974e0fc69ec11338`
contains the three unique build binlogs and the legacy/modern TRX reports.
This is compilation compatibility evidence; the CI compiler error is not
presented as a behavioral red-phase assertion.

### 2026-11-09: wrapped conditions and static initialization

Review against merged head `9355981614459aff8c398708ef6aeedddfed5be9`
supplied two concrete counterexamples to the automatic `All` rewrite:

- The analyzer already unwraps parenthesized and nullable-Boolean conditions,
  but the fixer searches the outer argument expression for explicit `All<Base>`
  type arguments. That loses the type arguments when the invocation is wrapped
  and can select a different predicate overload.
- Reading a static readonly field can execute a type initializer. Reversing
  `Source.Items` and `Filter.Predicate` can therefore change the limit captured
  by the predicate even though both fields are readonly.

The bounded correction retrieves the invocation from the analyzer's
unwrapped condition location and excludes static fields from reorder-safe
inputs. Instance readonly fields remain supported. Focused cases extend the
existing binding regression with both wrappers, assert no diagnostic for
separate explicit static constructors, and retain an instance-field control.

The comparison uses production head
`9355981614459aff8c398708ef6aeedddfed5be9`, whose merge base is
`19bfd517ba336e27bbeed4967fc5629e956ccc23`, plus the retained test-only patch
SHA-256 `2A224AFA9903C0D2823215862D212CE866BF7BC5784FD02C633C8E8C729DACA9`.
The corrected source/test patch SHA-256 is
`72C9110D459095C448A97403B96E4828D24BDCBDA519D5988530DDD39F6249B4`.
The test-file bytes are identical in both states, SHA-256
`6D7ECC4478872D4A0ECF668C31984D5DB0ACA58FF66DC4E19EB7F4FEA36736A2`.

#### Test-value evidence

| Contract and exact case | Actual pre-fix implementation | Corrected implementation |
| --- | --- | --- |
| `WhenUsingIsTrueAllWithExplicitBaseType_PreservesPredicateBinding`: `ContainsOnlyParenthesizedExplicitBaseType` and `ContainsOnlyNullableExplicitBaseType` | Both execute and fail the fixed-source comparison: expected `ContainsOnly<Base>`, actual `ContainsOnly`. | Both execute and pass. |
| `WhenUsingIsTrueAllWithStaticReadonlyFields_NoDiagnostic` | Executes and fails: expected zero diagnostics, actual one MSTEST0037 diagnostic. | Executes and passes with no diagnostic. |
| Unwrapped `ContainsOnlyExplicitBaseType` and `WhenUsingIsTrueAllWithInstanceReadonlyFields_SuggestsContainsOnly` controls | Both execute and pass. | Both execute and pass. |

The identical focused selection executes five cases: red has three intended
failures and two passes, exit 2; green has five passes, exit 0. Both have zero
skips. The whole related class passes 162/162 on net10.0 and 153/153 on net472,
zero skips, exit 0. Release builds on both targets use SDK
`12.0.100-alpha.1.26556.115`, treat warnings as errors, and finish with zero
warnings/errors. TRX parsing independently verifies counts, outcomes, and the
five paired identities.

Both variants use the session's isolated checkout and separate bin/obj roots
under `artifacts`; no production rollback or other checkout is changed.
Assembly method-level scheduling remains enabled with 32 effective workers
(`Workers = 0`). The static constructors exist only inside per-test Roslyn
sample strings; no host-global static state or scheduling declaration changes.

The following command arguments use `$ROOT` for the checkout root and `$RESULTS`
for `artifacts\TestResults\Release\agent-merge-wrapped-static-49c19b2b`.
Native commands were launched with the repo-local `.dotnet` and Windows
PowerShell/System32 directories prepended to PATH in the same `cmd.exe` process.

```powershell
$project = 'test\UnitTests\MSTest.Analyzers.UnitTests\MSTest.Analyzers.UnitTests.csproj'
$focus = 'FullyQualifiedName~WhenUsingIsTrueAllWithExplicitBaseType_PreservesPredicateBinding|FullyQualifiedName~WhenUsingIsTrueAllWithStaticReadonlyFields_NoDiagnostic|FullyQualifiedName~WhenUsingIsTrueAllWithInstanceReadonlyFields_SuggestsContainsOnly'
$redOutputs = @("/p:ArtifactsBinDir=$ROOT\artifacts\bin\wrapped-static-red\", "/p:ArtifactsObjDir=$ROOT\artifacts\obj\wrapped-static-red\")
$greenOutputs = @("/p:ArtifactsBinDir=$ROOT\artifacts\bin\wrapped-static-green\", "/p:ArtifactsObjDir=$ROOT\artifacts\obj\wrapped-static-green\")

.\.dotnet\dotnet.exe restore $project --verbosity minimal "-bl:$RESULTS\restore-red-{}.binlog" @redOutputs
# Exit 0; full project-reference graph, without a global TargetFramework override.
.\.dotnet\dotnet.exe build $project -f net10.0 -c Release --no-restore --no-incremental --verbosity quiet /p:TreatWarningsAsErrors=true "-bl:$RESULTS\build-red-{}.binlog" @redOutputs
# Exit 0; zero warnings/errors.
.\.dotnet\dotnet.exe artifacts\bin\wrapped-static-red\MSTest.Analyzers.UnitTests\Release\net10.0\MSTest.Analyzers.UnitTests.dll --filter $focus --report-trx --report-trx-filename red.trx --results-directory "$ROOT\$RESULTS\red" --progress off --no-ansi
# Exit 2; five execute, three intended failures, two passes, zero skips.

.\.dotnet\dotnet.exe restore $project --verbosity quiet "-bl:$RESULTS\restore-green-{}.binlog" @greenOutputs
# Exit 0; separate corrected-implementation intermediates.
.\.dotnet\dotnet.exe build $project -f net10.0 -c Release --no-restore --no-incremental --verbosity quiet /p:TreatWarningsAsErrors=true "-bl:$RESULTS\build-green-net10-{}.binlog" @greenOutputs
.\.dotnet\dotnet.exe build $project -f net472 -c Release --no-restore --no-incremental --verbosity quiet /p:TreatWarningsAsErrors=true "-bl:$RESULTS\build-green-net472-{}.binlog" @greenOutputs
# Both exit 0; zero warnings/errors.
.\.dotnet\dotnet.exe artifacts\bin\wrapped-static-green\MSTest.Analyzers.UnitTests\Release\net10.0\MSTest.Analyzers.UnitTests.dll --filter $focus --report-trx --report-trx-filename green-focused.trx --results-directory "$ROOT\$RESULTS\green-focused" --progress off --no-ansi
# Exit 0; the same five cases pass, zero skips.
.\.dotnet\dotnet.exe artifacts\bin\wrapped-static-green\MSTest.Analyzers.UnitTests\Release\net10.0\MSTest.Analyzers.UnitTests.dll --filter "FullyQualifiedName~UseProperAssertMethodsAnalyzerTests" --report-trx --report-trx-filename analyzers-net10.trx --results-directory "$ROOT\$RESULTS\net10" --progress off --no-ansi
# Exit 0; 162/162 pass, zero skips.
.\artifacts\bin\wrapped-static-green\MSTest.Analyzers.UnitTests\Release\net472\MSTest.Analyzers.UnitTests.exe --filter "FullyQualifiedName~UseProperAssertMethodsAnalyzerTests" --report-trx --report-trx-filename analyzers-net472.trx --results-directory "$ROOT\$RESULTS\net472" --progress off --no-ansi
# Exit 0; 153/153 pass, zero skips.
```

Each MSBuild invocation produced a distinct retained binlog. Results and
counterexample messages are in `red\red.trx`, `green-focused\green-focused.trx`,
`net10\analyzers-net10.trx`, `net472\analyzers-net472.trx`, and the corresponding
console logs below `$RESULTS`. These are local evidence, not uploaded artifacts.
The first root-build attempt supplied a duplicate restore flag; a subsequent
cached-toolset restore reached the workload hook before the updated SDK was
initialized. Neither setup attempt counts as red proof. Explicit initialization
through `eng\common\tools.ps1` installed the pinned SDK before the commands above.

This correction does not change framework behavior or public overloads. Earlier
package-consuming results remain bound to their recorded revisions, not
relabelled as fresh package evidence after the merge from main. Current-head CI
and actual rendered terminal/IDE inspection remain separate, unclaimed checks.

## Alternatives and decision

`ContainsAll` is already multiplicity-aware expected-item inclusion and permits
additional elements. Adding a universal predicate under that name would conflate
different quantifiers. `ContainsOnly` expresses positive intent and fits the
existing `Contains*` family without suggesting xUnit's action-inspector API.

`DoesNotContain(x => !predicate(x), collection)` is logically sufficient, but
double negation obscures the invariant and its failure message. A positive API
also supplies a clean analyzer/code-fix target.

First-counterexample reporting is intentional. LINQ `All` and existing
`DoesNotContain` stop as soon as the result is determined. Full scans could
execute later side effects, encounter exceptions, or consume infinite inputs
that the original assertion never reaches. Existing full-scan `AreAll*`
assertions are precedent for a different contract, not a reason to silently
change this one.

Counterexample formatting will use the existing formatter registry with a
non-enumerating built-in fallback. The normal renderer remains unchanged for
other assertions. Nested collections receive type-level fallback details unless
an explicit formatter supplies a representation.

## Validation and demonstration

Planned evidence:

- An unmodified focused framework baseline using the repository build scripts.
- Framework tests for both polarities, counterexample identity, overloads,
  short-circuiting, disposal, exception propagation, formatting, and scopes.
- Analyzer/code-fix tests for supported and unsupported call shapes.
- Fresh Release packages and the existing C# 12 source-compatibility harness,
  plus a real packaged execution scenario.
- A compiling counterfactual retaining the new API and the same tests while
  removing assertion/reporting behavior. Missing-symbol compilation failures
  will not be counted as red-phase proof.
- Inspection of actual built assertion output and neutral-resource/XLF
  validation.

The baseline, package consumers, and paired comparisons above are executed
evidence. CI on the eventual pushed head and rendered-surface inspection are
not claimed. Temporary logs and test-pipeline records belong in task-owned
artifact storage, not this source document.

## Open questions

- CI execution remains unproven until the eventual published revision runs.
- Actual rendered terminal/IDE layout inspection remains unperformed; exact
  packaged diagnostics and transcript content do not replace that surface check.
