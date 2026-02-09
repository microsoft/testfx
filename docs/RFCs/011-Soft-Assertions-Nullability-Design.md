# RFC 011 - Soft Assertions and Nullability Annotation Design

- [x] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

`Assert.Scope()` introduces soft assertions — assertion failures are collected and reported together when the scope is disposed, rather than throwing immediately. This fundamentally conflicts with C# nullability annotations (`[DoesNotReturn]`, `[DoesNotReturnIf]`, `[NotNull]`) that rely on the assumption that assertion failure always means throwing an exception. This RFC documents the problem, the options considered, and the chosen design.

## Motivation

### The soft-assertion goal

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

### The nullability problem

Before soft assertions, `ThrowAssertFailed` was annotated with `[DoesNotReturn]`, which let the compiler prove post-condition contracts. For example:

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

To support soft assertions, `ThrowAssertFailed` was changed so it no longer always throws — within a scope, it adds the failure to a queue and *returns*. This means:

1. `[DoesNotReturn]` can no longer be applied to the general failure path.
2. `[DoesNotReturnIf(false)]` on `IsTrue` / `[DoesNotReturnIf(true)]` on `IsFalse` become lies — the method can return even when the condition is not met.
3. `[NotNull]` on parameters like `IsNotNull(object? value)` becomes a lie — the method can return even when `value` is null.

If we lie about these annotations, **downstream code after the assertion will get wrong nullability analysis**, potentially causing `NullReferenceException` at runtime with no compiler warning.

## Options Considered

### Option 1: Remove all nullability annotations

Remove `[DoesNotReturn]`, `[DoesNotReturnIf]`, and `[NotNull]` from all assertions.

**Pros:** Honest to the compiler. No CS8777 warnings.
**Cons:** Massive regression in developer experience. Users who write `Assert.IsNotNull(obj); obj.Method();` would now get a nullable warning on every call after the assertion. This would be a major breaking change to the user experience of the framework.

**Verdict:** Rejected. Too disruptive for all users, including those who never use `Assert.Scope()`.

### Option 2: Keep all annotations, suppress all warnings

Keep `[DoesNotReturn]`, `[DoesNotReturnIf]`, `[NotNull]` on everything, blanket-suppress CS8777/CS8763.

**Pros:** No user-facing changes. Code compiles cleanly.
**Cons:** The annotations are lies inside a scope. `Assert.IsNotNull(obj)` inside a scope won't throw, meaning `obj` could still be null on the next line, but the compiler thinks it's non-null. This trades a visible assertion failure for a hidden `NullReferenceException`.

**Verdict:** Rejected. Lying about type-narrowing annotations (`[NotNull]`) is actively dangerous — it causes runtime crashes.

### Option 3: Pragmatic tier split (chosen)

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

- `AreEqual`, `AreNotEqual`, `AreSame`, `AreNotSame`
- `Inconclusive`
- `Contains`, `DoesNotContain`
- `IsNull`, `IsNotInstanceOfType`, `IsNotExactInstanceOfType`
- `StartsWith`, `EndsWith`, `Matches`, `DoesNotMatch`
- `IsGreaterThan`, `IsLessThan`, etc.
- `ThrowsException`, `ThrowsExactException`
- All `StringAssert.*` and `CollectionAssert.*` methods

**Pros:** Type-narrowing contracts are always truthful. Soft assertions work for the vast majority of assertions. The few assertions that must remain hard are exactly the ones where continuing would cause crashes.
**Cons:** `IsNotNull` / `IsInstanceOfType` / `IsExactInstanceOfType` won't participate in soft assertion collection — they still throw immediately within a scope.

**Verdict:** Chosen. This is the only option that is both honest to the compiler and safe at runtime.

### Option 3a: Sub-exception for precondition failures

A variant considered was introducing `internal AssertPreconditionFailedException : AssertFailedException` to distinguish hard failures from soft ones, enabling different handling in the adapter pipeline.

**Verdict:** Rejected. The existing adapter pipeline checks `is AssertFailedException` in multiple places (`ExceptionExtensions.TryGetUnitTestAssertException`, `TestClassInfo`, `TestMethodInfo`, etc.). A sub-exception would still match these checks. Adding a new exception type adds complexity without clear benefit, and risks breaking extensibility points that pattern-match on `AssertFailedException`.

## Chosen Design: Two Internal Methods

### `ReportHardAssertFailure`

```csharp
[DoesNotReturn]
[StackTraceHidden]
internal static void ReportHardAssertFailure(string assertionName, string? message)
```

- **Always throws**, even within an `AssertScope`.
- Carries `[DoesNotReturn]` — compiler can trust post-conditions.
- **Launches the debugger** if configured (`DebuggerLaunchMode.Enabled` / `EnabledExcludingCI`).
- Used by: Tier 1 assertions (`IsNotNull`, `IsInstanceOfType`, `IsExactInstanceOfType`, `Fail`, `ContainsSingle`), `CheckParameterNotNull`, `AssertScope.Dispose()`.

