# Informal Specification — `CommandLineParseResult.Equals`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Type**: `sealed class CommandLineParseResult(string? toolName, IReadOnlyList<CommandLineParseOption> options, IReadOnlyList<string> errors) : IEquatable<CommandLineParseResult>`
- **Method**: `bool Equals(CommandLineParseResult? other)`
- **Namespace**: `Microsoft.Testing.Platform.CommandLine`
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ParseResult.cs`
- **Phase**: 2 — Informal Spec

---

## Purpose

`CommandLineParseResult.Equals` implements structural equality for the result of parsing a command-line. Two results are equal if and only if they represent the same parsed command line: same tool name, same errors (order-sensitive), and same options with the same arguments (order-sensitive).

The method is used by equality comparisons in tests and validation pipelines that compare parse results to expected values.

---

## Data Model

```
CommandLineParseResult = {
  ToolName   : string? (nullable)
  Options    : IReadOnlyList<CommandLineParseOption>
  Errors     : IReadOnlyList<string>
}

CommandLineParseOption = {
  Name      : string
  Arguments : string[] (array, positional)
}
```

An empty parse result `Empty` is defined as `new(null, [], [])`.

---

## Method Signature

```csharp
public bool Equals(CommandLineParseResult? other)
```

---

## Preconditions

- `this` is a valid, fully-constructed `CommandLineParseResult` (never null, since it is a class).
- `other` may be `null` (nullable parameter).
- For a valid `CommandLineParseResult` instance, both `Options` and `Errors` are expected to be non-null; passing `null` for either is invalid input and not prevented by the primary constructor at runtime.
- For a valid `CommandLineParseOption` instance, `Arguments` is expected to be a non-null `string[]`; passing `null` is invalid input and not prevented by the primary constructor at runtime.

---

## Algorithm (from source)

1. If `other` is `null`, return `false`.
2. If `ReferenceEquals(this, other)`, return `true` (reference equality short-circuit).
3. If `ToolName != other.ToolName`, return `false` (string comparison via `!=`).
4. If `Errors.Count != other.Errors.Count`, return `false`.
5. For each index `i` in `0..<Errors.Count`: if `Errors[i] != other.Errors[i]`, return `false`.
6. If `Options.Count != other.Options.Count`, return `false`.
7. For each index `i` in `0..<Options.Count`:
   - If `Options[i].Name != other.Options[i].Name`, return `false`.
   - If `Options[i].Arguments.Length != other.Options[i].Arguments.Length`, return `false`.
   - For each index `j` in `0..<Options[i].Arguments.Length`: if `Options[i].Arguments[j] != other.Options[i].Arguments[j]`, return `false`.
8. Return `true`.

---

## Postconditions / Properties

### Property Group 1 — Basic Equality Contract

1. **Null-safety**: `x.Equals(null) == false` for all `x`.
2. **Reference-reflexivity**: `x.Equals(x) == true` for all `x`.
3. **Structural-reflexivity**: Two distinct objects with all fields equal compare equal.
4. **Symmetry**: `x.Equals(y) == y.Equals(x)` for all `x, y` (neither is null).
5. **Transitivity**: `x.Equals(y) ∧ y.Equals(z) → x.Equals(z)`.

### Property Group 2 — Extensionality

6. **Full extensionality (→)**: `x.Equals(y) → x.ToolName == y.ToolName ∧ x.Errors.SequenceEqual(y.Errors) ∧ x.Options.Count == y.Options.Count ∧ ∀i. x.Options[i].Name == y.Options[i].Name ∧ x.Options[i].Arguments.SequenceEqual(y.Options[i].Arguments)`
7. **Full extensionality (←)**: The converse: if all fields match component-wise then `x.Equals(y) == true`.

### Property Group 3 — Object Equality Consistency (documented, out-of-scope for Lean)

These properties describe the expected .NET/runtime relationship between the typed `Equals(CommandLineParseResult?)` method and the inherited `object.Equals(object?)` surface. They are kept here for completeness of the equality contract, but the Lean model for this spec reasons only about the typed overload above and does **not** model or prove obligations about the `object.Equals` overload.

8. **`object.Equals` agrees (runtime expectation, not a Lean proof target)**: `x.Equals((object)y) == x.Equals(y)` for `y : CommandLineParseResult`.
9. **`object.Equals(null)` is false (runtime expectation, not a Lean proof target)**: `x.Equals((object?)null) == false`.
### Property Group 4 — Order Sensitivity

10. **Errors are order-sensitive**: Two results differing only in the order of their errors are NOT equal.
    - E.g., `new(null, [], ["a","b"]).Equals(new(null, [], ["b","a"])) == false`.
11. **Options are order-sensitive**: Two results differing only in the order of their options are NOT equal.
    - E.g., `new(null, [opt("x",[]), opt("y",[])], []).Equals(new(null, [opt("y",[]), opt("x",[])], [])) == false`.
12. **Option arguments are order-sensitive**: Within a single option, argument order matters.
    - E.g., `opt("x", ["a","b"]) ≠ opt("x", ["b","a"])`.

### Property Group 5 — Empty value

13. **`Empty` equals itself**: `CommandLineParseResult.Empty.Equals(CommandLineParseResult.Empty) == true`.
14. **`Empty` equals null-tool-name empty result**: `CommandLineParseResult.Empty.Equals(new(null, [], [])) == true`.
15. **`Empty` does not equal a result with a tool name**: `CommandLineParseResult.Empty.Equals(new("tool", [], [])) == false`.
16. **`Empty` does not equal a result with options**: `CommandLineParseResult.Empty.Equals(new(null, [opt("x",[])], [])) == false`.

### Property Group 6 — ToolName comparison

17. **ToolName comparison is ordinal string equality** (C# `!=` on `string`): two results with different tool names (by ordinal case-sensitive comparison) are not equal.
18. **Null tool name equals null tool name**: `new(null, [], []).Equals(new(null, [], [])) == true`.
19. **Null tool name does not equal non-null tool name**: `new(null, [], []).Equals(new("tool", [], [])) == false`.

---

## Edge Cases

1. **`other = null`**: returns `false` immediately — no NullReferenceException.
2. **Empty errors and options**: only ToolName matters; works correctly.
3. **Single-character difference in ToolName**: detected immediately at step 3.
4. **Options with no arguments**: `Arguments.Length == 0` for both sides; inner loop skipped; names must still match.
5. **Options with the same name but different arguments**: returns `false` at argument comparison.
6. **Two `Empty` values**: reference check (`ReferenceEquals`) may or may not fire, but structural check will return `true`.
7. **Case sensitivity**: ToolName and option argument comparisons are case-sensitive (C# `!=` on strings). Option name comparison is also case-sensitive in this method (contrast with `IsOptionSet`, which trims prefix and uses OrdinalIgnoreCase).

---

## Invariants

1. The algorithm is linear in the number of errors plus the total number of option-arguments compared; in the worst case this is **O(E + n·m)**, where E = error count, n = option count, and m = max argument count per option. For typical command lines this is small.
2. The method is **side-effect free**.
3. No exception is thrown for any valid input (null `other` is handled, not thrown).
4. The implementation is **consistent with `GetHashCode`**: equal objects (by `Equals`) will have equal hash codes because equality requires matching `Options` and `Errors`, and `GetHashCode` hashes those values even though it does not include `ToolName`.

---

## Examples

| Expression | Result |
|------------|--------|
| `Empty.Equals(Empty)` | `true` |
| `Empty.Equals(null)` | `false` |
| `Empty.Equals(new(null, [], []))` | `true` |
| `new("tool", [], []).Equals(new("Tool", [], []))` | `false` (case-sensitive) |
| `new(null, [], ["e1"]).Equals(new(null, [], ["e1"]))` | `true` |
| `new(null, [], ["e1","e2"]).Equals(new(null, [], ["e2","e1"]))` | `false` (order matters) |
| `new(null, [opt("x",["a"])], []).Equals(new(null, [opt("x",["a"])], []))` | `true` |
| `new(null, [opt("x",["a"])], []).Equals(new(null, [opt("x",["b"])], []))` | `false` |
| `x.Equals(x)` for any `x` | `true` |

---

## Inferred Design Intent

The equality is **order-preserving structural equality** over the full parse tree. This mirrors the semantics of a command-line invocation: `--foo bar --baz` is semantically different from `--baz --foo bar` even if both produce option sets that "contain" the same options.  

This contrasts with `IsOptionSet` and `TryGetOptionArgumentList`, which are lookup methods that aggregate over all options with a matching name and are case-insensitive. The `Equals` method deliberately models "was the command line *exactly* the same string?", not "do both command lines select the same options?".

---

## Open Questions for Lean Formalisation

1. **Termination**: The algorithm terminates obviously (bounded loops over finite lists). No recursion, so no proof needed.
2. **Equality on `string?`**: In Lean, we model `string?` as `Option String` and null-equality as `Option.beq`. Or we can model `null` as a distinguished sentinel `none`.
3. **`IReadOnlyList<T>` vs `List T`**: Model as Lean's `List` for spec purposes. The actual runtime type (array, list, etc.) is abstracted away.
4. **`string[]` vs `List String`**: Model `string[]` as `List String`.
5. **Case-sensitive vs OrdinalIgnoreCase**: C# `string !=` uses ordinal comparison; model as Lean `String.decEq`. Note the contrast with `IsOptionSet` which uses `OrdinalIgnoreCase`.
6. **Self-equality when `ReferenceEquals`**: In Lean, there is no reference equality — pure structural equality is the natural model. The reference check is an optimisation; proofs should hold without it.

---

## Approximations for Lean Model

- Model `string` as Lean `String` with native `DecidableEq String`.
- Model `string?` as `Option String`; `null` maps to `none`.
- Model `IReadOnlyList<T>` as `List T`.
- Model `string[]` as `List String`.
- Ignore `GetHashCode` (not needed for equality proofs).
- Ignore `object.Equals` overload (adds type-erasure complexity without insight).
- Ignore `==` / `!=` operator semantics; `CommandLineParseResult` does not define custom equality operators in the source.
- The reference-equality short-circuit is invisible to the Lean model; proofs are about value equality only.
