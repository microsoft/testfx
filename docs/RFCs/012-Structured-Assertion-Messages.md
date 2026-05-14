# RFC 012 - Structured Assertion Messages

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

The first line starts with the universal prefix `Assertion failed.` followed by a human-readable summary of the failure on the same line:

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
| `AreEqual` (string) | `Assertion failed. Expected strings to be equal.` |
| `IsTrue` | `Assertion failed. Expected condition to be true.` |
| `IsNull` | `Assertion failed. Expected value to be null.` |
| `IsInstanceOfType` | `Assertion failed. Expected value to be of type String (or derived).` |
| `ThrowsExactly` | `Assertion failed. Expected exception of exact type ArgumentException but no exception was thrown.` |
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
expected:     42
actual:       37
ignore case:  true
culture:      tr-TR
```

In this example, `expected:` and `actual:` are the core value labels, while `ignore case:` and `culture:` are assertion-specific details. All labels share a single alignment column within the block.

Note: Alignment is applied **within each evidence block as a whole**. All labels in the block (value labels like `expected:` / `actual:` and detail labels like `ignore case:` / `culture:`) are padded to match the longest label in the block. This means values and details share a single alignment column.

When an assertion accepts `IEqualityComparer<T>` or `IComparer` and a non-default comparer is provided, the comparer type name is shown in the evidence block to help diagnose unexpected comparison results:

```text
expected: "Straße"
actual:   "STRASSE"
comparer: CaseInsensitiveComparer
```

The comparer line uses the short type name (`GetType().Name`) since comparer types are typically user-defined with meaningful names. The comparer line is omitted when the default comparer is used (e.g. `EqualityComparer<T>.Default`).

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
Assert.AreEqual(expectedPrice, actualPrice, <delta>)
```

The `<param>` placeholders indicate parameters that were passed but are not captured by `CallerArgumentExpression`. The placeholder uses the parameter name (e.g. `<delta>`) to help identify which argument occupies that position. The omitted parameters' values are shown in the assertion-specific details section when relevant.

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

**Approach 2 — Omit the multiline argument and show `<param>`:** Detect that the expression contains newlines and replace it with a `<param>` placeholder using the parameter name:

```text
Assert.AreEqual(<expected>, actualJson)
```

**Recommendation:** Use Approach 2 (omit with `<param>`) when the captured expression contains newlines. The full value is already displayed in the `expected:` / `actual:` lines, so repeating it in the call-site is redundant. The call-site should identify *where* the call happened, not *what* the values were.

#### Unavailable expressions

If `CallerArgumentExpression` data is unavailable or empty (e.g. when using an older compiler, indirect calls through helpers, or reflection-based invocations), the call-site line is omitted entirely. The stack trace still provides the location.

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
Assertion failed. Expected strings to be equal.
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

actual: false

Assert.IsTrue(order.IsValid)
   at MyTests.OrderTests.ValidOrder_ShouldBeValid() in OrderTests.cs:line 30
```

### Assert.ThrowsExactly (no exception thrown)

```text
Assertion failed. Expected exception of exact type ArgumentException but no exception was thrown.

Assert.ThrowsExactly<ArgumentException>(() => Validate(input))
   at MyTests.ValidationTests.InvalidInput_ShouldThrow() in ValidationTests.cs:line 18
```

### Assert.ThrowsExactly (wrong exception type)

```text
Assertion failed. Expected exception of exact type ArgumentException but caught InvalidOperationException.

expected type:    System.ArgumentException
actual type:      System.InvalidOperationException
actual exception: System.InvalidOperationException: Operation is not valid due to the current state of the object.

Assert.ThrowsExactly<ArgumentException>(() => Validate(input))
   at MyTests.ValidationTests.InvalidInput_ShouldThrow() in ValidationTests.cs:line 18
```

### Assert.AreEqual (large strings)

```text
Assertion failed. Expected strings to be equal.
Strings have different lengths (expected: 50000, actual: 49997) and differ at 1 location(s). First difference at index 1042.

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
actual:              ["apple", "cherry", "date"]

