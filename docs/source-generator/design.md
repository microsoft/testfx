# MSTest source generator — design

This document describes the design, scope and current limitations of the MSTest source
generator that ships in the `MSTest.SourceGeneration` package. It is intentionally
opinionated: where there is a deliberate gap, the gap and its rationale are documented so
that adding new code paths does not silently change the contract.

## Goals

1. **Trim/Native AOT safety for test assemblies.** When a test project is published with
   `PublishAot=true` or `PublishTrimmed=true`, MSTest must continue to discover and run
   tests without surfacing IL2026 / IL3050 warnings to the user and without runtime
   `MissingMethodException` failures caused by the trimmer removing test members.
2. **No user-visible API change.** Opting in is a single NuGet reference. Existing test
   code keeps working.
3. **Reduced reflection cost.** Move the per-assembly `Assembly.GetTypes()` and per-class
   `Type.GetMethods()` scans from startup to compile time.

## What the generator emits today

For every `[TestClass]` declared **directly** on a non-static, non-abstract, non-generic,
non-`file`-local, accessible type, the generator produces:

1. A `[ModuleInitializer]`-decorated static method (`MSTestSourceGeneratedReflectionMetadata.Initialize`).
2. `[DynamicDependency(All, typeof(T))]` on that method for the test class **and every
   accessible non-generic base type** in its inheritance chain (so members declared on an
   abstract base — `[ClassInitialize]`, `[ClassCleanup]`, `[AssemblyInitialize]`,
   `[AssemblyCleanup]`, `TestContext` setter — are preserved by the trimmer).
3. A `types` array containing the concrete test classes.
4. A `testMethods` dictionary mapping each test class to the `MethodInfo`s for its
   `[TestMethod]`-annotated (or `TestMethod`-subclass-annotated) methods, including
   methods inherited from base classes, deduped by signature.
5. A `ResolveMethod` helper that resolves each method by name + parameter types at module
   initialization, throwing `MissingMethodException` if the lookup fails.
6. A call to `ReflectionMetadataHook.Register` that hands this data to the adapter and
   replaces `ReflectionOperations` with `SourceGeneratedReflectionOperations` for the
   lifetime of the process.

The `[ModuleInitializer]` runs once per test assembly when the CLR first touches that
assembly. Multiple test assemblies in the same process register independently and are
merged into a `CompositeSourceGeneratedReflectionDataProvider`.

## What the generator does NOT emit

These fields exist on `SourceGeneratedReflectionDataProvider` but are not populated by the
current emitter. The adapter always falls back to runtime reflection for them. Closing
each gap is tractable engineering work — none of the fields is fundamentally blocked:

| Field on the provider | Used by | Status |
|---|---|---|
| `TypeAttributes` | `IReflectionOperations.GetCustomAttributes(Type)` | Always falls back |
| `TypeMethodAttributes` | `IReflectionOperations.GetCustomAttributes(MethodInfo)` | Always falls back |
| `AssemblyAttributes` | `IReflectionOperations.GetCustomAttributes(Assembly, Type)` | Always falls back |
| `TypeConstructors` | `IReflectionOperations.GetDeclaredConstructors` | Always falls back |
| `TypeConstructorsInvoker` | `IReflectionOperations.CreateInstance` | Always falls back |
| `TypeProperties` | `IReflectionOperations.GetDeclaredProperties` | Always falls back |
| `TypePropertiesByName` | `IReflectionOperations.GetRuntimeProperty` | Always falls back |
| `TypeMethodLocations` | Source-location navigation | Returns empty (no navigation) |

> **The source-gen path today is best understood as "type rooting + test-method
> pre-resolution + trimmer hints" rather than a full reflection replacement.** Attribute
> reads, constructor invocation and property reflection still hit the reflection path —
> the trimmer hints from `[DynamicDependency]` keep that path runnable under AOT/trimming.

## Discovery limitations

The following user-code shapes are silently skipped by the generator. They keep working
when source-gen is not active (because reflection sees them), but are invisible to the
source-gen registry. Where applicable, an analyzer warns about the limitation:

