# RFC 011 - Soft Assertions and Nullability Annotation Design

- [x] Approved in principle
- [x] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

`Assert.Scope()` introduces soft assertions — assertion failures are collected and reported together when the scope is disposed, rather than throwing immediately. This fundamentally conflicts with C# nullability annotations (`[DoesNotReturn]`, `[DoesNotReturnIf]`, `[NotNull]`) that rely on the assumption that assertion failure always means throwing an exception. This RFC documents the problem, the options considered, and the chosen design.

## Motivation

Today, every MSTest assertion throws `AssertFailedException` on failure. With `Assert.Scope()`, we want to allow multiple assertion failures to be collected and reported at once:

```csharp
using (Assert.Scope())
{
    Assert.AreEqual(1, actual.X);  // failure collected, execution continues
    Assert.AreEqual(2, actual.Y);  // failure collected, execution continues
    Assert.IsTrue(actual.IsValid); // failure collected, execution continues
}
// Dispose() throws AggregateException-like AssertFailedException with all 3 failures
```

## Technical challenges

Soft assertions create a fundamental tension with C# nullability annotations and, more broadly, with all assertion postconditions.

Before soft assertions, `ReportAssertFailed` was annotated with `[DoesNotReturn]`, which let the compiler prove post-condition contracts. For example:

```csharp
public static void IsNotNull([NotNull] object? value, ...)
{
    if (value is null)
    {
        ThrowAssertIsNotNullFailed(...); // [DoesNotReturn] — compiler trusts value is not null after this
    }
    // value is known non-null here
}
```

To support soft assertions, `ReportAssertFailed` was changed so it no longer always throws — within a scope, it adds the failure to a queue and *returns*. This means:

1. `[DoesNotReturn]` can no longer be applied to the general failure path.
2. `[DoesNotReturnIf(false)]` on `IsTrue` / `[DoesNotReturnIf(true)]` on `IsFalse` become lies — the method can return even when the condition is not met.
3. `[NotNull]` on parameters like `IsNotNull(object? value)` becomes a lie — the method can return even when `value` is null.

If we lie about these annotations, **downstream code after the assertion will get wrong nullability analysis**, potentially causing `NullReferenceException` at runtime with no compiler warning.

After discussion with the Roslyn team, a key insight emerged: **this is a general problem with postconditions, not specific to nullability**. `Assert.Scope()` means assertions are no longer enforcing *any* postconditions. Consider:

```csharp
using (Assert.Scope())
{
    Assert.AreEqual("blah", item.Prop);
    MyTestHelper(item.Prop); // may explode if Prop doesn't have expected form
}
```

`Assert.AreEqual` in scoped mode already does not enforce its postcondition (that the values are equal). Code after the assertion may use `item.Prop` assuming it has a particular value, and that assumption may be wrong. The nullability case (`IsNotNull` not guaranteeing non-null) is conceptually identical — it's just another postcondition that isn't enforced within a scope.

## Options Considered

### Option 1: Remove all nullability annotations

Remove `[DoesNotReturn]`, `[DoesNotReturnIf]`, and `[NotNull]` from all assertions.

**Pros:** Honest to the compiler. No CS8777 warnings.
**Cons:** Massive regression in developer experience. Users who write `Assert.IsNotNull(obj); obj.Method();` would now get a nullable warning on every call after the assertion. This would be a major breaking change to the user experience of the framework.

**Verdict:** Rejected. Too disruptive for all users, including those who never use `Assert.Scope()`.

### Option 2: Pragmatic tier split

Categorize assertions into tiers based on whether their post-conditions narrow types, and handle each tier differently.

**Tier 1 — Always throw (hard assertions):** Assertions whose annotations change the type state of a variable for subsequent code. These must always throw, even within a scope, because continuing execution with a wrong type assumption would cause immediate downstream errors unrelated to the assertion.

