# RFC 011 - Structured Assertion Messages

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Standardize the structure and layout of assertion failure messages across all MSTest `Assert.*` methods to improve readability, scannability, and developer experience in terminals, CI logs, and IDE test explorers.

## Motivation

Today, MSTest assertion messages use a single-line concatenated format:

```text
Assert.AreEqual failed. Expected:<hello>. Actual:<world>. 'expected' expression: 'expected', 'actual' expression: 'actual'.
```

This format has several problems:

1. **Hard to scan in CI output.** When many tests fail, finding the key information (what was expected vs. what actually happened) requires reading through a wall of text.
2. **User message is buried.** Custom messages provided via the `message` parameter are appended at the end, making them easy to miss even though they often carry the most intent.
3. **No visual hierarchy.** All information — framework boilerplate, user context, expected/actual values, caller expressions — is flattened onto one or two lines with no structure.
4. **Long values destroy readability.** When `expected` or `actual` are long strings, large collections, or multi-line objects, the entire message becomes unreadable because the important context (what went wrong) is buried between the values.

A structured, multi-line format lets developers identify the nature of the failure *before* reading the full values, and provides a consistent shape that tools and humans can rely on.

## Design Principles

1. **Most important information first.** The developer should understand *what went wrong* without scrolling.
2. **Consistent shape.** Every assertion failure should follow the same skeleton so developers build muscle memory for reading failures.
3. **Values are evidence, not the headline.** The `expected:` / `actual:` values confirm the failure — they should not *be* the failure description. The summary line describes the problem in human terms; the values prove it.
4. **Searchable.** The first line should contain a single, universal, grep-friendly prefix (`Assertion failed.`) so failures are easy to locate in logs — regardless of which assertion method was called.
5. **No information loss.** All information available today must still be present. Moving values to labeled lines means they can be shown in full — truncation should only be applied when a value is unreasonably large, not to compensate for a poor layout.
6. **Caller expressions are supplementary.** The call-site expression (e.g. `Assert.AreEqual(expectedPrice, actualPrice)`) is helpful for disambiguation but is a *detail*, not the headline. It should appear after the core failure information.

## Proposed Structure

```text
<assertion-prefix> <summary>                              ← Line 1: grep anchor + what went wrong
<summary continued>                                       ← optional: additional summary lines
<user-message>                                            ← optional: developer-provided context
                                                          ← blank line separator
expected: <expected>                                      ← labeled value
actual:   <actual>                                        ← labeled value (column-aligned with expected)
<assertion-specific details>                              ← optional: extra info (delta, culture, etc.)
                                                          ← blank line separator
<call-site expression>                                    ← the source expression
<stack trace>                                             ← provided by the test runner
```

### Line 1 — Assertion Prefix + Summary

The first line always starts with the universal prefix `Assertion failed.` followed by a human-readable summary of the failure on the same line:

```text
Assertion failed. Expected values to be equal.
```

The prefix is the approach used by most languages and runtimes — Python (`AssertionError`), Java/Kotlin (`AssertionError`), Rust (`assertion failed`), Go, and Node.js all use a universal prefix rather than a per-method one. Placing the summary on the same line means line 1 immediately tells you both *that* an assertion failed and *what* went wrong. Benefits:

- Always starts with `Assertion failed.` — trivial to grep (`Assertion failed\.`).
- Provides a consistent visual boundary between consecutive failures in CI logs.
- The specific assertion method is already visible in the call-site expression and the stack trace, so repeating it on line 1 adds noise rather than signal.
- Avoids coupling the message prefix to the API name, which would break parsers if methods are renamed or new assertions are added.

The summary describes the failure **without expanded values** (unless the values are very short, e.g. types or small integers). When the assertion has rich diagnostics (e.g. string diffs), additional summary lines can follow on subsequent lines.

Examples:

| Assertion | Line 1 |
| --------- | ------ |
| `AreEqual` (int) | `Assertion failed. Expected values to be equal.` |
| `AreEqual` (string) | `Assertion failed. Expected strings to be equal (case-sensitive).` |
| `IsTrue` | `Assertion failed. Expected condition to be true.` |
| `IsNull` | `Assertion failed. Expected value to be null.` |
| `IsInstanceOfType` | `Assertion failed. Expected value to be an instance of String.` |
| `ThrowsExactly` | `Assertion failed. Expected exception of type ArgumentException but no exception was thrown.` |
| `Contains` (collection) | `Assertion failed. Expected collection to contain the specified element.` |

The summary follows the verbal form **"Expected [subject] to [verb phrase]."** for consistency. Short values like type names or small counts can be inlined when they improve clarity without bloating the line.

### User Message

The optional message provided by the developer via the `message` parameter. Displayed on its own line, with no prefix or label — it is the developer's own words and should stand out.

```text
Assertion failed. Expected values to be equal.
Discount should be applied after tax

expected: 95.00
actual:   100.00
```

