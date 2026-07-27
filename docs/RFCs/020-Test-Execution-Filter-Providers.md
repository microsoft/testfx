# RFC 020 - Composable test execution filter providers

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

Microsoft.Testing.Platform (MTP) currently creates one request filter from the command line or
JSON-RPC payload and passes that filter to the test framework. Extensions cannot add an independent
constraint without replacing or reimplementing the platform's built-in filter creation.

This RFC introduces an experimental, multi-registration `ITestExecutionFilterProvider` API.
For console requests, MTP creates its built-in filter first, asks every enabled provider for zero or
one additional constraint, and combines all constraints with explicit logical **AND** semantics.
The platform normalizes UID constraints by intersecting them and represents remaining conjunctions
with `CompositeTestExecutionFilter`.

Providers produce constraints for one request. They do not own discovery/execution orchestration,
batching, sharding, retry scheduling, or any other multi-run plan.

## Motivation

The open filtering issues describe several related but distinct needs:

- [#3590](https://github.com/microsoft/testfx/issues/3590) asks for public filter extensibility.
  This RFC delivers the composable extension point but not custom filter implementations, so the
  issue stays open.
- [#3530](https://github.com/microsoft/testfx/issues/3530) asks for an aggregate filter.
- [#3528](https://github.com/microsoft/testfx/issues/3528) proposes index-based batching.
- [#4068](https://github.com/microsoft/testfx/issues/4068) asks for test sharding.
- [#4293](https://github.com/microsoft/testfx/issues/4293) shows that a platform-known filter can be
  recognized by MTP but unsupported by an adapter.
- [#7160](https://github.com/microsoft/testfx/issues/7160) describes the lack of a coherent,
  cross-framework filtering story.

Previous implementations in [PR #4200](https://github.com/microsoft/testfx/pull/4200) and
[PR #6677](https://github.com/microsoft/testfx/pull/6677) explored self-evaluating custom filters
and whole-filter factories. They exposed too much responsibility at once:

- A custom filter type can reach a framework that has no way to interpret it.
- A whole-filter factory replaces the built-in CLI filter rather than composing with it.
- Selecting one winning factory makes extension registration order or activation conflicts part of
  filter semantics.
- Letting filters own test-suite-wide planning conflates a per-request constraint with orchestration.

The platform needs a smaller abstraction that composes independently produced constraints without
silently losing any of them.

## Scope

This RFC covers:

- provider registration and lifecycle;
- request kind and request origin;
- platform-known filter representations;
- logical AND composition;
- UID-list intersection;
- unsupported-filter diagnostics;
- console and JSON-RPC boundaries;
- native MSTest and VSTestBridge translation responsibilities; and
- migration from the existing internal single-factory model.

This RFC does not define:

- a new command-line option or filter grammar;
- public custom `ITestExecutionFilter` implementations or the capability negotiation they need
  ([#3590](https://github.com/microsoft/testfx/issues/3590) stays open);
- affected-test terminology or source-to-test mapping;
- OR/NOT composition across provider contributions;
- a framework-neutral predicate language;
- discovery-before-run planning;
- sharding, batching, retry scheduling, or multi-process coordination; or
- public orchestration APIs.

Those planning concerns remain with an orchestrator. An orchestrator can discover tests, partition
the resulting UIDs, and launch one or more requests whose UID filters are ordinary constraints.

## Terminology

| Term | Meaning |
| --- | --- |
| Request filter | The filter MTP creates from the current CLI or JSON-RPC request. |
| Provider | An enabled `ITestExecutionFilterProvider` extension that may contribute one constraint. |
| Constraint | A filter that narrows the tests eligible for the current request. |
| Composition | Combining the request filter and provider constraints into one filter. |
| Request kind | Whether the current request performs discovery or execution. |
| Request origin | Whether the request came from the console path or the JSON-RPC server path. |
| Platform-known filter | A filter representation whose semantics MTP defines: `NopFilter`, `TestNodeUidListFilter`, `TreeNodeFilter`, or an AND `CompositeTestExecutionFilter` containing platform-known filters. |

## Public API

The initial experimental API is:

```csharp
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestExecutionFilterProvider : IExtension
{
    Task<ITestExecutionFilter?> GetFilterAsync(
        TestExecutionFilterContext context,
        CancellationToken cancellationToken);
}

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class TestExecutionFilterContext
{
    public TestExecutionFilterContext(
        TestExecutionRequestKind requestKind,
        TestExecutionRequestOrigin origin);

    public TestExecutionRequestKind RequestKind { get; }
    public TestExecutionRequestOrigin Origin { get; }
}

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestExecutionRequestKind
{
    Discovery,
    Run,
}

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestExecutionRequestOrigin
{
    Console,
    Server,
}

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum TestExecutionFilterOperator
{
    And,
}

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class CompositeTestExecutionFilter : ITestExecutionFilter
{
    public CompositeTestExecutionFilter(
        TestExecutionFilterOperator @operator,
        params ITestExecutionFilter[] filters);

    public TestExecutionFilterOperator Operator { get; }
    public IReadOnlyList<ITestExecutionFilter> Filters { get; }
}

builder.AddTestExecutionFilterProvider(serviceProvider => new MyProvider(serviceProvider));
```

MTP creates `TestExecutionFilterContext` for provider calls. Its public constructor lets provider
authors test their implementation without mocking platform internals.

## Provider lifecycle

Provider factories are registered while building the test application. Registration is additive;
there is no single winner.

MTP follows the normal extension lifecycle:

1. Instantiate each registered provider.
2. Validate extension UID/type uniqueness.
3. Call `IsEnabledAsync`.
4. Initialize enabled providers that implement `IAsyncInitializableExtension`.
5. Retain enabled providers for request creation and dispose them with the application services.
6. For every request, call `GetFilterAsync(context, cancellationToken)` once on each enabled
   provider.

Returning `null` means that the provider has no constraint for that request. Returning `NopFilter`
is accepted but equivalent to `null`.

The request cancellation token is passed through unchanged. MTP checks cancellation before invoking
each provider. Providers must stop expensive work promptly when cancellation is requested.

Provider invocation follows registration order for deterministic lifecycle behavior only.
Registration order does not define precedence or filter meaning: every contribution is an AND
constraint.

## Request kind and origin

Providers need two independent dimensions:

- `RequestKind.Discovery` versus `RequestKind.Run`, because a constraint may apply only to execution
  or may intentionally constrain both discovery and execution.
- `RequestOrigin.Console` versus `RequestOrigin.Server`, because IDE/Test Explorer requests carry
  client-owned selection semantics that must not be silently changed.

Origin deliberately describes transport/request ownership, not a product name such as "IDE" or an
implementation-specific scenario such as "affected tests".

## Filter representations

### No-op

`NopFilter` contributes no constraint. It is removed during normalization.

### UID list

`TestNodeUidListFilter` is a precise set constraint. Its contract is:

- a non-empty list selects nodes whose UID is in the list;
- duplicate UIDs have no additional meaning; and
- an empty list selects no tests.

The last rule is important for disjoint intersections and for JSON-RPC requests that explicitly send
an empty selection.

### Tree/query

`TreeNodeFilter` remains a platform-known representation. This RFC does not make every adapter
capable of evaluating it. An adapter that cannot translate it must fail with an actionable
unsupported-filter diagnostic; silently treating it as `NopFilter` is forbidden.

### Composite

`CompositeTestExecutionFilter` carries an explicit operator and child filters. Version 1 exposes
only `TestExecutionFilterOperator.And`.

OR is not exposed because combining independently owned constraints with OR broadens selection and
can defeat a security, cost, or correctness boundary imposed by another provider. OR also requires
clear interaction rules with framework-owned filter languages before it can be safe.

The constructor requires at least two non-null children. MTP flattens nested AND composites before
passing the filter to a framework.

### Custom filter types

Although `ITestExecutionFilter` is implementable, public providers in this version may return only
platform-known representations. MTP validates every provider contribution recursively.

A custom implementation is rejected and the diagnostic names both the provider UID and filter type.
It is never dropped. Supporting custom filter kinds later requires:

1. a framework capability that declares the kind;
2. negotiation before request execution;
3. a non-silent fallback/rejection policy for mixed frameworks; and
4. adapter translation or a platform-owned evaluation contract.

Because this RFC keeps custom filter kinds out of the composable surface, it does not resolve the
"open up filters for custom implementations" request tracked in
[#3590](https://github.com/microsoft/testfx/issues/3590). It is a prerequisite: composition and
recursive validation exist, so a later capability-negotiation RFC only has to decide which custom
kinds a framework can declare and accept.

A custom filter is only rejected when it takes part in composition. If no provider contributes a
constraint, the built-in request filter is returned untouched (see the composition algorithm below),
so an application whose internal factory produces a framework-specific representation is not broken
by this RFC.

## Composition algorithm

For a console request:

1. MTP creates the existing built-in request filter from `--filter-uid`,
   `--treenode-filter`, or no filter.
2. MTP asks every enabled provider for a contribution.
3. If no provider contributed a constraint, MTP returns the built-in request filter unchanged and
   the remaining steps are skipped.
4. MTP recursively flattens nested AND composites.
5. MTP removes no-op constraints.
6. MTP intersects all `TestNodeUidListFilter` constraints using ordinal UID equality.
7. MTP sorts the resulting UID set for a deterministic representation.
8. MTP returns:
   - `NopFilter` for zero constraints;
   - the only filter for one constraint; or
   - `CompositeTestExecutionFilter(And, ...)` for multiple constraints.

Step 3 is what makes composition strictly additive. Normalization and validation of the built-in
request filter only happen when at least one provider actually contributes, so an application with
no provider — or one whose providers all return `null`/`NopFilter` — observes the same filter
instance and the same behavior as before this RFC, even if its framework uses a filter
representation the composer does not know.

The intersection is computed in the platform rather than by every adapter. If any UID intersection
is empty, the resulting empty `TestNodeUidListFilter` means match none.

Examples:

| Built-in filter | Provider A | Provider B | Result |
| --- | --- | --- | --- |
| `Nop` | `null` | `null` | the same `Nop` instance |
| UIDs `{B,A}` | — | — | the same UIDs `{B,A}` instance (unsorted, no providers) |
| UIDs `{A,B}` | UIDs `{B,C}` | `null` | UIDs `{B}` |
| UIDs `{A}` | UIDs `{B}` | `null` | empty UID list (match none) |
| tree `T` | UIDs `{A,B}` | `null` | `AND(T, UIDs {A,B})` |
| runsettings filter | provider UID constraint | — | adapter evaluates `runsettings AND provider` |

Response files and large UID selections do not require another filter representation. Command-line
processing materializes built-in `--filter-uid` values before composition, including values supplied
through response files. The composer sees the resulting `TestNodeUidListFilter` and intersects it
with provider UID constraints in the same way as an inline value. Response-file parsing remains a
launcher/command-line concern rather than becoming provider API surface.

An in-process provider can return a large `TestNodeUidListFilter` directly, so its selection is not
limited by operating-system command-line length. An orchestrator that must cross a process boundary
can continue to use response files for the built-in option; this RFC does not remove or reinterpret
that fallback.

## Adapter responsibilities

MTP transports filter representations; the test framework or bridge still owns filtering because it
owns test discovery and execution.

Native MSTest and VSTestBridge must:

- traverse `CompositeTestExecutionFilter` recursively;
- implement only `And`;
- translate each UID list to the existing VSTest-compatible expression;
- combine translated constraints with command-line and runsettings filters using AND;
- translate an empty UID list to a valid expression that cannot match any test; and
- throw for `TreeNodeFilter`, unknown operators, and custom filter types they cannot evaluate.

The empty-list translation uses a contradiction rather than the invalid expression `()`:

```text
FullyQualifiedName=__MTP_EMPTY_UID_FILTER__
&
FullyQualifiedName!=__MTP_EMPTY_UID_FILTER__
```

No test can satisfy both clauses, including a test whose actual name happens to equal the sentinel.

Other test frameworks must add composite handling before opting into provider scenarios that can
produce more than one constraint.

## Capabilities

This RFC does not add a public custom-filter capability. Platform-known representations are the
initial compatibility boundary.

Tree filtering already has framework capability history, but capability declaration is not
consistently available across native frameworks and VSTestBridge. Until capability negotiation is
coherent, adapters must reject unsupported tree filters at translation time rather than ignore
them.

A future custom-kind capability should be additive to this design: MTP can validate a provider
contribution against the selected framework's capabilities before composition.

## Diagnostics

The following are fatal, actionable errors:

- a provider returns an unknown filter type;
- a composite has an unknown operator;
- a server-origin provider returns a non-no-op constraint;
- an adapter receives a known representation it cannot translate; or
- an adapter receives a custom representation.

Diagnostics identify the provider or adapter, the filter type/operator, and the supported action
(for example, return `null` for server origin). No unsupported filter is silently ignored.

## JSON-RPC server and IDE boundary

JSON-RPC `testing/runTests` and `testing/discoverTests` requests own their selection:

- `tests` becomes `TestNodeUidListFilter`;
- `filter` becomes `TreeNodeFilter`; and
- neither becomes `NopFilter`.

Version 1 does not apply provider constraints to server-origin requests. MTP still invokes providers
with `Origin.Server` so an origin-aware provider can explicitly return `null`. If a provider returns
a real constraint, MTP rejects the request with an actionable diagnostic. This is deliberate:

- existing client selection semantics stay unchanged;
- an extension is not silently ineffective; and
- the API shape can support negotiated server composition later without breaking providers.

An explicit empty `tests` array remains distinct from an omitted `tests` property and produces an
empty UID filter, which means match none.

## Compatibility and migration

The API is additive and experimental.

- Existing applications with no providers receive the same built-in filter object as before: the
  composer short-circuits before any normalization or validation, so `NopFilter` and
  `TestNodeUidListFilter` instances are not rebuilt and UID order is not changed.
- The same short-circuit applies when providers are registered but all of them return `null` or
  `NopFilter` for a request, which is the expected server-origin path in this version.
- Existing CLI option names and validation do not change.
- Existing JSON-RPC payloads do not change.
- The internal `ITestExecutionFilterFactory` remains the source of the console built-in filter; it
  is no longer proposed as the public extensibility point.
- A previous custom factory prototype should migrate to a provider that returns only its additional
  constraint. It no longer needs to recreate CLI parsing or replace the built-in filter.
- Frameworks that pattern-match directly on `ITestExecutionFilter` must add recursive composite
  support before consuming applications register providers.

### Concrete migration: affected-test selection

A private affected-test extension provides a concrete migration example without making affected-test
terminology part of MTP. Its current implementation has two ways to select tests:

1. The compatibility path uses a `RunAffectedTestsOrchestrator`. It computes the selected UIDs,
   removes its parent activation option to prevent recursive activation, and launches the test host
   with the built-in `--filter-uid` option. Large selections are written to response files, recursive
   response files are supported, and the child execution remains connected to `dotnet test`
   reporting.
2. A transitional direct path can conditionally register the single-winner
   `ITestExecutionFilterFactory` when an `AffectedTestsFilterApiAvailable` build switch is enabled.
   That avoids a child launch, but it replaces the platform factory and therefore cannot safely
   coexist with the built-in request filter or another extension factory.

When the provider API is available, only the second path changes. The extension registers an
`ITestExecutionFilterProvider` and contributes the selected UIDs as an additional constraint:

```csharp
builder.AddTestExecutionFilterProvider(
    serviceProvider => new AffectedTestsFilterProvider(serviceProvider));

public Task<ITestExecutionFilter?> GetFilterAsync(
    TestExecutionFilterContext context,
    CancellationToken cancellationToken)
{
    if (context.Origin == TestExecutionRequestOrigin.Server
        || context.RequestKind == TestExecutionRequestKind.Discovery)
    {
        return Task.FromResult<ITestExecutionFilter?>(null);
    }

    TestNodeUid[] affectedTestUids = GetAffectedTestUids(cancellationToken);
    return Task.FromResult<ITestExecutionFilter?>(
        new TestNodeUidListFilter(affectedTestUids));
}
```

The example name and UID-selection algorithm belong to the consumer. The platform sees only a
provider and a platform-known UID constraint. The provider's `IsEnabledAsync` remains gated by the
consumer's run-affected activation option; `AffectedTestsFilterApiAvailable` chooses the available
integration path rather than changing request semantics.

This migration has the following behavior:

- The provider no longer strips the activation option, relaunches the host, or recreates the
  built-in filter. MTP ANDs its UID set with any user/request constraint.
- An explicit user `--filter-uid` selection and the provider selection are intersected. Neither can
  override the other, and an empty intersection runs no tests.
- A large direct selection stays in memory and therefore has no command-line-length limit.
- When the provider API is unavailable, the existing orchestrator fallback remains unchanged,
  including recursive response-file handling and long UID lists.
- `dotnet test` reporting is preserved in both modes: the provider runs in the original test host,
  while the compatibility orchestrator keeps its existing child-reporting connection.
- The transitional single-winner factory registration is removed once the provider path is used;
  the factory must not remain as a second activation mechanism.

The extension's refresh operation remains an orchestrator. It performs managed x64 profiler
batching and owns a multi-run/instrumentation plan, so converting it to a filter provider would
violate the constraint-versus-planning boundary in this RFC.

Consumer migration tests should prove that the provider and orchestrator fallback select the same
UIDs, that an explicit built-in UID filter intersects correctly, that direct large selections are
not truncated, and that recursive response-file fallback and `dotnet test` reporting remain
unchanged.

## Relationship to draft PR #8820

[Draft PR #8820](https://github.com/microsoft/testfx/pull/8820) proposes a user-facing `--filter`
grammar, filter kinds, and framework registration of those kinds. This RFC is orthogonal:

- #8820 answers how a user expression becomes a request filter.
- This RFC answers how the request filter composes with independent extension constraints.

This implementation does not add, rename, claim, or alias any CLI option, so it does not collide
with framework-owned `--filter` options discussed in #8820. If #8820 lands later, its parsed result
can become the built-in request filter at step 1 of the same composition algorithm.

Custom kinds proposed by #8820 would still need capability negotiation before providers could
contribute them. This RFC does not pre-approve arbitrary custom filter objects.

## Drawbacks

- Frameworks must understand a new composite representation.
- Server-origin providers must branch on origin and return `null`.
- AND-only composition is intentionally less expressive than a general Boolean tree.
- UID normalization allocates a set and deterministic result array.
- Publicly implementable `ITestExecutionFilter` remains broader than the provider contract, so
  runtime validation is required.

## Alternatives

### Public whole-filter factory

Rejected. Replacing the built-in filter forces extensions to duplicate CLI/request parsing and
creates winner/conflict semantics when multiple factories are enabled.

### First enabled provider wins

Rejected. Registration order would define behavior and independent constraints could disappear.

### Self-evaluating custom filters

Rejected for the initial API. Frameworks do not necessarily expose every discovered node to the
platform before filtering, and framework-owned metadata/evaluation rules differ. This also cannot
preserve server selection without a capability contract.

### Let every framework merge providers

Rejected. It duplicates provider lifecycle, cancellation, composition, and UID intersection in each
adapter and risks semantic drift.

### General AND/OR/NOT expression tree

Deferred. AND is the safe operation for independently owned constraints. OR/NOT require a separate
design for ownership, capability negotiation, and framework-language interaction.

### Batch filter as a platform filter

Rejected as the solution to sharding. Index ranges assume stable ordering and make a single request
responsible for global planning. Orchestrators should partition discovered UIDs and issue precise
UID constraints.

## Testing strategy

Coverage includes:

- built-in-only behavior;
- disabled, one, and two enabled providers;
- request kind and origin;
- cancellation propagation;
- nested composite flattening;
- UID intersection, empty intersection, and built-in UID plus provider UID;
- tree plus UID composition;
- unsupported custom filters and server-origin contributions;
- native MSTest and VSTestBridge recursive translation;
- runsettings/command-line AND preservation;
- empty UID match-none behavior;
- JSON-RPC empty selection; and
- acceptance assets that register focused fake providers without coupling to a product scenario.

## Deferred work

- OR/NOT operators;
- custom filter capability negotiation;
- applying provider constraints to JSON-RPC requests;
- a common cross-framework user filter language;
- the CLI proposal in #8820;
- native TreeNodeFilter support in MSTest;
- public orchestration APIs; and
- sharding, batching, retry planning, and affected-test planning.
