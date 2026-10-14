# AOT reflection source generator diagnostics

The `MSTest.SourceGeneration` generator reports these warnings when it cannot emit
the metadata or invoker needed for a test class or member. The affected class or
member is skipped by the generated registry. Fix the reported shape so the test
can be included.

## AOTSG0001

### Test class is static

A static `[TestClass]` cannot be instantiated by the generated registry. Remove
the `static` modifier or move the tests to a non-static test class.

## AOTSG0002

### Test class is generic

An open generic test class, including one nested inside a generic outer type,
has no closed type available when the generator runs. Use a concrete test class
that closes every type parameter.

## AOTSG0003

### Test class is not accessible from generated code

Generated registry code cannot reference file-local test classes or classes
nested in a private or private-protected outer type. Make the test class and
each enclosing type at least `internal`.

## AOTSG0004

### Test method is generic

The generated invoker cannot choose type arguments for a generic test method.
Replace it with one or more non-generic methods that specialize the required
type arguments.

## AOTSG0005

### Parameter uses a by-ref kind

The generated invoker passes arguments through an `object?[]`, which cannot
preserve `ref`, `in`, or `out` parameter semantics. Remove the by-ref modifier
or move the dependency out of the test signature.
