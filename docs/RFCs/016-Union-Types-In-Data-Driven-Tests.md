# RFC 016 - Union Types in Data-Driven Tests

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

Tracking issue: [microsoft/testfx#7741](https://github.com/microsoft/testfx/issues/7741)

## Summary

C# is gaining first-class support for *unions* via a new `[System.Runtime.CompilerServices.Union]`
attribute applied to a `class`/`struct`/`record struct`. A union type advertises a set of
underlying case types (e.g. `string`, `int`) and the compiler provides an *implicit language
conversion* from each case type to the union, so callers can write `MethodTakingUnion("hello")`
or `MethodTakingUnion(42)` without ceremony.

This RFC explores whether — and how — MSTest's data-driven testing surface (`[DataRow]`,
`[DynamicData]`, `ITestDataSource`, and the `DataRowShouldBeValidAnalyzer`) should accommodate
union types so that authors can write something like:

```csharp
[Union]
public readonly partial record struct StringOrInt
{
    public StringOrInt(string value) => Value = value;
    public StringOrInt(int value) => Value = value;
    public object? Value { get; }
}

[TestMethod]
[DataRow("hello")]
[DataRow(42)]
public void TestMethod_AcceptsBothCases(StringOrInt value) { /* ... */ }
```

…and have it work, instead of getting either an analyzer error at compile time or an
`ArgumentException` from `MethodBase.Invoke` at run time.

The RFC recommends a phased, conservative approach: ship support transparently only once the
language feature is stable, gate runtime conversion behind a clear capability check on
`[Union]`, and **explicitly defer** automatic combinatorial expansion (because MSTest cannot
invent representative sample values).

## Motivation

### What works today

The following already works and does not need new framework support:

```csharp
[TestMethod]
[DynamicData(nameof(GetData))]
public void Test(StringOrInt value) { /* ... */ }

public static IEnumerable<object?[]> GetData() =>
[
    [new StringOrInt("hello")],
    [new StringOrInt(42)],
];
```

The data source materialises the union itself, so MSTest's existing argument binding (which is
a thin wrapper around `MethodBase.Invoke`) passes a value of the correct runtime type.

### What does not work today

```csharp
[TestMethod]
[DataRow("hello")]              // ❌ analyzer: MSTEST0014 type mismatch
[DataRow(42)]                   // ❌ analyzer: MSTEST0014 type mismatch
public void Test(StringOrInt value) { /* ... */ }
```

Two distinct problems combine:

1. **Compile time** — `DataRowShouldBeValidAnalyzer` validates the assignability of each
   constructor argument with `compilation.ClassifyCommonConversion(arg, param).IsImplicit`.
   Whether the implicit *union conversion* surfaces as an implicit `CommonConversion` from
   Roslyn's public API is an open question; in any case the analyzer is `[Union]`-unaware
   and may reject valid sites.
2. **Run time** — `TestMethodInfo.ResolveArguments` performs **no** type conversion at all
   (see `src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.ArgumentResolution.cs`).
   Whatever object the data source produced is forwarded straight to `MethodBase.Invoke`,
   which uses the CLR's binder rules. The implicit union conversion is a **language**
   conversion synthesised by the compiler — it does **not** exist in IL or reflection. So
   the call throws `ArgumentException: Object of type 'System.String' cannot be converted
   to type 'StringOrInt'`.

A user who already converted to "I'll just write `[DataRow]`" loses; the only escape hatch is
`[DynamicData]`, which is more verbose for the common "table of literals" case.

### Why this matters

`[DataRow]` is the most discoverable, most-used data-driven entry point in MSTest. The same
people who would adopt the union feature (folks modelling sum types of primitives — protocol
payloads, parser tokens, configuration values, etc.) are exactly the people who want to write
small tables of literals against those types. If `[DataRow]` silently breaks, they will either
fall back to `[DynamicData]` (loss of ergonomics) or duplicate test methods per case (loss of
factoring). Neither is a great story.

