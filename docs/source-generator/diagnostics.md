# AOT reflection source generator diagnostics

The `MSTest.SourceGeneration` generator reports these warnings in `ReflectionFree`
mode (`<MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>`) when it cannot
emit the metadata or invoker needed for a test class or member. The affected class
or member is skipped by the generated registry. Fix the reported shape so it can
be included; normal MSTest discovery and signature requirements still apply.
These warnings are not reported in `Rooting` mode.

## AOTSG0001

### Test class is static

A static `[TestClass]` cannot be instantiated by the generated registry. Remove
the `static` modifier or move the tests to a non-static test class.

## AOTSG0002

### Test class is generic

An open generic test class, including one nested inside a generic outer type,
has no closed type available when the generator runs. Move `[TestClass]` to a
concrete, non-generic subclass that specifies every type argument, or make the
class non-generic. The test class must also be outside any open generic enclosing
type.

## AOTSG0003

### Test class is not accessible from generated code

Generated registry code lives in the same assembly, but in a different file and
type. It cannot reference a test class when the class itself or any enclosing
type is file-local, `private`, `protected`, or `private protected`.

Remove the `file` modifier from the test class and any file-local enclosing type.
Make the test class and every enclosing type accessible from the same assembly:
use `public`, `internal`, or, for nested types, `protected internal`.

## AOTSG0004

### Test method is generic

The generated invoker cannot choose type arguments for a generic method on a
test class, including helper methods. Replace the method with one or more
non-generic methods that specialize the required type arguments.

## AOTSG0005

### Parameter uses a by-ref kind

The generated invoker passes arguments through an `object?[]`, which cannot
preserve `ref`, `in`, `out`, or `ref readonly` parameter semantics. This applies
to methods and constructors on a test class, including helper methods. Remove
the by-ref modifier or move the dependency out of the member signature.
