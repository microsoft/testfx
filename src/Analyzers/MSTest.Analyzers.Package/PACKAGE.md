# MSTest.Analyzers

MSTest is Microsoft supported Test Framework.

This package includes static code analyzers for C# and VB.NET helping you to better use and design your tests.
For access to the testing framework, install the MSTest.TestFramework package.

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