When no user message is provided, this line is simply omitted (no blank line is left in its place).

### Expected / Actual Values

Labeled lines showing the concrete values, separated from the narrative block above by a blank line:

```text
expected: "hello world"
actual:   "hello wrold"
```

The blank line creates a clear visual boundary between the narrative (summary + user message) and the evidence (values + details). Values are flush-left — no indentation — so they can be copy-pasted directly (e.g. in a TDD workflow where you update an expected value from the failure output).

Rules:

- `expected:` and `actual:` labels are left-aligned.
- For short values, the value follows the label on the same line after a single space: `expected: 42`.
- For visual alignment of same-line values, shorter labels are padded with trailing spaces to match the longest label in the block (e.g. `actual:` is padded to align with `expected:`).
- When an assertion does not have a natural `expected` / `actual` pair (e.g. `Assert.Fail`, `Assert.Inconclusive`), these lines are omitted entirely.

Multi-line values (e.g. JSON objects, large collections) are displayed starting on the next line, flush-left with no indentation:

```text
expected:
{ "name": "Alice", "age": 30 }
actual:
{ "name": "Alice", "age": 31 }
```

This keeps copy-paste zero-friction — you can select the value lines and paste them directly into source code without stripping leading whitespace.

Note: When a multi-line value is a string, it is still quoted according to the Value Rendering Rules. The opening quote appears on the line after the label, and the closing quote appears at the end of the value:

```text
expected:
"line one\nline two\nline three"
actual:
"line one\nLINE TWO\nline three"
```

For very long strings where the value is truncated, the `...` appears inside the quotes: `"...context around difference..."`.

### Assertion-Specific Details

Extra context that is specific to the assertion, displayed as labeled lines after the values:

```text
expected:   42
actual:     37
ignoreCase: true
culture:    tr-TR
```

In this example, `expected:` and `actual:` are the core value labels, while `ignoreCase:` and `culture:` are assertion-specific details. All labels share a single alignment column within the block.

Note: Alignment is applied **within each evidence block as a whole**. All labels in the block (value labels like `expected:` / `actual:` and detail labels like `ignoreCase:` / `culture:`) are padded to match the longest label in the block. This means values and details share a single alignment column.

### Call-Site Expression

The call-site expression is reconstructed from `CallerArgumentExpression` attributes and displayed after the evidence block, separated by a blank line. It helps the developer locate the exact assertion call in source.

#### How it is captured

`CallerArgumentExpression` is applied to the key semantic parameters of each assertion — typically `expected` and `actual` (or `condition`, `action`, `value`, etc.). **It is not applied to every parameter.** Parameters like `delta`, `ignoreCase`, `culture`, `comparer`, and `message` are not captured because they would require additional `CallerArgumentExpression` attributes and corresponding API surface.

This means the call-site expression is a **partial reconstruction**, not the full source call. For example:

```csharp
// Source code:
Assert.AreEqual(expectedPrice, actualPrice, 0.01, "after discount");
```

The call-site would display:

```text
Assert.AreEqual(expectedPrice, actualPrice, ...)
```

The `...` indicates that additional parameters were passed but are not captured. The omitted parameters (like `delta`) are shown in the assertion-specific details section when relevant.

#### Long variable names or expressions

When variables have long names or the arguments are complex expressions, the captured text can be lengthy:

```csharp
// Source code:
Assert.AreEqual(
    orderService.GetDiscountedPrice(customerId),
    paymentGateway.GetChargedAmount(transactionId));
```

Call-site display:

```text
Assert.AreEqual(orderService.GetDiscountedPrice(customerId), paymentGateway.GetChargedAmount(transactionId))
```

The call-site expression is displayed **without truncation** — since it appears at the bottom of the message (after the evidence), its length does not interfere with reading the failure summary or values. Long expressions may wrap in the terminal, which is acceptable.

#### Multiline constant arguments

When a parameter is a multiline expression (e.g. a raw string literal or a multi-line LINQ chain), `CallerArgumentExpression` captures it verbatim including all newlines and indentation:

```csharp
// Source code:
Assert.AreEqual(
    """
    {
      "name": "Alice",
      "age": 30
    }
    """,
    actualJson);
```

The raw captured expression for `expected` would be:

```text
"""
    {
      "name": "Alice",
      "age": 30
    }
    """
```

Displaying this verbatim in the call-site line would be confusing — the newlines break the visual structure of the message. Two approaches:

**Approach 1 — Collapse to single line:** Replace all newlines and consecutive whitespace in the captured expression with a single space, then truncate if needed:

```text
Assert.AreEqual(""" { "name": "Alice", "age": 30 } """, actualJson)
```

**Approach 2 — Omit the multiline argument and show `...`:** Detect that the expression contains newlines and replace it with `...`:

```text
Assert.AreEqual(..., actualJson)
```

