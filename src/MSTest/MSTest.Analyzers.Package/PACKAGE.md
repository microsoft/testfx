# MSTest.Analyzers

MSTest is Microsoft supported Test Framework.

This package includes static code analyzers for C# and VB.NET helping you to better use and design your tests.
For access to the testing framework, install the MSTest.TestFramework package.

## Requirements

Starting with MSTest 5, the analyzers and code fixes require Roslyn 4.14 or newer:
Visual Studio 2022 17.14 or a .NET SDK 9.0.300 or newer compiler host.
This requirement applies to the compiler or IDE, not the project's target framework.
Roslyn assemblies are supplied by the host and are not included in this package.
Older hosts cannot load these analyzers; use MSTest.Analyzers 4.x if you cannot
upgrade the host.

## Test discovery and synchronization

MSTEST0002 and MSTEST0003 validate classes and methods marked with attributes
derived from `TestClassAttribute` or `TestMethodAttribute`. MSTEST0002 also
reports concrete open generic test classes, including classes nested in open
generic types. Abstract generic base classes and closed derived classes remain
valid. No automatic fix removes generic parameters or changes static, abstract,
override, async-void, or other invalid return signatures; those edits can break
callers or inherited contracts.

MSTEST0007 reports `ResourceLock` and `DoNotParallelize` on non-overridable
fixture methods because discovery ignores those declarations. Overridable
fixtures are excluded because a test override may inherit the attributes.
No automatic move is offered:
placing a lock on a test or class can change its lifetime. MSTEST0074 and
MSTEST0075 require an effective exclusive lock for protected mutations; a
read-only lock alone is not sufficient.

## Combinatorial data and test attribute values

MSTEST0092 reports conflicting combinatorial value providers, unsupported
inferred parameter types, and invalid constant range or random-data settings.
Custom implementations of `ICombinatorialValuesProvider` and an unprovided
trailing `CancellationToken` are supported. The analyzer does not assume
unspecified random-data defaults from a different framework version.

MSTEST0093 reports nonpositive timeout, retry-count, and expected-duration
values at applicable test and fixture sites. It also reports undefined retry
backoff values and delays below -1, which cause `Task.Delay` to throw when a
retry occurs. A delay of -1 is accepted by `Task.Delay` but waits indefinitely.
Neither rule offers an automatic fix for the invalid configuration.

## Asynchronous exception assertions

MSTEST0064 reports inline async-void delegates and statically resolved async-void
method groups passed to synchronous
`Assert.Throws` or `Assert.ThrowsExactly`. Use an async assertion and await it
so exceptions after an asynchronous suspension can be observed. No automatic
fix is offered for these delegates.

## Public test classes

MSTEST0091 suppresses CA1515 (Consider making public types internal) on classes
marked with MSTest's `TestClassAttribute`, including derived test-class attributes.
This keeps public test classes compatible with default test discovery without
disabling CA1515 for unrelated public types. Partial test classes are supported,
and each annotated nested test class is evaluated on its own; unannotated
helpers, base classes, and enclosing types are not exempted.

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

## Exact-type assertions against abstract classes

MSTEST0025 reports `Assert.IsExactInstanceOfType<AbstractBase>(value)` and
`Assert.IsExactInstanceOfType(value, typeof(AbstractBase))` when the expected type
is abstract. An object's runtime type cannot be an abstract class, so these
assertions always fail.

Use `Assert.IsInstanceOfType<AbstractBase>(value)` when derived types are allowed,
or specify the intended concrete type when an exact match is required. Use
`Assert.Fail` when the failure is intentional. The C# code fix replaces standalone
assertions with `Assert.Fail`, preserving qualification and literal or interpolated
messages. It is not offered when the generic assertion's return value is used,
including discard assignments, or when an explicit `ref` message handler cannot
be forwarded to `Assert.Fail`.
Ordinary `IsInstanceOfType` assertions, unknown runtime `Type` values, and type
parameters constrained to an abstract base class are unchanged.

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
