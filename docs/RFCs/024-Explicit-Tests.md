# RFC 024 - Explicit tests

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Add `[Explicit]` for test classes and methods, and `IsExplicit` for data sources and data rows. An
explicit test is discovered and listed like any other test, but a broad **Run All** reports it as
skipped. It runs when the request positively selects it: you click it in Test Explorer, you pass its
UID, or you write a filter that names something it matches.

One sentence carries the whole design: **selecting tests activates them, excluding other tests does
not.** `TestCategory=Hardware` runs the explicit hardware tests. `TestCategory!=Slow` runs nothing
explicit, even though every explicit test matches it.

This is the design investigation for
[microsoft/testfx#5346](https://github.com/microsoft/testfx/issues/5346). No production code changes
before approval.

It extends decisions that already shipped and does not revisit them.
[RFC 018](018-Native-MTP-Integration-For-MSTest.md) established `UnitTestElement` as the neutral
engine boundary, so explicit metadata stays neutral and gets one transport per host.
[RFC 020](020-Test-Execution-Filter-Providers.md) defined provider filters as AND-composed
constraints, and this RFC adds only the provenance that keeps those constraints from becoming user
intent. [RFC 022](022-Test-Dependencies.md) introduced `FinishTestThatDidNotRunAsync` and
scheduler-owned skipped outcomes, which explicit skips reuse.
[RFC 010](010-MapNotRunnableToFailed-Attribute.md) covers malformed tests, and explicit is not one.

## Motivation

### The tests this is for

A test that reflashes the board attached to your desk. It is a real test, you want it in the suite,
you do not want it running on every F5:

```csharp
[TestClass]
public class DeviceTests
{
    [TestMethod]
    [Explicit("Reflashes the attached device.")]
    public void ResetsTheAttachedDevice()
    {
    }
}
```

A class of migration tests that share one staging subscription. Two people running them at the same
time is a bad afternoon:

```csharp
[TestClass]
[Explicit("Runs against the shared staging subscription.")]
public class StagingMigrationTests
{
    [TestMethod]
    public void UpgradeFromPreviousVersion()
    {
    }

    [TestMethod]
    public void UpgradeFromTwoVersionsBack()
    {
    }
}
```

One data row out of three that takes twenty minutes, while its siblings take a second:

```csharp
[TestMethod]
[DataRow(100)]
[DataRow(10_000)]
[DataRow(100_000_000, IsExplicit = true, ExplicitReason = "Takes about 20 minutes.")]
public void RebuildIndex(int rows)
{
}
```

The shape is the same in all three: the test is correct and worth keeping, but starting it needs a
decision from a person. MSTest has no way to say that today.

### Why `[Ignore]` is not the answer

`[Ignore]` looks close and behaves nothing like it:

- an ignored test cannot be started by selecting it, you edit the source and rebuild first;
- `[Ignore]` says "broken, not looking at it now", explicit says "fine, ask for it";
- CI cannot tell those two apart, so the skipped count stops being a useful health signal;
- the reason string is the only thing that distinguishes them, and nothing enforces it.

Conditions (`ConditionBaseAttribute`) are not the answer either. A condition knows the environment,
it cannot know what the user asked for. Explicitness is a property of the request, not of the
machine, so `[Explicit]` does not derive from `ConditionBaseAttribute`.

### Prior art

| Framework | Declaration | Run All | What runs them |
| --- | --- | --- | --- |
| **NUnit** | `[Explicit]` on fixture and method, `TestCase(Explicit = true)` | skipped | direct selection, or a positive filter such as `--where cat==Hardware` |
| **xUnit v3** | `[Fact(Explicit = true)]` | skipped | a run level switch, `-explicit on` or `-explicit only`; selecting the test is not enough |
| **TUnit** | `[Explicit]` on class and method | skipped | a filter whose entire match set is explicit |
| **MSTest today** | none | | |

NUnit's model is the one users ask MSTest for, and it is the one that matches how Test Explorer is
actually used: you find the test, you click Run, it runs. This RFC follows it. The xUnit style switch
is available as well (see [Configuration](#configuration)), as an override rather than as the only
way in.

TUnit's rule, run them when every matched test is explicit, is rejected here because the answer for
one test then depends on which other tests happen to exist. Adding an ordinary test to a class would
silently stop the explicit one from running.

## Design

### API

All APIs are in `Microsoft.VisualStudio.TestTools.UnitTesting`.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExplicitAttribute : Attribute
{
    public ExplicitAttribute();
    public ExplicitAttribute(string? reason);

    public string? Reason { get; }
}

public interface ITestDataSourceExplicitCapability
{
    bool IsExplicit { get; set; }
    string? ExplicitReason { get; set; }
}

public sealed class TestDataRow<T>
{
    // Added next to the existing IgnoreMessage, DisplayName and TestCategories.
    public bool IsExplicit { get; set; }
    public string? ExplicitReason { get; set; }
}
```

The source capability and the row properties stay separate, the way the ignore metadata already
works. `ITestDataSourceExplicitCapability` goes on data sources, so `DataRowAttribute` and
`DynamicDataAttribute` implement it exactly as they implement `ITestDataSourceIgnoreCapability`
today. `TestDataRow<T>` is a row and not an `ITestDataSource`, so it declares the two properties
directly instead, as `[DataMember]` next to `IgnoreMessage`, and the internal `ITestDataRow`
contract exposes them, so row metadata survives every existing unwrapping and serialization path.
Making the row implement a source named interface would claim a relationship that does not exist.

A custom `ITestDataSource` implements the capability to mark every row it produces, or returns
`TestDataRow<T>` to mark single rows:

```csharp
[TestMethod]
[DynamicData(nameof(GetMigrations))]
public void Migrate(MigrationCase migration)
{
}

public static IEnumerable<TestDataRow<MigrationCase>> GetMigrations()
{
    yield return new(new MigrationCase("Current"));
    yield return new(new MigrationCase("Legacy"))
    {
        IsExplicit = true,
        ExplicitReason = "Requires a legacy database image.",
    };
}
```

`Reason` and `ExplicitReason` are diagnostic text. They are not identifiers, they are not filterable,
and `null`, empty, and whitespace behave like no reason at all. `IsExplicit` is the declaration on
its own, a reason without it changes nothing, so a stray string can never change what executes.

Declarations OR-compose, a narrower one cannot switch a broader one off:

```text
effectiveExplicit = class || method || dataSource || dataRow
```

The reason comes from the most specific explicit declaration that has one: row, then source, then
method, then class, then the localized default. An explicit declaration without a reason does not
erase a broader one, so an unreasoned explicit row under `[Explicit("Requires staging.")]` still
reports "Requires staging.".

`Inherited = false` matches `[Ignore]` and keeps an override from becoming explicit behind its
author's back:

| Declared on | Test being executed | Explicit? |
| --- | --- | --- |
| test class | method declared on that class | yes |
| base test class | method declared on a derived test class | no |
| base method | that method discovered as a test of a derived class | yes |
| base method, override declares nothing | the override | no |
| method supplied through a custom `TestMethodAttribute` | that method | yes |

The API additions are recorded in `PublicAPI.Unshipped.txt`.

### Running explicit tests

**Visual Studio.** The test is listed like any other. Run All leaves it skipped with your reason next
to it. Right-click the test, its class, or its namespace node and Run, and it runs. Rerunning it from
a previous result runs it as well.

**Command line.** Any positive filter that matches it works:

```bash
# one test by name
dotnet test --filter "FullyQualifiedName~ResetsTheAttachedDevice"

# every explicit test in a category, which is how an opt-in suite is usually run
dotnet test --filter "TestCategory=Hardware"

# every explicit test in the assembly
dotnet test --filter "Explicit=True"
```

With Microsoft.Testing.Platform:

```bash
dotnet run --filter-uid <uid>
dotnet run --treenode-filter "/*/*/DeviceTests/*"
```

**CI.** The normal build does not change and does not need a new filter. Explicit tests show up in
its report as skipped, with the reason, so they stay visible instead of disappearing. The opt-in job
names what it wants:

```yaml
# runs on the self-hosted agent that has the device attached
- script: dotnet test --filter "TestCategory=Hardware"
  displayName: Hardware suite

# nightly, everything opt-in in one go
- script: dotnet test --filter "Explicit=True"
  displayName: Explicit suite
```

These do not run them, and that is the point:

```bash
dotnet test                                        # Run All
dotnet test --filter "TestCategory!=Slow"          # excludes, names nothing
dotnet test --filter "FullyQualifiedName!~Legacy"
```

The difference is between "I do not want those" and "I want this one". Only the second is a reason to
reflash somebody's device.

### What counts as selecting a test

Every execution carries two things:

1. a **constraint**, the existing answer to "which tests are in this run?";
2. an optional **activation**, the answer to "which tests did the user positively select?".

An ordinary test runs when it matches the constraint. An explicit test runs when it matches both.
With no activation, explicit tests are reported skipped.

Request filters feed both. Filters that come from `ITestFilterProvider` implementations, MTP
extension providers, and other policy sources feed only the constraint, even when their syntax
contains a positive leaf. They express repository policy, not user intent, so a provider that keeps
only `TestCategory=CanRunOnThisMachine` narrows the run and never starts an explicit test. The
executor receives the two values separately and must not try to recover activation from the composed
platform filter:

```csharp
internal sealed record TestSelection(
    ITestElementFilter Constraint,
    ITestElementFilter? ExplicitActivation);
```

This is internal to the adapter and platform services, not public API. Assemblies with no explicit
tests keep the current path and never evaluate activation.

Request shapes map like this:

| Request | Activation |
| --- | --- |
| VSTest `RunTests(sources, ...)` | none, this is Run All unless `TestCaseFilter` contributes one |
| VSTest `RunTests(tests, ...)` | exactly the supplied test cases |
| MTP `--filter-uid`, server request with UIDs | exactly those UIDs |
| MTP `--treenode-filter`, server graph filter | nodes matched through a discriminating segment |
| Empty MTP UID or node list | nothing, and no tests, this is not Run All |

Selecting a class, fixture, or namespace node activates the explicit tests under it. Selecting one
unfolded data row activates that row. Selecting a folded parent activates all of its rows, because
folded rows have no separate identity to select. VSTest's concrete overload keeps rejecting an empty
`IEnumerable<TestCase>` through its existing `Ensure.NotEmpty` guard, that host contract does not
change here.

### Filter expressions

VSTest `TestCaseFilter`, MSTest `--filter`, and equivalent MTP property filters share one rule: an
explicit test is activated only when a **positive leaf** takes part in a true path. `Property=value`,
`Property~value`, and a bare value are positive. `Property!=value` and `Property!~value` are not.

Evaluation returns `(matches, activates)`. `matches` keeps its current meaning and its current
results, only `activates` is new:

| Expression | `activates` |
| --- | --- |
| positive leaf | same as `matches` |
| exclusion leaf | always `false` |
| `A & B` | `matches`, and either child activates |
| `A \| B` | `(A.matches && A.activates) \|\| (B.matches && B.activates)` |

For an explicit test in category `Fast`:

| Filter | Activated? |
| --- | --- |
| no filter | no |
| `TestCategory!=Integration` | no |
| `TestCategory=Fast` | yes |
| `TestCategory=Fast & TestCategory!=Windows` | yes |
| `TestCategory!=Integration \| TestCategory=Fast` | yes, through the positive branch |
| `TestCategory=Other \| TestCategory!=Integration` | no, only the exclusion branch matched |
| `Explicit=True` | yes |
| `Explicit!=False` | no |

A positive leaf does not have to identify one test. `TestCategory=Hardware` activating every explicit
test in that category is the intent, that is what makes the opt-in CI job above a single line.

Malformed filters keep their current parse-error behavior and never fall back to Run All.

### Tree node and graph filters

`--treenode-filter` and the MTP server graph filter are one language, `ServerTestHost` builds a
`TreeNodeFilter` from the request's `GraphFilter`. Its leaves cannot be classified with the table
above, because a path segment can match everything without naming anything: `/**` is Run All written
as a filter, and `/*/*/*/*[Category!=Slow]` is a pure exclusion.

A segment is **discriminating** when it names something: a token with at least one literal character,
or `[Name=Value]` with a literal value. Wildcards, empty segments, `[Name!=Value]`, `[Name=*]`, and
`(!EXPR)` are not. Segments compose the same way expressions do: `A & B` is discriminating when the
segment matches and either side is, `A | B` only through a branch that both matched and is itself
discriminating, and `Token[FILTER_EXPR]` when either the token or the property expression is. So
`/*/*/*/(MyTest|(!Slow))` activates a node matched by `MyTest` and not one matched only by `(!Slow)`.

A node is activated when it matches the filter and at least one **non-root** segment on its path is
discriminating. Non-root, because the root names the test project rather than a test in it, and
`/MyAssembly/**` selects exactly what `/**` selects in a single assembly host.

| Filter | Activated? |
| --- | --- |
| `/**`, `/MyAssembly/**`, `/*/*/*/*` | no, these are Run All written as filters |
| `/*/*/*/MyTest` | yes |
| `/*/*/MyClass/*` | yes, through the class segment |
| `/*/MyNamespace/**` | yes, through the namespace segment |
| `/*/*/*/(!Slow)`, `/*/*/*/*[Category!=Slow]` | no |
| `/*/*/*/*[Category=Hardware]` | yes |
| `/*/*/MyClass/(!Slow)` | yes, through `MyClass` |
| `/*/*/*/(MyTest\|(!Slow))` | yes for a node matched by `MyTest`, no for one matched only by `(!Slow)` |
| `/*/*/*/*[Explicit=True]` | yes |

`TreeNodeFilter` lives in this repository and `Microsoft.Testing.Platform` already grants
`InternalsVisibleTo` to `MSTest.TestAdapter`, so it reports the discriminating result itself through
an internal match overload. No second grammar, no new public MTP API.

These tables describe how tree node and graph selection must behave once MSTest accepts such a
filter. Today `MSTestFilterContext` and the VSTest bridge throw `UnsupportedTestExecutionFilter` for
every leaf other than `NopFilter` and `TestNodeUidListFilter`, so neither reaches MSTest at all.

### Fail closed

That gap sets the rule for every request shape not listed above: **activation is proven, never
assumed**. A well-formed request that cannot be classified, an unsupported filter type, or a future
grammar addition constrains the run and activates nothing, so explicit tests under it are reported
skipped. A new filter feature cannot start running destructive tests before somebody has designed its
activation semantics.

Malformed input is a different case and keeps the existing behavior, it fails rather than degrading
to a constraint. A filter that does not parse reports its parse error, and an expression VSTest
accepts but the activation evaluator cannot parse fails the run as well. Neither falls back to
Run All, and neither falls back to "any positive token activates", which could start a destructive
test through the wrong `|` branch.

The same fail-closed direction applies to persisted metadata. A VSTest property that cannot be parsed
as a Boolean is treated as explicit and logged, so a stale cache cannot turn an opt-in test into a
Run All test.

### Precedence

Activation removes the explicit gate and nothing else:

| State | Activated? | Result |
| --- | --- | --- |
| Scheduler dependency, cycle, or cancellation outcome | either | existing scheduler outcome, the runner is not entered |
| Assembly `ITestFilter` returns Drop, Skip, or throws | either | existing drop, skip, or error outcome |
| Explicit only | no | skipped, "The test is explicit and was not selected." |
| Explicit only | yes | runs |
| Explicit and `[Ignore]`, or a false condition | no | explicit skip, fixture attributes are not evaluated |
| Explicit and `[Ignore]`, or a false condition | yes | existing ignore or condition skip |
| Explicit and invalid test method | no | explicit skip, validity is not resolved |
| Explicit and invalid test method | yes | existing invalid test behavior |

The assembly level `ITestFilter` runs first because it is execution policy, and its `Run` result
means "carry on with normal evaluation", never "this test was selected". `[Ignore]`, conditions, and
method validity are resolved only after type loading, so they are evaluated after activation.
Selecting an ignored test still leaves it ignored.

### Configuration

One setting, for environments that need a deterministic override:

```xml
<MSTest>
  <ExplicitTestMode>RequireSelection</ExplicitTestMode>
</MSTest>
```

```json
{
  "mstest": {
    "execution": {
      "explicitTestMode": "requireSelection"
    }
  }
}
```

| Value | Behavior |
| --- | --- |
| `RequireSelection` | Default. The rules in this RFC. |
| `Skip` | Never activates, not even for a directly selected test. For a protected CI environment. |
| `Run` | Treats every matching explicit test as activated, including Run All. For a job dedicated to them. |

Unknown values throw `AdapterSettingsException` in both parsers instead of falling back, matching
`ParallelWorkers` and `ExecutionScope`. A misspelled `Skip` must not quietly permit a destructive
test. `Run` is configuration rather than a CLI shortcut, a CI definition that wants the same effect
has `--filter "Explicit=True"`, which stays visible in the command line.

## Implementation

### The gate

The explicit check happens in `UnitTestRunner.RunSingleTestAsync`, after the assembly `ITestFilter`
and before `TypeCache.GetTestMethodInfo`. An unactivated class, method, or unfolded-row explicit test
therefore loads no test type, and runs no assembly initialization, class initialization, constructor,
`TestInitialize`, or body.

The gate does not promise more than that. Filter discovery loads the test assembly before it can look
for the attribute, and a registered `[TestFilterProvider]` is then constructed and asked about every
selected test, so module initializers and that filter run whatever the explicit state is. That is
existing `ITestFilter` behavior and this RFC does not change it. The filter keeps its place ahead of
the gate because it answers whether the test belongs in the run at all, which is a different question
from whether the user asked for it, and a test the policy drops must report as dropped rather than as
an explicit skip. Folded row declarations are the other exception, see below.

An unactivated test goes through `FinishTestThatDidNotRunAsync`, the same bookkeeping used by other
selected-but-not-run outcomes, so class test counts still reach zero and `ClassCleanup` and
`AssemblyCleanup` are neither skipped nor run early. A class that initialized for an ordinary test
still cleans up after the explicit skips have reported. Explicit declarations change nothing about
scheduling: activated tests keep their parallelization metadata and resource locks and go through the
same worker allocation as ordinary tests, unactivated ones consume no worker time beyond producing
their result.

### Data

Unfolded rows carry their effective state on the `UnitTestElement` and behave exactly like methods
from there: Run All skips the explicit row before any fixture work attributable to it, selecting the
row activates that row, selecting the method or class activates every selected row under it, and
ordinary sibling rows keep running.

Folded rows have no discovery identity, so class and method explicitness is checked first, and source
and row declarations are checked as the data is enumerated, before per-row `TestInitialize`, before
test-class construction where construction is per row, and before the body. Each unactivated row
produces its own skipped `UnitTestResult`, which keeps folded result cardinality exactly as it is
today. Data enumeration and the initialization needed to reach it can therefore happen even when
every produced row turns out to be explicit. That is inherent to folded data, the metadata does not
exist until the source runs, and it is not a reason to force unfolding.

`ITestDataSourceIgnoreCapability` and row `IgnoreMessage` keep precedence over the explicit state for
both folded and unfolded rows, they are known at the same point as the explicit metadata rather than
after type loading. If a data source throws while producing that metadata, existing data source
failure behavior wins, the adapter cannot know that the missing row would have been explicit.

### Retry

Retries never create activation. The gate runs before retry orchestration, so a test skipped by
Run All produces one skipped result, zero attempts, and never enters the retry UID set. A selected
explicit test that fails retries like any other test. The MTP process retry extension and a host's
"rerun failed tests" both build the next request from tests that actually ran and failed, so
narrowing a retry request cannot activate anything new.

### Metadata

Discovery always reports explicit tests, with no skipped state attached, a test is not skipped until
an execution request fails to activate it. Each `UnitTestElement` carries `IsExplicit` and
`ExplicitReason`, and each host gets one transport:

- VSTest: adapter owned `MSTestDiscoverer.Explicit` and `MSTestDiscoverer.ExplicitReason` properties,
  round-tripped by `ToTestCase` and `ToUnitTestElement`. These names are stable wire identifiers.
  `Explicit` is registered as a filterable string with values `True` and `False` compared
  case-insensitively, deliberately not a `bool`, so malformed persisted values reach the fail closed
  rule instead of being defaulted by VSTest's converter. A missing `Explicit` means false and a
  missing reason means no reason, so a case persisted by an older version still deserializes.
  Reasons are prose and stay unfilterable.
- Native MTP: `MSTestTestNodeConverter` adds `Explicit` with value `True` to explicit nodes only, and
  `ExplicitReason` only when a non-empty reason exists, on both discovered and result nodes. They are
  not traits, so they do not show up as user authored categories. UIDs do not change when
  `[Explicit]` is added, and a server client that ignores the metadata still receives ordinary nodes
  and ordinary skipped results.

A folded parent carries only its class and method declarations, source and row declarations do not
exist until the data is enumerated.

Skipped results carry `The test is explicit and was not selected.`, with `Reason: <reason>` appended
when one exists. The reason is copied verbatim, only the decision whether it is empty inspects it,
and it appears on the surfaces that already show skip reasons: `TestResult.ErrorMessage` and TRX, the
MTP skipped node, console output where the reporter prints skip reasons, and diagnostic logs. It is
never written as an error, a warning, standard output, or a test-context message.
`MapNotRunnableToFailed` does not turn it into a failure, explicit is a first class skipped outcome
rather than a malformed test.

Trace level logging records the UID, the effective declaration scope, the scope the reason came from,
the selection classification, and whether it activated. The two scopes are separate because a row can
inherit a method reason. It logs no data values beyond what the UID and display name already contain,
and it does not print one console line per skipped test.

### Activation plumbing

| Area | Change |
| --- | --- |
| Framework | `ExplicitAttribute`, the data capability interface, row properties, public API baseline |
| Discovery | `TypeEnumerator` reads class and method declarations, `AssemblyEnumerator` merges source and row declarations while unfolding |
| Execution | pre-initialization gate in `UnitTestRunner`, per-row checks in `TestMethodRunner.DataRow` |
| VSTest | classify source versus test-case execution in `MSTestExecutor`, register and round-trip properties, evaluate activation in `TestMethodFilter` |
| Native MTP | build activation from UID, tree, and property filters in `MSTestFilterContext` and `MtpTestElementFilter`, accept tree node and graph filters instead of throwing, add node metadata |
| Platform | `TestExecutionFilterComposer` and the request factories keep the original request filter next to the provider-constrained one, on an internal surface reached through the existing friend assembly |
| Settings | three `ExplicitTestMode` values with existing precedence, plus localized resources |
| Source generation | root `ExplicitAttribute` and capability bearing types, keep reading through the existing reflection abstraction so generated and reflection discovery produce identical metadata |

The platform change adds no public MTP API. `TestExecutionRequest.Filter` stays the effective
constraint, other frameworks keep reading only that, and MSTest reads the original request filter as
activation.

VSTest needs one extra piece. `ITestCaseFilterExpression` exposes only `MatchTestCase`, not the
parsed tree, so activation cannot be derived from it. MSTest adds an internal
`ExplicitActivationFilterExpression` in the adapter that implements the documented VSTest grammar,
escaping, precedence, bare-value expansion, and case-insensitive property names, and returns
`(matches, activates)`. It reads `ITestCaseFilterExpression.TestCaseFilterValue` on VSTest and the
original `--filter` or RunSettings string on native MTP, so both hosts get activation from the same
evaluator instead of two implementations. The VSTest expression stays authoritative for `matches`,
the new evaluator supplies only `activates`, and differential and fuzz vectors assert the two agree.
An upstream API that exposes a walkable tree replaces this later without changing any semantics here.

### Testing

- Unit tests for the attribute and data APIs, inheritance table, reason precedence, and the rule that
  a reason alone does not make anything explicit.
- Truth table tests for every row of the filter and tree node tables, over nested `&`/`|`,
  parentheses, escaping, bare values, and case-insensitive `Explicit` values. The same serialized
  vectors run against VSTest and native MTP so the two cannot drift, and against both expression
  evaluators so the duplicated parser cannot drift either.
- Ordering tests proving that assembly `ITestFilter` returning `Run` and provider constraints never
  activate, and that an unclassifiable request activates nothing. One of them registers a
  `[TestFilterProvider]` alongside an unactivated explicit test and asserts the filter is still
  constructed and called, pinning the boundary of what the gate skips.
- Execution tests proving no type load, no fixture, no body, and no retry for an unactivated test,
  correct cleanup counts for explicit skips, and folded and unfolded row behavior.
- One acceptance asset, run through both hosts, covering explicit method, class, base and override,
  ignored and conditional explicit tests, ordinary and explicit sibling rows, source-wide and
  row-specific dynamic data, retry, all three `ExplicitTestMode` values, and fixture counters written
  to disk so initialization can be asserted rather than assumed.
- Compatibility runs: an assembly with no declarations before and after the feature, an old adapter
  with a new framework, a persisted test case with missing and malformed properties, and unchanged
  MTP UIDs.

Both hosts ship together, and they ship equivalent. Selection, filtering, retry, results, and
diagnostics must behave the same under VSTest and native MTP, which is why the acceptance asset is
one asset run twice rather than two suites.

## Compatibility

The APIs are additive and binary compatible, and a test with no declarations keeps exactly its
current discovery, filtering, execution, retry, and result behavior. Existing custom data sources
compile and behave as before, the capability is opt-in.

The risk worth calling out in release notes is version skew. A new adapter with an old framework sees
no declarations and behaves as today, but a new framework with an old adapter can load `[Explicit]`
without recognizing it, and may run the test during Run All. The usual MSTest package alignment check
is the answer. Making the attribute derive from `[Ignore]` would paper over this and would also make
the test impossible to select on any adapter, so it is not done.

## Future work

- An analyzer for `ExplicitReason` set without `IsExplicit`, and for `[Explicit]` on a non-test
  member. The runtime behavior is fully defined without it.
- A selection-origin signal from VSTest and Visual Studio. Today the invocation shape is the contract,
  source execution means broad execution and test-case execution means the host selected those tests.
  The adapter must not guess the UI command from list size, timing, or cache state, so a legacy host
  that implements a broad command by sending every test case will run explicit tests. Diagnostic logs
  record `SourceRun`, `SelectedTestCases`, or `PositiveFilter` so a report can show which happened.
  No product-facing warning is printed for this, most selected runs are intentional and warning on
  every one of them would be noise.
- A platform level explicit contract, if other frameworks want the same distinction. This RFC keeps
  the semantics inside MSTest on purpose.

## Resolved questions

| Question | Decision |
| --- | --- |
| Is explicit a condition? | No, it depends on the request, not on the environment. |
| Are explicit tests discovered? | Always, and Run All reports them skipped rather than hiding them. |
| What activates one? | Concrete selection, or a positive branch of a request filter that matches it. |
| Does an exclusion filter activate? | Never, and neither does `/**` or a root-only tree path. |
| Can a provider or policy filter activate? | Never, including assembly `ITestFilter` returning `Run`. |
| Can selection override `[Ignore]` or a condition? | No, it removes the explicit gate only. |
| Does selecting a class or namespace activate what is under it? | Yes, and selecting a folded parent activates all its rows. |
| Can a row opt out of an explicit method or source? | No, declarations OR-compose. |
| Which reason wins? | The most specific explicit declaration that has one. |
| What happens when activation cannot be determined? | Nothing activates, the request constrains only. |
| Are skipped explicit tests retried? | No. Selected explicit failures retry normally. |
| Is there an override? | `ExplicitTestMode`, with `Skip` and `Run` on either side of the default. |

Approval is needed for the public API, the positive-filter activation model, the discriminating
segment rule for tree node and graph filters, the three configuration values, and the documented
legacy VSTest boundary.
