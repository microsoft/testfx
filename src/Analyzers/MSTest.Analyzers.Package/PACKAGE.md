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
