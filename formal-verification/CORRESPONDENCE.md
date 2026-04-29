# Lean–C# Correspondence

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

This document records how each Lean 4 model corresponds to the C# source it is meant to represent, including deliberate approximations and abstractions.

Each section follows a common structure:
- **Type / function mapping** — how C# constructs translate to Lean
- **Deliberate approximations** — what the model omits or simplifies
- **Excluded properties** — what is explicitly out of scope
- **Open questions** — design decisions not yet resolved

---

## Target 1 — `ArgumentArity`

**C# file**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs`
**Lean file** (planned): `formal-verification/lean/FVSquad/ArgumentArity.lean`
**Phase**: 2 (informal spec done; Lean file pending toolchain availability)

### Type Mapping

| C# construct | Lean 4 construct | Notes |
|---|---|---|
| `readonly struct ArgumentArity(int min, int max)` | `structure ArgumentArity where min : Int; max : Int` | `readonly struct` → immutable record; `int` → `Int` |
| `int` (fields `Min`, `Max`) | `Int` (unbounded integers) | Deliberate approximation — see below |
| `int.MaxValue` (2 147 483 647) | `(2147483647 : Int)` | Concrete literal enables `decide` |
| `static readonly ArgumentArity Zero = new(0, 0)` | `def ArgumentArity.zero : ArgumentArity := { min := 0, max := 0 }` | Five predefined constants modelled as `def` |
| `static readonly ArgumentArity ZeroOrOne = new(0, 1)` | `def ArgumentArity.zeroOrOne : ArgumentArity := { min := 0, max := 1 }` | |
| `static readonly ArgumentArity ZeroOrMore = new(0, int.MaxValue)` | `def ArgumentArity.zeroOrMore : ArgumentArity := { min := 0, max := 2147483647 }` | |
| `static readonly ArgumentArity ExactlyOne = new(1, 1)` | `def ArgumentArity.exactlyOne : ArgumentArity := { min := 1, max := 1 }` | |
| `static readonly ArgumentArity OneOrMore = new(1, int.MaxValue)` | `def ArgumentArity.oneOrMore : ArgumentArity := { min := 1, max := 2147483647 }` | |
| `bool Equals(ArgumentArity other) => Min == other.Min && Max == other.Max` | `instance : DecidableEq ArgumentArity` | Structural equality on `(min, max)` pair |
| `static bool operator ==(ArgumentArity left, ArgumentArity right) => left.Equals(right)` | `instance : BEq ArgumentArity` (delegates to `DecidableEq`) | `==` delegates to `Equals` |
| `static bool operator !=(ArgumentArity left, ArgumentArity right) => !(left == right)` | Derived from `BEq` | No separate definition needed |
| `override bool Equals(object? obj) => obj is ArgumentArity a && Equals(a)` | Not modelled | Out of scope (see Excluded Properties) |
| `override int GetHashCode()` | Not modelled | Out of scope (see Excluded Properties) |

### Deliberate Approximations

1. **`Int` instead of `Int32`**: C# `int` is a 32-bit signed integer (`-2147483648` to `2147483647`). The Lean model uses unbounded `Int`. This means the Lean model admits `min` and `max` values outside the C# range. For the five predefined constants (all within `[0, 2147483647]`), this approximation is irrelevant. For the general constructor (which admits any C# `int` values), proofs about the predefined constants remain sound; proofs about arbitrary `ArgumentArity` values are sound within the C# range but may differ outside it.

2. **No runtime exceptions**: Lean does not model runtime exceptions. If the C# code would throw (e.g., due to integer overflow), the Lean model silently succeeds with an `Int` result.

3. **`Int32.MaxValue` as sentinel**: The value `2147483647` is used as an "unbounded" sentinel in the C# design intent. The Lean model treats it as a concrete integer, which is the right approach for `decide`-based proofs.

### Excluded Properties

- **`GetHashCode`**: Not modelled. The hash code is platform-conditional (`HashCode.Combine` on .NET 5+; XOR on older). The hash code is a performance mechanism, not a semantic one — all we need is the contract `a.Equals(b) → a.GetHashCode() == b.GetHashCode()`, which follows from the structural definition of `Equals`.
- **`object.Equals(object?)`**: Not modelled. This overload adds type-erasure complexity without contributing to the core semantic properties.
- **Constructor invariant enforcement**: The C# constructor does not enforce `Min ≤ Max`. The Lean model mirrors this: the `ArgumentArity` structure admits ill-formed values. Well-formedness is a separate predicate (`def ArgumentArity.WellFormed (a : ArgumentArity) : Prop := 0 ≤ a.min ∧ a.min ≤ a.max`).

### Theorem Correspondence

