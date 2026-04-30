# Informal Specification: `TreeNodeFilter.MatchFilterPattern`

> 🔬 **Lean Squad** — auto-generated formal-verification artifact.
> **Target ID**: 7  |  **Phase**: 2 (informal spec)  |  **Date**: 2026-04-27

## 1. Purpose

`MatchFilterPattern` is the core Boolean evaluator of the `TreeNodeFilter` subsystem. Given a parsed `FilterExpression` tree (produced by `ParseFilter`), a node-path fragment (a single slash-separated segment of a test node's full path), and a `PropertyBag` of key-value metadata, it returns `true` if and only if the fragment (and any required properties) satisfies the filter expression.

It is called by `MatchesFilter` once per path segment, pairing each segment with the corresponding positional filter from the parsed filter list.

## 2. Function Signatures

```csharp
// Primary overload (public-facing via MatchesFilter)
private static bool MatchFilterPattern(
    FilterExpression filterExpression,
    string testNodeFullPath,
    int startFragmentIndex,
    int endFragmentIndex,
    PropertyBag properties)

// Core recursive overload
private static bool MatchFilterPattern(
    FilterExpression filterExpression,
    string testNodeFragment,
    PropertyBag properties)
```

The first overload is a thin slice wrapper: it calls the second overload with
`testNodeFragment = testNodeFullPath[startFragmentIndex..endFragmentIndex]`.

## 3. Inputs and Types

| Parameter | Type | Description |
|---|---|---|
| `filterExpression` | `FilterExpression` (abstract) | Root of the Boolean filter expression tree |
| `testNodeFragment` | `string` | Single path segment (no slashes; URL-encoded) |
| `properties` | `PropertyBag` | Heterogeneous bag of `IProperty` values |

## 4. FilterExpression Taxonomy

The `FilterExpression` hierarchy has **five concrete C# subclasses**: `ValueExpression`, `NopExpression`, `OperatorExpression`, `ValueAndPropertyExpression`, and `PropertyExpression`. These produce **six semantic evaluation forms** in `MatchFilterPattern`, because `OperatorExpression` covers three forms distinguished by a `FilterOperator` discriminant (And / Or / Not). `PropertyExpression` does not appear as a top-level node in `MatchFilterPattern`; it is only valid inside a `ValueAndPropertyExpression`'s property sub-tree and is evaluated by `MatchProperties` instead.

| Variant | C# class | Semantics |
|---|---|---|
| `leaf(pattern)` | `ValueExpression` | Regex match: `Regex($"^{pattern}$", IgnoreCase).IsMatch(fragment)` |
| `nop` | `NopExpression` | Always `true`; acts as logical top / universal identity |
| `and(subexprs)` | `OperatorExpression(And, ...)` | Conjunction: true iff **all** sub-expressions match |
| `or(subexprs)` | `OperatorExpression(Or, ...)` | Disjunction: true iff **any** sub-expression matches |
| `not(e)` | `OperatorExpression(Not, [e])` | Negation: true iff sub-expression does **not** match |
| `withProps(value, props)` | `ValueAndPropertyExpression` | Conjunction: `MatchFilterPattern(value, ...)` AND `MatchProperties(props, ...)` |

> **Note**: `PropertyExpression` (used in the `props` sub-tree of `withProps`) is only valid under `ValueAndPropertyExpression`. A bare `PropertyExpression` as a top-level filter expression triggers `ApplicationStateGuard.Unreachable()` (see §5 preconditions and open question Q1 in §12).

## 5. Preconditions

- `filterExpression` is not `null`.
- `testNodeFragment` is not `null`.
- `properties` is not `null`.
- `OperatorExpression(Not, ...)` has exactly one element in `SubExpressions` (enforced by `ValidateExpression` at parse time). Violation causes `SubExpressions.Single()` to throw.
- `OperatorExpression(And|Or, ...)` has at least two elements in `SubExpressions` (enforced by `ValidateExpression`). The implementation handles any count ≥ 0 via `All`/`Any`.
- There are no nested `ValueAndPropertyExpression` nodes within the `properties` sub-tree (the parser prohibits nested `[...]` syntax).
- `filterExpression` does not contain `PropertyExpression` nodes outside of a `ValueAndPropertyExpression`'s property sub-tree.

## 6. Postconditions

Given the recursive definition below, `MatchFilterPattern(e, s, bag) = evalFilter(e, s, evalProps(·, bag))` where:

```
evalFilter(leaf(pattern), s, _)     = Regex("^" + pattern + "$", IgnoreCase).IsMatch(s)
evalFilter(nop, s, _)               = true
evalFilter(or(exprs), s, P)         = ∃ e ∈ exprs. evalFilter(e, s, P)
evalFilter(and(exprs), s, P)        = ∀ e ∈ exprs. evalFilter(e, s, P)
evalFilter(not(e), s, P)            = ¬ evalFilter(e, s, P)
evalFilter(withProps(v, p), s, P)   = evalFilter(v, s, P) ∧ evalProps(p, bag)

evalProps(propExpr(name, val), bag) = ∃ TestMetadataProperty(k, v) ∈ bag.
                                        Regex("^" + name.Value + "$", IgnoreCase).IsMatch(k)
                                      ∧ Regex("^" + val.Value + "$", IgnoreCase).IsMatch(v)
evalProps(or(exprs), bag)           = ∃ e ∈ exprs. evalProps(e, bag)
evalProps(and(exprs), bag)          = ∀ e ∈ exprs. evalProps(e, bag)
evalProps(not(e), bag)              = ¬ evalProps(e, bag)
```

## 7. Key Invariants (Boolean Algebra)

These hold for all filter expressions `e`, `a`, `b` and fragments `s`:

| # | Property | Statement |
|---|---|---|
| B1 | Nop identity | `evalFilter(nop, s) = true` |
| B2 | Double negation | `evalFilter(not(not(e)), s) = evalFilter(e, s)` |
| B3 | De Morgan (Or) | `evalFilter(not(or(exprs)), s) = evalFilter(and(map not exprs), s)` |
| B4 | De Morgan (And) | `evalFilter(not(and(exprs)), s) = evalFilter(or(map not exprs), s)` |
| B5 | And commutativity | `evalFilter(and([a, b]), s) = evalFilter(and([b, a]), s)` |
| B6 | Or commutativity | `evalFilter(or([a, b]), s) = evalFilter(or([b, a]), s)` |
| B7 | And singleton | `evalFilter(and([e]), s) = evalFilter(e, s)` |
| B8 | Or singleton | `evalFilter(or([e]), s) = evalFilter(e, s)` |
| B9 | Nop is And-identity | `evalFilter(and([nop, e]), s) = evalFilter(e, s)` |
| B10 | Nop absorbs Or | `evalFilter(or([nop, e]), s) = true` |
| B11 | Vacuous And | `evalFilter(and([]), s) = true` |
| B12 | Vacuous Or | `evalFilter(or([]), s) = false` |

## 8. Regex Semantics of Value Tokens

Tokens are converted to regex patterns by `TokenizeFilter`:

| Input character | Regex equivalent | Meaning |
|---|---|---|
| `*` | `.*` | Match zero or more characters |
| `**` | `.*.*` (two `.*`) | Multi-level wildcard (only valid as last segment) |
| `\c` | `Regex.Escape(c)` | Literal character c |
| Other | `Regex.Escape(c)` | Literal (special regex chars are escaped) |

The final regex is `^{pattern}$` with `RegexOptions.IgnoreCase`. This means matching is:
- **Full-fragment** (anchored, not substring)
- **Case-insensitive**
- **Greedy** (`.`* matches greedily)

## 9. Edge Cases

| Scenario | Observed behaviour |
|---|---|
| `nop` expression | Always returns `true` regardless of fragment or properties |
| `and([])` (empty) | Returns `true` (`All` over empty sequence) |
| `or([])` (empty) | Returns `false` (`Any` over empty sequence) |
| `not(e)` with e always-true | Returns `false` |
| Fragment is empty string `""` | Matched against regex normally; `^$` leaf would match |
| Fragment contains regex metacharacters | Safe: characters are `Regex.Escape`d by tokenizer |
| Case difference: `UnitTests` vs `unittests` | Match (case-insensitive) |
| `withProps` with empty PropertyBag | Value must match fragment; `evalProps` with empty bag returns `false` for `PropertyExpression` |
| Negated property with empty PropertyBag | `not(propExpr)` → `!false` = `true` |
| Multi-level wildcard `**` as last segment | Represented as `.*.*`; matches any remaining path via `MatchesFilter` |
| URL-encoded `/` (`%2F`) | Treated as literal characters, not path separator |

## 10. `MatchesFilter` (Public Entry Point)

The public method that calls `MatchFilterPattern` per segment:

**Preconditions**:
- `testNodeFullPath` is not null.
- `testNodeFullPath` starts with `/` (the path separator).

**Behaviour**:
1. Split `testNodeFullPath` by `/` (skipping leading `/`) into segments.
2. Match segment `i` against filter `i`.
3. If the path is fully consumed (no more segments remain), return `true` — any remaining filters are not consulted (prefix-match semantics).
4. If path has more segments than filters, return `true` only if the last filter was `.*.*` (the multi-level wildcard).
5. If any segment fails its filter, return `false` immediately.

**Postcondition**:
```
MatchesFilter(path, bag) = true
  ↔ let n = min(|segments(path)|, |_filters|) in
      (∀ i < n. evalFilter(_filters[i], segment(path, i), bag))
    ∧ (|segments(path)| ≤ |_filters|
       ∨ _filters.Last() is ValueExpression(".*.*"))
```

> **Note**: The path may have *fewer* segments than `_filters`. In that case only the first `|segments(path)|` filters are evaluated and the rest are ignored. This is a deliberate prefix-match design: a more-specific filter (more segments) matches any node whose path is a prefix of the filter path.

## 11. Inferred Design Intent

The function implements a **path-segment-granular glob filter** modelled as a Boolean algebra over compiled regular expressions. Key design decisions:

1. **Boolean algebra over regexes**: Rather than one monolithic regex, the filter uses a structural tree, enabling `And`, `Or`, and `Not` compositions — a feature not trivially expressible in standard glob syntax.

2. **Full-anchor semantics**: Each segment is matched from start-to-end (`^...$`), preventing accidental substring matches.

3. **NopExpression as always-true sentinel**: Used internally to represent a "match anything" node in the filter tree. Its `evalFilter(nop, ·) = true` property makes it the identity element for `And` and the absorbing element for `Or`.

4. **Multi-level wildcard `**`**: Converted to the special string `.*.*` (not a valid single-level pattern). The filter evaluator detects this string as a signal that remaining path segments should all be accepted.

5. **Property sub-filters**: The `ValueAndPropertyExpression` construct separates node-path matching from metadata filtering, keeping the two concerns compositional.

## 12. Open Questions

1. **`PropertyExpression` in `MatchFilterPattern`**: The `MatchFilterPattern` switch has no arm for `PropertyExpression` directly — it relies on always reaching it through the `withProps` arm's `MatchProperties` sub-call. If a malformed `FilterExpression` were constructed with a bare `PropertyExpression` as a top-level filter, `ApplicationStateGuard.Unreachable()` would throw. Is this invariant documented anywhere?

2. **`NopExpression` reachability**: `NopExpression` has an explicit arm returning `true`. When is it actually constructed during parsing? The shunting-yard parser does not appear to push `NopExpression` nodes. Is it a legacy sentinel or used somewhere in the calling code?

3. **Regex caching**: Each `ValueExpression` pre-compiles its regex. Is there a concern about the number of distinct `ValueExpression` instances being created? The `Compiled` flag adds JIT cost.

4. **Empty `And`/`Or` lists**: The parser enforces ≥ 2 children, but the evaluator handles 0-child lists via `All`/`Any`. The vacuous semantics (`and [] = true`, `or [] = false`) are not tested; consider adding a test.

5. **Case sensitivity**: The regex is always case-insensitive (`RegexOptions.IgnoreCase`). Is this an intentional design decision? Could users need case-sensitive matching?

6. **`withProps` and property-only match**: If `withProps(nop, props)` were constructed, `MatchFilterPattern` would return `evalProps(props, bag)` regardless of the fragment. Is there a semantic for "match any fragment if properties satisfy"?

## 13. Sources

- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/FilterExpression.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/FilterOperator.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/OperatorExpression.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/ValueExpression.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/NopExpression.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/PropertyExpression.cs`
- `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/ValueAndPropertyExpression.cs`
- `test/UnitTests/Microsoft.Testing.Platform.UnitTests/Requests/TreeNodeFilterTests.cs`
- `formal-verification/CORRESPONDENCE.md` (Lean type-mapping notes)