### Why this might *not* matter

Counterarguments — taken seriously:

- The C# `[Union]` proposal is still preview as of this writing. Committing framework
  semantics to a moving target is risky.
- Authors *can* express the same thing with `[DynamicData]` today, and the cost is one helper
  method per test class. That is not a great user experience, but it is not broken either.
- MSTest cannot synthesise the right sample values for the user. "Expand all underlying cases
  automatically" — a tempting framing — is not actually well-defined.
- Any conversion logic we add is paying ongoing maintenance cost for a small slice of users.

This RFC's recommendation tries to thread that needle: do as little as possible up front,
preserve a clear opt-in surface, but make sure that when authors *do* reach for `[DataRow]`
with a union parameter, the framework does the obvious thing.

## Detailed design

### Goals

1. `[DataRow(literal)]` against a parameter whose type is `[Union]`-annotated and accepts
   `literal`'s type as one of its case constructors should produce a passing call.
2. The analyzer should not flag a valid `[Union]` site, and should give an actionable diagnostic
   for an invalid one (i.e. the literal isn't a member of any case constructor).
3. `[DynamicData]` and custom `ITestDataSource` implementations continue to work as today;
   if they hand back an underlying-case object, the same auto-conversion path applies.
4. Behaviour is opt-in *in spirit* — it only fires when the target parameter is `[Union]`. We
   do not introduce a global conversion pass that affects unrelated parameter types.
5. AOT / trim-friendly: any new reflection must be safe under `IsTrimmable` / `PublishAot`
   profiles that MSTest already supports, or fall back gracefully behind a feature switch.

### Non-goals

- Combinatorial expansion: we will **not** offer "given a union parameter, generate one
  `[DataRow]` per case". MSTest has no way to pick representative values; that responsibility
  belongs to the test author or to a domain-specific source generator.
- Conversion for non-union types. This is not a general "implicit conversion runner" — it is
  strictly scoped to types decorated with `[System.Runtime.CompilerServices.UnionAttribute]`.
- Changing the public shape of `DataRowAttribute`. No new properties on `DataRowAttribute`
  itself; existing test code is unaffected.

### Proposed solution (recommended path)

Two-line summary:

- **Runtime**: when binding arguments in the classic reflection adapter, if the target
  parameter type carries `[Union]`, convert the supplied value via the union's official
  language-blessed conversion surface (factory / `op_Implicit` / case constructor — see
  "Case discovery contract" below) before calling `MethodBase.Invoke`.
- **Analyzer**: when the target parameter type carries `[Union]`, accept an argument iff
  the *same* conversion surface used at runtime would accept it. Analyzer and runtime
  must implement identical rules.

#### Conversion is one shared rule, not two

A key correctness constraint surfaced during review: if the analyzer permits a site that
the runtime then rejects (or vice versa), users get the worst of both worlds — green code
that throws at execution. This RFC therefore mandates a **single specification** of the
allowed conversion set, implemented identically in:

- the analyzer (Roslyn `ClassifyCommonConversion` against each candidate target type), and
- the adapter (reflection-time conversion of the supplied object).

Concretely, the adapter must perform the same widening conversions the analyzer accepts
(e.g. `int → long`, `int → double`) **before** dispatching to the case constructor — not
rely on `ConstructorInfo.Invoke` to do them, because reflection's binder does not perform
standard C# implicit conversions. A small `StandardConversion.TryConvert(value, targetType)`
helper, covering the standard numeric and reference conversions enumerated by the C# spec,
is enough to keep analyzer and runtime aligned. If a desired conversion is outside that
set, both layers must reject the site.

#### Case discovery contract (depends on the language design)

The implementation depends on a discoverable, metadata-level contract for "what are the
cases of this union". The RFC **explicitly defers** runtime support until that contract is
ratified. Possible surfaces, in preferred order:

1. **Compiler-emitted `op_Implicit`** from each case type to the union. If C# union
   lowering emits these, MSTest uses them directly — same code path as today's
   user-defined implicit conversions, guaranteed to match language semantics.
2. **Compiler-emitted `[UnionCase]`-style metadata** on constructors / static factories.
   MSTest discovers cases by enumerating annotated members; this is the most explicit
   contract.
3. **Single-parameter public constructors** as a fallback heuristic. The RFC **does not
   recommend** shipping on this alone because helper constructors (logging adapters,
   internal copies, etc.) would be misclassified as cases. This option is listed only as
   the absolute fallback if the language ships nothing observable in metadata, in which
   case the right call is "do nothing" — see Alternative 1.

The implementation must isolate this rule behind a single `UnionMetadataCache` so that
when the language design lands, only one file changes.

#### Runtime conversion (classic reflection adapter)

Extend `TestMethodInfo.ResolveArguments` (in
`src/Adapter/MSTestAdapter.PlatformServices/Execution/TestMethodInfo.ArgumentResolution.cs`)
with a targeted hook applied per parameter slot **only when** the supplied value is
non-null *and* `value.GetType() != parameter.ParameterType` *and* `parameter.ParameterType`
is decorated with `UnionAttribute`.

> **Implementation note — the existing fast-path.** Today, when every parameter is required
> (no `params`, no optionals — the overwhelmingly common case) `ResolveArguments` short-circuits
> and returns the supplied `arguments` array unchanged. A naive insertion of the union hook
> further down the method would never run for that case, and the union value would flow into
> `MethodBase.Invoke` unchanged and throw the CLR's generic `ArgumentException`. The hook must
> therefore be applied **before** the fast-path returns — i.e. the fast-path must precompute
> (during discovery, cached on `TestMethodInfo`) whether *any* parameter is a `[Union]` type
> and, if so, walk the arguments array applying `ConvertToUnionIfNeeded` per slot before
> returning. When no union parameters are present (the truly common case) the cached flag is
> `false` and the fast-path stays a no-op, preserving today's zero-overhead behaviour.

```csharp
private static object? ConvertToUnionIfNeeded(object? value, Type unionType)
{
    if (value is null)
    {
        return value; // see "Null handling" below
    }

    UnionMetadataCache.CaseTable cases = UnionMetadataCache.GetCases(unionType);

    // 1. Exact runtime-type match on a case.
    if (cases.TryGetExactCase(value.GetType(), out UnionCase exact))
    {
        return exact.Convert(value);
    }

    // 2. Unique standard implicit conversion to a case (same set the analyzer uses).
    if (cases.TryGetUniqueImplicitlyConvertibleCase(value, out UnionCase implicit_))
    {
        return implicit_.Convert(StandardConversion.Convert(value, implicit_.CaseType));
    }

    // 3. No match (or ambiguous): throw a diagnostic ArgumentException listing the
    //    available cases, the offending value's runtime type, and the parameter name.
    throw UnionConversionFailed(value, unionType, cases, parameterName);
}
```

Key properties:

- **Zero overhead when no unions are involved**: the check is a single
  `ParameterType.IsDefined(typeof(UnionAttribute), inherit: false)` per parameter, cached
  during test discovery — a dictionary lookup at execution time, not a reflection scan.
- **Failure mode is a real diagnostic**, not the CLR binder's generic message.
- **No behaviour change for non-union parameters** — existing tests cannot regress.

##### `params` and array parameters

`ResolveArguments` already special-cases `params`: it allocates the params array, then
sets each subsequent argument with `Array.SetValue`. For `params StringOrInt[] values`,
each *element* must go through `ConvertToUnionIfNeeded(element, typeof(StringOrInt))`
before `Array.SetValue`, since `Array.SetValue` would otherwise throw on the type
mismatch.

> **The single-parameter `StringOrInt[]` case is a deliberate behaviour extension, not a
> natural fall-out.** Today's adapter only special-cases a *single* array-typed parameter
> when it is `object[]`: in that case (handled outside `ResolveArguments` — see
> `AssemblyEnumerator.cs:314` and `TestMethodRunner.cs:354`) the data-row's `object[]`
> is passed through *as the single argument*, not splatted element-by-element. For
> `StringOrInt[] values` we are proposing the opposite shape: the data-row's `object[]`
> elements become the array's elements, each converted to `StringOrInt`. That requires
> (1) detecting "single non-`params` array parameter whose element type is a union",
> (2) allocating a `StringOrInt[]` of length `arguments.Length`, and (3) applying the
> hook per element before `Array.SetValue`. It also requires the analyzer to permit
> `[DataRow("hello", 42)]` against `StringOrInt[]`, which it does not today. This is
> tracked here as an additional Phase 1 behaviour change, not a free extension of the
> existing `params` path; if it proves contentious it can be deferred to Phase 2 without
> blocking the rest of the feature.

##### Null handling

- If the parameter's union type is a reference type and the supplied value is `null`,
  pass `null` through (the test method receives `null`).
- If the union is a value type *and* exactly one case has a reference type (e.g.
  `Union(string)`), the RFC proposes: still pass `null` through *and* let the case
  constructor receive `null` if that's the unique applicable case. If two or more cases
  accept `null`, throw the same ambiguity diagnostic as above so users either explicitly
  construct the union or specify the case via a future, language-blessed mechanism.
- This intentionally departs from the original "always pass null as-is" stance after
  rubber-duck review surfaced that `DataRow(null)` against `Union(string?)` is a
  legitimate, common test case.

#### Analyzer relaxation

In `DataRowShouldBeValidAnalyzer.AnalyzeAttribute`, before reporting `MSTEST0014`
(type mismatch), check whether `paramType` carries `[Union]`. If it does:

- Enumerate the cases using the discovery contract above.
- For each case, check
  `compilation.ClassifyCommonConversion(argumentType, caseType).IsImplicit`.
- The site is valid iff **exactly one** case accepts the argument (matching the runtime's
  "unique implicit match" rule). Zero matches or multiple matches both produce a new
  diagnostic (`MSTESTxxxx`, number TBD) whose message lists the available case types.
- Restrict this relaxation to C# syntax trees. If union conversions are not emitted for
  VB.NET, the analyzer must keep its current strict behaviour there.

Phase 1 ships *both* the relaxation and the new diagnostic together. Shipping only the
relaxation would silence false positives at the cost of allowing real failures through.

#### Source generator path (`MSTest.SourceGeneration`)

The MTP-native source-generated path is materially different from the reflection adapter
and needs its own analysis:

- **`DataRowTestMethodArgumentsInfo`** (in `src/Analyzers/MSTest.SourceGeneration/ObjectModels/`)
  emits a typed `TestArgumentsEntry<T1,T2,...>(arg, uid)` constructor call where the type
  arguments come from the test method's parameter tuple. If C# emits the implicit union
  conversion as part of compiling that synthesised expression, **the source-gen `[DataRow]`
  path may already work** without changes — the conversion happens at compile time and
  there's no `MethodBase.Invoke` involved. Verifying this with a prototype is a
  prerequisite to Phase 1.
- **`DynamicDataTestMethodArgumentsInfo`** adapts an `IEnumerable<object?[]>` source; the
  generated wrapper casts `object` to the typed parameter, which **will not** perform the
  union conversion. This path needs the same runtime conversion helper as the reflection
  adapter (the source generator should emit a call to a public
  `MSTest`-side `UnionRuntime.ConvertIfNeeded(...)` helper around the cast).

#### AOT considerations

Two execution profiles to keep in mind:

- **`MSTest.AotReflection.SourceGeneration`** (classic adapter, AOT mode): metadata for
  union case constructors / `op_Implicit` must be reachable. The generator should treat
  any test method parameter whose declared type is `[Union]` as a root and emit the case
  table at compile time. Alternative: gate the runtime path behind
  `RuntimeFeature.IsDynamicCodeSupported` and fall back to "no conversion + diagnostic"
  when off (worse UX, but a safety net).
- **`MSTest.SourceGeneration`** (MTP-native, always AOT-friendly): see above — `[DataRow]`
  may inherit language conversion for free; `DynamicData` needs an explicit conversion
  helper emitted around each typed cast.

#### Version-skew compatibility

Analyzer and adapter ship in different packages and can drift independently. A user might
have a newer `MSTest.Analyzers` (relaxed for unions) with an older adapter / testhost
(still throws at runtime). The RFC requires the following mitigations:

- The analyzer's relaxation activates only when the project references an
  `MSTest.TestFramework` / `MSTest` meta-package version that includes the adapter-side
  fix. Below that floor, the analyzer keeps its current strict behaviour.
- When the floor check fails *and* the test site would otherwise be valid only by union
  conversion, the analyzer emits an info-level diagnostic explaining the package floor —
  not silent acceptance.
- Document the package floor in the release notes; treat it as a hard rule, not a
  best-effort.

#### Discovery and display names

Discovery is unaffected: `DataRow.GetData(methodInfo)` continues to return the raw `Data`
array. Conversion is a binding-time concern, not a discovery-time concern.

Display name generation already calls `TestDataSourceUtilities.ComputeDefaultDisplayName`
with the raw arguments; that behaviour is preserved. The displayed value is the underlying
case value (`"hello"`, `42`) rather than `StringOrInt { Value = hello }`, which is arguably
more useful — but if it matters, users can override with `DisplayName` or
`GetDisplayName(...)`.

### Public API impact

- No additions to `DataRowAttribute`.
- No additions to `DynamicDataAttribute`.
- One new analyzer diagnostic (number TBD) for the "no matching union case" failure mode.
- One new `Resources` entry for the runtime `ArgumentException` message.

The choice to add **no new attribute** is deliberate. The user-visible cue is that the test
parameter is typed as the union — that is the opt-in surface. Adding e.g. an
`[UnionConvert]` marker on the parameter would be redundant noise.

## Alternatives considered

### Alternative 1 — Do nothing (strengthened by review feedback)

Document in the `[DataRow]` XML doc and the data-driven tests doc page that union parameters
require `[DynamicData]`. Don't change the analyzer or adapter.

- **Pros**: Zero implementation, zero risk, zero coupling to a preview language feature.
  Avoids the entire class of "MSTest's case discovery diverges from the language's case
  discovery" problems flagged during review.
- **Cons**: Worst user experience. `[DataRow]` looks like it should work and doesn't.
  Discoverability of the workaround is poor; users will file the same issue repeatedly.
- **When to pick this**: If the language ships unions **without** a metadata-observable
  case contract (no `op_Implicit`, no `[UnionCase]`, nothing on the constructors), then
  any runtime conversion MSTest implements is necessarily a guess that may diverge from
  C#'s actual semantics. In that scenario, *the recommended path collapses to this one*.
  Pair with the optional `TestDataRow.OfUnion<T>(...)` helper (Alternative 4) to give
  authors at least one ergonomic escape hatch.

### Alternative 2 — A new `[UnionDataRow]` attribute (or `DataRow.ConvertToUnion = true`)

An explicit opt-in:

```csharp
[UnionDataRow("hello")]
[UnionDataRow(42)]
public void Test(StringOrInt value) { /* ... */ }
```

- **Pros**: No magic; the conversion only happens when explicitly requested; no risk of
  surprising existing users.
- **Cons**: Splits the data-source API surface for no real reason — every existing analyzer,
  IDE, tutorial, and stack-overflow answer talks about `[DataRow]`, not `[UnionDataRow]`.
  Authors will reach for `[DataRow]` first, fail, and *then* learn about
  `[UnionDataRow]` — which is the same UX problem we set out to fix, with extra steps.
- **When to pick this**: If the community is strongly opposed to any implicit conversion
  in `[DataRow]`. The recommendation argues against this, but it is a defensible position.

### Alternative 3 — A `[UnionExpansion]` attribute that generates cases

```csharp
[UnionExpansion(typeof(StringOrInt),
                Strings = ["hello", ""],
                Ints    = [0, int.MaxValue])]
public void Test(StringOrInt value) { /* ... */ }
```

- **Pros**: Combinatorial coverage without `[DynamicData]` boilerplate.
- **Cons**: Fundamentally a worse `[DynamicData]`. You still have to invent sample values,
  but now you invent them via attribute properties — which can only hold compile-time
  constants. Doesn't scale beyond toy unions. Encourages "throw all underlying values at
  the test" mindset that produces tests with poor failure-attribution.
- **Recommendation**: **Reject**. If users need this, they should write `[DynamicData]` and
  decide for themselves which cases are meaningful.

### Alternative 4 — `TestDataRow.OfUnion<T>(...)` builder

Add a fluent helper for `[DynamicData]` authors:

```csharp
public static IEnumerable<object?[]> GetData() =>
[
    TestDataRow.OfUnion<StringOrInt>("hello"),
    TestDataRow.OfUnion<StringOrInt>(42),
];
```

- **Pros**: Opt-in, no compiler dependency, works today across all TFMs MSTest targets.
- **Cons**: Cosmetic improvement; doesn't help `[DataRow]` users. Becomes redundant once
  the implicit-runtime-conversion path lands (you'd just write `[1, 2, 3]`).
- **Recommendation**: **Consider as an optional Phase 0** for users who want something
  *now*, before the language feature ships. Low cost, low risk; can be removed/deprecated
  cleanly once the recommended path is implemented.

### Alternative 5 — Source generator companion

A source generator inspects test methods, detects union parameters, and emits one concrete
overload (or one `[DataRow]`) per case for each `[DataRow]` literal it can prove is
convertible.

- **Pros**: Zero runtime cost; AOT-friendly by construction.
- **Cons**: Heavy machinery for a problem that's fundamentally about one reflection call.
  Source generators that synthesise test methods have a poor track record (debugging
  experience, IDE refresh, line-mapping in failure messages). The analyzer + adapter
  change in the recommended path achieves the same outcome with far less surface area.
- **Recommendation**: **Reject** unless Alternative 6 (AOT-only) forces our hand.

### Alternative 6 — Runtime conversion gated to non-AOT, source generator for AOT

Hybrid: do the reflection-based path on the JIT, do source-gen on AOT.

- **Pros**: Best-of-both for users who care about both modes.
- **Cons**: Two implementations to keep in lockstep; harder to reason about cross-mode
  consistency.
- **Recommendation**: Park as a fallback if the source-generator-only AOT path
  (recommended Option A under "AOT considerations") proves infeasible.

## Phased rollout

To bound risk, the recommendation is to ship in phases:

1. **Phase 0 (now)**: Documentation. Add an FAQ entry covering union parameters and the
   `[DynamicData]` workaround. Optionally ship the `TestDataRow.OfUnion<T>(...)` helper
   (Alternative 4) as a non-breaking convenience for users who want something today.
2. **Phase 1 (when the language case-discovery contract stabilises)**: Prototype-validate
   that the source-gen `[DataRow]` path inherits the implicit union conversion for free.
   Ship the analyzer relaxation, the new "no matching case" diagnostic, the runtime
   conversion in the classic adapter, the `params`/array element conversion, the
   `DynamicData` source-gen helper, and the AOT generator addition. **All together** —
   splitting analyzer from runtime causes the analyzer-green-runtime-red trap.
3. **Phase 2 (post-shipping)**: If telemetry / feedback shows the package-floor mismatch
   becoming a real source of confusion, harden the analyzer's floor check or move it into
   a build-time error.

**Hard prerequisites for Phase 1** (any one missing → fall back to Alternative 1):

- A metadata-observable case contract for `[Union]` (preferred: compiler-emitted
  `op_Implicit`; acceptable: `[UnionCase]`-style metadata on members).
- Confirmation that `compilation.ClassifyCommonConversion` returns `IsImplicit = true` for
  the union conversion (or a concrete plan to extend the analyzer to recognise it).
- Resolution of the version-skew floor: which `MSTest.TestFramework` version gates the
  analyzer relaxation.
- Decision on language scope (C#-only? VB? F#?).

## Scenarios checked / explicitly out of scope

- ✅ `[DataRow(literal)]` with a union parameter — primary target.
- ✅ `[DynamicData]` returning `object?[]` containing case values — covered by the
  runtime conversion (reflection adapter) and source-gen helper (MTP-native).
- ✅ `params Union[]` and single `Union[]` parameter with multiple data row args —
  per-element conversion in `ResolveArguments`.
- ✅ `[DataRow(null)]` on union parameters — covered by null handling rules above.
- ✅ Custom `ITestDataSource` implementations yielding case values — same conversion path
  as `[DataRow]` (they reach the same `ResolveArguments`).
- ❌ Async test methods — no special handling needed; argument binding is the same.
- ❌ Generic test methods with a union as a type argument — out of scope. Generic data row
  inference is already a constrained scenario; adding union semantics on top should be a
  separate RFC if demand emerges.
- ❌ Combinatorial expansion (Alternative 3) — rejected.

## Open questions

1. **Roslyn surface for `UnionConversion`** — does `compilation.ClassifyCommonConversion`
   return `IsImplicit = true` for the new union conversion? If not, the analyzer needs
   either a new Roslyn API or a hand-written conversion classifier that mirrors the
   compiler. The analyzer relaxation cannot ship before this is answered.
2. **Case discovery contract** — does the language emit `op_Implicit`, `[UnionCase]`
   metadata, or something else? The recommended path collapses to Alternative 1 if the
   answer is "nothing observable in metadata".
3. **Standard conversion set** — exactly which standard conversions should
   `StandardConversion.TryConvert` cover? At minimum: identity, numeric widening,
   reference upcast, `Nullable<T>` wrap. The set must be the intersection of "convertible
   per the C# spec" and "implementable without re-implementing the C# binder".
4. **Ambiguity tie-breaks** — for cases like `Union(int)` + `Union(long)` and a `short`
   literal: the recommended rule is "exact runtime-type match wins; otherwise unique
   implicit match wins; otherwise reject". Confirm this matches user expectations and
   doesn't surprise authors who expect C# overload resolution rules.
5. **Language scope** — confirm whether union conversions exist for VB/F#. If not,
   analyzer relaxation must be C#-only (`SyntaxTree.Options.Language`).
6. **Package-floor enforcement** — should the analyzer relaxation be silently disabled
   below the adapter floor (and emit only an info diagnostic), or hard-fail the build?
   Proposal: info diagnostic; users opt into stricter behaviour via `<NoWarn>` /
   `<WarningsAsErrors>`.
7. **`ITestDataSource` opt-in** — should custom `ITestDataSource` implementations get
   union conversion automatically, or only `[DataRow]` and `[DynamicData]`? Proposal:
   automatic for all paths that flow through `ResolveArguments`, with documentation that
   custom sources can pre-construct unions to skip the conversion entirely.
8. **Telemetry** — emit an MSTest counter ("union conversion applied") to validate
   whether the feature actually gets used? Useful for Phase 2 decisions.

## Unresolved questions

See "Open questions" above. Phase 1 cannot ship until questions 1, 2, 3, and 5 are
resolved. Questions 4, 6, 7, 8 can be settled during implementation.