Assert.Contains("banana", fruits)
   at MyTests.FruitTests.ShouldIncludeBanana() in FruitTests.cs:line 12
```

### Assert.AreEqual (with delta — uncaptured parameter)

Source:

```csharp
Assert.AreEqual(expectedPrice, actualPrice, 0.01);
```

Output — `delta` is not captured by `CallerArgumentExpression`, so the call-site shows `<delta>`:

```text
Assertion failed. Expected values to be equal within tolerance.

expected: 95.00
actual:   100.00
delta:    0.01

Assert.AreEqual(expectedPrice, actualPrice, <delta>)
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

Output — the multiline expression is replaced with `<expected>` in the call-site:

```text
Assertion failed. Expected strings to be equal.
Strings differ at 1 location(s). First difference at index 22.

expected: "{\n  \"name\": \"Alice\",\n  \"age\": 30\n}"
actual:   "{\n  \"name\": \"Alice\",\n  \"age\": 31\n}"

Assert.AreEqual(<expected>, actualJson)
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

Note: When the generic `AreEqual<T>` overload is called with `T = string` (without `ignoreCase`/`culture` parameters), the message **auto-detects the string type** and uses the string-specific format (`"Expected strings to be equal."`) with full string diff diagnostics. The generic overload defaults to ordinal equality (`EqualityComparer<string>.Default`), which is case-sensitive. Developers writing `Assert.AreEqual("expected", actual)` get string diagnostics without needing to know about the string-specific overload.

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
Assertion failed. Expected strings to be equal.
Strings have same length (11) but differ at 1 location(s). First difference at index 7.

expected: "hello world"
actual:   "hello wrold"
```

#### Assert.AreEqual (string, case-insensitive with culture)

```text
Assertion failed. Expected strings to be equal (case-insensitive).
Strings have different lengths (expected: 6, actual: 8) and differ at 1 location(s). First difference at index 6.

expected:     "straße"
actual:       "STRASSE!"
ignore case:  true
culture:      de-DE
```

Note: Under `de-DE` culture with case-insensitive comparison, `"straße"` and `"STRASSE"` are considered equal (ß expands to SS). The example above shows a genuinely failing comparison where the actual string has additional content beyond the case-equivalent portion.

#### Assert.AreNotEqual (generic)

```text
Assertion failed. Expected values to differ.

not expected: 42
actual:       42
```

#### Assert.AreNotEqual (with delta)

```text
Assertion failed. Expected values to differ beyond tolerance.

not expected: 3.14
actual:       3.14
delta:        0.001
```

#### Assert.AreNotEqual (string)

```text
Assertion failed. Expected strings to differ.

not expected: "hello"
actual:       "hello"
```

#### Assert.AreNotEqual (string, case-insensitive with culture)

```text
Assertion failed. Expected strings to differ (case-insensitive).

not expected: "Straße"
actual:       "STRASSE"
ignore case:  true
culture:      de-DE
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

expected: null
actual:   System.Object (hash: 0x4D5E6F)
```

#### Assert.AreSame (actual is null)

```text
Assertion failed. Expected both values to refer to the same object.

expected: System.Object (hash: 0x1A2B3C)
actual:   null
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

actual: false
```

#### Assert.IsTrue (condition is null)

```text
Assertion failed. Expected condition to be true.

actual: null
```

#### Assert.IsFalse

```text
Assertion failed. Expected condition to be false.

actual: true
```

#### Assert.IsFalse (condition is null)

```text
Assertion failed. Expected condition to be false.

actual: null
```

Note: `IsTrue` and `IsFalse` omit the `expected:` line because the expected value is inherent in the assertion name. The same convention applies to all assertions whose name fully implies the expected value (`IsNull`, `IsNotNull`, `IsEmpty`, `IsNotEmpty`, `IsPositive`, `IsNegative`).

### Assert — Null

#### Assert.IsNull

```text
Assertion failed. Expected value to be null.

actual: "some value"
```

#### Assert.IsNotNull

```text
Assertion failed. Expected value to not be null.
```