**Recommendation:** Use Approach 2 (omit with `...`) when the captured expression contains newlines. The full value is already displayed in the `expected:` / `actual:` lines, so repeating it in the call-site is redundant. The call-site should identify *where* the call happened, not *what* the values were.

#### Unavailable expressions

If `CallerArgumentExpression` is not available (e.g. older TFMs, indirect calls through helpers, or reflection-based invocations), the call-site line is omitted entirely. The stack trace still provides the location.

### Stack Trace

Provided by the test runner, not by the assertion itself. Appears after the call-site expression.

## Complete Examples

### Assert.AreEqual (integers)

```text
Assertion failed. Expected values to be equal.

expected: 42
actual:   37

Assert.AreEqual(expectedCount, actualCount)
   at MyTests.OrderTests.TotalItemCount_ShouldMatchCart() in OrderTests.cs:line 55
```

### Assert.AreEqual (strings, with user message)

```text
Assertion failed. Expected strings to be equal (case-sensitive).
Strings have same length (11) but differ at 1 location(s). First difference at index 7.
The greeting should include the user's full name

expected: "hello world"
actual:   "hello wrold"

Assert.AreEqual(expected, actual)
   at MyTests.GreetingTests.ShouldFormatName() in GreetingTests.cs:line 42
```

### Assert.IsTrue

```text
Assertion failed. Expected condition to be true.

expected: true
actual:   false

Assert.IsTrue(order.IsValid)
   at MyTests.OrderTests.ValidOrder_ShouldBeValid() in OrderTests.cs:line 30
```

### Assert.ThrowsExactly (no exception thrown)

```text
Assertion failed. Expected exception of type ArgumentException but no exception was thrown.

Assert.ThrowsExactly<ArgumentException>(() => Validate(input))
   at MyTests.ValidationTests.InvalidInput_ShouldThrow() in ValidationTests.cs:line 18
```

### Assert.ThrowsExactly (wrong exception type)

```text
Assertion failed. Expected exception of exactly type ArgumentException but caught InvalidOperationException.

expected type: System.ArgumentException
actual type:   System.InvalidOperationException

Assert.ThrowsExactly<ArgumentException>(() => Validate(input))
   at MyTests.ValidationTests.InvalidInput_ShouldThrow() in ValidationTests.cs:line 18
```

### Assert.AreEqual (large strings)

```text
Assertion failed. Expected strings to be equal (case-sensitive).
Strings differ at 1 location(s). First difference at index 1042.

expected:
"...configuration that spans many lines and contains the production database
connection string along with various timeout settings..."
actual:
"...configuration that spans many lines and contains the staging database
connection string along with various timeout settings..."

Assert.AreEqual(expectedConfig, actualConfig)
   at MyTests.ConfigTests.ShouldLoadProductionConfig() in ConfigTests.cs:line 88
```

### Assert.Contains (collection)

```text
Assertion failed. Expected collection to contain the specified element.

expected to contain: "banana"
collection:          ["apple", "cherry", "date"]

Assert.Contains(fruits, "banana")
   at MyTests.FruitTests.ShouldIncludeBanana() in FruitTests.cs:line 12
```

### Assert.AreEqual (with delta — uncaptured parameter)

Source:

```csharp
Assert.AreEqual(expectedPrice, actualPrice, 0.01);
```

Output — `delta` is not captured by `CallerArgumentExpression`, so the call-site shows `...`:

```text
Assertion failed. Expected values to be equal within tolerance.

expected: 95.00
actual:   100.00
delta:    0.01

Assert.AreEqual(expectedPrice, actualPrice, ...)
   at MyTests.PriceTests.DiscountedPrice_ShouldMatch() in PriceTests.cs:line 44
```

### Assert.AreEqual (multiline raw string literal as expected)

Source:

```csharp
Assert.AreEqual(
    """
    {
      "name": "Alice",
      "age": 30
    }
    """,
    actualJson);
```

Output — the multiline expression is replaced with `...` in the call-site:

```text
Assertion failed. Expected strings to be equal (case-sensitive).
Strings differ at 1 location(s). First difference at index 22.

expected: "{\n  \"name\": \"Alice\",\n  \"age\": 30\n}"
actual:   "{\n  \"name\": \"Alice\",\n  \"age\": 31\n}"

Assert.AreEqual(..., actualJson)
   at MyTests.JsonTests.ShouldSerializeUser() in JsonTests.cs:line 27
```

### Assert.AreEqual (long expression arguments)

Source:

```csharp
Assert.AreEqual(
    orderService.GetDiscountedPrice(customerId),
    paymentGateway.GetChargedAmount(transactionId));
```

Output — long expressions are not truncated, they may wrap in the terminal:

```text
Assertion failed. Expected values to be equal.

expected: 95.00
actual:   100.00

Assert.AreEqual(orderService.GetDiscountedPrice(customerId), paymentGateway.GetChargedAmount(transactionId))
   at MyTests.BillingTests.ChargedAmount_ShouldMatchDiscount() in BillingTests.cs:line 63
```