### `ReportSoftAssertFailure`

```csharp
[StackTraceHidden]
internal static void ReportSoftAssertFailure(string assertionName, string? message)
```

- Within an `AssertScope`: adds failure to the scope's queue and **returns**.
- Outside a scope: **throws** `AssertFailedException` (preserves existing behavior).
- **No `[DoesNotReturn]`** — compiler knows the method can return.
- **Does not launch the debugger** — the debugger is triggered later when `AssertScope.Dispose()` calls `ReportHardAssertFailure`.
- Used by: Tier 2 and Tier 3 assertions.

### `AssertScope.Dispose()`

When an `AssertScope` is disposed and it contains collected failures:

- **Single failure:** Calls `ReportHardAssertFailure(singleError)` — this throws the original `AssertFailedException` and triggers the debugger.
- **Multiple failures:** Calls `ReportHardAssertFailure(new AssertFailedException(combinedMessage, new AggregateException(allErrors)))` — wraps all collected failures into an `AggregateException` as the inner exception.

This design ensures the debugger breaks at the point where the scope is disposed, giving the developer visibility into all collected failures.

### `CheckParameterNotNull`

The internal helper `CheckParameterNotNull` validates that assertion *parameters* (not the values under test) are non-null. For example, validating that a `Type` argument passed to `IsInstanceOfType` is not null.

This uses `ReportHardAssertFailure` because:

1. A null parameter is a test authoring bug, not a test value failure.
2. It would be confusing to silently collect a "your parameter was null" error alongside real assertion results.
3. It preserves the existing behavior of throwing `AssertFailedException` (not `ArgumentNullException`), which avoids breaking the adapter pipeline that maps exception types to test outcomes.

### `Assert.Fail()` — hard by design

`Assert.Fail()` is a Tier 1 hard assertion. It calls `ReportHardAssertFailure` and always throws, even within a scope. This is the correct choice for two reasons:

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

Users who don't use `Assert.Scope()` experience **zero behavioral change**. All assertions throw exactly as before. The only user-visible annotation change is the removal of `[DoesNotReturnIf]` from `IsTrue`/`IsFalse`, which means the compiler will no longer narrow `bool?` to `bool` after these calls (a minor regression affecting a niche pattern).

### Within `Assert.Scope()`

| Assertion | Behavior |
| --------- | -------- |
| `IsNotNull`, `IsInstanceOfType`, `IsExactInstanceOfType` | Always throws immediately (hard). These assertions narrow types and cannot safely be deferred. |
| `Assert.Fail()` | Always throws immediately (hard). Semantically means unconditional failure — no reason to defer. |
| `Assert.ContainsSingle()` | Always throws immediately (hard). Returns the matched element — returning `default` in soft mode would give callers a bogus value. |
| `IsTrue`, `IsFalse` | Soft. Failures collected. `[DoesNotReturnIf]` removed. |
| All other assertions | Soft. Failures collected. |

## Design Decisions

### `IsTrue` / `IsFalse` are Tier 2 (soft, annotations removed)

`IsTrue` had `[DoesNotReturnIf(false)]` and `IsFalse` had `[DoesNotReturnIf(true)]`. These annotations let the compiler narrow `bool?` to `bool` after the call. By making these assertions soft, we had to remove the annotations — the compiler can no longer assume the condition held.

This was deemed acceptable because:

- The narrowing only affects `bool?` → `bool`, not reference types. The risk of a downstream `NullReferenceException` does not apply.
- The pattern of using `Assert.IsTrue` to narrow a nullable boolean is niche. Most callers pass a plain `bool`.
- Keeping these as hard assertions would significantly reduce the value of `Assert.Scope()`, since `IsTrue`/`IsFalse` are among the most commonly used assertions.

This decision can be reconsidered if the annotation loss proves more impactful than expected.

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

The exact shape of this API is not yet designed.

### Nested scopes

Currently, `AssertScope` uses `AsyncLocal<AssertScope?>` and supports a single active scope. Nested scopes could allow finer-grained grouping of assertion failures:

```csharp
using (Assert.Scope())
{
    Assert.AreEqual(1, actual.X);

    using (Assert.Scope())
    {
        Assert.AreEqual(2, actual.Y);
        Assert.AreEqual(3, actual.Z);
    }
    // Inner scope disposes here — should inner failures propagate to outer scope or throw?

    Assert.AreEqual(4, actual.W);
}
```

The semantics of inner scope disposal (propagate to parent vs. throw immediately) need to be defined.

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

This would require promoting some form of the `ReportSoftAssertFailure` / `ReportHardAssertFailure` API from `internal` to `public`, with careful API design to avoid exposing implementation details.
