# MSTest.Analyzers

MSTest is Microsoft supported Test Framework.

This package includes static code analyzers for C# and VB.NET helping you to better use and design your tests.
For access to the testing framework, install the MSTest.TestFramework package.

## Pinning constant contracts

MSTEST0032 reports assertions whose conditions are always true, but permits
`Assert.AreEqual(16, Protocol.HeaderSize)` when `HeaderSize` is a const field in
a referenced assembly. Comparing an independently written literal with a dependency's
constant can pin a compatibility contract: changing the constant and recompiling the
test makes the assertion fail. Replacing the dependency without recompiling the test
does not update an inlined constant.

The exemption accepts literal values (including strings and signed numbers) on either
side, named arguments, parentheses, and implicit built-in conversions. It does not
apply to constants compiled into the test assembly, including linked/shared source.
Local constants, arithmetic expressions, explicit casts, and self-comparisons remain
diagnostic. Boolean assertions such as `Assert.IsTrue(Protocol.HeaderSize == 16)`
are unchanged, as is the existing exemption for pinning enum members cast to their
exact underlying type.

## Queryable count and existence assertions

MSTEST0090 suggests keeping count and existence checks on the query provider
instead of passing an `IQueryable` to `Assert.HasCount`, `Assert.IsEmpty`,
`Assert.IsNotEmpty`, or `Assert.ContainsSingle` when its returned item is ignored.
Use `Assert.AreEqual(expected, query.Count())` for cardinality and
`Assert.IsTrue(query.Any())` / `Assert.IsFalse(query.Any())` for existence.

Materialize with `ToList` or `ToArray` when testing retrieved contents or reusing
a snapshot, not merely to check a count. `AsEnumerable` does not materialize or
cache results. The rule is advisory because an `IQueryable` can also be in-memory.
Queries are exempt when the selected assertion reads `ICollection.Count` or a
matching `ICollection<T>.Count` instead of enumerating. `ContainsSingle` still
enumerates even when the query exposes a count.
Discarded tuple elements, conditional branch results, and coalesce fallback
results count as ignored returns. Results inspected as conditions or coalesce
inputs, or passed on to other code, remain valid.
No automatic fix is provided: query translation, exception behavior, and
assertion messages need deliberate consideration.