## Assertion Message Catalog

This section maps every MSTest assertion to its structured failure message. For brevity, user message, call-site expression, and stack trace are omitted — they follow the same rules for every assertion. Only the summary line and evidence block are shown.

### Assert — Equality

#### Assert.AreEqual (generic)

```text
Assertion failed. Expected values to be equal.

expected: 42
actual:   37
```

#### Assert.AreEqual (with delta)

```text
Assertion failed. Expected values to be equal within tolerance.

expected: 3.14
actual:   3.15
delta:    0.001
```

Note: The `delta` overload exists for `float`, `double`, `decimal`, and `long`. All four types use the same message format. The rendered precision follows the type’s default `ToString()` formatting.

#### Assert.AreEqual (string, case-sensitive)

```text
Assertion failed. Expected strings to be equal (case-sensitive).
Strings have same length (11) but differ at 1 location(s). First difference at index 7.

expected: "hello world"
actual:   "hello wrold"
```

#### Assert.AreEqual (string, case-insensitive with culture)

```text
Assertion failed. Expected strings to be equal (case-insensitive).
Strings have different lengths (expected: 6, actual: 8) and differ at 1 location(s). First difference at index 6.

expected:   "straße"
actual:     "STRASSE!"
ignoreCase: true
culture:    de-DE
```

Note: Under `de-DE` culture with case-insensitive comparison, `"straße"` and `"STRASSE"` are considered equal (ß expands to SS). The example above shows a genuinely failing comparison where the actual string has additional content beyond the case-equivalent portion.

#### Assert.AreNotEqual (generic)

```text
Assertion failed. Expected values to differ.

notExpected: 42
actual:      42
```

#### Assert.AreNotEqual (with delta)

```text
Assertion failed. Expected values to differ beyond tolerance.

notExpected: 3.14
actual:      3.14
delta:       0.001
```

#### Assert.AreNotEqual (string)

```text
Assertion failed. Expected strings to differ.

notExpected: "hello"
actual:      "hello"
```

#### Assert.AreNotEqual (string, case-insensitive with culture)

```text
Assertion failed. Expected strings to differ (case-insensitive).

notExpected: "Straße"
actual:      "STRASSE"
ignoreCase:  true
culture:     de-DE
```

Note: Under `de-DE` culture with case-insensitive comparison, `"Straße"` and `"STRASSE"` are considered equal (ß expands to SS), so `AreNotEqual` fails.

### Assert — Reference Equality

#### Assert.AreSame (different references)

```text
Assertion failed. Expected both values to refer to the same object.

expected: System.Object (hash: 0x1A2B3C)
actual:   System.Object (hash: 0x4D5E6F)
```

Note: Hash codes are non-deterministic across runs in modern .NET. They are useful for same-run debugging but should not be relied upon for snapshot-based test verification of assertion messages.

#### Assert.AreSame (expected is null)

```text
Assertion failed. Expected both values to refer to the same object.
Expected is null.
```

#### Assert.AreSame (actual is null)

```text
Assertion failed. Expected both values to refer to the same object.
Actual is null.
```

#### Assert.AreSame (both are value types)

```text
Assertion failed. Expected both values to refer to the same object.
Do not pass value types to AreSame — value types are boxed on each call, so references will never be the same.
```

#### Assert.AreNotSame

```text
Assertion failed. Expected values to refer to different objects.
Both variables refer to the same object.
```

### Assert — Boolean

#### Assert.IsTrue

```text
Assertion failed. Expected condition to be true.

expected: true
actual:   false
```

#### Assert.IsTrue (condition is null)

```text
Assertion failed. Expected condition to be true.

expected: true
actual:   (null)
```

#### Assert.IsFalse

```text
Assertion failed. Expected condition to be false.

expected: false
actual:   true
```

#### Assert.IsFalse (condition is null)

```text
Assertion failed. Expected condition to be false.

expected: false
actual:   (null)
```

### Assert — Null

#### Assert.IsNull

```text
Assertion failed. Expected value to be null.

actual: "some value"
```

#### Assert.IsNotNull

```text
Assertion failed. Expected value to not be null.

actual: (null)
```

### Assert — Type Checking

#### Assert.IsInstanceOfType

```text
Assertion failed. Expected value to be an instance of String.

expected type: System.String
actual type:   System.Int32
actual value:  42
```

#### Assert.IsInstanceOfType (value is null)

```text
Assertion failed. Expected value to be an instance of String.

expected type: System.String
actual:        (null)
```

#### Assert.IsNotInstanceOfType

```text
Assertion failed. Expected value to not be an instance of String.

wrong type: System.String
actual:     "hello"
```

#### Assert.IsExactInstanceOfType