| Shape | Skipped because | Workaround | Warning |
|---|---|---|---|
| Inherited `[TestClass]` (attribute applied only to the base) | `SyntaxValueProvider.ForAttributeWithMetadataName` does not follow inheritance | Apply `[TestClass]` directly to the derived class | MSTEST0069 |
| Open generic test class (`class Foo<T>`) | `typeof(Foo<T>)` is invalid at module-initializer scope | Make the class non-generic, or instantiate a concrete derived class | — |
| Generic test method (`void Test<T>(T value)`) | `typeof(T)` is invalid at module-initializer scope | Use a non-generic test method that constructs the type itself | — |
| Test method with `ref` / `out` / `in` / `ref readonly` parameter | `typeof(T)` for by-ref parameters round-trips as `T&` and the resolver's `typeof(T) == ParameterType` check would fail | Use a wrapper type or a non-by-ref signature | — |
| `file`-local test class | The generated module initializer lives in a different file | Move the class out of file scope | — |
| Private / protected nested test class | The generated `internal` module initializer cannot reference it (CS0122) | Make the type `internal` or more visible | — |
| Static test class | Source-gen models instance-based test execution | Make the class non-static | — |
| Abstract test class | Not directly runnable; but its members are still rooted via `[DynamicDependency]` because of the per-base chain emission | Annotate a concrete derived class with `[TestClass]` | — |

### Future direction: inherited `[TestClass]`

Discovery of inherited `[TestClass]` is intentionally deferred. The plan is to introduce
an opt-in marker attribute (name TBD) that the user adds to the derived class. The
attribute carries no runtime behavior; it only signals the generator to emit the type as
a test class. This keeps the syntactic-attribute fast-path (`ForAttributeWithMetadataName`)
intact while letting users explicitly opt back into inheritance.

## `SourceGeneratedReflectionOperations` fallbacks

Every call site on `SourceGeneratedReflectionOperations` that can return reflection data
fits into one of three explicit categories. Each is marked with a `// Category X` comment
in the source to keep the design choice visible.

### Category A — Generator-gap fallback

The corresponding source-gen field is not populated by today's emitter. The method
always falls through. Closable by extending the emitter.

- `GetCustomAttributes(MemberInfo)` for `Type` and `MethodInfo`
- `GetCustomAttributes(Assembly, Type)`
- `GetDeclaredConstructors`
- `GetDeclaredProperties`
- `GetRuntimeProperty`
- `CreateInstance`

### Category B — Contract-mismatch fallback

The interface contract demands every method (or similar), but the source generator
intentionally models only test methods. Always-delegate is the correct design; the
generator cannot pre-resolve methods it does not know about.

- `GetDeclaredMethods`
- `GetRuntimeMethods`
- The not-found branch of `GetRuntimeMethod`

### Category C — Cross-assembly fallback

The lookup targets an assembly that did not opt into source generation (test framework,
adapter, extensions, test assets packed without the generator). No amount of generator
work eliminates this.

- `GetType(string)` (always — `Type.GetType` resolves only assembly-qualified names)
- The no-match branch of `GetType(Assembly, string)`
- `GetDefinedTypes` for assemblies with no source-gen registration
- `GetCustomAttributes(MemberInfo)` for non-`Type`, non-`MethodInfo` members

### Rule for new fallbacks

When adding a new method that can fall back to reflection, mark the call site with a
`// Category A/B/C: <reason>` comment. This keeps the design surface auditable: blind
corners are fallbacks that look intentional but were really oversights — labelling each
one prevents that.

## Trim / Native AOT story

This section answers the recurring question: *can we silence trim/AOT warnings without
the source generator "touching" the types?*

The story has two distinct halves: the **warning** story (compile/publish time) and the
**runtime** story.

### The warning story

Already solved today **without** the source generator. The adapter's reflection paths in
`ReflectionOperations`, `AssemblyResolver`, `ManagedNameHelper`,
`DataSerializationHelper`, etc. are annotated with `[UnconditionalSuppressMessage]`
(IL2026 / IL3050) with the standard justification:

> *"Native AOT support relies on MSTest source-generated reflection metadata, not on this
> code path."*

Because user test code itself never calls reflection-flagged APIs (it just declares
classes and methods), no IL2026 / IL3050 warning propagates from MSTest into a user's
test project. The compile-time / publish-time experience is clean regardless of whether
the user references `MSTest.SourceGeneration`.

### The runtime story

Suppression is **not** preservation. The trimmer still removes unused members — the
suppression just stops the compiler from complaining about it. When the user runs an
AOT-published or trimmed test assembly, the reflection paths in `ReflectionOperations`
will execute and find that their target methods / types have been trimmed away —
typically surfacing as `MissingMethodException` or zero discovered tests.