Note: `IsNotNull` omits the evidence block entirely because when this assertion fails, `actual` is always `null` — the evidence block provides no new information beyond what the summary already conveys. The same reasoning applies to `Assert.AreNotSame`.

### Assert — Type Checking

#### Assert.IsInstanceOfType

```text
Assertion failed. Expected value to be of type String (or derived).

expected type: System.String (or derived)
actual type:   System.Int32
actual value:  42
```

#### Assert.IsInstanceOfType (value is null)

```text
Assertion failed. Expected value to be of type String (or derived).

expected type: System.String (or derived)
actual:        null
```

#### Assert.IsNotInstanceOfType

```text
Assertion failed. Expected value to not be of type String (or derived).

not expected type: System.String (or derived)
actual value:      "hello"
```

#### Assert.IsExactInstanceOfType

```text
Assertion failed. Expected value to be exactly of type ArgumentException.

expected type: System.ArgumentException
actual type:   System.ArgumentNullException
actual value:  System.ArgumentNullException: Value cannot be null.
```

#### Assert.IsExactInstanceOfType (value is null)

```text
Assertion failed. Expected value to be exactly of type ArgumentException.

expected type: System.ArgumentException
actual:        null
```

#### Assert.IsNotExactInstanceOfType

```text
Assertion failed. Expected value to not be exactly of type String.

not expected type: System.String
actual value:      "hello"
```

### Assert — Exceptions

#### Assert.Throws (no exception thrown)

```text
Assertion failed. Expected exception of type ArgumentException (or derived) but no exception was thrown.
```

#### Assert.Throws (wrong exception type)

```text
Assertion failed. Expected exception of type ArgumentException (or derived) but caught InvalidOperationException.

expected type:    System.ArgumentException (or derived)
actual type:      System.InvalidOperationException
actual exception: System.InvalidOperationException: Operation is not valid due to the current state of the object.
```

#### Assert.ThrowsExactly (no exception thrown)

```text
Assertion failed. Expected exception of exact type ArgumentException but no exception was thrown.
```

#### Assert.ThrowsExactly (wrong exception type)

```text
Assertion failed. Expected exception of exact type ArgumentException but caught ArgumentNullException.

expected type:    System.ArgumentException
actual type:      System.ArgumentNullException
actual exception: System.ArgumentNullException: Value cannot be null.
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
actual:              ["apple", "cherry", "date"]
```

#### Assert.Contains (predicate)

```text
Assertion failed. Expected collection to contain an element matching the predicate.

predicate: x => x.StartsWith("b")
actual:    ["apple", "cherry", "date"]
```

#### Assert.DoesNotContain (item)

```text
Assertion failed. Expected collection to not contain the specified element.

element: "apple"
actual:  ["apple", "cherry", "date"]
```

#### Assert.DoesNotContain (predicate)

```text
Assertion failed. Expected no element in the collection to match the predicate.

predicate: x => x.StartsWith("a")
actual:    ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle

```text
Assertion failed. Expected collection to contain exactly one element but found 3.

expected count: 1
actual count:   3
actual:         ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle (predicate, none match)

```text
Assertion failed. Expected exactly one element to match the predicate but found 0.

predicate:   x => x.StartsWith("z")
match count: 0
actual:      ["apple", "cherry", "date"]
```

#### Assert.ContainsSingle (predicate, multiple match)

```text
Assertion failed. Expected exactly one element to match the predicate but found 2.

predicate:   x => x.Length == 5
match count: 2
actual:      ["apple", "cherry", "date"]
```

#### Assert.HasCount

```text
Assertion failed. Expected collection to have 5 element(s) but found 3.

expected count: 5
actual count:   3
actual:         ["apple", "cherry", "date"]
```

#### Assert.IsEmpty

```text
Assertion failed. Expected collection to be empty but found 3 element(s).

actual count: 3
actual:       ["apple", "cherry", "date"]
```

#### Assert.IsNotEmpty

```text
Assertion failed. Expected collection to not be empty.

actual count: 0
```

### Assert — Comparison (IComparable)

#### Assert.IsInRange

```text
Assertion failed. Expected value to be in range 5..10.

min value: 5
max value: 10
actual:    3
```