```text
Assertion failed. Expected value to be exactly of type ArgumentException.

expected type: System.ArgumentException
actual type:   System.ArgumentNullException
```

#### Assert.IsExactInstanceOfType (value is null)

```text
Assertion failed. Expected value to be exactly of type ArgumentException.

expected type: System.ArgumentException
actual:        (null)
```

#### Assert.IsNotExactInstanceOfType

```text
Assertion failed. Expected value to not be exactly of type String.

wrong type: System.String
actual:     "hello"
```

### Assert — Exceptions

#### Assert.Throws (no exception thrown)

```text
Assertion failed. Expected exception of type ArgumentException (or derived) but no exception was thrown.
```

#### Assert.Throws (wrong exception type)

```text
Assertion failed. Expected exception of type ArgumentException (or derived) but caught InvalidOperationException.

expected type: System.ArgumentException (or derived)
actual type:   System.InvalidOperationException
```

#### Assert.ThrowsExactly (no exception thrown)

```text
Assertion failed. Expected exception of exactly type ArgumentException but no exception was thrown.
```

#### Assert.ThrowsExactly (wrong exception type)

```text
Assertion failed. Expected exception of exactly type ArgumentException but caught ArgumentNullException.

expected type: System.ArgumentException
actual type:   System.ArgumentNullException
```

#### Assert.ThrowsAsync / Assert.ThrowsExactlyAsync

Same message format as their synchronous counterparts.

#### Overloads with `Func<Exception?, string> messageBuilder`

`Throws`, `ThrowsExactly`, `ThrowsAsync`, and `ThrowsExactlyAsync` all have overloads accepting a `Func<Exception?, string> messageBuilder` instead of `string message`. The builder receives the caught exception (or `null` if no exception was thrown) and returns a custom message string. The builder's output is treated identically to a `string message` — it appears in the user message position within the structured format.

### Assert — String Operations

All string assertions that accept a `StringComparison` parameter display the comparison type in the evidence block when a non-default value is used. Examples below show the default (ordinal, case-sensitive) behavior. When `StringComparison.OrdinalIgnoreCase` or another variant is specified, an additional `comparison:` line appears:

```text
comparison: OrdinalIgnoreCase
```

#### Assert.Contains (string)

```text
Assertion failed. Expected string to contain the specified substring.

expected to contain: "world"
actual:              "hello earth"
```

#### Assert.DoesNotContain (string)

```text
Assertion failed. Expected string to not contain the specified substring.

substring: "world"
actual:    "hello world"
```

#### Assert.StartsWith

```text
Assertion failed. Expected string to start with the specified prefix.

expected prefix: "Hello"
actual:          "Goodbye world"
```

#### Assert.DoesNotStartWith

```text
Assertion failed. Expected string to not start with the specified prefix.

prefix: "Hello"
actual: "Hello world"
```

#### Assert.EndsWith

```text
Assertion failed. Expected string to end with the specified suffix.

expected suffix: "world"
actual:          "hello earth"
```

#### Assert.DoesNotEndWith

```text
Assertion failed. Expected string to not end with the specified suffix.

suffix: "world"
actual: "hello world"
```

#### Assert.MatchesRegex

```text
Assertion failed. Expected string to match the specified pattern.

pattern: ^\d{3}-\d{4}$
actual:  "12-3456"
```

#### Assert.DoesNotMatchRegex

```text
Assertion failed. Expected string to not match the specified pattern.

pattern: ^\d{3}-\d{4}$
actual:  "123-4567"
```

### Assert — Collection Operations

#### Assert.Contains (item in collection)

```text
Assertion failed. Expected collection to contain the specified element.

expected to contain: "banana"
collection:          ["apple", "cherry", "date"]
```

#### Assert.Contains (predicate)

```text
Assertion failed. Expected collection to contain an element matching the predicate.

predicate:  x => x.StartsWith("b")
collection: ["apple", "cherry", "date"]
```

#### Assert.DoesNotContain (item)

```text
Assertion failed. Expected collection to not contain the specified element.

element:    "apple"
collection: ["apple", "cherry", "date"]
```

#### Assert.DoesNotContain (predicate)

```text
Assertion failed. Expected no element in the collection to match the predicate.

predicate:  x => x.StartsWith("a")
collection: ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle

```text
Assertion failed. Expected collection to contain exactly one element but found 3.

expected count: 1
actual count:   3
collection:     ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle (predicate, none match)

```text
Assertion failed. Expected exactly one element to match the predicate but found 0.

predicate:   x => x.StartsWith("z")
match count: 0
collection:  ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle (predicate, multiple match)

```text
Assertion failed. Expected exactly one element to match the predicate but found 2.

predicate:   x => x.Length == 5
match count: 2
collection:  ["apple", "cherry", "date"]
```

#### Assert.HasCount

```text
Assertion failed. Expected collection to have 5 element(s) but found 3.