To keep tests runnable under AOT/trimming, *something* has to root the test types and
their members. The choices are:

#### Option 1 — The current source generator

Emits `[DynamicDependency(All, typeof(MyTests))]` per `[TestClass]` and per accessible
base type. Preserves exactly the test-related types and their members; everything else
in the assembly remains eligible for trimming. **Pros:** minimal binary size, no manual
configuration. **Cons:** generator work for every emitter gap (see Category A above).

#### Option 2 — `<TrimmerRootAssembly>`

The user (or `MSTest.Sdk`, on their behalf) adds the test assembly as a trimmer root:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="$(AssemblyName)" />
</ItemGroup>
```

This tells the trimmer "do not trim anything in this assembly". Tests run because all
test types and members in the test assembly survive.

This is a viable alternative for the *rooting* concern, but **it does not replace the
source generator**. The differences are concrete:

- `TrimmerRootAssembly` is a build-time decision only. At runtime the adapter still
  calls `Assembly.GetTypes()` + per-class `Type.GetMethods()` to discover tests, because
  `ReflectionMetadataHook.Register` is never called and `SourceGeneratedReflectionOperations`
  is never installed. The source generator replaces that scan with a pre-computed
  registry handed to the adapter at module-init time. This is the headline performance
  win — large test assemblies (and especially Native-AOT, where the type system is
  slower than on CoreCLR) feel it most.
- `TrimmerRootAssembly Include="$(AssemblyName)"` only preserves *your* assembly. Base
  classes that live in a shared library (a `TestCommon.dll` carrying
  `[AssemblyInitialize]`-bearing fixtures, etc.) are still trimmable. The source
  generator emits `[DynamicDependency(All, typeof(BaseInOtherAssembly))]` which the
  trimmer honors across assemblies.
- `TrimmerRootAssembly` keeps the entire test assembly — helpers, mocks, fixtures, dead
  code. Source generation roots only the test classes and their base chain; everything
  else remains trimmable. Smaller published binary.
- Configuration cost: NuGet reference vs. a manually-added MSBuild item. (`MSTest.Sdk`
  could of course automate the latter.)

In return, `TrimmerRootAssembly` covers cases the generator skips today — inherited
`[TestClass]`, open-generic test classes, generic test methods, `file`-local classes —
because the reflection-fallback paths inside `SourceGeneratedReflectionOperations` will
still find their members. This is why the two are useful together (see Recommendation
below), but they are not interchangeable.

> The repo already uses `TrimmerRootAssembly` to validate trim safety of the framework
> itself — see `test/IntegrationTests/MSTest.Acceptance.IntegrationTests/TrimTests.cs`.
> The same lever is available to consumers.

#### Option 3 — Hand-written `[DynamicDependency]`

The user puts `[DynamicDependency(All, typeof(MyTests))]` on a stub method per test
class. **Pros:** preserves exactly what's needed. **Cons:** tedious, easy to forget; the
analyzer warning surface to catch missing roots would essentially have to reinvent the
source generator. Not recommended in practice.

#### Option 4 — Build-time MSBuild emission

A target in `MSTest.Sdk` that scans `[TestClass]`-bearing types in compiled IL (e.g. via
a small post-compile MSBuild task) and emits a generated file with the right
`[DynamicDependency]` attributes. **Pros:** no Roslyn dependency. **Cons:** essentially
re-implements the source generator with a different host; loses the IDE-time incremental
benefit of `IIncrementalGenerator`. Not pursued.

### Recommendation

For **most** AOT/trimmed test scenarios, the recommended sequence is:

1. Reference `MSTest.SourceGeneration` — this gives you the registry hand-off (skips
   `Assembly.GetTypes()` at startup), fine-grained rooting, and cross-assembly base-type
   preservation.
2. If a test shape is not yet supported by the generator (inherited `[TestClass]`, open
   generics, etc.), add `<TrimmerRootAssembly Include="$(AssemblyName)" />` as a
   backstop. The reflection fallback paths in `SourceGeneratedReflectionOperations` will
   still work; the trimmer will now keep the members they read.

The source generator and `<TrimmerRootAssembly>` solve overlapping but distinct
problems. The generator is the discovery + rooting path; `TrimmerRootAssembly` is a
coarse rooting backstop. Pairing them is the most robust configuration today.

## Roadmap

In rough priority order:

1. **Document every `// Category A/B/C` site in code** — done.
2. **This document** — done.
3. **Opt-in attribute for inherited `[TestClass]`** — a new attribute (name TBD) that
   the user applies to a derived class to add it to the generator's discovery set
   without re-applying `[TestClass]`. Replaces / refines MSTEST0069.