Note: The labels `min value:` and `max value:` map directly to the API parameter names `minValue` and `maxValue`. This differs from `IsGreaterThan` / `IsLessThan` which use `lower bound:` / `upper bound:` because those assertions have `lowerBound` / `upperBound` parameters. All IComparable assertion labels follow their respective parameter names for consistency with the API.

#### Assert.IsGreaterThan

```text
Assertion failed. Expected value to be greater than the lower bound.

lower bound: 10
actual:      7
```

#### Assert.IsGreaterThanOrEqualTo

```text
Assertion failed. Expected value to be greater than or equal to the lower bound.

lower bound: 10
actual:      7
```

#### Assert.IsLessThan

```text
Assertion failed. Expected value to be less than the upper bound.

upper bound: 5
actual:      7
```

#### Assert.IsLessThanOrEqualTo

```text
Assertion failed. Expected value to be less than or equal to the upper bound.

upper bound: 5
actual:      7
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

Note: Today, `Assert.Inconclusive` uses the same `"{0} failed. {1}"` format as other assertions (producing `Assert.Inconclusive failed. <message>`). This RFC proposes dropping the word "failed" for inconclusive results, since an inconclusive outcome is not a failure — it signals that the test could not be run to completion. This is a **breaking change** to the `AssertInconclusiveException.Message` format and should be listed alongside the other backward-compatibility notes. `Assert.Inconclusive` throws `AssertInconclusiveException` (not `AssertFailedException`) and is intentionally excluded from the universal `Assertion failed.` prefix.

#### Assert.That

```text
Assertion failed. Expected condition to be true.

condition:   order.Total > 0
order.Total: -5

Assert.That(() => order.Total > 0)
```

Note: `Assert.That` is an extension method on the `Assert` type, added via C# 14's `extension(Assert _)` syntax. The call syntax is `Assert.That(() => order.Total > 0)`. The call-site expression captured by `CallerArgumentExpression` is the lambda argument (e.g. `() => order.Total > 0`), not the full invocation. `Assert.That` uses expression tree analysis to provide a detailed breakdown of the evaluated sub-expressions.

### Scope: Legacy Assertion Classes

The `CollectionAssert` and `StringAssert` classes are considered legacy. All `StringAssert` operations already have modern `Assert` equivalents (`Assert.Contains`, `Assert.StartsWith`, `Assert.EndsWith`, `Assert.MatchesRegex`, `Assert.DoesNotMatchRegex`) whose messages are documented above. Several `CollectionAssert` operations (`AllItemsAreNotNull`, `AllItemsAreUnique`, `AllItemsAreInstancesOfType`, `IsSubsetOf`, `IsNotSubsetOf`, `AreEquivalent`, `AreNotEquivalent`, ordered `AreEqual`/`AreNotEqual`) do not yet have modern `Assert` equivalents. When those `Assert` methods are introduced, their message catalog entries will be added to this RFC following the same structured format. Until then, `CollectionAssert` and `StringAssert` are out of scope — their messages will be updated to the structured format opportunistically but are not specified here.

## User Message Placement

The user-provided message appears **after the summary line but before the evidence block** (values and details):

```text
Assertion failed. Expected values to be equal.
Discount should be applied after tax

