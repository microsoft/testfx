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
  `ContainsOnly` argument failures use MSTest's parameter reporting and remain
  safe inside `Assert.Scope`.
- Extend MSTEST0037 for positive `Enumerable.All` checks only when the referenced
  framework exposes the new API. Do not change `Queryable.All` or negative
  `IsFalse(All)` checks.

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

The existing-test baseline above is executed evidence, not proof of the new
behavior. Red-phase results, package provenance, and CI execution are not claimed
yet. Temporary logs and test-pipeline records belong in task-owned artifact
storage, not this source document.

## Open questions

- Record final test identities, commands, exits, package provenance, and
  counterfactual patch identities after execution.
- Inspect the final isolation contract for formatter registrations and assertion
  scopes without changing assembly scheduling.
- CI execution remains unproven until the eventual published revision runs.