expected count: 5
actual count:   3
```

#### Assert.IsEmpty

```text
Assertion failed. Expected collection to be empty but found 3 element(s).

expected count: 0
actual count:   3
```

#### Assert.IsNotEmpty

```text
Assertion failed. Expected collection to not be empty.

actual count: 0
```

### Assert — Comparison (IComparable)

#### Assert.IsInRange

```text
Assertion failed. Expected value to be in range [5, 10].

value:    3
minValue: 5
maxValue: 10
```

#### Assert.IsGreaterThan

```text
Assertion failed. Expected value to be greater than the lower bound.

lowerBound: 10
actual:     7
```

#### Assert.IsGreaterThanOrEqualTo

```text
Assertion failed. Expected value to be greater than or equal to the lower bound.

lowerBound: 10
actual:     7
```

#### Assert.IsLessThan

```text
Assertion failed. Expected value to be less than the upper bound.

upperBound: 5
actual:     7
```

#### Assert.IsLessThanOrEqualTo

```text
Assertion failed. Expected value to be less than or equal to the upper bound.

upperBound: 5
actual:     7
```

#### Assert.IsPositive

```text
Assertion failed. Expected value to be positive.

actual: -3
```

#### Assert.IsNegative

```text
Assertion failed. Expected value to be negative.

actual: 7
```

### Assert — Other

#### Assert.Fail

```text
Assertion failed.
Order processing should not reach this branch
```

When no user message is provided:

```text
Assertion failed.
```

Note: `Assert.Fail` has no summary line beyond the prefix — the user message (if any) is the entire content. Because there is no evidence block, the blank-line separator between narrative and evidence is omitted. When no user message is provided, the output is simply `Assertion failed.`

#### Assert.Inconclusive

```text
Assert.Inconclusive. Database server not available for integration tests.
```

Note: `Assert.Inconclusive` throws `AssertInconclusiveException` (not `AssertFailedException`) and uses a distinct prefix. It is intentionally excluded from the universal `Assertion failed.` prefix because an inconclusive result is not a failure — it signals that the test could not be run to completion.

#### Assert.That

```text
Assertion failed. Expected condition to be true.

condition: order.Total > 0
values:
  order.Total = -5

Assert.That(() => order.Total > 0)
```

Note: `Assert.That` is the singleton property on `Assert`, and the `That(Expression<Func<bool>>)` method is a C# extension method on the `Assert` type. The call-site expression captured by `CallerArgumentExpression` is the lambda argument (e.g. `() => order.Total > 0`), not the full invocation. The `Assert.That.That(...)` form shown above reflects the actual call syntax. `Assert.That` uses expression tree analysis to provide a detailed breakdown of the evaluated sub-expressions.

### CollectionAssert (legacy)

The `CollectionAssert` class predates the modern `Assert.Contains`/`Assert.HasCount` APIs and does **not** use `CallerArgumentExpression`. Its messages follow the same structured format but without a call-site expression line.

#### CollectionAssert.Contains

```text
Assertion failed. Expected collection to contain the specified element.
```

#### CollectionAssert.DoesNotContain

```text
Assertion failed. Expected collection to not contain the specified element.
```

#### CollectionAssert.AllItemsAreNotNull

```text
Assertion failed. Expected all items in the collection to be non-null.
Found null element at index 2.
```

#### CollectionAssert.AllItemsAreUnique

```text
Assertion failed. Expected all items in the collection to be unique.
Duplicate element found: "apple".
```

#### CollectionAssert.AllItemsAreInstancesOfType

```text
Assertion failed. Expected all items in the collection to be instances of String.
Element at index 2 is of type Int32.

expected type: System.String
actual type:   System.Int32 (at index 2)
```

#### CollectionAssert.IsSubsetOf

```text
Assertion failed. Expected collection to be a subset of the specified collection.
```

#### CollectionAssert.IsNotSubsetOf

```text
Assertion failed. Expected collection to not be a subset of the specified collection.
```

#### CollectionAssert.AreEquivalent

```text
Assertion failed. Expected collections to contain the same elements regardless of order.
Missing 2 element(s) from actual. Found 1 unexpected element(s).

missing:    ["cherry", "date"]
unexpected: ["fig"]
```

#### CollectionAssert.AreNotEquivalent

```text
Assertion failed. Expected collections to not contain the same elements.
```

#### CollectionAssert.AreEqual

```text
Assertion failed. Expected collections to be equal (same elements in same order).
Collections have 5 element(s). 2 element(s) differ. First difference at index 2.

expected[2]: "cherry"
actual[2]:   "date"
```

#### CollectionAssert.AreNotEqual

```text
Assertion failed. Expected collections to differ.
```

### StringAssert (legacy)

The `StringAssert` class predates the modern `Assert.Contains`/`Assert.StartsWith` APIs and does **not** use `CallerArgumentExpression`. Its messages follow the same structured format but without a call-site expression line.

#### StringAssert.Contains

```text
Assertion failed. Expected string to contain the specified substring.