- `IsNotNull` — annotated `[NotNull]` on the value parameter
- `IsInstanceOfType` — annotated `[NotNull]` on the value parameter
- `IsExactInstanceOfType` — annotated `[NotNull]` on the value parameter
- `Fail` — semantically means "unconditional failure"; annotated `[DoesNotReturn]` on public API
- `ContainsSingle` — returns the matched element; returning `default` in soft mode would give callers a bogus `null`/`default(T)` causing downstream errors

**Tier 2 — Soft, but annotations removed:** Assertions that had conditional `[DoesNotReturnIf]` annotations. The annotation is removed so the compiler no longer assumes the condition is guaranteed. The assertions become soft (collected within a scope).

- `IsTrue` — `[DoesNotReturnIf(false)]` removed
- `IsFalse` — `[DoesNotReturnIf(true)]` removed

**Tier 3 — Soft, no annotation impact:** All other assertions that don't carry type-narrowing annotations. These become fully soft within a scope.

**Pros:** Type-narrowing contracts are always truthful. Soft assertions work for the vast majority of assertions.
**Cons:** `IsNotNull` / `IsInstanceOfType` / `IsExactInstanceOfType` won't participate in soft assertion collection — they still throw immediately within a scope. This significantly reduces the value of `Assert.Scope()` for common test patterns like null-checking multiple properties. Users lose `[DoesNotReturnIf]` narrowing on `IsTrue`/`IsFalse` even outside scopes.

**Verdict:** Rejected. Carving out exceptions makes the scoping feature less useful, and the safety benefit is questionable given that *all* postconditions (not just nullability ones) are already unenforced in scoped mode.

### Option 3: Keep all annotations, suppress compiler warnings (chosen)

Keep `[DoesNotReturn]`, `[DoesNotReturnIf]`, `[NotNull]` on all assertion methods. Make all assertions soft within a scope (except `Assert.Fail()` and `CheckParameterNotNull`). Suppress `#pragma warning disable CS8777` / `CS8763` where the compiler objects.

This is the approach recommended by the Roslyn team: leave all nullable attributes on, but do not actually ensure any of the postconditions when in `Assert.Scope()` context. This is consistent with what we are already doing for all postconditions unrelated to nullability — `Assert.AreEqual` doesn't guarantee equality in scoped mode, `Assert.IsTrue` doesn't guarantee the condition was true, and so on. The nullability annotations are no different.

**Pros:**

- **No user-facing annotation changes.** Users outside `Assert.Scope()` get the exact same experience — `Assert.IsNotNull(obj); obj.Method()` has no nullable warning, `Assert.IsTrue(b)` narrows `bool?` to `bool`. Zero regression.
- **All assertions participate in soft collection.** `IsNotNull`, `IsInstanceOfType`, `IsExactInstanceOfType`, `ContainsSingle`, `IsTrue`, `IsFalse` are all soft within a scope. This maximizes the value of `Assert.Scope()`.
- **Consistent mental model.** The rule is simple: within `Assert.Scope()`, assertion failures are collected and postconditions are not enforced. This applies uniformly to all assertions (except `Assert.Fail()`), whether the postcondition is about nullability, type narrowing, equality, or anything else.

**Cons:**

- **The annotations are lies inside a scope.** `Assert.IsNotNull(obj)` inside a scope won't throw when `obj` is null, meaning `obj` could still be null on the next line, but the compiler thinks it's non-null. This can cause `NullReferenceException` at runtime with no compiler warning.
- **Requires `#pragma warning disable` to suppress CS8777/CS8763.** The compiler correctly identifies that our implementation doesn't fulfill the annotation promises in all code paths.