expected: 42
actual:   37
```

This placement was chosen because:

- The user message carries *intent* — it explains *why* the developer wrote the assertion, which is more important than the framework's description of *what* went wrong.
- ~20 internal developers polled preferred user message before framework details.
- Preserves the grep-friendly `Assertion failed.` prefix on line 1.
- Avoids burying the user message after long values (the original problem this RFC aims to solve).
- Other assertion libraries (NUnit, pytest, Rust) that support user messages also place them prominently.

No visual distinction (label, indentation, or quotation marks) is applied to the user message. The framework's summary diagnostics follow a predictable `"Expected [subject] to [verb]."` sentence pattern, while user messages are freeform — ambiguity between the two is rare in practice.

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

The "substantially different" threshold is **50%**, measured by edit distance ratio (`editDistance / max(len1, len2) > 0.5`). For strings shorter than 20 characters, the per-index detail is always shown regardless of percentage — short strings are cheap to diff visually.

Note: Inline diff markers (e.g. `^` caret under the first differing character, or xUnit-style `↓`/`↑` arrows) are intentionally **deferred** to a future enhancement. The first-difference-index in the summary line provides location information, and adding caret markers introduces rendering complexity (alignment with tabs, Unicode, control characters) that is fragile across terminal environments. The structured format makes it straightforward to add diff markers later as an additional line between `expected:` and `actual:` without breaking the format.

### Collection Diff Rules

| Scenario | Summary line |
| -------- | ------------ |
| Same length, elements differ | `Collections have N element(s). K element(s) differ. First difference at index I.` |
| Different lengths | `Collections have different lengths (expected: N, actual: M).` |
| Different lengths + diffs | `Collections have different lengths (expected: N, actual: M). First difference at index I.` |
| Equivalence (unordered, missing/extra) | `Missing K element(s) from actual. Found J unexpected element(s).` |

For **unordered equivalence comparisons**, all missing and unexpected elements are listed in the evidence block because the developer needs the full picture to fix set-difference issues:

```text
missing:    ["cherry", "date"]
unexpected: ["fig"]
```

For **ordered collection equality**, only the first differing element is shown in the evidence block (with its index). The total diff count in the summary signals whether more fixes are needed.

## Value Rendering Rules

To ensure consistency across all assertions, values displayed in the evidence block follow these rendering conventions:

| Value | Rendering | Notes |
| ----- | --------- | ----- |
| `null` | `null` | Unquoted. Unambiguous because strings are always rendered with double quotes (the literal string `"null"` renders as `"null"`). |
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
| Regex patterns | `^\d{3}-\d{4}$` | Pattern text without quotes, regardless of whether the source was a `string` or `Regex` object. Patterns are structural descriptors (like type names), not arbitrary string values. |
| Objects | `ToString()` result | If `ToString()` is not overridden, the fully qualified type name is shown. |

## Value Truncation

Values should be displayed in full whenever practical. Truncation should be a last resort, not a layout workaround. When truncation *is* necessary (e.g. a 10 MB string), the following rules apply:

1. Truncation is indicated by `...` at the point of truncation.
2. The maximum displayed length defaults to **1024 characters**. This fits ~20 lines of 50-character-wide terminal output — enough to show meaningful context around a diff point without flooding CI logs. Configurable via `.runsettings` or `testconfig.json` (see [Configuration](#configuration)).
3. For strings, truncation preserves context around the first point of difference when applicable.
4. The full value length is noted in the summary line (e.g. `Strings have different lengths (expected: 50000, actual: 50001)`).
5. When the diff diagnostic indicates strings are "substantially different" (>50%), truncation is more aggressive since showing 1024 characters of two completely different strings is low-value.

### Collection Truncation

Collections follow similar truncation rules:

1. A maximum of **32 elements** are displayed by default. Configurable via `.runsettings` or `testconfig.json` (see [Configuration](#configuration)).
2. Truncation is indicated by `... (N more)` at the end: `["a", "b", "c", ... (97 more)]`.
3. For ordered collection comparisons, elements around the first point of difference are prioritized when truncating (e.g. 5 elements before and 5 after the diff point, plus head/tail preview).
4. For equivalence comparisons (`AreEquivalent`), the `missing:` and `unexpected:` lists are each independently truncated.
5. Nested collections are rendered recursively but capped at a total character budget to prevent unbounded output.

### Collection Multi-Line Rendering

When the total rendered length of a collection exceeds **120 characters** (a standard terminal width), the collection switches to multi-line rendering with one element per line:

```text
actual:
[
  "a very long element name that takes up space",
  "another lengthy element value here",
  "and a third one"
]
```

Both `expected:` and `actual:` collections use the same rendering style (inline or multi-line) for visual comparison. The rendering style is determined by the longer of the two collections — if either exceeds the 120-character threshold, both are rendered multi-line.

## Newline Handling

All newlines within the message (between the assertion prefix and the stack trace) use `Environment.NewLine` to ensure consistent rendering across platforms. The test runner is responsible for the stack trace formatting.

## Configuration

Truncation limits are configurable via `.runsettings` or `testconfig.json`. Both settings are optional — when omitted, the defaults apply.

### .runsettings

```xml
<RunSettings>
  <MSTest>
    <AssertMessageMaxValueLength>1024</AssertMessageMaxValueLength>
    <AssertMessageMaxCollectionElements>32</AssertMessageMaxCollectionElements>
  </MSTest>