| Informal spec property | Planned Lean theorem |
|---|---|
| `Zero.Min == 0 ∧ Zero.Max == 0` | `theorem zero_spec : ArgumentArity.zero = { min := 0, max := 0 } := by decide` |
| `ZeroOrOne.Min == 0 ∧ ZeroOrOne.Max == 1` | `theorem zeroOrOne_spec : ArgumentArity.zeroOrOne = { min := 0, max := 1 } := by decide` |
| `ZeroOrMore.Min == 0 ∧ ZeroOrMore.Max == Int32.MaxValue` | `theorem zeroOrMore_spec : ArgumentArity.zeroOrMore = { min := 0, max := 2147483647 } := by decide` |
| `ExactlyOne.Min == 1 ∧ ExactlyOne.Max == 1` | `theorem exactlyOne_spec : ArgumentArity.exactlyOne = { min := 1, max := 1 } := by decide` |
| `OneOrMore.Min == 1 ∧ OneOrMore.Max == Int32.MaxValue` | `theorem oneOrMore_spec : ArgumentArity.oneOrMore = { min := 1, max := 2147483647 } := by decide` |
| All predefined constants have `Min ≥ 0` | `theorem predefined_nonneg_min` (by `decide`) |
| All predefined constants have `Min ≤ Max` | `theorem predefined_wellformed` (by `decide`) |
| `Equals` is extensional: `a == b ↔ a.min == b.min ∧ a.max == b.max` | `theorem eq_extensional : ∀ a b : ArgumentArity, a = b ↔ a.min = b.min ∧ a.max = b.max` (by `simp [ArgumentArity.ext_iff]`) |
| All five predefined constants are pairwise distinct | `theorem predefined_distinct` (by `decide`) |

### Open Questions

1. Should the Lean model expose a `WellFormed` predicate, or require well-formedness as a type precondition (subtype `{a : ArgumentArity // a.min ≤ a.max}`)? **Recommendation**: expose as a separate predicate for maximum flexibility; the proofs about the five predefined constants can then cite `WellFormed` without constraining the general type.
2. Is it worth modelling the `int.MaxValue` sentinel as a named constant `ArgumentArity.Unbounded` in Lean? **Recommendation**: yes, to mirror the design intent and make proofs self-documenting.

---

## Target 2 — `CommandLineParser.TryUnescape`