expected to contain: "world"
actual:              "hello earth"
```

#### StringAssert.StartsWith

```text
Assertion failed. Expected string to start with the specified substring.

expected prefix: "Hello"
actual:          "Goodbye world"
```

Note: `StringAssert.StartsWith` uses the parameter name `substring` in its API, but the label uses `expected prefix:` for clarity since the semantic role is a prefix check. This matches the label convention in the modern `Assert.StartsWith`.

#### StringAssert.EndsWith

```text
Assertion failed. Expected string to end with the specified substring.

expected suffix: "world"
actual:          "hello earth"
```

Note: Same convention as `StringAssert.StartsWith` — the API parameter is `substring` but the label uses `expected suffix:` for clarity.

#### StringAssert.Matches

```text
Assertion failed. Expected string to match the specified pattern.

pattern: ^\d{3}-\d{4}$
actual:  "12-3456"
```

#### StringAssert.DoesNotMatch

```text
Assertion failed. Expected string to not match the specified pattern.

pattern: ^\d{3}-\d{4}$
actual:  "123-4567"
```

## Open Question: User Message Placement

There is an inherent tension in where the user-provided message should appear. This is the **primary open question** of this RFC and should be resolved before implementation.

### Option A — User message after summary (proposed above)

```text
Assertion failed. Expected values to be equal.
Discount should be applied after tax        ← user message

expected: 42
actual:   37
```

**Arguments for:**

- ~20 internal developers polled preferred user message before framework details.
- The user message carries *intent* — it explains *why* the developer wrote the assertion, which is arguably more important than the framework's description of *what* went wrong.
- Other assertion libraries (NUnit) put user messages prominently.

**Arguments against:**

- In CI logs with many failures, the user message line has no stable prefix, making it harder to visually find where each assertion starts and ends.
- Developers who don't use custom messages (common) would see no difference — but those who do may find the extra line between the "what" and "evidence" sections disruptive.

### Option B — User message after values

```text
Assertion failed. Expected values to be equal.

expected: 42
actual:   37

Discount should be applied after tax        ← user message
```

**Arguments for:**

- The core technical information (summary + values) is together, uninterrupted.
- The user message serves as *commentary* on the evidence, which reads naturally after the evidence.
- The visual flow is: *what failed* → *the proof* → *the developer's context* → *where in code*.

**Arguments against:**

- User message is further from the top, which can feel like it's deprioritized.
- If values are very long, the user message may be pushed far down and be easy to miss.

### Option C — User message on line 1 (before the prefix)

```text
Discount should be applied after tax        ← user message
Assertion failed. Expected values to be equal.