4. **Populate `TypeAttributes`** so type-attribute reads stop falling back. This is the
   highest-value Category A gap because attribute reads happen for every test class at
   discovery.
5. **Populate `TypeMethodAttributes`** for the same reason at the method level.
6. **Populate `TypeConstructors` + `TypeConstructorsInvoker`** so instance creation runs
   through a generated invoker. This is the trim/AOT win that goes beyond just
   "preserve the constructor": it also avoids `Activator.CreateInstance`.
7. **Populate `TypeProperties` + `TypePropertiesByName`** for `TestContext` and similar
   well-known properties.
8. **Source-location data** for IDE navigation parity with the reflection path.

Each item is small enough to be its own PR with its own tests. None of them blocks any
other.

## Performance positioning vs. delegate-based source generators (e.g. TUnit)

This is worth being explicit about because it shapes which roadmap items deliver real
user value.

### What the current generator saves at startup

- `Assembly.GetTypes()` — skipped. The registry already lists test types. (Inexpensive
  on CoreCLR; meaningful on Native AOT where the type system is JIT-less.)
- Per-class `Type.GetMethods()` + `GetCustomAttribute<TestMethodAttribute>` filtering —
  skipped. The registry already lists test methods per class.
- `Type.GetMethod(name, paramTypes)` per test method — still reflection, but executed
  once at module-init rather than at discovery time.

For a large test assembly this is a real cold-start improvement.

### What the current generator does NOT save at execution time

Every test execution still goes through the same code paths as reflection-mode MSTest:

- `Activator.CreateInstance(typeof(MyTests))` to construct the test instance.
- `MethodInfo.Invoke(instance, args)` to invoke the test body.
- `GetCustomAttributes(...)` for `[ExpectedException]`, `[Timeout]`, `[TestProperty]`,
  etc.
- `PropertyInfo.SetValue(...)` for `TestContext` injection.

The trimmer hints we emit keep those reflection calls working under AOT, but they do
not make them faster. **The per-test hot path is essentially the same speed as
reflection-mode MSTest.**

### How a delegate-based source generator (TUnit-style) differs

Frameworks like TUnit took a fundamentally different design. Instead of populating a
reflection registry that the existing execution engine consumes, they emit per-test
*delegates*:

| Operation | TUnit-style | MSTest source-gen today |
|---|---|---|
| Construct test instance | `static () => new MyTests()` | `Activator.CreateInstance(typeof(MyTests))` |
| Invoke test method | `static (instance, args) => ((MyTests)instance).MyTest((int)args[0])` | `MethodInfo.Invoke(instance, args)` |
| Read `[Timeout(5000)]` | Generator reads attribute at compile time, bakes `Timeout = 5000` into a metadata record | `method.GetCustomAttribute<TimeoutAttribute>().Timeout` |
| `DataRow` binding | Typed constants + typed casts inside the delegate | Reflection + `Convert.ChangeType` |
| `TestContext` injection | Baked property setter delegate | `PropertyInfo.SetValue` |

`MethodInfo.Invoke` is roughly an order of magnitude slower than a direct delegate call
for trivial method bodies. For tests whose own body is fast (microseconds),
delegate-based generators measurably win on raw throughput. **We do not compete on
per-test execution throughput today.**

### Why not just emit delegates here too?