</RunSettings>
```

### testconfig.json

```json
{
  "mstest": {
    "assertMessageMaxValueLength": 1024,
    "assertMessageMaxCollectionElements": 32
  }
}
```

| Setting | Default | Description |
| ------- | ------- | ----------- |
| `AssertMessageMaxValueLength` | 1024 | Maximum number of characters to display for a single value before truncating. |
| `AssertMessageMaxCollectionElements` | 32 | Maximum number of collection elements to display before truncating. |

Additional configuration mechanisms (MSBuild properties, environment variables) can be added based on user feedback.

## Structured Exception Data

`AssertFailedException` exposes structured `ExpectedText` and `ActualText` properties as `string?` in addition to the formatted `Message`:

```csharp
public class AssertFailedException : UnitTestAssertException
{
    public string? ExpectedText { get; }
    public string? ActualText { get; }
}
```

These carry the pre-formatted string representations (the same text that appears in the `expected:` / `actual:` lines of the evidence block). This enables IDEs and tooling to present structured diff views without parsing the `Message` property.

Note: The engine's `AssertFailedException` already stores these values via `ex.Data["assert.expected"]` and `ex.Data["assert.actual"]`. The TestFramework's `AssertFailedException` will expose them as proper typed properties.

When an assertion has no natural expected/actual pair (e.g. `Assert.Fail`, `Assert.Inconclusive`), both properties are `null`. For assertions with only an `actual` value (e.g. `Assert.IsNull`), `ExpectedText` is `null`.

## Evidence Block Internal API

Assertions pass labeled evidence to the message formatter via a structured `EvidenceBlock` type:

```csharp
internal record struct EvidenceLine(string Label, string Value);
internal record struct EvidenceBlock(IReadOnlyList<EvidenceLine> Lines);
```

This gives:

- **Type-safe construction** — assertions build evidence as label/value pairs, not pre-formatted strings.
- **Automatic label alignment** — the formatter computes alignment from the longest label in the block.
- **Extensibility** — third-party assertion authors (when the API is promoted to public) get a clean contract.

The `EvidenceBlock` type is initially `internal`. It will be promoted to `public` when third-party extensibility demand is proven. The formatted `Message` string remains the only public contract for assertion failure output.

## Backward Compatibility

This is a **breaking change** for anyone who parses assertion messages as structured data (e.g. regex-based log parsers). The `AssertFailedException.Message` property will contain the new multi-line format.

Specific breaking changes:

- **Message format**: All assertion messages change from single-line concatenated format to the structured multi-line format described in this RFC.
- **`Assert.Inconclusive`**: The message prefix changes from `Assert.Inconclusive failed. <message>` to `Assert.Inconclusive. <message>` (dropping the word "failed"), since an inconclusive outcome is not a failure. This changes the `AssertInconclusiveException.Message` format.

Mitigation:

- **Assertion messages are not part of the public API contract.** The `Message` property of `AssertFailedException` and `AssertInconclusiveException` is intended for human consumption (test output, logs, IDE display) — not for programmatic parsing. We do not consider assertion message format to be a stable API surface, and changes to message wording, structure, or layout are not treated as breaking changes in the semantic versioning sense. Code that relies on parsing assertion messages is inherently fragile and unsupported.
- As a consequence, these message format changes **do not require a new major version**. They can ship in any MSTest v4.x release.
- These changes happen to coincide with MSTest v4, which already includes other breaking changes, but the message format changes are independent of the major version boundary.
- The assertion prefix line (`Assertion failed.`) is preserved and can still serve as a parsing anchor for consumers that choose to parse messages despite the above.