**C# file**: `src/Platform/Microsoft.Testing.Platform/CommandLine/Parser.cs`
**Lean file** (planned): `formal-verification/lean/FVSquad/TryUnescape.lean`
**Phase**: 2 (informal spec in open PR #7833; Lean file pending)

### Type Mapping

| C# construct | Lean 4 construct | Notes |
|---|---|---|
| `static bool TryUnescape(string input, string? option, IEnvironment env, out string? unescapedArg, out string? error)` | `def tryUnescape (input : String) (newLine : String) : Except String String` | Simplified signature — see below |
| Return value `true` + `out string? unescapedArg` | `Except.ok unescapedArg` | Success case |
| Return value `false` + `out string? error` | `Except.error errMsg` | Failure case |
| `IEnvironment environment` (used only for `environment.NewLine`) | `newLine : String` parameter | Only the newline string is needed |
| `string? option` parameter | **dropped** | Affects only error message formatting; no effect on parse result |
| `string.StartsWith('\'')` + `string.EndsWith('\'')` → single-quote mode | `if input.startsWith "'" && input.endsWith "'"` | String prefix/suffix checks |
| `string.StartsWith('"')` + `string.EndsWith('"')` → double-quote mode | `if input.startsWith "\"" && input.endsWith "\""` | |
| Pass-through mode (no matching quotes) | `Except.ok input` | Return input verbatim |
| `input[1..^1]` (strip outer quotes) | `input.extract 1 (input.length - 1)` | Range slicing |
| `input.IndexOf('\'', 1, input.Length - 2)` | `(input.extract 1 (input.length - 1)).contains '\''` | Interior quote search |
| Sequential `string.Replace` calls (double-quote mode) | Sequential `String.replace` calls | Same left-to-right order |
| `\\` → `\` | `String.replace "\\\\" "\\"` | First escape sequence |
| `\"` → `"` | `String.replace "\\\"" "\""` | Second escape sequence |
| `\$` → `$` | `String.replace "\\$" "$"` | Third escape sequence |
| `` \` `` → `` ` `` | `` String.replace "\\`" "`" `` | Fourth escape sequence |
| `\<NewLine>` → `<NewLine>` | `String.replace ("\\" ++ newLine) newLine` | Fifth: platform newline |

### Deliberate Approximations

1. **`option` parameter dropped**: The `option` string appears only in error messages (`string.Format(..., option, ...)`). It has no effect on the boolean return value or the `unescapedArg` content. The Lean model omits it. Proofs about parsing correctness remain complete.

2. **`IEnvironment` abstracted to `newLine : String`**: The `IEnvironment` interface provides `NewLine` (the platform newline string). This is the only member accessed by `TryUnescape`. The Lean model passes `newLine` directly, avoiding the need to model the `IEnvironment` interface. Proofs parameterised over `newLine` hold for any platform.

3. **Error messages not modelled**: The exact content of error strings is not modelled. The model only distinguishes success (`Except.ok`) from failure (`Except.error`). Error message format tests belong in unit tests, not formal proofs.

4. **Exception-throwing behaviour (bugs) treated as unreachable**: The two confirmed bugs (EC-1: length-1 `'`; EC-2: length-1 `"`) cause `ArgumentOutOfRangeException` in the current C# implementation. The Lean model specifies the **intended** behaviour (empty single-quoted string → `""`, or rejection). The Lean model is a **specification**, not a transcription of the buggy code.

5. **`String.Replace` is modelled as sequential single-pass substitution**: Lean's `String.replace` performs a left-to-right single-pass substitution. C#'s `string.Replace` is also left-to-right single-pass. The semantics match for the specific escape sequences used here (no overlap between patterns). This equivalence should be verified if patterns are ever extended.

### Excluded Properties

- **Error message content**: Not modelled (too fragile, not semantically meaningful for proofs).
- **`string.Trim()` call by caller**: `TryUnescape` does not trim; trimming is done by the caller. The Lean model takes the pre-trimmed string.
- **`IEnvironment` beyond `NewLine`**: Other `IEnvironment` members are irrelevant to `TryUnescape`.

### Key Theorems

| Informal spec property | Lean 4 approach |
|---|---|
| Pass-through mode returns input verbatim | `theorem unquoted_passthrough : ¬(input.startsWith "'" && input.endsWith "'") → ... → tryUnescape input nl = Except.ok input` |
| Empty single-quoted string → `""` | `tryUnescape "''" nl = Except.ok ""` (by `decide` or `native_decide`) |
| Single-quoted happy path (no interior `'`) → stripped interior | `theorem sq_happy_path` |
| Double-quoted mode never fails | `theorem dq_never_fails : ∃ s, tryUnescape input nl = Except.ok s` when double-quoted |
| Empty double-quoted string → `""` | `tryUnescape "\"\"" nl = Except.ok ""` (by `decide`) |
| Escape sequences applied left-to-right | `theorem dq_escape_order` |
| Option parameter has no effect on result | Holds by construction (parameter dropped) |

### Open Questions

1. **Length-1 quote inputs**: Should the Lean spec model them as returning `Except.ok ""` (treating them as empty quoted strings) or as `Except.error`? The C# implementation crashes. The informal spec proposes either treatment as preferable to crashing. **Recommendation**: specify `"'"` → `Except.error` (malformed) to distinguish from `"''"` → `Except.ok ""`.
2. **`String.replace` semantics in Lean**: Lean's `String.replace` replaces the **first** occurrence or **all** occurrences? Must verify before writing the implementation model.

---

## Target 3 — `TreeNodeFilter.MatchFilterPattern`

**C# file**: `src/Platform/Microsoft.Testing.Platform/Requests/TreeNodeFilter/TreeNodeFilter.cs`
**Lean file** (planned): `formal-verification/lean/FVSquad/TreeNodeFilter.lean`
**Phase**: 1 (identified; informal spec not yet written)

### Type Mapping (planned)

| C# construct | Lean 4 construct | Notes |
|---|---|---|
| `abstract class FilterExpression` hierarchy | `inductive FilterExpr` | Algebraic data type |
| `ValueExpression` (regex match) | `FilterExpr.leaf (pred : String → Bool)` | Abstract predicate — regex abstracted away |
| `NopExpression` | `FilterExpr.nop` | Always-true leaf |
| `OperatorExpression { Op = Or, SubExpressions }` | `FilterExpr.or (exprs : List FilterExpr)` | `Any` over sub-expressions |
| `OperatorExpression { Op = And, SubExpressions }` | `FilterExpr.and (exprs : List FilterExpr)` | `All` over sub-expressions |
| `OperatorExpression { Op = Not, SubExpressions = [e] }` | `FilterExpr.not (e : FilterExpr)` | Negation (exactly one child) |
| `ValueAndPropertyExpression` | `FilterExpr.withProps (value : FilterExpr) (props : FilterExpr)` | Secondary target — may be deferred |
| `static bool MatchFilterPattern(FilterExpression, string, PropertyBag)` | `def matchFilter (e : FilterExpr) (s : String) : Bool` | `PropertyBag` abstracted — see below |
| `PropertyBag` | `props : String → String → Bool` | Abstract property lookup function |

### Deliberate Approximations

1. **Regex abstracted to `String → Bool` predicate**: `ValueExpression` holds a compiled `Regex`. The Lean model abstracts this to an arbitrary `String → Bool` predicate. Proofs about `MatchFilterPattern`'s Boolean algebra (De Morgan, double negation) hold for any predicate, so the abstraction is sound and yields more general theorems.

2. **`PropertyBag` abstracted**: `PropertyBag` is a heterogeneous property collection. For the Boolean-algebra theorems, only the `MatchProperties` function matters, and it also satisfies the same Boolean laws. Modelling `PropertyBag` as `String → String → Bool` (key-value lookup) is sufficient for the core proofs.

3. **`ValueAndPropertyExpression` deferred**: This construct combines a value filter with a property filter using `&&`. It can be modelled as `FilterExpr.withProps`, but the core Boolean laws (`NopExpression`, De Morgan, double negation) do not require it. It will be added in a later phase.

4. **Empty `SubExpressions` lists**: In practice, `Or` and `And` nodes always have ≥ 2 children (the parser enforces this). The Lean model admits empty lists, where `or [] s = false` (vacuously: `Any` over empty list) and `and [] s = true` (vacuously: `All` over empty list). This is the standard convention and does not affect real proofs.

### Key Theorems

| Boolean law | Lean 4 approach |
|---|---|
| `NopExpression` is always `true` | `theorem nop_true : matchFilter FilterExpr.nop s = true` (by `rfl`) |
| De Morgan (Or/Not → And/Not) | `theorem de_morgan_or : ∀ exprs s, matchFilter (not (or exprs)) s = matchFilter (and (exprs.map not)) s` |
| De Morgan (And/Not → Or/Not) | `theorem de_morgan_and : ∀ exprs s, matchFilter (not (and exprs)) s = matchFilter (or (exprs.map not)) s` |
| Double negation | `theorem double_neg : ∀ e s, matchFilter (not (not e)) s = matchFilter e s` (by `simp [matchFilter, Bool.not_not]`) |
| `And [e]` = `e` | `theorem and_singleton : matchFilter (and [e]) s = matchFilter e s` |
| `Or [e]` = `e` | `theorem or_singleton : matchFilter (or [e]) s = matchFilter e s` |
| `And [nop, e]` = `And [e]` | `theorem and_nop_absorb` |
| Commutativity of `And [a, b]` | `theorem and_comm : matchFilter (and [a, b]) s = matchFilter (and [b, a]) s` (by `simp [Bool.and_comm]`) |
| Commutativity of `Or [a, b]` | `theorem or_comm : matchFilter (or [a, b]) s = matchFilter (or [b, a]) s` (by `simp [Bool.or_comm]`) |

---

## Remaining Targets (Phases 1)

The following targets are identified but informal specs are not yet written. Correspondence notes will be added once informal specs are extracted.

| Target | C# type/function | Lean type planned |
|---|---|---|
| `ResponseFileHelper.SplitCommandLine` | `static string[] SplitCommandLine(string)` | `def splitCommandLine (s : String) : List String` |
| `CommandLineParser.ParseOptionAndSeparators` | `static void ParseOptionAndSeparators(string, out string?, out string?)` | `def parseOptionAndSeparators (s : String) : String × Option String` |
| `CommandLineOptionsValidator` arity validation | `static void ValidateOptionsArgumentArity(...)` | `def validateArity (arity : ArgumentArity) (count : Nat) : Option String` |
| `CommandLineParseResult.Equals` | `override bool Equals(object? obj)` | Equivalence-relation instance on `CommandLineParseResult` |

---

## General Correspondence Conventions

The following conventions apply across all targets in this project:

| C# concept | Lean 4 modelling convention |
|---|---|
| `string` | `String` |
| `int` (32-bit signed) | `Int` (unbounded) — approximation; `Int32` or `BitVec 32` if overflow matters |
| `bool` | `Bool` |
| `null` / `string?` | `Option String` |
| `(bool result, out T? value, out string? error)` return pattern | `Except String T` (sum type) |
| `IEquatable<T>` | `instance : DecidableEq T` |
| `static readonly` constants | `def` |
| `abstract class` hierarchy | `inductive` type |
| `Regex.IsMatch` | `String → Bool` predicate (abstracted) |
| `Exception`-throwing behaviour | Excluded from spec (bugs noted separately) |
| Platform-conditional `#if NET` code | Model the .NET 5+ path; note the conditional |
