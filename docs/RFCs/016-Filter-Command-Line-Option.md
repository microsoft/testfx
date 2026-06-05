# RFC 016 - Filter command-line option

- [ ] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

> **Driving issue:** [microsoft/testfx#4293](https://github.com/microsoft/testfx/issues/4293)

## Summary

Replace `--treenode-filter` with a first-class, user-friendly `--filter <expression>`
option on Microsoft.Testing.Platform (MTP). The new option uses a small expression
grammar with **prefix-routed kinds** (e.g. `ClassName=Foo`, `Query=/asm/**`,
`TestCategory~Smoke`), and a **bare-value form** that performs a substring match
against a new optional `FilterMatchTextProperty` on `TestNode` (falling back to
`TestNode.DisplayName`).

Filter kinds are an **extensibility point**: built-in kinds are provided by the
platform; test frameworks and bridges (the VSTest bridge being the first
consumer) register their own kinds via a new
`ITestNodeFilterKindProvider` interface, exactly as
`ICommandLineOptionsProvider` adds CLI options today.

`--treenode-filter` is kept as a deprecated alias of `--filter Query=...` for one
release window.

## Motivation

### Current state is fragmented and unfriendly

Today MTP exposes filtering through **two** options:

| Option | Owner | What it does |
| --- | --- | --- |
| `--treenode-filter <expr>` | Platform (`TreeNodeFilterCommandLineOptionsProvider`) | Tree-node path/property grammar (see [graph-query-filtering.md](../mstest-runner-graphqueryfiltering/graph-query-filtering.md)) |
| `--filter-uid <uid>` | Platform | Exact-match list of `TestNode.Uid`s |

This is unsatisfactory for several reasons:

1. **`--treenode-filter` is jargon.** Users do not think in trees. They think
   in *assemblies, namespaces, classes, methods, categories, traits*. The
   name is opaque to a newcomer reading `--help`.
2. **No `--filter` at the platform level.** Several test frameworks already
   support `--filter` on top of MTP (MSTest and NUnit ship it today; xUnit v3
   is filling the gap in 4.x). But each adapter implements its own grammar and
   nothing flows from MTP. Users that drop down to a platform-only entry point
   — or to a framework that has not added the feature yet — still hit
   `Unknown option '--filter'`, and the `dotnet test --filter "FullyQualifiedName~Foo"`
   muscle memory does not work uniformly across the ecosystem.
3. **No bridge to VSTest filter expressions.** Users with existing CI
   pipelines that pass `--filter TestCategory=Smoke&Priority=1` have no
   migration path that does not require them to first learn the tree-node
   grammar.
4. **No extensibility.** Frameworks cannot teach MTP that they understand
   `Owner`, `Priority`, `Traits`, or any framework-specific kind. Everything
   has to be expressed in terms of MTP `Properties` plus the tree-node
   grammar, even when the framework already has rich, well-named concepts.
5. **Per-framework `--filter` grammars diverge.** MSTest, NUnit, and xUnit v3
   (4.x) each accept `--filter`, but they each interpret the expression with
   their own grammar. Without an MTP convention for prefix-routed kinds, the
   ecosystem stays fragmented and a `--filter` that works under one adapter is
   not portable to another.

### The shape of the solution

Three observations drive the design:

- The dominant user mental model is **`kind <operator> pattern`**
  (`ClassName=Foo`, `Category~Smoke`). VSTest established it; users have it
  in muscle memory.
- The dominant user mental model for the *bare* form is **"matches the
  test name"** (`--filter Foo` ≈ "tests whose name contains Foo"). VSTest
  established this too (`--filter Foo` is shorthand for
  `FullyQualifiedName~Foo`).
- The existing MTP tree-node query grammar is **strictly more expressive**
  than the prefix grammar, but the prefix grammar is more *familiar*. There
  is no need to choose: the tree-node grammar becomes one **kind**
  (`Query=/asm/**`) and lives alongside the friendlier kinds.

## Naming

| Candidate | Verdict |
| --- | --- |
| `--filter` | **Selected.** Matches VSTest muscle memory. The collision with VSTest's syntax is intentional: `--filter Foo` does the right thing, and `--filter ClassName=Foo` is unambiguous for users coming from VSTest. |
| `--filter-query` | Rejected. Suggests `query` is just one of several `--filter-*` siblings, which is **not** the design (cross-kind composition forces a single expression layer; shipping both is worst-of-both-worlds). Also already used by xUnit v3 for its own boolean-expression grammar. |
| `--filter-pattern` | Rejected. "Pattern" overpromises a regex/glob; the grammar is neither. |
| `--filter-treenode` | Rejected. Still jargon. |
| `--treenode-filter` *(status quo)* | Rejected. See "Current state is fragmented and unfriendly". |

The collision with VSTest's `--filter` is **a feature, not a bug**:

- Bare values (`--filter Foo`) match VSTest's default semantics:
  `contains` (the `~` operator) on `FullyQualifiedName`
  (`--filter Foo` is shorthand for `FullyQualifiedName~Foo`).
- VSTest's structured expressions (`--filter "ClassName=Foo&Priority=1"`)
  parse identically under the new grammar.
- VSTest's filter expressions that reference VSTest-specific kinds
  (`TestCategory`, `Priority`, `Owner`, traits) work as soon as the VSTest
  bridge registers a kind provider — see "VSTest bridge: first customer".

There is no collision with xUnit v3, which uses `--filter-query` for its
own boolean-expression grammar. xUnit-targeted MTP projects keep
`--filter-query` for the xUnit syntax and gain `--filter` for the MTP
syntax described in this RFC; kind providers are an **opt-in**
registration, so an xUnit-targeted MTP project picks up xUnit's prefix
set rather than VSTest's when one is shipped.

## Design principles

1. **One option, one grammar.** `--filter <expression>` is the single
   filter surface for end-users. Every richer concept (tree-node queries,
   framework-specific kinds, VSTest-compat kinds) is a *kind* inside that
   expression.
2. **Bare values are the dominant case.** `--filter Foo` is the 90% path
   and must Just Work. Prefix-routed kinds are for the remaining 10%.
3. **The bare-match target is framework-controlled but role-named.** A new
   `FilterMatchTextProperty` lets each framework decide what bare matching
   should hit, without overloading existing identity properties (`Uid`,
   `DisplayName`) with filter semantics.
4. **Kinds are extensible.** Frameworks register their own kinds without
   patching the platform. The VSTest bridge is the first consumer; xUnit,
   MSTest, NUnit-on-MTP, and others can each register their own.
5. **Kinds are first-class in `--help`.** Every registered kind contributes
   a row to a "Filter expression syntax" section in `--help`.
6. **No silent semantic change for existing scripts.** `--treenode-filter`
   continues to work for one release window, aliased to
   `--filter Query=...`.
7. **Grammar is consistent with the existing MTP tree-node query grammar**
   (already shipped, already documented). Outer operators (`&`, `|`, `()`,
   `=`, `!=`, `\` escape) match. Users who know one know the other.

## Detailed design

### Grammar

```text
filter      := expression
expression  := term ( ( '&' | '|' ) term )*
term        := '(' expression ')'
             | predicate
predicate   := bare-value
             | kind operator pattern
bare-value  := <any non-empty string with no top-level &, |, (, ), =, ~, !=, !~, escaped via \>
kind        := <identifier registered by a kind provider>
operator    := '=' | '~' | '!=' | '!~'
pattern     := <string; semantics defined by the kind>
```

Operator semantics for **string-valued kinds** (the common case):

| Operator | Meaning |
| --- | --- |
| `=` | exact match (case-insensitive by default; kind may override) |
| `~` | substring match (case-insensitive by default) |
| `!=` | negated exact match |
| `!~` | negated substring match |

Kinds that take **structured patterns** (notably `Query=`) interpret the
pattern themselves; `Query!=/foo` and `Query~/foo` are reserved for future
use and rejected today.

Combinators:

- `&` — logical AND
- `|` — logical OR
- `()` — explicit grouping (required when mixing `&` and `|`, matching the
  existing tree-node grammar's rule)
- Precedence: in the absence of parentheses, `&` and `|` at the same level
  are left-associative; **mixing them without parens is an error**, to
  avoid the "is `A&B|C` `(A&B)|C` or `A&(B|C)`?" trap.

Escaping:

- `\&`, `\|`, `\(`, `\)`, `\=`, `\~`, `\!`, `\\` produce literal characters
  inside a `bare-value` or `pattern`.
- A whole `bare-value`/`pattern` may also be wrapped in `"..."` to skip
  escaping for `&`, `|`, `(`, `)`. Inside quotes, only `\"` and `\\` are
  meaningful escapes.

Bare-value form:

- `--filter Foo` is sugar for the unspecified-kind predicate. It matches a
  `TestNode` if the test node's `FilterMatchTextProperty.Value` **contains**
  `Foo` (case-insensitive). When no `FilterMatchTextProperty` is present,
  the bare value falls back to **`TestNode.DisplayName` contains**.
- Bare values may be combined with prefix kinds:
  `--filter "Foo&Category=Smoke"`.

### Built-in kinds

| Kind | Source | Pattern semantics | Notes |
| --- | --- | --- | --- |
| `DisplayName` | `TestNode.DisplayName` | string | Always available. |
| `Uid` | `TestNode.Uid` | string | Always available. Exact match is the typical use; `~` works but is rarely useful. |
| `Query` | tree-node grammar | tree-node path+predicate expression | Only `=` operator is valid. Implementation reuses today's `TreeNodeFilter`. This is how the existing grammar surfaces under the new option. |
| `Namespace` | `TestMethodIdentifierProperty.Namespace` | string | Skipped (predicate is false) for nodes without `TestMethodIdentifierProperty`. |
| `ClassName` | `TestMethodIdentifierProperty.TypeName` | string | Same skip rule. `TypeName` would be more accurate but `ClassName` matches VSTest muscle memory; we ship `ClassName` as the canonical name and may add a `TypeName` alias. |
| `MethodName` | `TestMethodIdentifierProperty.MethodName` | string | Same skip rule. |
| `FullyQualifiedName` | synthesized | string | Computed as `{Namespace}.{TypeName}.{MethodName}` when `TestMethodIdentifierProperty` is present. Returns false (no match) otherwise. **Not** read from `FilterMatchTextProperty` — the latter is for bare matching and may legitimately contain non-FQN content. |

**Frameworks are responsible for populating `TestMethodIdentifierProperty`**
on their `TestNode`s for the structured kinds to work. This is already the
case for MSTest; xUnit v3 / NUnit / etc. either already do this or will do
it as part of opting into MTP.

### `FilterMatchTextProperty` (new public API)

```csharp
namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Optional property attached to a <see cref="TestNode"/> that supplies the text against which
/// the unspecified (bare) form of the <c>--filter</c> command-line option performs a
/// substring match. Frameworks populate this property freely; the platform does not
/// constrain its content, but the recommended value is the fully qualified test name
/// without any data-row suffix (e.g. <c>MyNamespace.MyClass.MyTestMethod</c>).
/// </summary>
/// <remarks>
/// <para>
/// When a test node has no <see cref="FilterMatchTextProperty"/>, the platform falls back
/// to matching against <see cref="TestNode.DisplayName"/>.
/// </para>
/// <para>
/// This property is intentionally separate from <see cref="TestMethodIdentifierProperty"/>:
/// the latter is consumed by the structured filter kinds (<c>ClassName=</c>,
/// <c>FullyQualifiedName=</c>, ...) and tooling that needs a stable, ECMA-335-compliant
/// identifier; the former is the framework's free-form opinion on "what the user would
/// type to find this test by name".
/// </para>
/// </remarks>
public sealed class FilterMatchTextProperty : IProperty, IEquatable<FilterMatchTextProperty>
{
    public FilterMatchTextProperty(string value);

    public string Value { get; }

    // ToString / Equals / GetHashCode elided.
}
```

The property is added to a `TestNode`'s `Properties` bag (it is not a
direct member of `TestNode`). This mirrors `TestMethodIdentifierProperty`
and keeps `TestNode`'s direct surface unchanged.

### Filter-kind extensibility

A new extension point lets frameworks contribute filter kinds:

```csharp
namespace Microsoft.Testing.Platform.Extensions.CommandLine;

/// <summary>
/// Provides one or more <see cref="TestNodeFilterKind"/> entries that the
/// <c>--filter</c> command-line option recognizes as prefix-routed kinds.
/// </summary>
public interface ITestNodeFilterKindProvider : IExtension
{
    IReadOnlyCollection<TestNodeFilterKind> GetFilterKinds();
}

/// <summary>
/// A single filter kind registration. Kind names are unique in the registry: built-in kinds
/// are registered first, and a provider may register a kind whose name collides with an
/// existing one only when <see cref="AllowShadowing"/> is <c>true</c>, in which case the
/// later registration wins. The chosen kind's <see cref="Predicate"/> is the sole authority
/// on whether a node matches; its <see cref="TestNodeFilterKindMatch"/> return value
/// indicates match, no-match, or that the kind cannot be evaluated against the node.
/// </summary>
public sealed class TestNodeFilterKind
{
    public TestNodeFilterKind(
        string name,
        string description,
        TestNodeFilterPredicate predicate,
        TestNodeFilterOperators supportedOperators = TestNodeFilterOperators.All,
        bool allowShadowing = false);

    /// <summary>The kind name as it appears in the expression, e.g. "ClassName" or "Trait".</summary>
    public string Name { get; }

    /// <summary>Single-line help-text description. Surfaces in <c>--help</c>.</summary>
    public string Description { get; }

    /// <summary>Which operators this kind accepts. Producing an unsupported operator is a CLI error before any test runs.</summary>
    public TestNodeFilterOperators SupportedOperators { get; }

    /// <summary>When true, this kind may be registered with a name that collides with a built-in or earlier-registered kind, replacing it.</summary>
    public bool AllowShadowing { get; }

    /// <summary>The predicate.</summary>
    public TestNodeFilterPredicate Predicate { get; }
}

[Flags]
public enum TestNodeFilterOperators
{
    None     = 0,
    Equals   = 1 << 0,  // '='
    Contains = 1 << 1,  // '~'
    NotEquals   = 1 << 2,  // '!='
    NotContains = 1 << 3,  // '!~'
    All = Equals | Contains | NotEquals | NotContains,
}

/// <summary>
/// Evaluates a single (kind, operator, pattern) predicate against a single test node.
/// </summary>
/// <returns>
/// <see cref="TestNodeFilterKindMatch.Matches"/> when the node satisfies the predicate,
/// <see cref="TestNodeFilterKindMatch.DoesNotMatch"/> when it does not, and
/// <see cref="TestNodeFilterKindMatch.NotApplicable"/> when the kind cannot be evaluated
/// against this node (e.g. <c>ClassName=</c> against a node with no
/// <see cref="TestMethodIdentifierProperty"/>). <see cref="TestNodeFilterKindMatch.NotApplicable"/>
/// is treated as "does not match" for filtering purposes but is reported separately
/// in diagnostics.
/// </returns>
public delegate TestNodeFilterKindMatch TestNodeFilterPredicate(
    TestNode node,
    TestNodeFilterOperator op,
    string pattern);

public enum TestNodeFilterOperator { Equals, Contains, NotEquals, NotContains }

public enum TestNodeFilterKindMatch { Matches, DoesNotMatch, NotApplicable }
```

Registration mirrors `ICommandLineManager.AddProvider`:

```csharp
namespace Microsoft.Testing.Platform.CommandLine;

public interface ITestNodeFilterKindManager
{
    void AddProvider(Func<ITestNodeFilterKindProvider> providerFactory);
    void AddProvider(Func<IServiceProvider, ITestNodeFilterKindProvider> providerFactory);
}

// New member added to ITestApplicationBuilder:
public interface ITestApplicationBuilder
{
    // ... existing members elided ...
    ITestNodeFilterKindManager TestNodeFilterKinds { get; }
}
```

### Resolution algorithm

`CommandLineParser` is unchanged. After parsing, when the platform needs
to evaluate the `--filter` expression against a discovered test, the
following pipeline runs once per `TestNode`:

1. Walk the parsed expression tree.
2. For each `bare-value` leaf: read `FilterMatchTextProperty` (or
   `TestNode.DisplayName` fallback) and apply case-insensitive substring
   match.
3. For each `kind <op> pattern` leaf: look up `kind` in the
   `MergedKindRegistry` (built-ins + provider-supplied; first match wins,
   shadowing rules apply). Invoke the predicate. `NotApplicable` evaluates
   to false.
4. Combine with `&`/`|` per the parsed tree.

Kind-name resolution and operator validation happen **at command-line
validation time** (before any test runs), so misspelled kinds and
unsupported operators produce immediate, actionable errors:

```text
error: filter expression uses unknown kind 'Categry'. Did you mean 'Category'?
       Registered kinds: ClassName, DisplayName, FullyQualifiedName, MethodName, Namespace, Query, TestCategory, Trait, Uid.

error: filter kind 'Query' does not support the '~' operator. Use 'Query=' instead.
```

### `--help` integration

`--help` gains a new section after the option list:

```text
Filter expression syntax:
  --filter accepts an expression of (kind=value | kind~value | bare-value)
  combined with & (AND), | (OR), and parentheses for grouping.

  Registered kinds:
    ClassName            Matches the test method's class (TypeName).
    DisplayName          Matches TestNode.DisplayName.
    FullyQualifiedName   Matches the synthesized Namespace.TypeName.MethodName.
    MethodName           Matches the test method's name.
    Namespace            Matches the test method's namespace.
    Query                Tree-node path+predicate expression. See:
                         https://aka.ms/mtp/treenode-filter
    TestCategory         (provided by Microsoft.Testing.Extensions.VSTestBridge)
    Trait                (provided by Microsoft.Testing.Extensions.VSTestBridge)
    Uid                  Matches TestNode.Uid.

  Bare values match TestNode's FilterMatchTextProperty (falling back to
  DisplayName) using substring, case-insensitive comparison.
```

The bottom block is generated from `MergedKindRegistry` at runtime, so any
registered kind self-documents.

### Backward compatibility: `--treenode-filter`

For **one release window** (target: shipped in vNext, removed in vNext+2),
`--treenode-filter <expr>` keeps working as an alias for
`--filter Query=<expr>`. Registering it raises an obsoletion notice in
`--help`:

```text
  --treenode-filter <expr>   [Deprecated: use --filter Query=<expr>]
```

Using `--treenode-filter` at runtime emits a single banner-level warning
before tests run and otherwise behaves identically to today.

### MSTest rollout

When MSTest builds its `TestNode`s, it sets:

- `TestMethodIdentifierProperty` — already done.
- `FilterMatchTextProperty` = `{Namespace}.{TypeName}.{MethodName}` (no
  data-row suffix, no parameter list). This gives parametrized tests the
  same bare-match behavior as VSTest's `FullyQualifiedName~` default and
  avoids the surprise where `--filter MyTest` would not find
  `MyTest(arg=1)` because the data-row suffix was baked into
  `DisplayName`.

### VSTest bridge: first customer

The VSTest bridge (`Microsoft.Testing.Extensions.VSTestBridge`) ships an
`ITestNodeFilterKindProvider` that registers the kinds users expect from
their VSTest pipelines, mapping them onto `TestNode.Properties` populated
by the bridge:

```csharp
internal sealed class VSTestFilterKindProvider : ITestNodeFilterKindProvider
{
    public string Uid => nameof(VSTestFilterKindProvider);
    public string Version => "1.0.0";
    public string DisplayName => "VSTest-compatible filter kinds";
    public string Description => "Adds TestCategory, Priority, Owner and Trait filter kinds.";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<TestNodeFilterKind> GetFilterKinds() =>
    [
        new("TestCategory",
            "Matches a VSTest TestCategory trait value.",
            (node, op, pattern) => MatchTrait(node, "TestCategory", op, pattern)),
        new("Priority",
            "Matches a VSTest Priority trait value.",
            (node, op, pattern) => MatchTrait(node, "Priority", op, pattern)),
        new("Owner",
            "Matches a VSTest Owner trait value.",
            (node, op, pattern) => MatchTrait(node, "Owner", op, pattern)),
        new("Trait",
            "Matches an arbitrary trait. Pattern format: 'name=value'.",
            MatchArbitraryTrait),
    ];

    // ... predicate implementations elided ...
}
```

Registered from the bridge's builder hook:

```csharp
testApplicationBuilder.TestNodeFilterKinds.AddProvider(_ => new VSTestFilterKindProvider());
```

Behavior:

- `--filter "TestCategory=Smoke"` works identically to its VSTest equivalent.
- `--filter "Priority=1&TestCategory~Integration"` parses and evaluates as
  expected.
- `--filter "(TestCategory=Smoke|TestCategory=Sanity)&Priority!=3"` — same.
- Bridge-targeted MTP runs accept any VSTest filter expression that uses
  the registered kinds.

## Cross-cutting validation rules

Enforced at platform startup, before any user input:

- A filter-kind name MUST be a non-empty identifier matching
  `[A-Za-z_][A-Za-z0-9_]*`.
- A filter-kind name registered with `AllowShadowing = false` MUST NOT
  collide with an existing registered kind. Diagnostic:
  `Filter kind '<name>' is already registered by '<existing provider Uid>'. Set AllowShadowing = true to replace it.`
- `SupportedOperators` MUST contain at least one operator.

Enforced at command-line validation time (before any test runs):

- Every `kind` referenced in the expression MUST exist in the registry.
- Every `<op>` used with a kind MUST be in that kind's `SupportedOperators`.

Enforced at evaluation time (per test node):

- Predicate exceptions become an `error` with the offending kind name,
  pattern, and provider Uid, and abort the run.

## Help and discoverability

In addition to the `--help` "Filter expression syntax" section described
above, `--info` includes the registered kinds in the structured output so
that tooling can introspect what the current run accepts.

`docs/mstest-runner-graphqueryfiltering/graph-query-filtering.md` is moved
under a `Query=` section in the new `docs/mtp-filter.md`. The old file is
kept as a redirect for one release.

## Open questions

1. **Should `DisplayName` be in the default bare-match fallback chain at
   all?** The current proposal: yes, only when `FilterMatchTextProperty`
   is absent. Alternative: drop the fallback to force frameworks to
   populate `FilterMatchTextProperty`, accepting that older frameworks
   produce "no matches" until they update.
2. **Default case sensitivity.** Proposed: case-insensitive everywhere.
   VSTest is case-insensitive on `FullyQualifiedName~` but case-sensitive
   on `Property=` for some properties. Going uniformly case-insensitive
   simplifies the user model; opting back into case-sensitive is a
   per-kind decision the predicate is free to make.
3. **Wildcards in bare values.** VSTest does not support them. The
   tree-node grammar does (via `\*`). Proposal: bare values are pure
   substrings (no wildcards). Users who need wildcards use `Query=`.
4. **Should we ship `Trait=name=value` with two `=` separators**, or
   prefer `Trait[name]=value`? The former matches VSTest verbatim; the
   latter is unambiguous. Proposal: ship the VSTest form, since the
   right-hand side is opaque to MTP and the kind provider parses it.
5. **`Query~` and `Query!=`** — reserved-and-rejected today. Worth
   defining future semantics now?
6. **Should `--treenode-filter` keep working forever** rather than being
   deprecated? Cost is small; "purity" is the only argument for removal.
7. **Long-form vs short-form kind names** (e.g. `Class` as alias for
   `ClassName`, `Method` for `MethodName`). Proposal: ship the long
   forms only; add aliases on user demand.
8. **Per-project inconsistency in mixed-framework solutions.** Different
   frameworks register different kind sets; running one filter expression
   across a solution may match in one project and error in another
   ("unknown kind"). Proposed mitigation: at solution entry, downgrade
   "unknown kind" from an error to a warning **only when** the kind is
   known by at least one project in the solution. Costly to implement;
   defer to a follow-up RFC.

9. **Inner alternation (`TestCategory=A|B`) and inner conjunction.**
   VSTest does not support inner `|` shorthand; users must write
   `TestCategory=A|TestCategory=B`. Some users find the verbose form
   noisy. Three approaches were considered:

   - **Outer parser splits `|` / `&` inside values.** Rejected: this
     forces every kind into the same RHS grammar. The `Trait=name=value`
     kind already uses `=` inside its value; a `Query=` value can
     legitimately contain `|` (`Query=/asm/test|other/`). Making `|` mean
     "OR within value" universally breaks both.
   - **Per-kind opt-in `AllowInnerAlternation` flag on
     `TestNodeFilterKind`.** Workable but adds API surface to v1 and
     forces a precedence decision (does `TestCategory=A|B&Priority=1`
     mean `(TestCategory=A | TestCategory=B) & Priority=1` or
     `TestCategory=A | (TestCategory=B & Priority=1)`?).
   - **A separate values-list syntax** (e.g. `TestCategory=[A,B,C]` or
     `TestCategory in (A,B,C)`) added later. Non-breaking: the new
     punctuation is invalid in today's grammar, so adopting it later
     requires no migration.

   **Proposal for v1:** Ship without inner operators. Document
   `kind=A|kind=B` as the canonical idiom and match VSTest exactly.
   Revisit if user feedback justifies it, preferring a values-list
   syntax over reinterpreting `|`/`&` inside values.

## Migration plan

1. **vNext (this RFC):**
   - Ship `--filter` with built-in kinds and the `FilterMatchTextProperty` API.
   - Ship the VSTest bridge kind provider.
   - Update MSTest to populate `FilterMatchTextProperty`.
   - Keep `--treenode-filter` as an alias; add the deprecation banner.
   - Update `--help` / `--info` snapshots in the acceptance test files
     listed in [Repo CLI options guidelines](../../.github/copilot-instructions.md).
2. **vNext+1:** Telemetry-only — measure `--treenode-filter` usage.
3. **vNext+2:** Remove `--treenode-filter`.

## Public API summary

New public API surfaces (each landing in the appropriate
`PublicAPI.Unshipped.txt`):

```text
Microsoft.Testing.Platform.Extensions.Messages.FilterMatchTextProperty
Microsoft.Testing.Platform.Extensions.Messages.FilterMatchTextProperty.FilterMatchTextProperty(string! value) -> void
Microsoft.Testing.Platform.Extensions.Messages.FilterMatchTextProperty.Value.get -> string!

Microsoft.Testing.Platform.Extensions.CommandLine.ITestNodeFilterKindProvider
Microsoft.Testing.Platform.Extensions.CommandLine.ITestNodeFilterKindProvider.GetFilterKinds() -> System.Collections.Generic.IReadOnlyCollection<Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind!>!

Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.TestNodeFilterKind(string! name, string! description, Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterPredicate! predicate, Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators supportedOperators = ..., bool allowShadowing = false) -> void
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.Name.get -> string!
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.Description.get -> string!
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.SupportedOperators.get -> Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.AllowShadowing.get -> bool
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKind.Predicate.get -> Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterPredicate!

Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperator
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperator.Equals
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperator.Contains
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperator.NotEquals
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperator.NotContains

Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.None = 0
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.Equals = 1
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.Contains = 2
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.NotEquals = 4
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.NotContains = 8
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterOperators.All = 15

Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKindMatch
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKindMatch.Matches
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKindMatch.DoesNotMatch
Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterKindMatch.NotApplicable

Microsoft.Testing.Platform.Extensions.CommandLine.TestNodeFilterPredicate

Microsoft.Testing.Platform.CommandLine.ITestNodeFilterKindManager
Microsoft.Testing.Platform.CommandLine.ITestNodeFilterKindManager.AddProvider(System.Func<Microsoft.Testing.Platform.Extensions.CommandLine.ITestNodeFilterKindProvider!>! providerFactory) -> void
Microsoft.Testing.Platform.CommandLine.ITestNodeFilterKindManager.AddProvider(System.Func<System.IServiceProvider!, Microsoft.Testing.Platform.Extensions.CommandLine.ITestNodeFilterKindProvider!>! providerFactory) -> void

Microsoft.Testing.Platform.Builder.ITestApplicationBuilder.TestNodeFilterKinds.get -> Microsoft.Testing.Platform.CommandLine.ITestNodeFilterKindManager!
```

No `init` accessors are used on any new MTP public API, per repo
guidelines. All `Test*` types use constructors with required parameters.