The runtime risk is acceptable for the same reason that non-nullability postconditions being unenforced is acceptable: the assertion *will* be reported as failed when the scope disposes. The user will see the failure. If downstream code crashes due to a violated postcondition (whether it's a `NullReferenceException` from a null value, or some other error from an unexpected value), that crash is a secondary symptom of the already-reported assertion failure — not a silent, hidden bug.

Users who need a postcondition to be enforced for subsequent code to work correctly can use `Assert.Fail()` (which always throws) or restructure their test to not depend on the postcondition after the assertion within a scope.

**Verdict:** Chosen. This approach gives the best user experience both inside and outside `Assert.Scope()`, and is consistent with how all other postconditions already behave in scoped mode.

## Detailed Design

This section describes the implementation of Option 3 (keep all annotations, suppress compiler warnings), which centers on a single `ReportAssertFailed` method that switches behavior based on whether an `AssertScope` is active.

### `ReportAssertFailed`

```csharp
[StackTraceHidden]
internal static void ReportAssertFailed(string assertionName, string? message)
```

- Within an `AssertScope`: adds failure to the scope's queue and **returns**.
- Outside a scope: **throws** `AssertFailedException` (preserves existing behavior).

### `AssertScope.Dispose()`

When an `AssertScope` is disposed and it contains collected failures:

- **Single failure:** Throws the original `AssertFailedException` and triggers the debugger.
- **Multiple failures:** Throws a new `AssertFailedException` wrapping all collected failures into an `AggregateException` as the inner exception.

This design ensures the debugger breaks at the point where the scope is disposed, giving the developer visibility into all collected failures.

### Nullable annotations: kept but unenforced in scoped mode

All nullable annotations (`[NotNull]`, `[DoesNotReturnIf]`) are kept on their respective assertion methods. Within a scope, these postconditions are not enforced — the method may return without the postcondition being true. Compiler warnings (CS8777, CS8763) arising from this are suppressed with `#pragma warning disable`.

This is the same approach the Roslyn team recommended. As Rikki from the Roslyn team noted:

> It feels like this is an issue with postconditions in general... Assert.Scoped() means assertions are no longer enforcing postconditions. [...] I would honestly start by just trying leaving all the nullable attributes on, but not actually ensuring any of the postconditions, when in Assert.Scoped() context. Since that is essentially what you are doing already with all postconditions unrelated to nullability. See how that works in practice, and, if the usability feels bad, you could consider introducing certain assertions that throw regardless of whether you're in scoped context or not.

### `Assert.Fail()` — hard by design

`Assert.Fail()` is the only assertion that always throws, even within a scope. It inlines its throw logic (bypassing `ReportAssertFailed`) for two reasons:

1. **Semantics:** `Fail()` means "this test has unconditionally failed." There is no meaningful scenario where you'd want to collect a `Fail()` and keep executing — the developer explicitly declared the test a failure.
2. **Public API contract:** `Assert.Fail()` is annotated `[DoesNotReturn]`, and users rely on this for control flow:

```csharp
var result = condition switch
{
    Case.A => HandleA(),
    Case.B => HandleB(),
    _ => Assert.Fail("Unexpected case") // compiler requires [DoesNotReturn] or it's CS0161
};
```

Making `Fail()` hard keeps the `[DoesNotReturn]` annotation truthful with no pragma suppression needed.

## Impact on Users

### No `Assert.Scope()` — no change

Users who don't use `Assert.Scope()` experience **zero behavioral change**. All assertions throw exactly as before. All nullable annotations remain in place. There is no regression.

### Within `Assert.Scope()`

All assertions participate in soft failure collection, with the following exceptions:

- **`Assert.Fail()`** is the only assertion API that does not respect soft failure mode. It always throws immediately, even within a scope, because it semantically means "this test has unconditionally failed" — there is no reason to defer.
- **Null precondition checks** inside Assert APIs (e.g., validating that a `Type` argument passed to `IsInstanceOfType` is not null) also throw directly rather than collecting. These are internal parameter validation checks (`CheckParameterNotNull`), not assertions on the value under test. Note that `Assert.IsNotNull` / `Assert.IsNull` are *not* precondition checks — they are assertions on test values and participate in soft collection normally.

### Dealing with postcondition-dependent code in scoped mode

When using `Assert.Scope()`, code after an assertion should not depend on the assertion's postcondition. This applies to all postconditions, whether nullability-related or not:

```csharp
using (Assert.Scope())
{
    Assert.IsNotNull(item);
    // item might still be null here — the assertion failure was collected, not thrown.
    // If you need item to be non-null for the rest of the test, use Assert.Fail()
    // or restructure the test.

    Assert.AreEqual("blah", item.Prop);
    MyTestHelper(item.Prop);
    // item.Prop might not be "blah" — same issue, different postcondition.
}
```

If a test helper depends on a postcondition being true, the user has several options:

1. **Use `Assert.Fail()` for critical preconditions** — it always throws, even in scoped mode.
2. **Restructure the test** to not depend on postconditions within the scope.
3. **Accept the secondary failure** — the primary assertion failure will be reported, and any downstream crash is a secondary symptom.

This is simply part of the adoption/onboarding cost of using `Assert.Scope()`. The scoping feature trades strict postcondition enforcement for the ability to see multiple failures at once.

## Design Decisions

### Why lying to the compiler is acceptable

The decision to keep annotations that are not enforced in scoped mode is justified by:

1. **Consistency.** All assertion postconditions are already unenforced in scoped mode. Making nullability postconditions the exception adds complexity without meaningful safety improvement.
2. **User experience.** Removing annotations would regress the experience for all users, including those who never use `Assert.Scope()`.
3. **Practicality.** The Roslyn team confirmed this approach is reasonable. Tests that use `Assert.Scope()` are inherently opting into a mode where postconditions are deferred, and users should expect that downstream code may encounter unexpected state.
4. **Observable failures.** The violated postcondition doesn't cause silent bugs — the assertion failure *is* reported when the scope disposes. Any secondary crash is additional evidence of the already-reported failure.
5. **Experimental API.** The `Assert.Scope()` API is currently marked as experimental, which allows us to gather concrete usages and feedback from users before committing to a stable release. Real-world usage patterns will inform whether any adjustments to the annotation strategy or scoping behavior are needed, and we can iterate on the design without breaking stable API contracts.

### Why nested scopes are not supported

Nested `Assert.Scope()` calls are currently not allowed. We do not see a compelling usage scenario that justifies the added complexity of defining nested scope semantics (e.g., should inner scope failures propagate to the parent scope or throw immediately?). This decision can be revisited based on customer feedback if concrete use cases emerge.

## Future Improvements

### Explicit hard-assertion opt-in within a scope

There may be cases where a user wants a specific assertion to throw immediately within a scope, even though it would normally be soft. A possible API could be:

```csharp
using (Assert.Scope())
{
    Assert.AreEqual(1, actual.Count);          // soft
    Assert.Hard.AreEqual("expected", actual);  // hard — throws immediately
    Assert.AreEqual(2, actual.Other);          // soft
}
```

The exact shape of this API is not yet designed. As the Roslyn team suggested, users may want certain assertions to always throw so they can enforce postconditions that subsequent code depends on, even within a scope. This would be part of the natural evolution of the feature based on real-world usage feedback.

### Extensibility for custom assertion authors

Third-party libraries and users who author custom assertions (via `Assert.That` extension methods or standalone assertion classes) currently have no public API to participate in soft assertion collection. They can only call `Assert.Fail()` (which is hard) or throw `AssertFailedException` directly (which bypasses the scope).

A future improvement could expose a public API for custom assertion authors to report soft failures, e.g.:

```csharp
public static class MyCustomAssertions
{
    public static void HasProperty(this Assert assert, object obj, string propertyName)
    {
        if (obj.GetType().GetProperty(propertyName) is null)
        {
            Assert.ReportFailure("MyAssert.HasProperty", $"Expected property '{propertyName}' not found.");
        }
    }
}
```

This would require promoting `ReportAssertFailed` (or a new public variant) from `internal` to `public`, with careful API design to avoid exposing implementation details.
