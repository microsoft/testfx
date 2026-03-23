# RFC 011 - Structured Assertion Failure Messages

- [x] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

This document describes the unified format for assertion failure messages across `Assert`, `StringAssert`, `CollectionAssert`, and `Assert.That`. All assertion failures now follow a consistent structured layout that separates the call site, user message, framework explanation, and diagnostic values into distinct, predictable sections.

## Motivation

Before this change, assertion failure messages used inconsistent formats across the framework:

- **Mixed tone**: Some messages used passive voice ("String does not contain..."), others active ("Expected a non-null value"), others factual ("Wrong exception type was thrown"), and others imperative ("Do not pass value types...").
- **User message embedding**: User-provided messages were sometimes embedded inside the framework message via `string.Format` positional placeholders (`{0}`), making them hard to visually separate from the diagnostic information.
- **No structured parameters**: Values like `expected`, `actual`, and `delta` were inlined into prose sentences with inconsistent formatting (angle brackets `<>`, single quotes, or no decoration).
- **No call site**: The old format started with `Assert.AreEqual failed.` — telling the user *what* failed but not *what was passed*.
- **`Assert.That`**: Used a different layout entirely (`Assert.That(...) failed.` / `Message: ...` / `Details:` / `x = 5`).
- **`CollectionAssert`/`StringAssert`**: Used legacy `string.Format` with positional placeholders for both user messages and values, making the output hard to parse visually.

These inconsistencies made it harder for users to quickly scan failure output and understand what went wrong.

## Design Goals

1. **Consistent structure**: Every assertion failure follows the same layout regardless of which assert class or method is used.
2. **User message first**: When the user provides a custom message, it appears before the framework explanation — it is the most important context.
3. **Expressions over names**: The call site shows the syntactic expressions the user wrote (via `CallerArgumentExpression`), not just parameter names.
4. **Aligned parameters**: Diagnostic values are indented and column-aligned for easy scanning.
5. **Localizable**: All user-facing framework messages go through `FrameworkMessages.resx` for localization support.
6. **Actionable**: Messages describe what was expected, not just what happened.

## Message Format

Every assertion failure message follows this structure:

```text
<CallSite>
[<UserMessage>]
<FrameworkMessage>
[  <param1>: <value1>]
[  <param2>: <value2>]
```

### Line 1: Call Site

The first line identifies which assertion failed and what expressions were passed:

```text
Assert.AreEqual(expectedVar, actualVar)
Assert.IsTrue(result.IsValid)
Assert.That(x > 10)
CollectionAssert.AreEqual
StringAssert.Contains
```

For `Assert.*` methods, expressions are captured via `[CallerArgumentExpression]` and truncated at 50 characters. For `CollectionAssert` and `StringAssert` (legacy APIs without expression capture), the method name alone is shown.

#### Omitted Parameters

When overloads accept additional parameters not captured by `CallerArgumentExpression` (such as `delta`, `ignoreCase`, `culture`), the call site uses a trailing `...` to signal that the displayed signature is abbreviated:

```text
Assert.AreEqual(1.0m, 1.1m, ...)     // delta overload
Assert.AreEqual(expected, actual, ...) // culture overload
```

This avoids mixing runtime values with source expressions in the call site.

#### Lambda Stripping

For `Assert.That`, the `() =>` lambda wrapper is stripped from the call site since it is syntactic noise:

```text
// Source code: Assert.That(() => x > 10)
// Call site:   Assert.That(x > 10)
```

### Line 2 (Optional): User Message

If the user provided a custom message, it appears on its own line immediately after the call site, without any prefix:

```text
Assert.AreEqual(result, 42)
The calculation returned an unexpected value
Expected values to be equal.
  expected: 42
  actual:   37
```

This was a deliberate choice. Earlier iterations prefixed user messages with `Message:` or embedded them inline with the framework message. Both approaches made the user's intent harder to spot in multi-line output. Placing the user message on its own line — before the framework explanation — gives it the highest visual priority.

### Line 3: Framework Message

The framework's explanation of what was expected. All messages follow the tone **"Expected [subject] to [verb phrase]."**:

```text
Expected values to be equal.
Expected string to start with the specified prefix.
Expected collection to contain the specified item.
Expected the specified exception type to be thrown.
Expected condition to be true.
```

