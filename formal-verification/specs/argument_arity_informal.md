# Informal Specification — `ArgumentArity`

> 🔬 **Lean Squad** — auto-generated and maintained by the Lean Squad FV agent.

## Target

- **Type**: `readonly struct ArgumentArity(int min, int max)`
- **Namespace**: `Microsoft.Testing.Platform.Extensions.CommandLine`
- **File**: `src/Platform/Microsoft.Testing.Platform/CommandLine/ArgumentArity.cs`
- **Phase**: 2 — Informal Spec
- **Reference**: [System.CommandLine argument arity docs](https://learn.microsoft.com/dotnet/standard/commandline/syntax#argument-arity)

---

## Purpose

`ArgumentArity` is a value type that describes the number of arguments a command-line option is allowed to accept. It stores a minimum (`Min`) and a maximum (`Max`) integer bound. The validator (`CommandLineOptionsValidator.ValidateOptionsArgumentArity`) uses these bounds to reject command-line invocations that provide too few or too many arguments for an option.

---

## Data Model

```
ArgumentArity = { Min : Int32, Max : Int32 }
```

There are five predefined named constants:

| Name        | Min | Max            | Intended meaning              |
|-------------|-----|----------------|-------------------------------|
| `Zero`      | 0   | 0              | No arguments allowed          |
| `ZeroOrOne` | 0   | 1              | Optional single argument      |
| `ZeroOrMore`| 0   | `Int32.MaxValue`| Unlimited optional arguments |
| `ExactlyOne`| 1   | 1              | Required single argument      |
| `OneOrMore` | 1   | `Int32.MaxValue`| At least one argument        |

`Int32.MaxValue` (2 147 483 647) is used as an "unbounded" sentinel.

---

## Preconditions

- The struct has no validation in its constructor. Any combination of `min` and `max` is accepted, including negative values and `min > max`.
- Well-formed arities satisfy `0 ≤ Min ≤ Max`. The five predefined constants all satisfy this.
- The caller is responsible for providing well-formed arities; no exception is thrown for ill-formed ones.

**Open question / potential bug**: if a caller creates `new ArgumentArity(5, 0)` (Min > Max), the validator produces inconsistent error messages. This is not currently caught.

---

## Postconditions / Properties

### Property Group 1 — Predefined constants

1. `Zero.Min == 0 ∧ Zero.Max == 0`
2. `ZeroOrOne.Min == 0 ∧ ZeroOrOne.Max == 1`
3. `ZeroOrMore.Min == 0 ∧ ZeroOrMore.Max == Int32.MaxValue`
4. `ExactlyOne.Min == 1 ∧ ExactlyOne.Max == 1`
5. `OneOrMore.Min == 1 ∧ OneOrMore.Max == Int32.MaxValue`

### Property Group 2 — Well-formedness of predefined constants

6. `∀ c ∈ {Zero, ZeroOrOne, ZeroOrMore, ExactlyOne, OneOrMore}, c.Min ≥ 0`
7. `∀ c ∈ {Zero, ZeroOrOne, ZeroOrMore, ExactlyOne, OneOrMore}, c.Min ≤ c.Max`

### Property Group 3 — Equality

8. **Reflexivity**: `a.Equals(a) == true` for all `a`
9. **Symmetry**: `a.Equals(b) == b.Equals(a)` for all `a, b`
10. **Transitivity**: `a.Equals(b) ∧ b.Equals(c) → a.Equals(c)` for all `a, b, c`
11. **Extensionality**: `a.Equals(b) ↔ (a.Min == b.Min ∧ a.Max == b.Max)`
12. **Operator consistency**: `(a == b) ↔ a.Equals(b)` and `(a != b) ↔ !a.Equals(b)`
13. `object.Equals` agrees with typed `Equals`: if `obj` is an `ArgumentArity`, then `a.Equals(obj) == a.Equals((ArgumentArity)obj)`

### Property Group 4 — Distinctness of predefined constants

14. All five predefined constants are pairwise distinct.
   - `Zero ≠ ZeroOrOne ≠ ZeroOrMore ≠ ExactlyOne ≠ OneOrMore`
   - (There are C(5,2) = 10 distinct pairs, all unequal.)

---

## Edge Cases

- `new ArgumentArity(0, 0)` is definitionally equal to `Zero`.
- `new ArgumentArity(1, Int32.MaxValue)` is definitionally equal to `OneOrMore`.
- Negative `min` or `max` is accepted by the constructor but is not used by any predefined constant.
- `min > max` is accepted by the constructor but creates an "impossible" arity that the validator would handle in an unspecified order.
- `GetHashCode` is platform-conditional (`#if NET` uses `HashCode.Combine`; otherwise XOR). The equality contract requires that equal values have equal hash codes; this holds because `Equals` only checks `Min` and `Max`.

---

## Invariants

1. The struct is immutable (`readonly`).
2. Equality is defined structurally by `(Min, Max)` pair comparison.
3. The predefined constants are `static readonly` fields — they are created once and shared.

---

## Examples

| Expression | Result |
|------------|--------|
| `ArgumentArity.Zero.Equals(new ArgumentArity(0, 0))` | `true` |
| `ArgumentArity.ExactlyOne == ArgumentArity.ZeroOrOne` | `false` |
| `ArgumentArity.OneOrMore.Min` | `1` |
| `ArgumentArity.ZeroOrMore.Max` | `2147483647` |
| `new ArgumentArity(2, 5).Min ≤ new ArgumentArity(2, 5).Max` | `true` |
| `new ArgumentArity(5, 0).Min ≤ new ArgumentArity(5, 0).Max` | `false` (ill-formed!) |

---

## Inferred Design Intent

The design mirrors [System.CommandLine's `ArgumentArity`](https://learn.microsoft.com/dotnet/standard/commandline/syntax#argument-arity). The five constants cover the most common practical arity patterns. The struct is intentionally simple: no validation, no invariant enforcement, just a transparent (Min, Max) pair with structural equality. The burden of using well-formed arities lies with option providers.

---

## Open Questions for Lean Formalisation

1. **Int32.MaxValue as sentinel**: Should we model `Int32.MaxValue` as Lean's `Int.ofNat (2^31 - 1)` (concrete) or as an opaque `unbounded` constant? The concrete approach allows `decide` to close proofs about predefined constants.
2. **Ill-formed arities**: Should the Lean model admit ill-formed arities (Min > Max) or restrict to a subtype `{ a : ArgumentArity // a.Min ≤ a.Max }`? The subtype approach makes well-formedness part of the proof structure.
3. **GetHashCode**: The hash code is platform-conditional. Should we model it? Probably not — it is not observable in the properties we want to verify.
4. **`object.Equals` overload**: Should we verify the `bool Equals(object?)` overload? Yes — it adds a type-erasure property: `a.Equals((object)a)` is always true.

---

## Approximations for Lean Model

- Model `int` as `Int` (unbounded integers) or `UInt32` or `Int32` (32-bit signed). For the predefined constants, `UInt32` suffices; for the general case, we need to handle negative values if we want to model ill-formed arities.
- Do NOT model `GetHashCode` (out of scope).
- Do NOT model `object.Equals` deeply (it adds complexity without insight).
- DO model the `==` and `!=` operators as wrappers over `Equals`.