expected: 42
actual:   37
```

**Arguments for:**

- Gives user message maximum visual priority — it's the very first thing you read.
- Mirrors how some developers think: "what was I checking?" before "what went wrong?".

**Arguments against:**

- Breaks the grep anchor — `Assertion failed.` is no longer always on line 1.
- Makes it harder to visually scan where assertion boundaries are in a multi-failure log.
- When no user message is provided, line 1 changes identity, which is inconsistent.

### Recommendation

This RFC proposes **Option A** as the default. The user message appears after the summary but before the values, giving it high prominence while preserving the grep-friendly prefix on line 1. However, this is explicitly an open question and feedback is welcome.

## Diff Diagnostics

When comparing strings or collections, the summary line provides a concise diff diagnostic. The guiding principle is: **first difference index + total count** — enough to understand the scope of the problem without overwhelming the output.

### String Diff Rules

| Scenario | Summary line |
| -------- | ------------ |
| Same length, 1 difference | `Strings have same length (N) but differ at 1 location(s). First difference at index I.` |
| Same length, multiple differences | `Strings have same length (N) but differ at K location(s). First difference at index I.` |
| Different lengths | `Strings have different lengths (expected: N, actual: M) and differ at K location(s). First difference at index I.` |
| Substantially different (>50% chars differ) | `Strings are substantially different (expected length: N, actual length: M).` |

The diff count tells the developer whether this is a typo (1 location) or a fundamentally different string (many locations) — avoiding the fix-and-rerun loop for multi-location diffs, while not listing every index when the strings are completely different.

### Collection Diff Rules

| Scenario | Summary line |
| -------- | ------------ |
| Same length, elements differ | `Collections have N element(s). K element(s) differ. First difference at index I.` |
| Different lengths | `Collections have different lengths (expected: N, actual: M).` |
| Different lengths + diffs | `Collections have different lengths (expected: N, actual: M). First difference at index I.` |
| Equivalence (unordered, missing/extra) | `Missing K element(s) from actual. Found J unexpected element(s).` |

For **CollectionAssert.AreEquivalent**, all missing and unexpected elements are listed in the evidence block because the developer needs the full picture to fix set-difference issues:

```text
missing:    ["cherry", "date"]
unexpected: ["fig"]
```

For **ordered collection equality** (`CollectionAssert.AreEqual`), only the first differing element is shown in the evidence block (with its index). The total diff count in the summary signals whether more fixes are needed.

## Value Rendering Rules

To ensure consistency across all assertions, values displayed in the evidence block follow these rendering conventions:

| Value | Rendering | Notes |
| ----- | --------- | ----- |
| `null` | `(null)` | Parenthesized to distinguish from the literal string `"null"`. |
| Empty string `""` | `""` | Shown as quoted empty string. |
| Whitespace-only string | `"   "` | Quoted, so whitespace is visible. |
| Strings | `"value"` | Always quoted with double quotes to delimit boundaries. |
| Strings with embedded quotes | `"she said \"hello\""` | Internal double quotes are backslash-escaped. |
| Strings with control characters | `"line1\nline2\ttab"` | Control characters rendered as C#-style escape sequences (`\n`, `\r`, `\t`, `\0`). Other non-printable characters rendered as `\uXXXX`. |
| Numeric types | `42`, `3.14`, `-7` | Default `ToString()` formatting. No quotes. |
| Boolean | `true`, `false` | Lowercase, no quotes. |
| Types | `System.String` | Fully qualified type name in evidence blocks. Short name (`String`) in summary lines for readability. No quotes. |
| Collections | `["a", "b", "c"]` | JSON-style array notation. Elements follow the same rendering rules recursively. |
| Empty collections | `[]` | Empty brackets. |
| Objects | `ToString()` result | If `ToString()` is not overridden, the fully qualified type name is shown. |

## Value Truncation

Values should be displayed in full whenever practical. Truncation should be a last resort, not a layout workaround. When truncation *is* necessary (e.g. a 10 MB string), the following rules apply:

1. Truncation is indicated by `...` at the point of truncation.
2. The maximum displayed length is configurable (default TBD, suggested: 1024 characters).
3. For strings, truncation preserves context around the first point of difference when applicable.
4. The full value length is noted in the summary line (e.g. `Strings have different lengths (expected: 50000, actual: 50001)`).

### Collection Truncation

Collections follow similar truncation rules:

1. A maximum number of displayed elements is configurable (default TBD, suggested: 32 elements).
2. Truncation is indicated by `... (N more)` at the end: `["a", "b", "c", ... (97 more)]`.
3. For ordered collection comparisons, elements around the first point of difference are prioritized when truncating.
4. For equivalence comparisons (`AreEquivalent`), the `missing:` and `unexpected:` lists are each independently truncated.
5. Nested collections are rendered recursively but capped at a total character budget to prevent unbounded output.

## Newline Handling

All newlines within the message (between the assertion prefix and the stack trace) use `Environment.NewLine` to ensure consistent rendering across platforms. The test runner is responsible for the stack trace formatting.

## Backward Compatibility

This is a **breaking change** for anyone who parses assertion messages as structured data (e.g. regex-based log parsers). The `AssertFailedException.Message` property will contain the new multi-line format.

Mitigation:

- The change ships in a new major version (MSTest v4).
- The assertion prefix line (`Assertion failed.`) is preserved and can still serve as a parsing anchor.

## Unresolved Questions

1. **User message placement** — See the "Open Question" section above. Needs broader feedback.
2. **Maximum truncation length** — What should the default be? 512? 1024? 4096?
3. **Diff rendering for strings** — Should we include an inline diff (e.g. `^` caret under the first differing character)? This would be valuable but adds complexity.
4. **Collection rendering limits** — How many elements of a collection should be shown before truncating? Should we show elements around the point of failure? Proposed: 32 elements.
5. **Structured data for tooling** — Should `AssertFailedException` carry structured properties (e.g. `Expected`, `Actual`) in addition to the formatted message, to enable richer IDE/tooling integration without parsing?
6. **"Substantially different" threshold** — At what percentage of differing characters should strings be considered "substantially different" and the per-index summary be dropped? Proposed: 50%.
7. **Custom comparer display** — Several assertions accept `IEqualityComparer<T>` or `IComparer`. Should the comparer type name be shown in the evidence block (e.g. `comparer: MyCustomComparer`) to help diagnose unexpected comparison results?
8. **Framework diagnostic vs user message ambiguity** — The framework’s multi-line summary diagnostics and the developer’s user message are both displayed as plain text on consecutive lines with no distinguishing prefix. Should user messages be visually distinguished (e.g. with a label, indentation, or quotation marks) to avoid confusion?
9. **`AreEqual<T>` where `T` is `string`** — When the generic `AreEqual<T>` overload is called with `T = string` (without `ignoreCase`/`culture` parameters), should the message use the generic format (`"Expected values to be equal."`) or auto-detect the string type and use the string-specific format (`"Expected strings to be equal (case-sensitive)."`)? Proposed: use the generic format, since the caller chose the generic overload.
