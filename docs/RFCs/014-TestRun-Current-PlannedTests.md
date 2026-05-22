# RFC 014 - TestRun.Current ambient run-state API

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation (initial slice: `PlannedTests`)
- [ ] Shipped

## Summary

Introduce a static, ambient `TestRun.Current` surface on top of an `ITestRunInfo` abstraction that exposes information about the *test run as a whole* (as opposed to `TestContext`, which describes a single executing test). The v1 slice exposes the set of tests that have been discovered and have passed the active filter for the current assembly (`PlannedTests`). Later additions can extend the surface with running/completed snapshots and events without breaking consumers.

The motivating scenario is [microsoft/testfx#7311](https://github.com/microsoft/testfx/issues/7311): inside `[AssemblyInitialize]`, decide whether to perform expensive partial setup based on whether any test matching a given criterion will actually run.

## Motivation

Users today have no supported way to ask "given the filter the user typed in VS / on the command line, which tests are actually going to run in this assembly?". The information exists inside the adapter (it has to, in order to dispatch the tests), but it is not exposed.

Concrete scenarios from the field (issue #7311 and adjacent feedback):

- Building a compatibility solution before integration tests, but only when at least one compatibility test will run.
- Spinning up a Docker container or a real database before tests that need it, and skipping it entirely when the user runs a single non-DB test in VS.
- In `[AssemblyCleanup]`, deciding whether to publish telemetry based on whether any test of category X actually executed.
- Letting fixtures (NUnit-style shared setup) decide whether to spin up their dependency, without forcing users to wire ad-hoc booleans through `[AssemblyInitialize]`.

Two design directions were discussed on the issue:

1. **Put it on `TestContext`** (or a new per-phase `AssemblyInitializeTestContext`). This was rejected because `TestContext` is by design per-test, `TestContext.Current` is an `AsyncLocal` that is `null` outside test execution (so a static helper, fixture, or extension can't read it), and growing the surface to "the whole run" muddies what `TestContext` represents.
2. **Add a separate ambient run-state object.** This is what this RFC proposes.

The same shape is used by other frameworks: xUnit v3 separates per-test `TestContext.Current` from run-wide pipeline objects, and NUnit separates `TestContext.CurrentContext` from `TestExecutionContext.CurrentContext`.

## Detailed design

### Public API

All types live in `Microsoft.VisualStudio.TestTools.UnitTesting` (same as `TestContext`) and ship inside `MSTest.TestFramework.Extensions` (where `TestContext` lives). All are gated behind `[Experimental("MSTESTEXP")]` for v1.

```csharp
namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public static class TestRun
{
    public static ITestRunInfo Current { get; }
}

[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public interface ITestRunInfo
{
    IReadOnlyCollection<PlannedTest> PlannedTests { get; }
}

[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public sealed class PlannedTest
{
    public PlannedTest(
        string fullyQualifiedTestClassName,
        string testName,
        string? testDisplayName,
        string assemblyPath,
        string? managedTypeName,
        string? managedMethodName,
        string? declaringFilePath,
        int? declaringLineNumber,
        IReadOnlyCollection<string> testCategories,
        IReadOnlyCollection<KeyValuePair<string, string>> testProperties);

    public string FullyQualifiedTestClassName { get; }      // mirrors TestContext
    public string TestName { get; }                         // mirrors TestContext
    public string? TestDisplayName { get; }                 // mirrors TestContext
    public string AssemblyPath { get; }
    public string? ManagedTypeName { get; }                 // ECMA-335 stable id
    public string? ManagedMethodName { get; }               // ECMA-335 stable id (incl. parameter types)
    public string? DeclaringFilePath { get; }               // matches TestMethodAttribute.DeclaringFilePath
    public int? DeclaringLineNumber { get; }                // matches TestMethodAttribute.DeclaringLineNumber
    public IReadOnlyCollection<string> TestCategories { get; }                                  // [TestCategory]
    public IReadOnlyCollection<KeyValuePair<string, string>> TestProperties { get; }            // [TestProperty]
}
```

### Naming rationale

Every property name on `PlannedTest` either mirrors an existing public MSTest API or matches the user-facing attribute the user wrote:

- `FullyQualifiedTestClassName` / `TestName` / `TestDisplayName` → mirror `TestContext`.
- `DeclaringFilePath` / `DeclaringLineNumber` → mirror the public `TestMethodAttribute` properties auto-captured via `[CallerFilePath]` / `[CallerLineNumber]`.
- `TestCategories` → mirrors `TestCategoryAttribute.TestCategories`.
- `TestProperties` → mirrors `[TestProperty(Name, Value)]`. Internally MSTest converts these to VSTest "Trait" objects (`GetTestPropertiesAsTraits` in `ReflectionHelper.cs`), but the user-facing concept the user wrote is `[TestProperty]`, so the public surface uses that name.

### Type choices

- **`TestRun` is a `static class`.** Matches `TestContext.Current` ambient-access habit. There is only one current run; no instance to manage.
- **`ITestRunInfo` is an `interface`.** Lets the platform swap the concrete impl (empty default, populated, future test doubles for advanced scenarios).
- **`PlannedTest` is a `sealed class`** with public constructor and get-only properties.
  - Not a `struct`: 10 reference-typed fields, far past the size at which value semantics make sense.
  - Not a `record`: structural equality across collection fields is surprising and expensive; positional records emit `init` accessors which the [repo guidelines explicitly forbid for new public APIs](../../.github/copilot-instructions.md#public-api-guidelines).
  - `sealed` to lock in the shape and allow non-breaking additions later (extra get-only properties + extra ctor overload).
- **Collections**:
  - `IReadOnlyCollection<string>` for `TestCategories` rather than `IReadOnlySet<string>` because `IReadOnlySet<T>` is not available on netstandard2.0 / net462 (the TFMs `TestFramework.Extensions` targets) and the repo does not currently polyfill it.
  - `IReadOnlyCollection<KeyValuePair<string, string>>` for `TestProperties` (flat list) because `[TestProperty]` is multi-valued — the same name may appear several times — and a flat list represents that naturally without forcing an inner collection on every entry.

### Lifetime / scoping

- `TestRun.Current` is **never `null`**. Before any source begins execution, it returns an empty `ITestRunInfo` whose `PlannedTests` is empty. After execution, it retains the most recently populated snapshot until the next source replaces it.
- `PlannedTests` is scoped to **the current assembly's filtered tests**, populated once by the platform before the first test in the assembly runs.
- Scope: process-wide and (on .NET Framework with AppDomain isolation) AppDomain-wide. The implementation populates the static *inside* the `UnitTestRunner` constructor, which is the type instantiated in the child AppDomain when isolation is in use — so the snapshot is visible in the same domain that runs `[AssemblyInitialize]` and the tests themselves. Cross-process test hosts each have their own snapshot.

### Implementation outline

1. New types in `src/TestFramework/TestFramework.Extensions/`:
   - `TestRun.cs` — static class with `Current` (get) and `internal SetCurrent(ITestRunInfo?)`. Default value is an internal `EmptyTestRunInfo` that returns empty collections.
   - `ITestRunInfo.cs` — the interface.
   - `PlannedTest.cs` — the DTO.
2. Internal adapter type `TestRunInfo` in `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestRunInfo.cs`:
   - Implements `ITestRunInfo`.
   - Provides `static TestRunInfo CreateFrom(IReadOnlyList<UnitTestElement>)` that materializes one `PlannedTest` per filtered `UnitTestElement`, reading `TestMethod.{FullClassName,Name,DisplayName,AssemblyName,ManagedTypeName,ManagedMethodName}`, `UnitTestElement.{DeclaringFilePath,DeclaringLineNumber,TestCategory,Traits}`.
3. Wire-up in `UnitTestRunner` constructor (post `_classCleanupManager = …`):

   ```csharp
   TestRun.SetCurrent(TestRunInfo.CreateFrom(testsToRun));
   ```

   This runs in the right domain/process for both the VSTest adapter path and the Microsoft.Testing.Platform path.

### Example consumer code

```csharp
[TestClass]
public static class GlobalSetup
{
    [AssemblyInitialize]
    public static void Init(TestContext _)
    {
        bool anyCompatibilityTest = TestRun.Current.PlannedTests
            .Any(t => t.TestCategories.Contains("Compatibility"));

        if (anyCompatibilityTest)
        {
            BuildCompatibilitySolution();
        }
    }
}
```

The same call works from a helper class, a fixture, or any other code reachable during the run — not only from `[AssemblyInitialize]`.

## Drawbacks

- **New public API surface.** Adds three new public types (`TestRun`, `ITestRunInfo`, `PlannedTest`) to an already broad framework. Mitigation: `[Experimental]`, narrow v1 surface, minimal DTO shape designed for additive growth.
- **Ambient state is easy to misuse.** Reading run-wide state from inside `[TestMethod]` bodies can encourage hidden coupling between tests (test order dependencies based on what other tests have / haven't passed). Mitigation: v1 only exposes the plan (no outcomes), and a future analyzer can flag access from `[TestMethod]` bodies once `RunningTests` / `CompletedTests` are added.
- **AppDomain / process isolation.** Each test host process and each child AppDomain has its own snapshot. This is documented but may surprise users running multi-targeted tests in VSTest's parallel out-of-proc host.
- **Data-driven rows.** Tests whose data rows are only unfolded at execution time (non-serializable data, `UnfoldingStrategy.Fold`, `[DataSource]`) appear as a single `PlannedTest` rather than one per row. This is documented on `PlannedTests`.

## Alternatives

1. **Add `PlannedTests` (and friends) to `TestContext`** — rejected. `TestContext.Current` is `null` outside test execution (defeats the "queryable from anywhere" goal), per-run data on a per-test type is the bloat the team already pushed back on, and once we add `RunningTests` / `CompletedTests` they cannot live on `TestContext`.
2. **Split `TestContext` into per-lifecycle-phase subtypes** (`AssemblyInitializeTestContext`, `ClassInitializeTestContext`, …) and put `PlannedTests` on the assembly/class ones. Larger refactor; doesn't help consumers outside the lifecycle hooks (fixtures, helpers, extensions); doesn't compose with the future ambient state additions. Useful as an orthogonal cleanup, not as the place to put run-wide data.
3. **A first-class fixture model** (NUnit/xUnit-style) where setup runs the first time a class requesting the fixture is about to execute. Strongly preferred in the long run and complementary to this RFC: a fixture implementation can use `TestRun.Current.PlannedTests` internally. Tracked separately.
4. **Multiple gated `[AssemblyInitialize(WhenAnyTestMatches=…)]` attributes** — declarative, but only handles a single predicate per init method and adds a new gating-expression language.
5. **Do nothing.** Forces users to either over-eager setup in `[AssemblyInitialize]` (slow) or lazy `Lazy<T>` patterns that start mid-run (breaks the "report time spent on setup" desire from the issue thread).

## Compatibility

- **Not a breaking change.** All additions; no existing types are modified.
- **Experimental.** The whole surface is annotated with `[Experimental("MSTESTEXP")]`. Consumers must explicitly opt in with `#pragma warning disable MSTESTEXP` (or equivalent). This lets us evolve the shape based on early feedback before locking it.
- **TFM coverage.** `TestFramework.Extensions` targets are unchanged; the new types compile across `netstandard2.0`, `net462`, `net8.0`, `net9.0`, UWP and WinUI.
- **Source compatibility for derived `TestContext`s.** None affected; we did not touch `TestContext`.

### Unresolved questions

- Should `Current` reset to empty between sources, or accumulate across all sources in the run? v1 ships per-source semantics (matches the AssemblyInitialize use case). A future `IRunWideTestInfo` could expose the union.
- Should we additionally expose `Hierarchy` (Namespace / Class / Method) on `PlannedTest`? Skipped in v1 since `FullyQualifiedTestClassName` is sufficient; add later if requested.
- Will users want a `TryGetTestProperty(name, out values)` convenience on `PlannedTest`, or always do their own LINQ? Defer until we see feedback.
- Future extensions to `ITestRunInfo` (e.g. `RunningTests`, `CompletedTests`, `TestStateChanged` event) — separate RFC once this slice ships.