**The work has already started.** A proof-of-concept generator that emits exactly this
shape lives in `src/Analyzers/MSTest.AotReflection.SourceGeneration/` (marked
`<IsShipping>false</IsShipping>`, tracked by
[issue #1837](https://github.com/microsoft/testfx/issues/1837)). It emits a per-assembly
`MSTestReflectionMetadata` registry where each test class carries:

- `Func<object?[]?, object> Invoke` per constructor — replaces `Activator.CreateInstance`.
- `Func<object?, object?[]?, object?> Invoke` per test method — replaces
  `MethodInfo.Invoke`.
- `Func<object?, object?> Get` / `Action<object?, object?> Set` per property — replaces
  `PropertyInfo.SetValue` / `GetValue`.
- Pre-materialized `Attribute[]` arrays — replaces `GetCustomAttributes(...)`.

What is left is the *wiring* from that registry into the adapter, which can be staged:

1. Merge / route the PoC's output through `MSTest.SourceGeneration` and feed it into
   `SourceGeneratedReflectionDataProvider` (populate `TypeConstructorsInvoker`,
   `TypeAttributes`, `TypeMethodAttributes`, etc.). The Category A fast paths in
   `SourceGeneratedReflectionOperations` activate automatically — no engine change.
2. Replace the `MethodInfo` returned by `ITestMethod.MethodInfo` with a
   `GeneratedTestMethodInfo` (new class, mirroring `ReflectionTestMethodInfo` in
   `src/TestFramework/TestFramework/Internal/`) whose `Invoke` override calls the
   generated `Func<object?, object?[]?, object?>` instead of doing reflection. Because
   `MethodInfoExtensions.InvokeAsSynchronousTask` calls `methodInfo.Invoke(...)`
   polymorphically, **the execution engine itself needs no changes** — this is the
   intentional seam behind the existing API contract:

   > *`ITestMethod.MethodInfo`: "Do not directly invoke the method using MethodInfo. Use
   > `ITestMethod.Invoke` instead."*
3. Migrate `[DataRow]`, `[DynamicData]`, `[DataSource]` parameter binding to use the
   compile-time parameter types instead of reflection-based `Convert.ChangeType`.

The only `Activator.CreateInstance` site that does **not** fit into this story is
`TestSourceHost.CreateInstanceForType` — it instantiates arbitrary adapter host /
runner types, not user test classes, so the generator can't pre-resolve it. It is not
on the per-test hot path, so the impact is limited to host setup.

The work is meaningful (a real refactor + thorough behaviour coverage so existing
extensions that consume `ITestMethod.MethodInfo` keep working) but **it is not "v2 of
the source generator from scratch"** — the architecture has been designed for it, the
seams exist, and the PoC generator output is ready to be wired in.

### Recommended framing

The current generator's value proposition, stated honestly, is:

- ✅ Trim/Native AOT *correctness*: tests run at all after trimming/AOT publish.
- ✅ Cold-start *throughput*: skip the assembly + type scans.
- ⚠️ Per-test *throughput*: unchanged from reflection MSTest. Closing this requires
  wiring the PoC delegate-emitting generator (`MSTest.AotReflection.SourceGeneration`,
  issue #1837) into the adapter via the seams already in place (`ITestMethod.MethodInfo`
  returning a delegate-backed `MethodInfo` subclass, populating `TypeConstructorsInvoker`,
  etc.).

Setting expectations this way avoids over-promising what the existing generator
delivers today and makes the case for wiring the PoC concrete when prioritising it.

### What wiring the delegate generator unlocks beyond perf

The case for wiring `MSTest.AotReflection.SourceGeneration` is **not just per-test
throughput**. Several non-perf design issues with the current source-gen story dissolve
once the registry holds delegates and pre-materialized attributes instead of
`MethodInfo` + name lookups.

- **Source-gen mode and reflection mode become truly equivalent.** Today, attribute
  reads on user test types fall back to `_fallback` because `TypeAttributes` /
  `TypeMethodAttributes` are empty (Category A). That is a quiet behavior split: an
  attribute the trimmer removed will simply not be returned under source-gen even though
  the user expects parity. Baking the `Attribute[]` arrays into the registry makes the
  paths converge.
- **The framework-wide `[UnconditionalSuppressMessage("IL2026"/"IL3050")]` becomes
  truly justified.** The standard rationale — *"Native AOT support relies on MSTest
  source-generated reflection metadata, not on this code path"* — is only partially
  true today, because the registry doesn't supply enough data to avoid the fallbacks
  in source-gen mode. Wiring the delegates makes the suppressed code paths really
  unreachable for user test code under source-gen.
- **The `[DynamicDependency]` rooting becomes unnecessary.** A static delegate
  `static (instance, args) => ((MyTests)instance).MyTest((int)args[0])` *is* the
  rooting — the trimmer keeps every member statically reachable from the delegate body,
  including inherited members. The whole abstract-base-type chain we currently walk to
  emit `[DynamicDependency]` becomes obsolete in that mode.
- **Compile-time validation of attribute shapes becomes possible.** Reading
  `[DataRow]` / `[DataSource]` / `[ExpectedException]` etc. at compile time to bake them
  also lets the generator surface analyzer diagnostics for:
  - `[DataRow(1, "two")]` against `void Test(int a, double b)` — today fails at runtime
    via `Convert.ChangeType`; would become a compile error from the typed delegate cast.
  - `[DataSource("MyMethod")]` where the source doesn't exist or has the wrong
    signature — diagnostic instead of runtime failure.
  - `[ExpectedException(typeof(NotAnException))]` — diagnostic.
- **`ref` / `out` / `in` parameter support.** Today these are silently skipped because
  the registry's signature comparison uses `typeof(T)`, which round-trips as `T&` for
  by-ref types. A generated delegate uses the call-site syntax directly and side-steps
  the type comparison entirely.
- **IDE source navigation under source-gen mode.** The Roslyn generator knows each test
  method's `SyntaxNode`, so `TypeMethodLocations` can be populated with file path + line
  number constants. IDE "Go to test source" works in source-gen mode (today it returns
  empty).
- **Cleaner extension surface.** Third-party `TestMethodAttribute` subclasses and
  custom data sources have a typed registry to plug into instead of having to layer on
  top of `MethodInfo.Invoke`.

What it does *not* fix (be honest with prioritisation):

- Inherited `[TestClass]` — still a `ForAttributeWithMetadataName` limitation; still
  needs the opt-in marker attribute.
- Private / `file`-local nested test classes — generated code is `internal`, can't
  reference them.
- Open-generic test classes / generic test methods — `typeof(T)` is still invalid at
  module-init scope.
- Cross-assembly reflection (Category C) — when the target assembly didn't opt in,
  reflection is still the only answer.
- Custom `TestMethodAttribute` subclasses via inheritance — same FAWMN limitation.

## Sunset plan for the current generator + `MSTest.Engine`

Once the delegate-emitting generator (`MSTest.AotReflection.SourceGeneration`) is wired
into `MSTest.TestAdapter` directly, the current architecture — open-source
`MSTest.SourceGeneration` package + closed-source `MSTest.Engine` runtime — becomes
redundant. The recommended sunset plan:

1. **Do not gate the existing source-gen path behind a feature flag.** A conditional
   "old vs new" code path doubles the maintenance surface (every refactor in
   `SourceGeneratedReflectionOperations`, every fix to inheritance walking, every base-
   type rooting tweak has to be validated against both paths) and the two paths will
   drift. The recently added base-type `[DynamicDependency]` chain becomes obsolete
   under the delegate approach (the delegate body roots inherited members
   transitively); keeping both maintains rooting math that no one uses.
2. **Delete the source-gen path from `main` in a single PR.** The old code is preserved
   in git history; a release tag (e.g. `mstest-aot-rewrite-base`) makes
   re-introduction a `git restore` away if a regression surfaces. This is how
   `dotnet/runtime` retires experimental APIs — git, not gated dead code, is the
   archive.
3. **Sunset the published packages gracefully.**
   - Ship one final `2.0.0-alpha.<date>` of `MSTest.SourceGeneration` containing an
     info-severity analyzer diagnostic (`MSTEST0NNN`) that reads
     *"`MSTest.SourceGeneration` is being replaced by integrated AOT support in
     `MSTest.TestAdapter` X.Y; see &lt;link&gt;"*. Then stop publishing.
   - Stop publishing `MSTest.Engine` from its (closed-source) repo on the same
     cadence. Users on the alpha packages pin the last version if they cannot move.
4. **Preserve the public API surface even after deleting the implementation.** Keep
   `ReflectionMetadataHook.Register` and `SourceGeneratedReflectionDataProvider` as
   public types. The *data* they hold changes (delegates instead of `MethodInfo` lookups),
   but third-party adapters / extension authors may already depend on these types and
   deserve a migration window. This is API stewardship, not feature-gating two parallel
   implementations.

The justification, condensed:

- Both packages are still `1.0.0-alpha.*` / `2.0.0-alpha.*` — users on alpha already
  opted into "things may change". Download counts confirm the blast radius is small.
- The new approach is strictly better, not "different tradeoffs": delegate emission
  handles every case the current approach handles (and adds support for several it
  silently skips today) while eliminating the rooting math entirely. There is no
  partial-rollback story that makes engineering sense.
- Conditional gating in code is not a substitute for version control. Git tags preserve
  the option; gated code only preserves the maintenance burden.
