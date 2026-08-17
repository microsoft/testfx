# RFC 024 - Explicit tests

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Add an `[Explicit]` attribute and equivalent data-row metadata to MSTest. Explicit tests are
discovered and displayed like ordinary tests, but a broad **Run All** request reports them as skipped.
They run only when the request positively selects them, for example by choosing the test in Test
Explorer, by UID, by tree node, or with an inclusive filter expression.

The design uses one host-independent selection model for VSTest and Microsoft.Testing.Platform
(MTP). It distinguishes:

- a **constraint**, which decides whether a test is part of a run; from
- an **activation**, which proves that the user or client positively selected an explicit test.

That distinction is essential. `TestCategory!=Integration`, a policy filter supplied by an extension,
or an empty server selection can constrain a run, but none expresses intent to run an explicit test.

This RFC is the design investigation requested by
[microsoft/testfx#5346](https://github.com/microsoft/testfx/issues/5346). It intentionally makes no
production-code change before approval.

## Motivation

Some tests are valid and valuable but should not run in every build: destructive environment
checks, expensive end-to-end scenarios, hardware-dependent tests, migration verification, and
manual diagnostics are common examples. `[Ignore]` is not a good representation of those tests:

- ignored tests cannot be enabled by selecting them;
- changing source code or RunSettings is required before they can run;
- an ignore often communicates "temporarily broken", while these tests are intentionally opt-in;
- CI and Test Explorer cannot distinguish an intentionally opt-in test from disabled debt.

NUnit has supported `[Explicit]` on fixtures and test methods for many years and also supports
explicit individual test cases. The original request notes that the absence of this capability can
drive users to another framework. MSTest should provide the capability without making Run All
unsafe or making host behavior depend on an IDE-specific heuristic.

### Existing behavior this design builds on

`IgnoreAttribute` in
`src/TestFramework/TestFramework/Attributes/TestMethod/IgnoreAttribute.cs` is sealed, targets
classes and methods, and uses `Inherited = false`. It derives from `ConditionBaseAttribute`; the
executor evaluates it with the other conditions and reports a skipped result.

Explicitness is different. A condition can decide whether the environment permits a test to run,
but it cannot know whether the execution request positively selected that test. `[Explicit]`
therefore does **not** derive from `ConditionBaseAttribute`. Discovery records explicit metadata,
and execution combines that metadata with a host-independent description of the request.

VSTest filters currently pass through `TestMethodFilter`, while native MTP requests pass through
`MSTestFilterContext` and `MtpTestElementFilter`. At the platform layer,
`TestExecutionFilterComposer` composes the request filter with filters supplied by extension
providers. The final composite answers "does this test match?" but loses *why* it matched. This
design preserves request selection separately so a provider constraint cannot accidentally activate
an explicit test.

### Repository history and retained decisions

This design extends decisions that already shipped or have implementations on `main`; it does not
replace them:

- [RFC 018 - Native MTP integration](018-Native-MTP-Integration-For-MSTest.md) established
  `UnitTestElement` and the platform-services interfaces as the neutral engine boundary. Its native
  filter and context phases landed through
  [microsoft/testfx#9743](https://github.com/microsoft/testfx/pull/9743),
  [microsoft/testfx#9748](https://github.com/microsoft/testfx/pull/9748), and
  [microsoft/testfx#9755](https://github.com/microsoft/testfx/pull/9755). Explicit metadata stays
  neutral and gets one VSTest transport plus one native MTP transport; it does not restore the
  VSTest bridge on the MTP path.
- [RFC 020 - Composable test execution filter providers](020-Test-Execution-Filter-Providers.md),
  implemented by [microsoft/testfx#10235](https://github.com/microsoft/testfx/pull/10235), defines
  provider output as AND-composed constraints. This RFC retains that definition and adds only the
  provenance needed to prevent those constraints from becoming user activation.
- [RFC 022 - Test dependencies](022-Test-Dependencies.md), implemented by
  [microsoft/testfx#10260](https://github.com/microsoft/testfx/pull/10260), established
  `FinishTestThatDidNotRunAsync`, selected-test cleanup accounting, and scheduler-owned skipped
  outcomes. Explicit skips reuse those mechanics.
- [RFC 020 - Resource lock attribute](020-Resource-Lock-Attribute.md) established the current
  scheduling metadata model. Explicit declarations do not change resource locks, worker allocation,
  or parallelization after activation.
- [RFC 010 - Map not runnable tests to failed](010-MapNotRunnableToFailed-Attribute.md) is limited to
  malformed/non-runnable tests. Explicit is an intentional skipped state and is not mapped to failed.
- The assembly `[TestFilterProvider]` implementation for
  [microsoft/testfx#8894](https://github.com/microsoft/testfx/issues/8894) already performs a
  user-policy Drop/Skip gate before type and fixture initialization. The explicit gate follows that
  gate and reuses its cleanup path.

Relevant history also moved filtering onto neutral elements and reduced NativeAOT reflection in
[microsoft/testfx#9861](https://github.com/microsoft/testfx/pull/9861). The implementation must keep
that property: native MTP activation is evaluated from `UnitTestElement`, not by materializing a
VSTest `TestCase`.

### Goals

- Give classes, methods, inline data rows, dynamic data sources, and dynamic data rows explicit
  semantics.
- Discover explicit tests in both hosts and keep them visible after Run All.
- Define exactly which direct selections and filter expressions activate an explicit test.
- Keep selection, filtering, retry, result, and diagnostic behavior equivalent in VSTest and MTP.
- Suppress class- or method-explicit tests before assembly/class initialization and test-type loading.
- Preserve the existing fast path for assemblies with no explicit tests.
- Add no MTP public API solely for MSTest.

### Non-goals

- Replacing `[Ignore]`, condition attributes, categories, or ordinary filters.
- Allowing a selection to override `[Ignore]` or a failed condition.
- Defining a general platform-wide explicit-test contract for every framework.
- Changing whether data is folded or unfolded.
- Adding an IDE prompt before an explicit test runs.
- Running explicit tests from Run All by default.
- Guessing which Visual Studio command produced an ambiguous legacy VSTest invocation.

## Terminology

| Term | Definition |
| --- | --- |
| **Declaration** | `[Explicit]` or data-source/row metadata that marks a test explicit. |
| **Effective explicitness** | The OR of applicable class, method, data-source, and data-row declarations. |
| **Constraint filter** | A predicate that removes tests from consideration. Every request and provider filter is a constraint. |
| **Activation filter** | The positive, user/client-originated part of a request that proves intent to run matching explicit tests. |
| **Direct selection** | A host request containing concrete test cases, UIDs, or selected tree nodes. |
| **Run All** | A source-based request with no activating selector. The UI label is not authoritative; the request shape is. |
| **Folded data** | Data rows executed behind one discovered parent test, with no independently selectable row identity. |
| **Unfolded data** | Data rows discovered as individual tests with their own UIDs. |

## Public API

All APIs are in `Microsoft.VisualStudio.TestTools.UnitTesting`.

### Classes and methods

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExplicitAttribute : Attribute
{
    public ExplicitAttribute();
    public ExplicitAttribute(string? reason);

    public string? Reason { get; }
}
```

Examples:

```csharp
[TestClass]
[Explicit("Uses the shared staging subscription.")]
public class StagingMigrationTests
{
    [TestMethod]
    public void UpgradeFromPreviousVersion()
    {
    }
}

[TestClass]
public class HardwareTests
{
    [TestMethod]
    [Explicit]
    public void ResetsTheAttachedDevice()
    {
    }
}
```

`reason` is diagnostic text, not an identifier and not a filter expression. `null`, empty, and
whitespace values are accepted and behave like no supplied reason. The stable default skip message
is used in those cases.

`AllowMultiple = false` prevents contradictory reasons on the same declaration. `Inherited = false`
matches `[Ignore]` and prevents an override from becoming explicit without its author declaring it.
The inheritance details below distinguish an override from a base-declared test inherited by a
derived test class.

### Data sources and rows

```csharp
public interface ITestDataSourceExplicitCapability
{
    bool IsExplicit { get; set; }
    string? ExplicitReason { get; set; }
}
```

The standard data APIs implement the capability:

```csharp
public class DataRowAttribute : Attribute, ITestDataSource,
    ITestDataSourceIgnoreCapability, ITestDataSourceExplicitCapability
{
    public bool IsExplicit { get; set; }
    public string? ExplicitReason { get; set; }
}

public sealed class DynamicDataAttribute : Attribute, ITestDataSource,
    ITestDataSourceEmptyDataSourceExceptionInfo, ITestDataSourceIgnoreCapability,
    ITestDataSourceExplicitCapability
{
    public bool IsExplicit { get; set; }
    public string? ExplicitReason { get; set; }
}

[DataContract]
public sealed class TestDataRow<T> : ITestDataRow
{
    [DataMember]
    public bool IsExplicit { get; set; }

    [DataMember]
    public string? ExplicitReason { get; set; }
}
```

The declarations are independent:

```csharp
[TestMethod]
[DataRow(0)]
[DataRow(int.MaxValue, IsExplicit = true, ExplicitReason = "Takes several minutes.")]
public void RebuildIndex(int size)
{
}

[TestMethod]
[DynamicData(nameof(GetCases), IsExplicit = true,
    ExplicitReason = "All generated cases modify the staging tenant.")]
public void ProvisionTenant(TestCase testCase)
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

`IsExplicit` is the declaration. `ExplicitReason` by itself does not make a row explicit; when
`IsExplicit` is false the reason is ignored. This avoids a reason string silently changing execution
behavior. A future analyzer may report a reason without `IsExplicit`, but the runtime behavior is
fully defined without one.

A custom `ITestDataSource` can implement `ITestDataSourceExplicitCapability` to mark every row
produced by that source. A custom source can return `TestDataRow<T>` to mark individual rows. The
capability is additive to `ITestDataSourceIgnoreCapability`; neither replaces the other.

The two `TestDataRow<T>` properties are `[DataMember]`, and the internal `ITestDataRow` contract
exposes them alongside `IgnoreMessage`, `DisplayName`, and `TestCategories`. This preserves row
metadata through every existing unwrapping and serialization path.

The API additions are recorded in
`src/TestFramework/TestFramework/PublicAPI/PublicAPI.Unshipped.txt`. The target-specific API files do
not need entries because these types are part of the common surface.

## Effective declaration rules

Effective explicitness is monotonic: a narrower declaration cannot turn off a broader one.

```text
effectiveExplicit =
    class.IsExplicit
    || method.IsExplicit
    || dataSource.IsExplicit
    || dataRow.IsExplicit
```

When more than one applicable declaration has a non-empty reason, the most specific explicit
declaration supplies the reason:

1. data row;
2. data source;
3. method;
4. class;
5. the localized default message.

An explicit declaration with no reason does not erase a broader non-empty reason. For example, an
unreasoned explicit row under a method with `[Explicit("Requires staging.")]` uses
`"Requires staging."`.

### Class and method inheritance

| Declaration | Test being executed | Effective? | Reason |
| --- | --- | --- | --- |
| `[Explicit]` on a test class | Method declared on that class | yes | Class declaration applies to all its tests. |
| `[Explicit]` on a base test class | Method declared on a derived test class | no | Class attributes are not inherited. |
| `[Explicit]` on a base-declared test method | That same method discovered as a test of a derived class | yes | The declaration belongs to the method that is being executed. |
| `[Explicit]` on an overridden base method | Override has no `[Explicit]` | no | Method attributes are not inherited across overrides. |
| `[Explicit]` on an override | Override is executed | yes | The override declares it directly. |
| `[Explicit]` on a method supplied through a custom `TestMethodAttribute` | Method is executed | yes | Explicitness is independent of the test-method attribute subtype. |

### Precedence with other non-run states

Explicit activation only removes the explicit gate. It does not override any other reason not to
run a test. Some states are intentionally evaluated before the explicit gate and some require
loading the test type, so their diagnostic ordering is:

| State | Activated? | Result |
| --- | --- | --- |
| Scheduler dependency/cycle/cancellation outcome | either | Existing scheduler outcome; the runner is not entered. |
| Assembly `ITestFilter` returns Drop, Skip, or throws | either | Existing drop, custom skip, or error outcome. |
| Explicit only | no | Skipped: explicit test was not selected. |
| Explicit only | yes | Runs. |
| Explicit and `[Ignore]` or condition is false | no | Explicit skip; fixture attributes are not loaded/evaluated. |
| Explicit and `[Ignore]` or condition is false | yes | Existing ignore/condition skip reason. |
| Explicit and invalid test method | no | Explicit skip; method validity is not resolved. |
| Explicit and invalid test method | yes | Existing invalid-test behavior. |

The assembly-level programmatic `ITestFilter` is evaluated first because it is an execution policy:
Drop must still remove a test and Skip/error must keep their diagnostics. Its `Run` result means
"continue normal evaluation"; it never activates an explicit test. This is the same
provider-constraints-never-activate rule used at request composition.

`[Ignore]`, custom conditions, and method validity are evaluated only after type resolution today.
Evaluating them for an unactivated explicit test would defeat the pre-initialization gate and could
execute user condition code. They therefore win only after activation. Selection still never
overrides them: an activated ignored test remains ignored.

## Discovery

Explicit tests are always discovered. Discovery does not apply the activation rule because discovery
has no execution intent.

Each neutral `UnitTestElement` carries:

```text
IsExplicit: bool
ExplicitReason: string?
```

For an unfolded row these values are already the effective row values. For a folded parent they
contain only class/method declarations; source and row declarations are evaluated when the folded
data is enumerated.

### VSTest transport

`AdapterTestProperties` defines two adapter-owned `TestProperty` values:

```text
MSTestDiscoverer.Explicit        string
MSTestDiscoverer.ExplicitReason string
```

`UnitTestElementExtensions.ToTestCase` writes them and `TestCaseExtensions.ToUnitTestElement` reads
them. They must round-trip through discovery followed by selected execution, because selected VSTest
execution reconstructs the neutral element from the `TestCase`.

`Explicit` is registered as a filterable property with `TestMethodFilter` and has string values
`True` and `False`, case-insensitively. The VSTest property is deliberately registered as
`typeof(string)`, not `typeof(bool)`: MSTest must observe malformed persisted values so it can apply
the fail-closed compatibility rule rather than letting VSTest's property converter choose a default.
`ExplicitReason` is not filterable: reasons are prose and using them as identifiers would create a
compatibility burden.

### Native MTP transport

`MSTestTestNodeConverter` adds an MSTest-owned metadata property named `Explicit` with value `True`
to explicit test nodes, and an `ExplicitReason` property only when a non-empty reason exists. This
keeps the metadata visible to MTP filters and server clients without adding a platform contract.
The properties are not traits and therefore do not appear as user-authored `TestCategory` values.

Both properties are present on discovered explicit nodes and on result nodes. No skipped state is
attached during discovery; a test is not skipped until an execution request fails to activate it.

## Selection and activation

### The two-filter model

Every execution has:

1. a **constraint filter**, preserving the current answer to "which tests are in this request?"; and
2. an optional **activation filter**, answering "which tests did the user/client positively select?"

A non-explicit test runs when it matches the constraint filter. An explicit test runs when it
matches both filters. With no activation filter, explicit tests are reported skipped.

Provider and policy filters are composed only into the constraint filter. Request filters are
composed into the constraint filter and, where positive, into the activation filter. The executor
must receive the two values separately; it must not reconstruct activation from the already-composed
platform filter.

The neutral shape is conceptually:

```csharp
internal sealed record TestSelection(
    ITestElementFilter Constraint,
    ITestElementFilter? ExplicitActivation);
```

This is an internal adapter/platform-services concept, not a public API. Assemblies with no explicit
elements use the existing filter path without evaluating `ExplicitActivation`.

### Direct selection

Concrete selections activate the concrete tests they contain:

| Request | Activation |
| --- | --- |
| VSTest `RunTests(IEnumerable<string> sources, ...)` | none; this is Run All unless `TestCaseFilter` contributes a positive activation. |
| VSTest `RunTests(IEnumerable<TestCase> tests, ...)` | exactly the supplied test cases. |
| MTP `--filter-uid <uid>` | the matching UID. |
| MTP `--treenode-filter <expression>` | nodes matched through a discriminating segment; see [tree-node and graph filters](#tree-node-and-graph-filters). |
| MTP server request containing test-node UIDs | exactly those UIDs. |
| MTP server graph filter | the same grammar and the same rule as `--treenode-filter`. |
| Empty MTP UID/node list | no activation and no tests; it is not Run All. |

Selecting a class, fixture, or namespace node activates all descendant explicit tests selected by
that node. Selecting one unfolded data row activates only that row. Selecting a folded parent
activates all of its rows because folded rows have no separate discovery identity.

VSTest's concrete-test overload rejects an empty `IEnumerable<TestCase>` through its existing
`Ensure.NotEmpty` guard. VSTest does not use an empty list to express either Run All or an empty
selection, and this feature does not relax that host contract.

### Expression filters

VSTest `TestCaseFilter`, MSTest `--filter`, and equivalent MTP property filters use the same
activation rules. A matching expression activates an explicit test only when a **positive leaf**
participates in a true path.

Positive leaves:

```text
Property = value
Property ~ value
bare value          (the host's ordinary FullyQualifiedName contains form)
```

Exclusion leaves:

```text
Property != value
Property !~ value
```

Evaluation returns two values, `(matches, activates)`:

| Expression | `matches` | `activates` |
| --- | --- | --- |
| Positive leaf | Existing leaf result | Same as `matches`. |
| Exclusion leaf | Existing leaf result | Always `false`. |
| `A & B` | `A.matches && B.matches` | `matches` and either child activates. |
| `A \| B` | `A.matches \|\| B.matches` | `(A.matches && A.activates) \|\| (B.matches && B.activates)`. |
| Parentheses | Inner result | Inner result. |

Consequences:

| Filter | Explicit `Fast` unit test | Activated? |
| --- | --- | --- |
| no filter | matches broad run | no |
| `TestCategory!=Integration` | matches | no |
| `FullyQualifiedName!~Slow` | matches | no |
| `TestCategory=Fast` | matches | yes |
| `TestCategory=Fast & TestCategory!=Windows` | matches | yes |
| `TestCategory!=Integration \| TestCategory=Fast` | matches both branches | yes, through the positive branch |
| `TestCategory=Other \| TestCategory!=Integration` | matches only exclusion branch | no |
| `Explicit=True` | matches | yes |
| `Explicit=False` | does not match | no |
| `Explicit!=False` | matches | no |

The positive leaf need not uniquely identify one test. `TestCategory=Hardware` intentionally
activates every explicit test in that category. This is useful for opt-in CI jobs and follows the
meaning of a positive user filter.

Malformed filters retain existing parse-error behavior and never fall back to Run All.

#### Obtaining the expression tree

The VSTest `ITestCaseFilterExpression` API exposes only `MatchTestCase`; it does not expose the
parsed AND/OR tree needed for this activation algebra. `TestMethodFilter` therefore cannot derive
activation from the opaque evaluator.

MSTest adds an internal `ExplicitActivationFilterExpression` parser/evaluator in the adapter layer.
It consumes `ITestCaseFilterExpression.TestCaseFilterValue` on VSTest and the original `--filter` /
RunSettings strings on native MTP. It implements the same documented VSTest grammar, escaping,
operator precedence, bare-value expansion, and case-insensitive property-name behavior, but returns
`(matches, activates)`. The existing VSTest expression remains authoritative for the constraint
result; the new evaluator supplies only activation.

This duplication is contained and tested rather than hidden:

- shared differential vectors assert that VSTest's evaluator and the new evaluator return the same
  `matches` result for every supported expression;
- fuzzed combinations of escaped values, operators, and parentheses compare the two evaluators;
- the VSTest package version is pinned with the repository dependencies, so a grammar update changes
  the compatibility tests in the same pull request;
- if VSTest accepts a filter the activation parser cannot parse, the run reports a filter error and
  does not execute tests. It never degrades to "any positive token activates", which could run a
  destructive explicit test through the wrong OR branch.

An upstream VSTest API that exposes a tree-walkable expression can replace this parser later without
changing the semantics in this RFC.

### Tree-node and graph filters

`--treenode-filter` and the MTP server graph filter are one language, not two: `ServerTestHost`
builds a `TreeNodeFilter` from the request's `GraphFilter` string, so one rule covers both rows of
the direct-selection table.

That language is not the expression language above and its leaves cannot be classified with the
expression table. A tree-node filter is a `/`-separated path of segment expressions:

```text
TREE_NODE_FILTER = EXPR ( '/' EXPR )*
EXPR             = '(' EXPR ')' | EXPR OP EXPR | NODE_VALUE
FILTER_EXPR      = '(' FILTER_EXPR ')' | TOKEN '=' TOKEN | TOKEN '!=' TOKEN
                 | FILTER_EXPR OP FILTER_EXPR | TOKEN
OP               = '&' | '|'
NODE_VALUE       = TOKEN | TOKEN '[' FILTER_EXPR ']'
```

`(!EXPR)` negates a segment, `*` matches within one segment, and a trailing `**` matches everything
below. A tree-node filter can therefore match a node without naming anything: `/**` is Run All
written as a filter, and `/*/*/*/*[Category!=Slow]` is a pure exclusion. Reading "matched" as
"selected" would let either of those start a destructive explicit test, which is the outcome the
constraint/activation split exists to prevent.

Segments produce the same `(matches, activates)` pair as expression filters. `matches` keeps its
current meaning and its current results; only `activates` is new.

| Segment expression | `activates` |
| --- | --- |
| Token with at least one literal character (`MyTest`, `My*Test`) | Same as `matches`. |
| Wildcard-only token (`*`, `**`) or an empty segment | Always `false`. |
| `[Name=Value]` where the value has a literal character | Same as `matches`. |
| `[Name=*]` | Always `false`. |
| `[Name!=Value]` | Always `false`. |
| `(!EXPR)` | Always `false`. |
| `A & B` | `matches` and either child activates. |
| `A \| B` | `(A.matches && A.activates) \|\| (B.matches && B.activates)`. |
| `Token[FILTER_EXPR]` | `matches` and either the token or the property expression activates. |

A segment whose `activates` is true is **discriminating**: it names something rather than accepting
everything or rejecting something. A node is activated when it matches the filter and at least one
**non-root** segment on its path is discriminating.

Both halves of that rule carry weight:

- *At least one segment*, because a path is a conjunction of segments and the `A & B` row above
  already says a conjunction activates when one side names something. `/*/*/MyClass/(!Slow)` selects
  a class and then narrows it, so the class segment activates its descendants, which is the same
  answer as the existing rule that selecting a class or namespace node activates the explicit tests
  under it.
- *Non-root*, because the root segment names the test project rather than a test in it. In a
  single-assembly host `/MyAssembly/**` and `/**` select exactly the same tests, so they must
  activate the same way; otherwise naming the assembly is a silent step from a safe Run All to
  running every destructive test in it. A user who wants that run has `/*/*/*/*[Explicit=True]` or
  `ExplicitTestMode=Run`.

| Filter | Explicit test it matches | Activated? |
| --- | --- | --- |
| `/**` | matches | no; this is Run All written as a filter |
| `/MyAssembly/**` | matches | no; the root names the project, not tests |
| `/*/*/*/*` | matches | no |
| `/*/*/*/MyTest` | matches | yes |
| `/*/*/MyClass/*` | matches | yes, through the class segment |
| `/*/MyNamespace/**` | matches | yes, through the namespace segment |
| `/*/*/*/(!Slow)` | matches | no |
| `/*/*/*/*[Category!=Slow]` | matches | no |
| `/*/*/*/*[Category=Hardware]` | matches | yes |
| `/*/*/MyClass/(!Slow)` | matches | yes, through `MyClass` |
| `/*/*/*/(MyTest\|(!Slow))` | matches through either branch | yes for `MyTest`; no for a node matched only by `(!Slow)` |
| `/*/*/*/(MyTest&(!Slow))` | matches | yes |
| `/*/*/*/*[Explicit=True]` | matches | yes |
| `/*/*/*/*[Explicit!=False]` | matches | no |

#### Evaluating and enforcing tree activation

The VSTest side needs a second parser because `ITestCaseFilterExpression` is an opaque evaluator
owned by another repository. That reasoning does not carry over here. `TreeNodeFilter` lives in this
repository and `Microsoft.Testing.Platform` already grants `InternalsVisibleTo` to
`MSTest.TestAdapter`, so the activation result is produced by `TreeNodeFilter` itself, through an
internal match overload that also reports whether the match was discriminating, and reaches the
adapter through the same friend-assembly surface as the rest of the provenance work. There is no
second grammar to keep in sync and no new public MTP API.

The tables above describe how tree-node and graph selection must behave once MSTest accepts such a
filter, not what today's build does. `MSTestFilterContext` and the VSTest bridge's
`ContextAdapterBase` currently throw `UnsupportedTestExecutionFilter` for every leaf filter other
than `NopFilter` and `TestNodeUidListFilter`, so neither `--treenode-filter` nor a graph filter
reaches MSTest at all.

That gap sets the general rule for every request shape not covered above: **activation is proven,
never assumed**. When activation cannot be determined, the request constrains the run and activates
nothing, so explicit tests under it are reported skipped. That covers an unsupported filter type, a
shape the activation evaluator cannot classify, and any future grammar addition. It is the same
fail-closed direction as malformed expressions and malformed persisted metadata, and it means a new
filter feature cannot silently start running destructive tests before its activation semantics are
designed.

### Provider filters

Filters returned by framework `ITestFilterProvider` implementations or MTP extension providers are
constraints only, even when their syntax contains a positive leaf. They express repository or
extension policy, not user intent.

For example, a provider that includes only `TestCategory=CanRunOnThisMachine` may remove tests, but
it cannot activate an explicit test. If the request itself selects `TestCategory=Hardware`, the
explicit test runs only when it satisfies both the provider constraint and request activation.

This requires retaining provenance in `MSTestFilterContext` and
`TestExecutionFilterComposer`: request filters and provider filters remain separately available
after their ordinary constraint composition.

### Visual Studio and the VSTest intent boundary

VSTest exposes two execution entry points to adapters:

- source execution means broad execution;
- test-case execution means the host selected those test cases.

That invocation shape is the normative contract. The MSTest adapter cannot reliably infer the
Visual Studio command name from a list of test cases, and it must not guess based on list size,
whether all discovered tests appear in the list, or timing. Those heuristics race discovery,
break filtered runs, and make behavior depend on adapter cache state.

Therefore:

- a Visual Studio Run All flow that invokes the source overload skips explicit tests;
- Run Selected Tests, Run Tests in Context, a class/namespace selection, and rerunning an individual
  result invoke selected execution and activate the supplied explicit tests;
- if a legacy Visual Studio/VSTest version implements a broad UI command by supplying concrete
  `TestCase` objects, MSTest treats those objects as selected and explicit tests run.

The last case is an unavoidable limitation of the old contract, not an unresolved MSTest rule.
Exact UI-intent parity requires VSTest/Visual Studio to provide a selection-origin signal in the run
context. If such a signal is added, MSTest uses it in preference to the overload fallback. Until
then, diagnostic logging records whether execution was classified as `SourceRun`,
`SelectedTestCases`, or `PositiveFilter` so reports can identify the host behavior.

No product-facing warning is printed for the legacy ambiguity; most selected runs are intentional
and warning on every one would be noise.

## Execution lifecycle

### Class- and method-level explicit tests

The explicit gate runs in `UnitTestRunner.RunSingleTestAsync` before:

- loading the test type;
- assembly initialization;
- class initialization;
- test-class construction;
- `TestInitialize`;
- the test body.

The existing assembly `[TestFilterProvider]` gate runs immediately before it. Drop, Skip, and filter
errors keep their existing outcome; `TestFilterResult.Run` continues to the explicit gate and does
not activate it. The filter provider assembly may be loaded, but the explicit test type and its
fixture lifecycle are not.

When not activated, the runner calls the same `FinishTestThatDidNotRunAsync` bookkeeping path used
by other selected-but-not-run outcomes. This is required so class test counts reach zero and
`ClassCleanup`/`AssemblyCleanup` are neither lost nor run early.

An assembly containing only unactivated class/method-explicit tests performs no user initialization
or cleanup. An assembly containing ordinary tests initializes and cleans up for those ordinary
tests as usual.

### Unfolded data

`AssemblyEnumerator.TryUnfoldITestDataSource` combines class, method, source, and row declarations
into each unfolded `UnitTestElement`. Each row then follows the ordinary class/method path:

- Run All reports an explicit row skipped before fixture initialization attributable to that row;
- direct row selection activates only that row;
- parent class/method selection activates every selected child row;
- an ordinary sibling row continues to run during Run All.

Existing `ITestDataSourceIgnoreCapability` and row `IgnoreMessage` processing has precedence over
the explicit state.

### Folded data

A folded parent has only one discoverable identity. Its behavior is:

1. class/method explicitness is checked before initialization and data enumeration;
2. if the parent is allowed to proceed, the existing folded-data path enumerates its sources;
3. each source/row explicit declaration is checked before `TestInitialize`, test-class construction
   where construction is per row, and the test body for that row;
4. an unactivated explicit row produces its own skipped `UnitTestResult`;
5. an activated folded parent activates every row under it.

Data enumeration and any assembly/class initialization required to reach folded rows can therefore
occur even when every produced row is explicit. That is inherent in folded discovery: row metadata
does not exist until the source runs. The RFC does not silently force unfolding because some data
cannot be serialized or enumerated safely at discovery time.

The implementation must nevertheless check before per-row `TestInitialize` and the test body, so an
unactivated explicit row cannot perform the test's operation.

If a data source throws while producing explicit-row metadata, existing data-source failure behavior
wins; the adapter cannot know that the unavailable row would have been explicit.

### Parallelism and cleanup

Explicit tests retain existing parallelization metadata. Unactivated tests consume no worker time
beyond producing their skipped result. Activated tests enter the same class/method-level scheduling
and resource-lock paths as ordinary tests.

Skipped explicit tests still decrement `ClassCleanupManager` counts. A class whose only selected
tests are unactivated explicit tests does not initialize and therefore does not clean up. A class
that initialized for another test cleans up after all selected tests, including explicit skips, have
reported their outcomes.

## Results and diagnostics

Run All reports one skipped result for every discovered explicit test or unfolded row. Folded rows
produce results as they are enumerated, matching existing folded-data result cardinality.

The localized default message is:

```text
The test is explicit and was not selected.
```

When a reason is supplied, the result message is:

```text
The test is explicit and was not selected. Reason: <reason>
```

The reason is copied verbatim after trimming only the decision whether it is empty; its content is
not interpreted. It appears in:

- the VSTest `TestResult.ErrorMessage`/skip-reason surface used by Test Explorer and TRX;
- the native MTP skipped result node;
- console output when the reporter ordinarily prints skipped reasons;
- diagnostic logs.

It is not written as an error, warning, standard output, or test-context message. Run summaries
count it as skipped/not executed according to the host's existing mapping.

`MapNotRunnableToFailed` does **not** turn an unactivated explicit test into a failure. Explicit is a
first-class skipped outcome, not a malformed or non-runnable test. Existing settings that suppress
or display skipped tests affect presentation only.

At diagnostic trace level, each explicit decision records:

```text
test UID
effective declaration scope (class/method/source/row)
selection classification
activated true/false
winning reason scope
```

The log must not include data values beyond what the test UID/display name already contains, and it
must not emit one normal-console message per skipped explicit test.

## Retry behavior

Retries never create activation.

### MSTest `[Retry]`

The explicit gate precedes framework retry orchestration:

- an unactivated explicit test produces one skipped result and zero attempts;
- an activated explicit test runs and uses `[Retry]` exactly like an ordinary test;
- if all attempts fail, existing aggregate retry diagnostics are unchanged;
- explicit data rows are retried independently according to the existing unfolded/folded behavior.

### MTP process retry extension

The process retry extension builds the next request from UIDs of tests that actually ran and failed.
Consequently:

- explicit tests skipped by Run All never enter the retry UID set;
- an explicitly activated test that failed is selected by UID on retry and remains activated;
- narrowing the retry request must not activate another explicit test that was absent from the
  failed UID set;
- provider constraints continue to constrain the retry request without becoming activation.

The same rule applies to a host's "rerun failed tests": only the concrete failed test cases supplied
by the host are activated.

## Configuration

The initial release has one optional adapter setting for environments that require a deterministic
safety override:

```xml
<MSTest>
  <ExplicitTestMode>RequireSelection</ExplicitTestMode>
</MSTest>
```

Equivalent `testconfig.json`:

```json
{
  "mstest": {
    "execution": {
      "explicitTestMode": "requireSelection"
    }
  }
}
```

Values:

| Value | Behavior |
| --- | --- |
| `RequireSelection` | Default. Uses the activation rules in this RFC. |
| `Skip` | Never activates explicit tests, even when directly selected. Useful for protected CI environments. |
| `Run` | Treats all matching explicit tests as activated, including Run All. Intended only for an opt-in job dedicated to explicit tests. |

Unknown values fail settings parsing; they do not fall back to `RequireSelection`. Specifically,
both XML and configuration parsers throw `AdapterSettingsException`, matching the existing
hard-failure path for invalid `ParallelWorkers` and `ExecutionScope`, rather than the
warn-and-default behavior of presentation settings. This is a safety choice: a misspelled `Skip`
policy must not permit a directly selected destructive test to run. RunSettings and
`testconfig.json` precedence follows the existing MSTest settings precedence. The setting changes
only the explicit gate and never overrides filters, ignore, conditions, dependencies, or
cancellation.

`Run` is deliberately explicit configuration rather than a CLI shortcut. A CI definition can use
`Explicit=True` for the safer, filter-visible behavior; `Run` exists for hosts whose selection
contract is too old to express intent reliably.

## Compatibility

### Source and binary compatibility

The APIs are additive. Existing tests have no explicit declarations and retain exactly their current
discovery, filtering, initialization, execution, retry, and result behavior. `ExplicitAttribute`
does not change `[Ignore]` or condition APIs.

The data capability is optional. Existing custom data sources compile and behave as before.
Adding properties to `DataRowAttribute`, `DynamicDataAttribute`, and `TestDataRow<T>` is binary
compatible.

### Adapter/framework versions

The feature requires an adapter that understands the metadata:

- a new adapter with an older framework sees no declarations and behaves as today;
- a new framework with an old adapter can load `[Explicit]`, but that adapter does not recognize it
  and may run the test during Run All;
- the normal MSTest package version-alignment checks remain the supported deployment model.

Release notes must call out the old-adapter risk. The design does not make the attribute inherit
from `[Ignore]` as a compatibility workaround, because that would make direct selection impossible
on every adapter.

### Persisted and remote test cases

The VSTest property names are stable wire identifiers. Missing `Explicit` means false; missing reason
means no reason. Unknown future values fail closed for execution: a value that cannot be parsed as a
Boolean is treated as explicit and logged, preventing an old/corrupt cache from turning an opt-in
test into a broad-run test.

Native MTP UIDs do not change when explicit metadata is added. Server clients that ignore the
metadata still receive ordinary nodes and skipped results.

### NativeAOT and source-generated reflection

`ExplicitAttribute` is read through the existing reflection abstraction used for other test
attributes. Source-generated reflection metadata must root and reproduce it for classes and methods.
The generated and reflection discovery paths must produce byte-for-byte equivalent neutral explicit
metadata. Data source capability properties are read when the source is available and require no
new reflection contract.

## Implementation surfaces

| Concern | Primary locations | Required change |
| --- | --- | --- |
| Public attribute | `src/TestFramework/TestFramework/Attributes/TestMethod/ExplicitAttribute.cs` | Add the sealed class/method attribute. |
| Data APIs | `DataRowAttribute.cs`, `DynamicDataAttribute.cs`, `TestDataRow.cs`, `ITestDataRow.cs`, new `ITestDataSourceExplicitCapability.cs` | Add source/row declaration, reason, and serialized internal row contract. |
| Public API tracking | `src/TestFramework/TestFramework/PublicAPI/PublicAPI.Unshipped.txt` | Record all public members. |
| Neutral metadata | `ObjectModel/UnitTestElement.cs`, `ObjectModel/TestMethod.cs` if needed | Carry effective class/method/source/row state without host types. |
| Class/method discovery | `Discovery/TypeEnumerator.cs` | Read class and method declarations and apply inheritance rules. |
| Data unfolding | `Discovery/AssemblyEnumerator.cs` | Merge source/row declarations with ignore metadata. |
| Folded data | `Execution/TestMethodRunner.DataRow.cs` | Evaluate source/row explicitness before per-row initialization/body. |
| Selection contract | `MSTestEngine.cs`, `TestExecutionManager.cs`, `ITestElementFilter.cs` area | Carry constraint and activation separately. |
| Pre-initialization gate | `Execution/UnitTestRunner.RunSingleTest.cs` | Produce skip before type/fixture initialization and use non-run cleanup bookkeeping. |
| VSTest request origin | `VSTestAdapter/MSTestExecutor.cs` | Classify source versus test-case execution. |
| VSTest metadata | `AdapterTestProperties.cs`, `UnitTestElementExtensions.cs`, `TestCaseExtensions.cs` | Register and round-trip explicit properties. |
| VSTest expressions | `TestMethodFilter.cs`, new adapter-layer `ExplicitActivationFilterExpression.cs` | Parse the original expression, evaluate `(matches, activates)`, differential-test against VSTest, and expose `Explicit`. |
| MTP tree/graph expressions | `Requests/TreeNodeFilter/TreeNodeFilter.Matching.cs`, `MSTestFilterContext.cs` | Report whether a match was discriminating through an internal overload, and accept tree-node/graph filters instead of throwing `UnsupportedTestExecutionFilter`. |
| Native MTP request origin | `TestingPlatformAdapter/MSTestTestFramework.cs`, `MSTestFilterContext.cs`, `MtpTestElementFilter.cs` | Build activation from UID/tree/property request filters. |
| Native MTP metadata/results | `MSTestTestNodeConverter.cs`, `MtpTestResultRecorder.cs` | Add discovery metadata and skipped reasons. |
| Platform filter provenance | `TestExecutionFilterComposer.cs`, `TestExecutionRequest.cs`, `RunTestExecutionRequest.cs`, `ConsoleTestExecutionRequestFactory.cs`, `ServerTestExecutionRequestFactory.cs`, server request mapping | Preserve the original request selection separately from the provider-constrained effective filter through an internal/friend surface, not new public MTP API. |
| Server selections | `ServerTestHost.RequestExecution.cs` | Classify node-list selections as activation and graph filters through the tree-node rule. |
| Settings | `MSTestSettings.cs`, `.RunSettingsXml.cs`, `.Configuration.cs` | Parse the three explicit modes with existing precedence. |
| Localization | Adapter/platform-services `.resx` resources and generated accessors | Add default skip and invalid-setting messages; regenerate XLF, never edit XLF manually. |
| Source generation | `TestFramework.SourceGeneration`, `SourceGeneration/ReflectionMetadataHook.cs`, `SourceGeneratedReflectionOperations` tests | Ensure explicit attributes and capability-bearing data types are rooted; attribute reading remains on the existing reflection abstraction. |
| Retry | Framework retry and MTP retry tests; no expected production redesign | Prove skipped UIDs never enter retry and selected failed UIDs stay activated. |
| Documentation | MSTest attribute/reference docs and release notes | Explain Run All, direct selection, filter, data, and old-adapter behavior. |

The platform change exposes provenance to the MSTest integration without defining
framework-specific semantics. `TestExecutionFilterComposer` returns both the original request filter
and the final provider-constrained filter. Console and server request factories retain both on an
internal request property accessible to the in-repository MSTest adapter through the existing friend
assembly mechanism; the public `TestExecutionRequest.Filter` remains the effective constraint.
MSTest interprets the original filter as activation, while other frameworks continue to read only
`Filter`. This adds no public MTP API.

## Testing plan

### Framework API unit tests

Add tests in `TestFramework.UnitTests` for:

- both `ExplicitAttribute` constructors, null/empty/whitespace reasons, target usage, non-inheritance,
  and single-use behavior;
- default and assigned `IsExplicit`/`ExplicitReason` on `DataRowAttribute`,
  `DynamicDataAttribute`, and `TestDataRow<T>`;
- standard data sources implementing `ITestDataSourceExplicitCapability`;
- reason alone not changing `IsExplicit`;
- Public API baselines.

Add analyzer coverage only if approval includes an analyzer for reason-without-explicit. No analyzer
is required for the runtime feature.

### Adapter/platform-services unit tests

Add focused tests for:

- class, method, base-declared method, override, and custom `TestMethodAttribute` discovery;
- effective reason precedence and non-erasure by an empty narrower reason;
- dependency/cancellation precedence and activated versus unactivated ignore/condition diagnostics;
- assembly `ITestFilter` Drop/Skip/error/Run ordering, proving that Run does not activate;
- unfolded ordinary and explicit sibling rows;
- source-wide and row-specific declarations from built-in and custom data sources;
- folded source/row skips before per-row initialization;
- no type load, assembly/class initialization, construction, `TestInitialize`, body, or retry for an
  unactivated class/method explicit test;
- cleanup count decrement for explicit skips, and no cleanup when initialization never occurred;
- VSTest `TestCase` round-trip, missing properties, and malformed Boolean fail-closed behavior;
- native MTP metadata/result conversion;
- source-generated and reflection discovery parity.

### Filter truth-table unit tests

`TestMethodFilterTests`, `MSTestFilterContextTests`, `MtpTestElementFilter` tests, and
`TestExecutionFilterComposerTests` cover every row of the expression table plus:

- nested AND/OR expressions and parentheses;
- VSTest/new-parser differential and fuzz vectors, including escaping and bare values;
- one true exclusion branch plus one false positive branch;
- one true positive branch plus one false exclusion branch;
- bare-name filters;
- case-insensitive Boolean `Explicit` values;
- request positive filter plus provider positive constraint;
- provider-only positive constraint;
- source Run All, concrete `TestCase`, UID, tree, server node list, and server graph origins;
- tree-node/graph segment vectors: wildcard-only (`/**`, `/*/*/*/*`), root-only (`/MyAssembly/**`),
  literal method, class, and namespace segments, negated segment `(!X)`, `[Name!=Value]`,
  `[Name=Value]`, `[Name=*]`, and `&`/`|` branches where only one branch is positive;
- a request shape whose activation cannot be classified activating nothing;
- empty MTP direct selections and the unchanged VSTest empty-list exception;
- malformed expressions;
- `RequireSelection`, `Skip`, and `Run`.

The same serialized expression vectors are used by VSTest and native MTP tests so their semantics
cannot drift.

### VSTest acceptance tests

Add an acceptance asset containing:

- an ordinary test;
- explicit method with and without a reason;
- explicit class;
- base-declared explicit method and non-explicit override;
- ignored explicit test;
- conditional explicit test;
- ordinary and explicit `DataRow` siblings;
- source-wide and row-specific dynamic data;
- an explicit test that fails before passing under `[Retry]`;
- fixture counters written to output/files so initialization can be asserted.

Run it through the VSTest host and verify:

1. discovery returns every test, explicit properties, reasons, and stable IDs;
2. source Run All passes ordinary tests and reports explicit tests skipped with exact reasons;
3. Run All does not execute explicit bodies or retries;
4. selected `TestCase` execution runs the selected explicit method/class/row;
5. selecting a folded parent runs all its rows;
6. `TestCategory=...`, `FullyQualifiedName~...`, and `Explicit=True` activate matching tests;
7. `!=`/`!~`-only filters do not activate them;
8. mixed AND/OR filters follow the truth table;
9. RunSettings `TestCaseFilter` behaves identically to the command-line filter;
10. platform provider filters and assembly `ITestFilter` Run constrain/continue but never activate;
11. `[Ignore]` and false conditions still win when selected;
12. selected explicit failures use framework retry;
13. TRX records skipped outcome and custom/default reasons;
14. `ExplicitTestMode` has all three behaviors and invalid values fail;
15. class/assembly cleanup counts remain correct;
16. reflection and source-generated test assets agree where supported.

The acceptance test should invoke the source and test-case executor entry points independently rather
than assuming a particular installed Visual Studio version. A separate manual/IDE validation records
which invocation shape current supported Visual Studio versions use for Run All, Run Selected, class
selection, and rerun failed.

### Native MTP acceptance tests

Run the same asset through the native MTP host and verify:

1. no-selector Run All skips explicit tests;
2. `--filter-uid` runs only the selected explicit UID;
3. `--treenode-filter` activates selected methods, classes, namespaces, and unfolded rows, and does
   not activate through `/**`, a root-only path, a negated segment, or a `!=` property predicate;
4. MSTest `--filter` positive/exclusion/mixed expressions match VSTest;
5. server UID lists activate only the listed nodes, and graph filters follow the `--treenode-filter`
   rule;
6. empty server selection runs nothing;
7. provider filters constrain without activation;
8. discovery and result nodes carry explicit metadata and reasons;
9. folded/unfolded data behavior matches VSTest;
10. framework `[Retry]` behavior matches VSTest;
11. process retry does not retry Run All skips and does retry a selected failed UID;
12. `testconfig.json` modes match RunSettings modes;
13. console and TRX reporters render exact skip reasons;
14. fixture initialization and cleanup assertions match VSTest;
15. NativeAOT/source-generated discovery and execution preserve explicit metadata.

### Compatibility tests

- Run an assembly with no explicit declarations and compare discovery/result snapshots before and
  after the feature.
- Run a new-framework explicit asset with the previous adapter and document the expected unsupported
  behavior in release tests.
- Run an old-framework asset with the new adapter.
- Deserialize a persisted VSTest case with no explicit properties and one with malformed metadata.
- Verify MTP UIDs do not change when `[Explicit]` is added.

## Rollout

1. Approve the API and selection semantics in this RFC.
2. Add framework API and neutral metadata behind no feature flag; no declaration means no behavior
   change.
3. Implement VSTest and native MTP paths together. The feature does not ship with only one host.
4. Land the shared expression vectors and both acceptance suites before marking implementation
   complete.
5. Validate supported Visual Studio versions and file an upstream VSTest/Visual Studio issue if a
   broad UI command still arrives as selected test cases.
6. Publish documentation and old-adapter compatibility warning with the release.

## Resolved design questions

| Question | Decision |
| --- | --- |
| Is explicit a condition? | No; it depends on request intent, not environment state. |
| Are explicit tests discovered? | Always. |
| Does Run All omit or skip them? | Report skipped, preserving visibility and diagnostics. |
| Can direct selection override ignore/conditions? | No. It removes only the explicit gate. |
| What activates an explicit test? | Concrete selection or a matching positive request-filter branch. |
| Does an exclusion filter activate? | Never. |
| Does a wildcard-only or root-only tree/graph filter activate? | No; `/**` and `/MyAssembly/**` are Run All written as filters. |
| What happens when activation cannot be determined? | Nothing is activated. The request constrains only, and explicit tests under it are reported skipped. |
| Can a provider/policy filter activate? | Never, including assembly `ITestFilter` returning Run. |
| Does selecting a class/namespace activate descendants? | Yes. |
| Does selecting a folded parent activate its rows? | Yes; rows have no independent identity. |
| Can one row opt out of an explicit method/source? | No; declarations OR-compose. |
| Which reason wins? | Most specific non-empty explicit declaration. |
| When is the gate evaluated? | After assembly `ITestFilter`, before fixture initialization for class/method/unfolded rows; before per-row initialization/body for folded row metadata. |
| Are skipped explicit tests retried? | No. Selected explicit failures retry normally. |
| How is VSTest Run All identified? | By source execution. Test-case execution is selected execution. |
| What if a legacy host sends all test cases for Run All? | They are selected by contract and run; diagnostic classification exposes the limitation. |
| Is there a safety override? | `ExplicitTestMode=Skip`; `Run` provides the inverse opt-in override. |
| Must both hosts ship together? | Yes. |

There are no remaining behavioral decisions required before implementation. Approval is needed for
the public API, the positive-filter activation model, the discriminating-segment rule for tree-node
and graph filters, the three-value configuration override, and the documented legacy VSTest
boundary.