This tone was chosen after evaluating several alternatives:

| Style | Example | Verdict |
| ----- | ------- | ------- |
| Passive: "String does not match..." | `String does not contain the expected substring.` | Rejected — describes outcome, not expectation |
| Factual: "Wrong exception type was thrown." | `No exception was thrown.` | Rejected — not actionable |
| Active nominal: "Expected a non-null value." | `Expected a positive value.` | Rejected — inconsistent structure with parameterized variants |
| **Active verbal: "Expected [X] to [Y]."** | `Expected value to be null.` | **Chosen** — consistent, actionable, parameterizable |

The verbal form scales naturally to parameterized messages like `Expected value {0} to be greater than {1}.` and negative forms like `Expected value to not be null.`

### Lines 4+: Aligned Parameters

Diagnostic values are shown as indented, colon-separated, column-aligned pairs:

```text
  expected: 42
  actual:   37
```

The alignment padding ensures all values start at the same column, making it easy to compare expected vs actual at a glance. When labels have different lengths, the shorter ones are padded:

```text
  expected prefix: "Hello"
  value:           "World"
```

Additional contextual parameters like `delta`, `ignore case`, and `culture` appear when relevant:

```text
  expected:    "i"
  actual:      "I"
  ignore case: False
  culture:     en-EN
```

Collection previews are shown inline with truncation:

```text
  collection: [1, 2, 3, ... 97 more]
```

## `Assert.That` Expression-Aware Messages

`Assert.That` accepts an `Expression<Func<bool>>` and uses the expression tree to generate context-specific failure messages instead of a generic "Expected condition to be true."

| Expression Type | Example | Message |
| --------------- | ------- | ------- |
| `==` | `x == 5` | `Expected 3 to equal 5.` |
| `!=` | `s != "test"` | `Expected "test" to not equal "test".` |
| `>` | `x > 10` | `Expected 5 to be greater than 10.` |
| `>=` | `x >= 10` | `Expected 5 to be greater than or equal to 10.` |
| `<` | `year < 2000` | `Expected 2026 to be less than 2000.` |
| `<=` | `x <= 3` | `Expected 5 to be less than or equal to 3.` |
| `!flag` | `!flag` | `Expected flag to be false.` |
| Bool member | `user.IsActive` | `Expected user.IsActive to be true.` |
| `StartsWith` | `text.StartsWith(...)` | `Expected string to start with the specified prefix.` |
| `Contains` (string) | `text.Contains(...)` | `Expected string to contain the specified substring.` |
| `Contains` (collection) | `list.Contains(...)` | `Expected collection to contain the specified item.` |
| `All` | `nums.All(...)` | `Expected all elements to match the predicate.` |
| `Any` | `coll.Any(...)` | `Expected at least one item to match the predicate.` |
| `&&` / `\|\|` / fallback | compound | `Expected condition to be true.` |

For binary comparisons, both sides of the expression are evaluated at runtime and their values are displayed. For known methods (`StartsWith`, `Contains`, `All`, `Any`), the corresponding framework message is reused. String-specific methods are type-checked to avoid false matches on types that happen to have methods with the same name. Compound expressions (`&&`, `||`) fall back to the generic message since the specific failing sub-expression cannot be determined.

Variable details are extracted from the expression tree and displayed below the message:

```text
Assert.That(x > 10)
Expected 5 to be greater than 10.
  x = 5
```

## `CollectionAssert` and `StringAssert`

These legacy APIs follow the same structural pattern but without `CallerArgumentExpression` (since they predate it):

```text
CollectionAssert.AreEqual
User-provided message
Element at index 1 do not match.
  expected: 2
  actual:   5
```

```text
StringAssert.Contains
Expected string to contain the specified substring.
  substring: "xyz"
  value:     "The quick brown fox..."
```

User messages are positioned using `AppendUserMessage` (before the framework message), and parameter values use `FormatAlignedParameters` for consistent alignment.

## Value Formatting

All values are formatted through a unified `FormatValue<T>` method that applies consistent rules:

| Type | Format | Example |
| ---- | ------ | ------- |
| `null` | `null` | `null` |
| `string` | Quoted, escaped, truncated at 256 chars | `"hello\r\nworld"` |
| `int` | Plain | `42` |
| `long` | With suffix | `42L` |
| `float` | With suffix | `1.5f` |
| `decimal` | With suffix | `0.1m` |
| `double` | Plain | `3.14` |
| Collections | Inline preview with truncation | `[1, 2, 3, ... 97 more]` |
| Types (no useful ToString) | Angle-bracketed full name | `<System.Object>` |
| Other (custom ToString) | Escaped, truncated | `MyCustomType{Id=5}` |

Numeric primitives are formatted using `CultureInfo.InvariantCulture` to ensure consistent output across locales. Collections are safe-enumerated with budget-based truncation to avoid hanging on infinite sequences.

### ToString Handling

For non-primitive, non-collection types, `FormatValue` checks whether the runtime type has a meaningful `ToString()` override:

1. If `ToString()` is overridden (i.e., declared on a type other than `System.Object` or `System.ValueType`), its result is used, escaped, and truncated.
2. If `ToString()` throws an exception, the exception is caught and the type name is displayed instead (e.g., `<MyNamespace.MyType>`).
3. If `ToString()` is not overridden (inherited from `Object`), the type name is displayed directly — this avoids showing the unhelpful default `"MyNamespace.MyType"` as if it were a meaningful value.

This ensures that types with useful `ToString()` (like `DateTime`, records, or custom domain objects) show their value, while types without it show their type name in angle brackets.

### Size Limits

| Element | Limit | Behavior when exceeded |
| ------- | ----- | ---------------------- |
| Expression in call site | 50 characters | Truncated with `...` suffix |
| Formatted value (string, ToString) | 256 characters | Truncated with `... N more` suffix |
| Collection preview | 256 characters total | Elements stop being added; remaining count shown as `... N more` (or `N+` for non-ICollection) |
| Collection element value | 50 characters | Each element individually truncated |
| Newlines in values | N/A | Escaped as `\r\n`, `\n`, `\r` — never produce actual line breaks |

## Implementation Details

### `StringPair` struct

To support `net462` (which lacks `System.ValueTuple`), the aligned parameter and call site methods use a simple `StringPair` struct instead of tuple syntax:

```csharp
internal readonly struct StringPair
{
    public StringPair(string name, string value) { Name = name; Value = value; }
    public string Name { get; }
    public string Value { get; }
}
```

### Localization

All user-facing message strings are defined in `FrameworkMessages.resx` and generated via the standard xlf pipeline. This includes the `Assert.That` expression-aware messages, which use `string.Format` placeholders (`{0}`, `{1}`) for runtime values.

### Collection Safety

Collection parameters are materialized at the assertion boundary (via `as ICollection<T> ?? [.. collection]`) to prevent multiple enumeration. The `FormatCollectionPreview` method uses budget-based truncation and catches enumeration exceptions gracefully, falling back to a `...` suffix rather than failing the assertion formatting.

## Examples

### `Assert.AreEqual` (generic)

```text
Assert.AreEqual(expected, actual)
Expected values to be equal.
  expected: 42
  actual:   37
```

### `Assert.AreEqual` (delta overload)

```text
Assert.AreEqual(1.0m, 1.1m, ...)
Expected difference to be no greater than 0.001.
  expected: 1.0m
  actual:   1.1m
  delta:    0.001m
```

### `Assert.AreEqual` (string with culture)

```text
Assert.AreEqual(expected, actual, ...)
Case differs.
  expected:    "i"
  actual:      "I"
  ignore case: False
  culture:     en-EN
```

### `Assert.IsNull`

```text
Assert.IsNull(result)
Expected value to be null.
  value: 42
```

### `Assert.Throws`

```text
Assert.Throws(action)
Expected the specified exception type to be thrown.
  action: () => service.Process()
  expected exception type: <System.InvalidOperationException>
  actual exception type: <System.ArgumentException>
```

### `Assert.That` (comparison)

```text
Assert.That(x > 10)
x should be greater than 10
Expected 5 to be greater than 10.
  x = 5
```

### `CollectionAssert.AreEqual`

```text
CollectionAssert.AreEqual
Element at index 1 do not match.
  expected: 2
  actual:   5
```

### `StringAssert.StartsWith`

```text
StringAssert.StartsWith
Expected string to start with the specified prefix.
  expected prefix: "Hello"
  value:           "World says goodbye"
```
