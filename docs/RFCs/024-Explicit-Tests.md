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

Conditions (`ConditionBaseAttribute`) look closer than they are. A condition answers a question about
the machine, and explicitness answers a question about the request, so the two are asked at different
times: `IsConditionMet` is read after the type is loaded, and the gate below exists to answer before
that. Whether `[Explicit]` should nonetheless live in that hierarchy is an open question rather than a
settled one, and it is put in [Base type](#base-type) with what it would buy and cost.

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

    public string? ExplicitReason { get; }
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

A `DynamicData` member returns `TestDataRow<T>` directly to mark single rows:

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

A custom `ITestDataSource` has the other two ways in. It implements
`ITestDataSourceExplicitCapability` to mark every row it produces, and it marks a single row by
wrapping the row in a one-element array, because `GetData` returns `IEnumerable<object?[]>` and the
existing unwrapping contract recognizes a row only as the single element of one:

```csharp
yield return new object?[] { new TestDataRow<MigrationCase>(migration) { IsExplicit = true } };
```

The array type is written out rather than shortened to a collection expression because the array is
the point: this is the same shape row `IgnoreMessage` and `DisplayName` already require from a custom
source, and it is worth stating because the `DynamicData` example above cannot be copied into one.

The reason is called `ExplicitReason` on all three surfaces, matching `IgnoreAttribute.IgnoreMessage`
and row `IgnoreMessage` rather than shortening to `Reason` on the attribute alone. It is diagnostic
text: not an identifier, not filterable, and `null`, empty, and whitespace behave like no reason at
all. `IsExplicit` is the declaration on its own, a reason without it changes nothing, so a stray
string can never change what executes.

Declarations OR-compose, a narrower one cannot switch a broader one off:

```text
effectiveExplicit = class || method || dataSource || dataRow
```

The reason comes from the most specific explicit declaration that has one: row, then source, then
method, then class. An explicit declaration without a reason does not erase a broader one, so an
unreasoned explicit row under `[Explicit("Requires staging.")]` still reports "Requires staging.". A
test whose declarations all lack one has no reason, not a default one: the localized skip message
under [Metadata](#metadata) is always reported and already says why the test did not run, so putting a
default at the end of this chain would append it to itself.

That chain resolves the reason for one test. A folded parent skipped before enumeration is not one
test, it is one result standing for every row under it, so it takes the aggregate rule under
[Data](#data) instead: the declaration's reason when a class or method declaration gated it, and
otherwise the reason its sources agree on. The two only differ when a parent is gated with sources
that also declare a reason, and there the parent is deliberately less specific than this chain would
be, because a source's explanation cannot speak for rows the source does not produce.

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

### Naming

`Explicit` is the name every framework that has this feature already uses, and the prior-art table
above is the argument for it: someone arriving from NUnit or xUnit searches for `[Explicit]`, and a
migration guide that has to explain a renamed concept is a worse guide. The reason is `ExplicitReason`
so it reads next to `IgnoreMessage`, and the same stem carries the filterable property, the setting,
and the diagnostics, so there is one word to learn.

`OnDemand` is the alternative on the table, as `[OnDemand]`, `IsOnDemand`, `OnDemandReason` and
`OnDemandTestMode`. It describes the behavior instead of naming the category: the test stays
discoverable and runs when something positively asks for it, which is what this document spends most
of its length defining. `Explicit` says nothing about that on its own, and a reader who has not met
the term elsewhere has to be told what it means either way. `Manual` is rejected under either name,
because it reads as "cannot be automated" when a one-line CI filter runs the whole set.

The choice is between matching the ecosystem and describing the behavior, it applies to every surface
in this document at once, and it cannot be changed after the API ships. It is therefore listed as
something approval decides rather than settled here. `Explicit` is used throughout the rest of this
document because the document needs one name to be readable, not because the question is closed.

### Base type

`ExplicitAttribute` derives from `Attribute` above. Deriving it from `ConditionBaseAttribute` instead
is the alternative, and the shape that makes it work is a non-nullable virtual
`EvaluateCondition(ConditionEvaluationContext context)` on the base whose default implementation
returns `IsConditionMet`. `ExplicitAttribute.IsConditionMet` returns `false`, and it overrides
`EvaluateCondition` to answer from `context.IsTestActivated`. New adapters call the method, existing
conditions keep answering through the property, and no shipped condition changes behavior.

It buys two things. The first is that it closes the pre-3.10.0 boundary that
[Compatibility](#compatibility) otherwise cannot: `AttributeHelpers.IsIgnored` finds conditions by
pattern-matching the base type, so an adapter that predates `[Explicit]` still finds the attribute
through `ConditionBaseAttribute`, reads `IsConditionMet` as `false`, and skips. The test is ignored
rather than run, which is the wrong reason for the right outcome, and it is the outcome the design
wants when the adapter is too old to ask the question properly. Without the derivation that adapter
runs the test during Run All and the framework has no way to stop it. The second is that one hierarchy
covers every declarative gate that ends in a skipped result, `[Ignore]` included, so `[Explicit]`
composes with STA, UI, and user-defined test-method attributes and can be extended to class,
data-source, and data-row scope in one place.

It costs a virtual method and a context type on a base class that has shipped, and it does not replace
the gate. Conditions are evaluated after `TypeCache.GetTestMethodInfo`, and [The gate](#the-gate)
exists to answer before any type load, so the early gate stays and the condition path becomes a second
mechanism answering the same question at a later point for the benefit of older adapters. Two
mechanisms that must agree is the real cost, and the `IsConditionMet` value that makes the old adapter
fail closed is also the value a new adapter must be careful never to read.

Approval decides this one. The discovery metadata, the early gate, and everything downstream of them
are unchanged either way.

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

# every test declared explicit at discovery, which is class, method, unfolded row, and data source
dotnet test --filter "Explicit=True"
```

`Explicit=True` reaches what discovery knows, and a folded data-driven method whose explicitness is
declared on individual rows is not part of that, because a row declaration does not exist until the
source runs. Selecting the method reaches those rows, by name or by any filter that matches it, since
selecting a folded parent activates every row under it. A job that wants everything opt-in regardless
of where it was declared wants `ExplicitTestMode=Run` rather than this filter.

With Microsoft.Testing.Platform:

```bash
dotnet run -- --filter-uid <uid>
dotnet run -- --treenode-filter "/*/*/DeviceTests/*"
```

**CI.** The normal build does not change and does not need a new filter. Explicit tests show up in
its report as skipped, with the reason, so they stay visible instead of disappearing. The opt-in job
names what it wants:

```yaml
# runs on the self-hosted agent that has the device attached
- script: dotnet test --filter "TestCategory=Hardware"
  displayName: Hardware suite

# nightly, everything declared explicit at discovery
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

Request filters feed both. Filters that come from an MTP `ITestExecutionFilterProvider` extension,
from MSTest's own `[TestFilterProvider]` assembly policy, and from other policy sources feed only the
constraint, even when their syntax contains a positive leaf. They express repository policy, not user
intent, so a provider that keeps only `TestCategory=CanRunOnThisMachine` narrows the run and never
starts an explicit test. The executor receives the two values separately and must not try to recover
activation from the composed platform filter:

```csharp
internal sealed record TestSelection(
    ITestElementFilter Constraint,
    ITestElementFilter? ExplicitActivation);
```

This is internal to the adapter and platform services, not public API. Activation is classified once
per request rather than per test, so every run computes it and only an explicit test consults it. It
is deliberately not skipped for an assembly that looks free of explicit tests at discovery: a folded
source can produce a row whose `IsExplicit` is first seen during execution, and an assembly with no
declaration anywhere would otherwise have no activation to check when that row appears.

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
| `A & B` | `matches && (A.activates \|\| B.activates)` |
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
| `Explicit!=True` | no, an exclusion, and it selects the ordinary tests |

A positive leaf does not have to identify one test. `TestCategory=Hardware` activating every explicit
test in that category is the intent, that is what makes the opt-in CI job above a single line.

Malformed filters keep their current parse-error behavior and never fall back to Run All.

### Tree node and graph filters

`--treenode-filter` and the MTP server graph filter are one language, `ServerTestHost` builds a
`TreeNodeFilter` from the request's `GraphFilter`. Its leaves cannot be classified with the table
above, because a path segment can match everything without naming anything: `/**` is Run All written
as a filter, and `/*/*/*/*[Slow!=*]` is a pure exclusion.

A segment is **discriminating** when it names something. A path token qualifies with at least one
literal character. A property predicate qualifies when it uses `=` and carries a literal on either
side, so both `[Explicit=True]` and `[Hardware=*]` do. The key side counts because that is where
MSTest puts a category: the converter writes `[TestCategory("Hardware")]` as
`TestMetadataProperty("Hardware", string.Empty)`, so a category is selected as `[Hardware=*]` and
keying discrimination on the value alone would mean naming a category could never start its explicit
tests, which is the opt-in job this design is mostly for.

Nothing else qualifies. Wildcards, empty segments and `(!EXPR)` do not, and neither does any `!=`
predicate whatever literals it carries, since the operator makes it an exclusion. Nor does any
predicate that no node can fail, because a predicate every node satisfies selects everything, which is
Run All written as a property. That is about what the predicate matches, not how it is spelled,
because `TreeNodeFilter` expands `*` inside a property name and matches the result against every
metadata key, so `[*=*]` reduces to "has any metadata property at all". `Explicit` is written only on
the nodes that are explicit, the same way a category is, so `[Explicit=*]` names a property most nodes
do not carry and discriminates for exactly the reason `[Hardware=*]` does.

Segments compose the same way expressions do: `A & B` is discriminating when the
segment matches and either side is, `A | B` only through a branch that both matched and is itself
discriminating, and `Token[FILTER_EXPR]` when either the token or the property expression is. So
`/*/*/*/(MyTest|(!Slow))` activates a node matched by `MyTest` and not one matched only by `(!Slow)`.

A node is activated when it matches the filter and at least one **non-root** path token on its path is
discriminating, or any segment carries a discriminating property predicate. The root exclusion is
about the path token, because the root names the test project rather than a test in it and
`/MyAssembly/**` selects exactly what `/**` selects in a single assembly host. A property predicate
names a characteristic of the node rather than a container, so it discriminates wherever it is
written, root segment included, and `/**[Explicit=True]` activates while `/**` and `/MyAssembly/**`
do not.

| Filter | Activated? |
| --- | --- |
| `/**`, `/MyAssembly/**`, `/*/*/*/*` | no, these are Run All written as filters |
| `/*/*/*/MyTest` | yes |
| `/*/*/MyClass/*` | yes, through the class segment |
| `/*/MyNamespace/**` | yes, through the namespace segment |
| `/*/*/*/(!Slow)`, `/*/*/*/*[Slow!=*]` | no |
| `/*/*/*/*[Hardware=*]` | yes, this is how a category is written, and it is the opt-in suite |
| `/*/*/MyClass/(!Slow)` | yes, through `MyClass` |
| `/*/*/*/(MyTest\|(!Slow))` | yes for a node matched by `MyTest`, no for one matched only by `(!Slow)` |
| `/**[Explicit=True]`, `/*/*/*/*[Explicit=True]` | yes |
| `/**[Explicit=*]`, `/**[Exp*=True]` | yes, only explicit nodes carry the key |
| `/**[Explicit!=True]` | no, an exclusion, and it selects the ordinary tests |
| `/**[*=*]` | no, "has any metadata property" names nothing |

`TreeNodeFilter` lives in this repository and `Microsoft.Testing.Platform` already grants
`InternalsVisibleTo` to `MSTest.TestAdapter`, so it reports the discriminating result itself through
an internal match overload. No second grammar, no new public MTP API.

These tables describe how tree node and graph selection must behave once MSTest accepts such a
filter. Today `MSTestFilterContext` and the VSTest bridge throw `UnsupportedTestExecutionFilter` for
every leaf other than `NopFilter` and `TestNodeUidListFilter`, so neither reaches MSTest at all.

A property predicate has to be written on a segment that reaches the node. `MatchesFilter` walks the
path fragment by fragment and, once the path has more fragments than the filter has segments, matches
only when the last segment is `**`, so a one-segment filter selects a one-segment path and nothing
below it. An MSTest node is assembly, namespace, class, method, so the two forms that select explicit
tests are `/**[Explicit=True]`, which is the shorter one and is used for the rest of this document,
and `/*/*/*/*[Explicit=True]`, which spells the depth out. Both activate under the rule above: the
path token is wildcard-only in each, and `[Explicit=True]` is a property predicate with a literal, so
it discriminates. `/**[Explicit=True]` is the case the property-predicate half of the rule exists for,
since it has one segment and that segment is the root.

The property predicate needs one thing the other rows do not. `TreeNodeFilter.IsMatchingProperty`
compares `[Key=Value]` against `TestMetadataProperty` and nothing else, so `Explicit` is a
`TestMetadataProperty` on the node, the same property type MSTest already gives categories and
traits. The matcher needs no change. "Not a trait" in [Metadata](#metadata) means the property does
not come from `[TestCategory]` or `[TestProperty]` and is not in `UnitTestElement.Traits`, not that
it is some other kind of property.

MSTest filters before a `TestNode` exists, though. `MtpTestElementFilter` evaluates over
`UnitTestElement`s so the native path never materializes a VSTest `TestCase`, and a tree node filter
is evaluated there too, against a path and property bag built from the element. That bag carries the
same key/value pairs `MSTestTestNodeConverter` would write for the node, including `Explicit` read
from `UnitTestElement.IsExplicit`. Pre-node filtering and node matching then read one source, and
`/**[Explicit=True]` cannot select one set before nodes exist and a
different one after.

### Fail closed

The filters that do not reach MSTest today set the rule for every request shape not listed above:
**activation is proven, never assumed**. Two questions are asked in order, and only the second one is
new.

**Can the constraint be evaluated?** A filter MSTest cannot evaluate does not become a constraint
that happens to activate nothing, because a filter whose semantics are unknown cannot narrow a run
either. It keeps its existing failure. An `ITestExecutionFilter` type MSTest does not understand goes
on throwing `UnsupportedTestExecutionFilter`, and a filter string that does not parse goes on
reporting its parse error. Nothing runs, nothing is reported skipped, and neither falls back to
Run All.

**Can activation be classified?** This question is asked only of a filter that survived the first
one, so the constraint is evaluable and the run is correctly narrowed either way. A well-formed
request whose leaves the tables above do not cover, and a future grammar addition the activation
evaluator has no rule for, constrain the run normally and activate nothing, so explicit tests they
select are reported skipped. A new filter feature cannot start running destructive tests before
somebody has designed its activation semantics.

One case looks like the second and is deliberately treated as the first. An expression VSTest accepts
but the adapter's activation evaluator cannot parse means the two parsers have diverged, which is a
bug in the duplicated grammar rather than an undesigned feature, so it fails the run and the
divergence surfaces instead of being absorbed as "nothing activated". The differential vectors under
[Testing](#testing) exist to keep that case empty. It does not fall back to "any positive token
activates" either, which could start a destructive test through the wrong `|` branch.

The same fail-closed direction applies to persisted metadata. The `Explicit` property is a string and
exactly two values are recognized, `True` and `False`, compared ordinal case-insensitively with no
trimming or other normalization. An absent property means false, which is what lets a case persisted
before the feature still deserialize. Any present value that is not one of the two is treated as
explicit and logged, so `" False"`, `"0"`, and a truncated cache entry all resolve to explicit and a
stale cache cannot turn an opt-in test into a Run All test.

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
test. The three names above are the whole accepted set in both formats, compared ordinal
case-insensitively the way `ExecutionScope` and `DebuggerLaunchMode` already are, so the lowercase
`requireSelection` in the JSON example is the same value as the `RequireSelection` in the XML one and
not a second spelling to support. There are no friendly aliases, and a purely numeric value is
rejected rather than read as the underlying enum number, the same guard `TryParseCaptureMode` already
applies for the same reason: `Enum.TryParse` would accept `2`, and a number that happened to land on
`Run` would widen activation with nothing in the settings naming it.

`Run` is configuration rather than a CLI shortcut, and the nearest command line is
`--filter "Explicit=True"`, which stays visible in the command line. The two are not interchangeable
and a CI definition has to choose deliberately. `Run` widens activation and leaves selection alone,
so Run All still selects the whole suite and now runs the explicit tests in it as well. The filter
narrows selection instead, so the run is the explicit tests plus whatever folding attaches to them,
and a job meant to exercise only the explicit tests wants the filter rather than `Run`, which runs the
entire suite.

Folding blurs both edges of that filter and neither edge is fixable from the filter side. It selects a
folded parent whose sources are not all explicit, and selecting a parent runs every row under it, so
an ordinary source beside an explicit one runs too. It also misses a folded method declared explicit
only on its rows, which is not selectable at discovery at all. A suite that has to be exactly the
explicit tests wants unfolded rows, where every row is selectable on its own; a CI definition that
cannot guarantee that should not rely on the result set being explicit-only.

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

Folded rows have no discovery identity, but not every declaration needs the data. Class, method, and
source explicitness are all readable without running the source, because `IsExplicit` is a property
on the `ITestDataSource` attribute instance, reached by reflection exactly like `IgnoreMessage`.

A method can carry several sources, and `TryExecuteFoldedDataDrivenTestsAsync` runs all of them under
one parent, so one flag on the parent cannot answer both questions being asked of it. The parent
carries `IsExplicit`, `ExplicitFromDeclaration` and `ExplicitFromSources`, described under
[Metadata](#metadata):

- **Gating**, `ExplicitFromDeclaration` and `ExplicitFromSources`. The parent is gated as explicit
  when the class or the method declares it, or when every source on the method declares it,
  `ExplicitFromSources` of `All`. Those are the cases where the whole method skips, so the ordinary
  gate skips an unactivated parent before `TypeCache.GetTestMethodInfo`, with no type load, exactly as
  for a method declaration. A method with no class or method declaration whose sources disagree is
  `Some`: it is not gated, it reaches enumeration, and each source is resolved on its own below.
- **Selection**, `IsExplicit`. The parent is reported explicit when the class, the method, or any
  source declares it, so `/**[Explicit=True]` and `--filter "Explicit=True"` select it. Discovery does
  not record the source-wide ignore message on a folded parent today, and explicitness has to be
  recorded: an ignored parent never needs to be selectable, while an explicit one does.

A gated parent reports one result standing for everything under it, so its reason has to be true of
everything under it. A parent gated by a class or method declaration takes that declaration's reason,
the more specific of the two, because one declaration covers the whole method and there is nothing to
disagree with it. A parent gated by its sources takes the reason its sources agree on, and reports
none on any disagreement, including one source carrying a reason where a sibling carries none. Picking
the most specific available reason there would attribute one source's explanation to a result standing
for all of them. Each source's own reason is still reported when the sources are resolved
individually, which is where a per-source explanation belongs.

`TryExecuteFoldedDataDrivenTestsAsync` then checks each source before calling `GetData` on it,
immediately ahead of that source's `IgnoreMessage` check, the same place and the same granularity the
ignore check already uses. An unactivated explicit source produces one skipped result and enumerates
nothing, and the sources beside it are unaffected, so a method mixing an explicit source with an
ordinary one reports one explicit skip plus the ordinary source's rows. That check also catches a
gated-looking parent that arrives without the discovery metadata.

Only row declarations need the data. For a source that is not explicit, or one that is activated,
each row is checked as it is produced, before per-row `TestInitialize`, before test-class
construction where construction is per row, and before the body. Each unactivated row produces its
own skipped `UnitTestResult`, which keeps folded result cardinality exactly as it is today.
Enumeration and the initialization needed to reach it can therefore still happen when every produced
row turns out to be explicit. That is inherent to folded data, a row declaration does not exist until
the source runs, and that is not a reason to force unfolding. The same is not true of a source
declaration, which is why the source check does not wait for enumeration.

`ITestDataSourceIgnoreCapability` and row `IgnoreMessage` win over the explicit state wherever both
are resolved at the same point, and that is not every point. The [Precedence](#precedence) table sets
the rule for a method: an unactivated explicit test reports an explicit skip, because `[Ignore]` is
resolved only after type loading and the gate exists to avoid that load. Folded data follows it.

- Unfolded rows carry both on the `UnitTestElement` from discovery, so ignore is resolved at the same
  point as explicitness and wins for every row.
- A folded parent gated as explicit and unactivated reports one explicit skip. The gate reads the
  element, which carries the explicit state and not the ignore message, so reporting an ignored result
  would mean loading the type and calling the source. It must not: an explicit test does not run its
  data source to find out that it would have been ignored anyway. `GetData` is not called and no row
  metadata exists to take precedence.
- The per-source check in `TryExecuteFoldedDataDrivenTestsAsync` sits ahead of that source's ignore
  check for the same reason, so a source resolved there gives the answer the gate would have given for
  it. That changes nothing for a source that declares ignore only, because the explicit check is a
  no-op for it.
- A folded parent that is gated at none of those scopes, or is activated, reaches enumeration, so
  row ignore metadata arrives alongside row explicit metadata and wins there, row by row. An
  activated parent whose source declares ignore reports one ignored result, exactly as today.

Every one of these ends in a skipped result, so what the ordering decides is the reported reason and
the result count, never whether anything ran.

If a data source throws while producing that metadata, existing data source failure behavior wins,
the adapter cannot know that the missing row would have been explicit.

### Retry

An automatic retry does not create activation. A test skipped by Run All produces one skipped result,
zero attempts, and never enters the retry set, and a selected explicit test that fails retries like
any other test. MSTest's own `[Retry]` re-runs an already activated method in process and starts no
new request.

The retry set alone does not give that, because folding makes it coarse. Every folded row reports
under its parent's UID, so an ordinary row failing puts the parent UID in the set, and the MTP retry
extension then replaces the original filter with `--filter-uid` for it. Read as a fresh UID
selection, that request activates the parent, and the explicit rows the first attempt skipped run on
the second.
A retry attempt therefore inherits the activation of the attempt it is retrying and never derives one
from its own UID list. The retry pipe cannot carry that by itself.
`RetryArgumentsBuilder.BuildAttemptArgumentsAsync` adds `--internal-retry-pipename` to every attempt it
launches, including the first, and only the `--filter-uid` replacement is conditional on there being
failed tests to retry. Attempt 1 of a user-selected UID run and attempt 2 of a retried Run All can
therefore carry the same two options, and nothing in either of them says which request the user made.

The activation travels as its own hidden option, `--internal-retry-activation`, written by the retry
extension on every attempt it launches, alongside `--internal-retry-pipename` and following the same
naming and registration convention. It carries the classification the orchestrator made of the
original request, `SourceRun`, `SelectedTestCases`, or `PositiveFilter`, with the original filter
string when the last one applies, which are the three values
[Future work](#future-work) already names for diagnostics. MSTest reads it in preference to
classifying the request it can see, so an original Run All stays unactivated on every attempt and an
originally selected explicit test keeps retrying.

Absence has to be defined rather than assumed, because it is reachable without any corruption. The
retry extension ships on its own version line, so an older one paired with a newer MSTest supplies the
pipe and the UID list and no activation, and that pairing is outside the framework and adapter
alignment check under [Compatibility](#compatibility), which compares only those two. When a process
is a retry attempt and the option is absent, MSTest activates nothing rather than reading
`--filter-uid` as selection. That is the fail closed direction the rest of this design takes: it costs
an originally selected explicit test its retries under an old extension, which is one skipped result
that names its reason and is recoverable by running the test again, while the alternative starts a
twenty-minute row that nobody asked for.

A host's "rerun failed tests" is a user action rather than orchestration. It arrives as ordinary
test-case selection and activates what it selects, which for a folded parent is every row under it.
That is the coarseness of folding rather than anything retry adds, and it is the answer
[What counts as selecting a test](#what-counts-as-selecting-a-test) already gives for selecting the
parent.

### Metadata

Discovery always reports explicit tests, with no skipped state attached, a test is not skipped until
an execution request fails to activate it. Each `UnitTestElement` carries three fields, because
selection and gating are different questions for a folded parent and the same field cannot answer
both:

| Field | Meaning | Read by |
| --- | --- | --- |
| `IsExplicit` | the class, the method, or any source declares it | filters, node metadata, VSTest properties, reporting |
| `ExplicitFromDeclaration` | the class or the method declares it | the execution gate in `UnitTestRunner` |
| `ExplicitFromSources` | what the data sources say: `None`, `All`, or `Some` | the execution gate in `UnitTestRunner` |
| `ExplicitReason` | see below, and none whenever the sources a gated parent stands for disagree | reporting |

The gate skips when `IsExplicit` is true unless all three of these hold: `ExplicitFromDeclaration` is
false, `ExplicitFromSources` is `Some`, and the element is a folded data-driven parent. Deferring to
the per-source checks is the one answer that must be earned, so it takes a positive assertion from
every one of them, and everything else gates.

The shape condition is normative rather than a description of what happens to be true today, because
the folded parent is the only shape with a per-source check downstream for the deferral to defer to.
Unfolding clones the parent element, so without the shape condition an unfolded row would inherit
`ExplicitFromDeclaration=False` and `ExplicitFromSources=Some` from a mixed-source parent, take a
deferral nothing downstream honors, and run under Run All, which is exactly the twenty-minute row in
[Motivation](#motivation). `ExplicitFromSources` is therefore also element-scoped rather than
method-scoped: while unfolding, `AssemblyEnumerator` overwrites it on each cloned row with the value
for the source that produced that row, `All` when that source declared explicitness and `None` when it
did not, so an unfolded row never carries `Some` and never reaches the deferral in the first place.
The two work together on purpose. The element scoping is what makes ordinary and explicit sibling rows
correct, and the shape condition is what keeps them correct for an element that arrives with fields
this discovery run did not write, such as one rebuilt from serialized VSTest properties.

They are two independent fields rather than one because a single field cannot be checked against
anything. A boolean has two values, so one corrupted bit turns any explicit parent into a legitimate
looking deferral. Collapsing them into one scope with three names is no better: a parent explicit at
class scope whose sources are entirely ordinary, corrupted to say `Some`, still reads as a valid
mixed-source deferral, and its per-source checks then find nothing explicit and run every row under
Run All. Split, the class or method declaration is still asserted separately, so that combination is
visibly inconsistent and gates. A declaration that always gates on its own never becomes deferrable
through a value that describes the sources.

Neither field can be recomputed where it is read, because recomputing means reading the attributes,
which loads the type the gate exists to avoid loading, so they travel rather than being derived. A
VSTest `TestCase` reconstructed from serialized properties has only what was written to it, and
`IsExplicit` alone cannot distinguish a parent where one of several sources is explicit from one where
all of them are.

What the fail closed rule covers here is worth stating exactly, because it is narrower than it may
read. Metadata that cannot be believed skips the test: a field absent, malformed, unrecognized, or in
a combination that cannot arise from any real declaration. Metadata that has been rewritten into a
different, individually valid value is not covered and cannot be. `ExplicitFromDeclaration=True` with
`ExplicitFromSources=Some` rewritten to `False` with `Some` reads exactly like a source-only mixed
parent, and no amount of added provenance closes that: a third field is corrupted the same way, and
`Explicit=True` rewritten to `False` defeats the feature at the first field before any of them are
consulted. A store that rewrites valid values into other valid values is a broken store, not a threat
this design can absorb, and pretending otherwise would buy a false guarantee at the price of more
fields to keep consistent.

- VSTest: adapter owned `MSTestDiscoverer.Explicit`, `MSTestDiscoverer.ExplicitFromDeclaration`,
  `MSTestDiscoverer.ExplicitFromSources` and `MSTestDiscoverer.ExplicitReason` properties,
  round-tripped by `ToTestCase` and `ToUnitTestElement`.
  These names are stable wire identifiers. `Explicit` is registered as a filterable string with values
  `True` and `False` compared case-insensitively, deliberately not a `bool`, so malformed persisted
  values reach the fail closed rule instead of being defaulted by VSTest's converter. It is written
  only when it is true, so an ordinary case carries no `Explicit` property at all. A missing
  `Explicit` means false to the gate and a missing reason means no reason, so a case persisted by an
  older version still deserializes. The two gate inputs are not filterable, they exist only so the gate
  has them, and both are strings compared ordinal case-insensitively for the same reason `Explicit` is.
  The deferral is honored only on a folded data-driven parent, the one shape with a per-source check
  downstream, and the case says which shape it is in metadata it already carries, so no type load is
  needed to ask. On any other shape it is inconsistent rather than a deferral and the gate fires, as it
  does for a missing or unrecognized value in either field on any shape. Over-skipping costs a folded
  parent with disagreeing sources one explicit skip instead of its ordinary source's rows, which is
  visible and recoverable by selecting the test. Reasons are prose and stay unfilterable.
- Native MTP: `MSTestTestNodeConverter` adds `Explicit`, value `True`, to explicit nodes only, on both
  discovered and result nodes. `Explicit` is a `TestMetadataProperty`, which is the property type
  `[Key=Value]` in a tree node filter matches, so `/**[Explicit=True]` works with no platform matcher
  change. It is deliberately not written to ordinary nodes. An earlier draft wrote `Explicit=False`
  to every node so that both operators would have something to match, on the stated grounds that the
  registered VSTest property evaluates an ordinary test as `False`. It does not:
  `TestMethodFilter.PropertyValueProvider` returns a value only when
  `currentTest.Properties.Contains(testProperty)` and otherwise falls through to `null`, so a
  registered property that was never written to a `TestCase` is missing rather than false. Both hosts
  already answer a missing property the same way, `=` does not match it and `!=` does, and the
  universal write was the thing that would have made them disagree.
  The reason is deliberately not a node metadata property: `IsMatchingProperty`
  matches every `TestMetadataProperty`, so writing it as one would make it filterable on MTP and
  unfilterable on VSTest, which contradicts both the API contract and the equivalence rule. It reaches
  the user through the skip message on the result, the same place the other hosts read it. UIDs do
  not change when `[Explicit]` is added, and a server client that ignores the metadata still receives
  ordinary nodes and ordinary skipped results. The native path keeps its elements in process, so
  nothing is reconstructed there and the two gate inputs need no node metadata.

Writing `Explicit` as a `TestMetadataProperty` puts it on the surfaces that carry test metadata
downstream, and this RFC states that rather than claiming the property stays inside the filter engine.
Server mode serializes every `TestMetadataProperty` under `traits`, `--list-tests` JSON emits it in the
`traits` array, the TRX report writes it under `<Properties>`, and the OpenTelemetry handler emits
`test.metadata.Explicit`. Restricting the write to explicit nodes is what keeps that a property of the
feature rather than a change to every MSTest-on-MTP run: a test with no declaration carries no new
metadata on any of those surfaces, which is what the [Compatibility](#compatibility) statement
promises. On a test that does declare `[Explicit]` the addition is intended and is listed there as a
compatibility change. It is still not a user authored category or trait, it is produced from
`IsExplicit` rather than from `[TestCategory]` or `[TestProperty]` and it is not in
`UnitTestElement.Traits`, but it shares their transport, so the test plan asserts which surfaces it
reaches rather than asserting it reaches none.

That leaves one filter answer to state plainly, because dropping the universal write changes it. Both
hosts match a missing property the same way, so on both of them `Explicit=True` selects the explicit
tests, `Explicit!=True` selects the ordinary ones, `Explicit=False` selects nothing, and
`Explicit!=False` selects everything. `Explicit=False` is not the way to ask for ordinary tests on
either host, and nothing in this design needs it to be: `ExplicitTestMode` is how a run changes the
default, and `Explicit!=True` is how a filter excludes them. VSTest additionally has `None`, a
reserved value in its filter grammar meaning "this property is not set", so `Explicit=None` selects
the ordinary tests there. `TreeNodeFilter` has no equivalent token, so that spelling has no MTP
counterpart. The divergence belongs to the two filter grammars rather than to this feature, it is the
same for every unset property VSTest knows, and the vectors under [Testing](#testing) record what each
host answers so neither drifts.

`Explicit` is a reserved property name on both hosts, compared ordinal case-insensitively, because
both hosts match case-insensitively: `ValueExpression` builds its regex with `RegexOptions.IgnoreCase`,
and `TestMethodFilter`'s supported-property dictionary and its trait fallback both use
`OrdinalIgnoreCase`. Reserving only the exact spelling would leave `[TestProperty("explicit", "True")]`
colliding.

The reservation covers `[TestCategory]` as well as `[TestProperty]`, because MTP puts both in one key
space: `MSTestTestNodeConverter` writes each category as `TestMetadataProperty(category, string.Empty)`
and each trait as `TestMetadataProperty(name, value)`, and `IsMatchingProperty` succeeds on any
matching value. Covering only `[TestProperty]` would let `[TestCategory("Explicit")]` add a second
value under the reserved key beside the built-in one and bypass the reservation without ever naming a
property. A `[TestCategory]` or a `[TestProperty]` whose name matches the reserved name in any casing
is written to none of the metadata surface, the categories, or the traits, and discovery reports a
warning naming the test, so the built-in value is the only one either host can match.

`ExplicitReason` is deliberately not reserved. The reason is not written to node metadata and is not
filterable on either host, so no built-in surface occupies that name for a user property to collide
with, and reserving it would only take the name away from existing
`[TestProperty("ExplicitReason", ...)]` and `[TestCategory("ExplicitReason")]` users in exchange for
nothing. If a later change exposes the reason on a matchable surface, reserving the name belongs in
that change, where it can be justified by what it protects.

A folded parent carries its class, method, and source declarations. A source declaration is read from
the attribute instance and needs no enumeration, so a method with any explicit source is reported
explicit at discovery and `/**[Explicit=True]` and `--filter "Explicit=True"` select it, whether or not
its other sources are explicit. Only row declarations are missing from the parent, they do not exist
until the data is enumerated, so a folded method whose explicitness lives only in rows is not
selectable by those filters and is reached by selecting the method instead.

Skipped results carry `The test is explicit and was not selected.`, with `Reason: <reason>` appended
when one exists. `ExplicitTestMode=Skip` gets its own message, `The test is explicit and
ExplicitTestMode is Skip.`, because under that mode a directly selected test is still skipped and
telling its author it was not selected would send them to look for a selection problem that does not
exist. The reason is appended to either one. The reason is copied verbatim, only the decision whether
it is empty inspects it,
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
| Discovery | `TypeEnumerator` reads class and method declarations, `AssemblyEnumerator` scans every data source's declaration before it decides whether to unfold, since `TryUnfoldITestDataSources` returns for both fold modes ahead of reading the attributes and can also return once a source fails to unfold, and while unfolding it merges row declarations and overwrites the cloned row's `ExplicitFromSources` with its own producing source's value |
| Execution | pre-initialization gate in `UnitTestRunner`, per-source and per-row checks in `TestMethodRunner.DataRow` |
| VSTest | classify source versus test-case execution in `MSTestExecutor`, register and round-trip properties, evaluate activation in `TestMethodFilter` |
| Native MTP | build activation from UID, tree, and property filters in `MSTestFilterContext` and `MtpTestElementFilter`, accept tree node and graph filters instead of throwing, add node metadata |
| Platform | `TestExecutionFilterComposer` and the request factories keep the original request filter next to the provider-constrained one, on an internal surface reached through the existing friend assembly |
| Retry | the process retry extension adds `--internal-retry-activation` beside `--internal-retry-pipename` on every attempt it launches, so a retry attempt inherits the original activation instead of reading its own `--filter-uid` as a selection, and its absence activates nothing |
| Settings | three `ExplicitTestMode` values with existing precedence, `explicitTestMode` added to `docs/testconfig.schema.json`, whose `mstest.execution` object sets `additionalProperties: false`, plus localized resources |
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
  parentheses, escaping, bare values, and case-insensitive `Explicit` values. They run against nodes
  the converter actually produced rather than hand-built property bags, because that is what would
  have caught a category being written as `[Hardware=*]` while the table claimed `[Category=Hardware]`.
  Wildcarded property keys are vectors too. `[Exp*=*]` and `[Exp*=True]` assert activation and `[*=*]`
  asserts none, because `Explicit` is written only to explicit nodes, so a wildcard that resolves to
  that key names something most nodes do not carry while one that resolves to every key names nothing.
  The same serialized
  vectors run against VSTest and native MTP so the two cannot drift, and against both expression
  evaluators so the duplicated parser cannot drift either. Every `Explicit` vector runs in all four
  combinations of the two operators and the two values, because that is where the hosts would diverge
  first, and all four assert the same answer on both: `Explicit=True` selects the explicit tests,
  `Explicit!=True` the ordinary ones, `Explicit=False` nothing, and `Explicit!=False` everything. That
  is the missing-property behavior both hosts already have, and the vectors exist to keep anyone from
  restoring the universal `Explicit=False` write that would break it. `Explicit=None` is a vector too,
  asserting it selects the ordinary tests on VSTest, where `None` is a reserved value in the filter
  grammar, and that `/**[Explicit=None]` selects nothing on MTP, which has no such token, so the
  grammar-level difference is recorded rather than discovered later.
- Ordering tests proving that assembly `ITestFilter` returning `Run` and provider constraints never
  activate, and that an unclassifiable request activates nothing. One of them registers a
  `[TestFilterProvider]` alongside an unactivated explicit test and asserts the filter is still
  constructed and called, pinning the boundary of what the gate skips.
- Fail-closed tests separating the two outcomes: an unsupported filter type and an unparseable filter
  string still fail the run, while a well-formed but unclassifiable filter runs its ordinary tests and
  reports the explicit ones skipped. Settings parsing is covered the same way in both formats: the
  three names round-trip in any casing, and a misspelling, an alias, and a numeric value each throw
  rather than resolve to a mode.
- Filter-path agreement tests for `Explicit` as node metadata: `/**[Explicit=True]` selects the same
  tests through `MtpTestElementFilter` before nodes exist as `TreeNodeFilter` matches against the
  `TestMetadataProperty` the converter writes. One covers the root-segment case specifically, that
  `/**[Explicit=True]` activates
  while `/**` and `/MyAssembly/**` do not, since that is the whole reason the rule separates the path
  token from the property predicate. One asserts the documented reachability boundary directly: a
  folded method with an explicit source is selected, a folded method whose explicitness is only on rows
  is not, and selecting that method by name runs its explicit rows. Another puts
  `[TestProperty("Explicit", "True")]` on an ordinary test, and `[TestProperty("explicit", ...)]` and
  `[TestCategory("EXPLICIT")]` beside it, asserting both hosts ignore every casing and both attribute
  kinds, that none reaches the node metadata, the categories, the traits, or the VSTest property, that
  `--filter "Explicit=True"` no longer matches them on VSTest where it does today, and that discovery
  warns for each. The category form is a vector in its own right rather than an afterthought, because
  categories and traits share one `TestMetadataProperty` key space on MTP, so covering only
  `[TestProperty]` would leave the reservation bypassable.
- Metadata-surface tests pinning what the node property reaches, replacing the earlier claim that it
  reaches nothing. For a test declaring `[Explicit]`, `Explicit=True` is asserted present in server
  mode `traits`, in the `--list-tests` JSON `traits` array, in TRX `<Properties>`, and as the
  OpenTelemetry `test.metadata.Explicit` tag. For a test with no declaration, all four are asserted to
  carry nothing new against a baseline captured before the feature, which is the assertion that keeps
  the compatibility promise honest and would fail immediately if the universal write came back.
- A reason-is-not-filterable test: an explicit test with a reason, asserting `/**[ExplicitReason=*]`
  matches nothing on MTP and the reason still reaches the skip message on both hosts. Beside it, a
  test carrying `[TestProperty("ExplicitReason", "mine")]` asserts the opposite of the `Explicit`
  vectors, that the name is not reserved: the property still reaches the traits, the node metadata and
  the VSTest property, `--filter "ExplicitReason=mine"` still matches it, and discovery does not warn.
- A late-row test: an assembly whose only explicit declaration is on a `TestDataRow<T>` produced
  during execution, asserting the row is still skipped under Run All and still runs when the method is
  selected, so an assembly that looks free of explicit tests at discovery does not lose activation.
- Mixed-source tests over one folded method carrying several data sources: an explicit source beside
  an ordinary one reports one explicit skip and still runs the ordinary source's rows under Run All,
  the parent is selected by `Explicit=True`, selecting it runs both sources, a method whose sources
  are all explicit is gated with no type load, and the gated parent's reason is the one its sources
  agree on, with no reason reported when they disagree, including one source carrying a reason beside
  a sibling that carries none, and one where the method declares a reason and every source declares a
  different one, asserting the method's wins because the method gated it, which is where the aggregate
  rule and the per-test precedence chain diverge. Each source still reports its own when resolved
  individually. The
  same methods round-trip through a serialized VSTest `TestCase` and keep their gate answers, and the
  unbelievable-metadata vectors all assert a skip rather than a run: either gate input stripped, either
  unrecognized, a deferral claimed on a plain explicit method, and a deferral claimed on a folded
  parent whose `ExplicitFromDeclaration` is still true, which is the one that would otherwise run every
  row because the per-source checks find nothing explicit to stop. They stop there deliberately: a
  value rewritten into a different valid value is outside what the gate can detect, as described under
  [Metadata](#metadata), so there is no vector for it and no assertion claiming one.
- A discovery test for the folded paths: a method whose sources are all explicit is recorded and
  selectable under both fold strategies and when an earlier source fails to unfold, pinning that the
  source scan happens ahead of every early return in `TryUnfoldITestDataSources` rather than inside
  the unfolding it skips.
- An unfolding test under the default strategy, run on both hosts, over one method carrying an
  ordinary `[DataRow]` and an explicit one. Under Run All the ordinary row runs and the explicit row
  is skipped, which is the vector that fails if a cloned row keeps the parent's `Some` and takes the
  deferral no per-source check backs. It asserts the cause and not only the symptom: each unfolded
  element carries its own producing source's `ExplicitFromSources`, `All` or `None` and never `Some`.
  Beside it, an element hand-built as a non-folded shape claiming `ExplicitFromDeclaration=False` with
  `ExplicitFromSources=Some` asserts the gate skips it, pinning the shape condition itself rather than
  only the discovery code that currently keeps it satisfied.
- A custom `ITestDataSource` test covering both ways in, the capability for every row and a
  `TestDataRow<T>` yielded as the single element of an `object?[]` for one row, asserting the wrapped
  row is recognized exactly as the `DynamicData` shape is.
- Execution tests proving no type load, no fixture, no body, and no retry for an unactivated test,
  correct cleanup counts for explicit skips, and folded and unfolded row behavior. One covers the
  folded parent that is itself explicit and unactivated, asserting the data source never runs and the
  method reports one explicit skip even when its rows would have been ignored. Another moves the
  declaration from the method to the source and asserts the same outcome, with a counter incremented
  in `GetData` proving enumeration did not happen, and a source declaring both ignore and explicit
  reporting one explicit skip while unactivated and one ignored result once activated, asserted both
  from the discovery metadata and from a parent that arrives without it. Row-specific explicitness is
  the case that does enumerate, and it asserts one result per row with the ordinary siblings still
  running.
- Retry tests over a folded parent whose rows are one ordinary failing row and one explicit row.
  Under Run All the ordinary row fails and retries, and the explicit row is skipped on every attempt,
  pinning that the parent UID in the retry set does not activate it. The same asset selected by UID
  retries both. Two more cover the signal rather than the outcome: one asserts every attempt the
  extension launches carries `--internal-retry-activation`, including the first, since the pipe alone
  cannot tell a retried Run All from a user-selected UID run. The other is the version-skew vector,
  launching the attempt with the pipe and the failed-UID list but no activation option, and asserting
  the explicit row is skipped rather than run, which is the fail closed answer an older retry
  extension paired with a newer MSTest has to get.
- One acceptance asset, run through both hosts, covering explicit method, class, base and override,
  ignored and conditional explicit tests, ordinary and explicit sibling rows, source-wide and
  row-specific dynamic data, retry, all three `ExplicitTestMode` values, and fixture counters written
  to disk so initialization can be asserted rather than assumed. It also runs `ExplicitTestMode=Run`
  and `--filter "Explicit=True"` over the same asset and asserts the result sets differ, pinning that
  the two are not interchangeable.
- Compatibility runs: an assembly with no declarations before and after the feature, a mismatched
  adapter and framework pair asserting the existing alignment error rather than any explicit
  behavior, a persisted test case with missing and malformed properties, and unchanged MTP UIDs. The
  pre-3.10.0 adapter boundary below is not covered by a test, it needs a published old adapter and
  what it would assert is the absence of the feature.

Both hosts ship together, and they ship equivalent. Selection, filtering, retry, results, and
diagnostics must behave the same under VSTest and native MTP, which is why the acceptance asset is
one asset run twice rather than two suites.

## Compatibility

The APIs are additive and binary compatible, and a test with no declarations keeps exactly its
current discovery, filtering, execution, retry, and result behavior, on every surface. That last part
is why `Explicit` is written only to nodes that declare it. A `TestMetadataProperty` is not confined to
the filter engine, it is the transport for test metadata downstream, so writing the property to every
node would have added `Explicit=False` to server mode `traits`, to `--list-tests` JSON, to TRX
`<Properties>` and to an OpenTelemetry tag for every MSTest-on-MTP run in existence, including runs of
assemblies that never heard of this feature. Writing it only where it is declared keeps this paragraph
true. Existing custom data sources compile and behave as before, the capability is opt-in.

Reserving one property name is the one exception, and it needs a release note that covers both hosts.
A test carrying `[TestProperty("Explicit", ...)]` or `[TestCategory("Explicit")]`, in any casing,
compiles and runs as before, but the value stops reaching the filterable surfaces
and discovery warns about it. On MTP a tree node filter written against it stops matching. On VSTest
it stops matching too, and not because the trait is dropped in isolation: `TestMethodFilter` resolves
any name it does not recognize from `TestCase.Traits`, so `--filter "Explicit=True"` matches such a
test today, and registering `Explicit` as a supported property takes that name over regardless. The
break is therefore on both hosts rather than on MTP alone, the fix is to rename the property or the
category, and it is called out here because it is a behavior change for a test with no explicit
declaration of its own, which nothing else in this design touches.

`ExplicitReason` is not reserved, so `[TestProperty("ExplicitReason", ...)]` keeps working unchanged
and is not part of this break. An earlier draft reserved it alongside `Explicit`; since the reason is
not written to node metadata and is not filterable on either host, there was no built-in value for a
user property to collide with and the reservation only widened the break.

A test that does declare `[Explicit]` gains `Explicit=True` on those same downstream surfaces. That is
a change to what a run reports, not only to what it selects, so a report or dashboard that enumerates
traits or TRX properties sees one more entry on declaring tests. It is intended and additive, no
existing key changes value, and it is listed here so it is not discovered by a diff.

The risk worth calling out in release notes is version skew, and the alignment check already closes
most of it. `MSTestExecutor`'s module initializer compares the informational versions of
`MSTest.TestAdapter` and `MSTest.TestFramework` and throws when they differ, so a mixed pair fails
the run before anything is discovered. A new adapter with an old framework does not quietly see no
declarations, and an old adapter with a new framework does not quietly run `[Explicit]`. Both stop
with the existing alignment error, and this RFC does not change that check.

One boundary is left that the alignment check cannot close. The check itself shipped in 3.10.0, so an
adapter older than that, paired with a new framework, does not perform it, does not recognize
`[Explicit]`, and runs the test during Run All. The gate lives in the adapter, so an
`Attribute`-derived `[Explicit]` cannot defend itself against an adapter that predates it, and that is
a release-note boundary. Deriving from `ConditionBaseAttribute` closes it, because that base type is
old enough for the old adapter to find the attribute through it, which is the concrete reason
[Base type](#base-type) is an open question rather than a stylistic one.

Making the attribute derive from `[Ignore]` would also hide this, and would additionally make the test
impossible to select on any adapter, so it is not done.

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

## Open questions

Two questions are deliberately left for approval rather than answered here, because both change every
surface at once and neither can be revised after the API ships.

| Question | Options | Where |
| --- | --- | --- |
| What is the concept called? | `Explicit`, matching NUnit, xUnit and TUnit, or `OnDemand`, describing the behavior. `Manual` is rejected under either. | [Naming](#naming) |
| What does the attribute derive from? | `Attribute`, or `ConditionBaseAttribute` with a virtual `EvaluateCondition`, which additionally closes the pre-3.10.0 adapter boundary. | [Base type](#base-type) |

Everything else in this document is written as though the answers were `Explicit` and `Attribute`,
because it needs one spelling to be readable. Neither answer changes the activation model, the gate,
the metadata, or the tests.

## Resolved questions

| Question | Decision |
| --- | --- |
| Is explicit a condition? | Not in semantics: it depends on the request, not on the environment. Whether the attribute nonetheless derives from `ConditionBaseAttribute` is open, see [Base type](#base-type). |
| Are explicit tests discovered? | Always, and Run All reports them skipped rather than hiding them. |
| What activates one? | Concrete selection, or a positive branch of a request filter that matches it. |
| Does an exclusion filter activate? | Never, and neither does `/**` or a root-only tree path. |
| Can a provider or policy filter activate? | Never, including assembly `ITestFilter` returning `Run`. |
| Can selection override `[Ignore]` or a condition? | No, it removes the explicit gate only. |
| Does selecting a class or namespace activate what is under it? | Yes, and selecting a folded parent activates all its rows. |
| Does `Explicit=True` reach every explicit test? | It reaches every declaration known at discovery. A folded method explicit only on rows is reached by selecting the method, or by `ExplicitTestMode=Run`. |
| What if a folded method has several data sources? | It is gated only when all of them are explicit, otherwise each source is resolved on its own before its `GetData`. |
| Can a row opt out of an explicit method or source? | No, declarations OR-compose. |
| Which reason wins? | For a test, the most specific explicit declaration that has one. For a folded parent skipped before enumeration, the one its sources agree on, or the class or method declaration that gated it. |
| What happens when activation cannot be determined? | Nothing activates, the request constrains only. |
| Are skipped explicit tests retried? | No. Selected explicit failures retry normally, and a retry attempt inherits the original activation rather than making one. |
| Is there an override? | `ExplicitTestMode`, with `Skip` and `Run` on either side of the default. |

Approval is needed for the name, the attribute's base type, the public API, the positive-filter
activation model, the discriminating segment rule for tree node and graph filters, the three
configuration values, and the documented legacy VSTest boundary. The first two are the ones
[Open questions](#open-questions) leaves undecided.
