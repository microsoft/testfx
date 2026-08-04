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
non-`file`-local, accessible type, both modes emit a `[ModuleInitializer]`, the concrete
test types, and the test methods (including inherited methods, deduped by signature), then
call `ReflectionMetadataHook.Register`.

The selected mode determines the rest:

- **`ReflectionFree` (the default)** emits complete materializable type, inherited type,
  assembly, and method attribute arrays; constructor and method invocation delegates; and
  applicable property setter delegates. It also emits direct references that root the
  modeled members.
- **`Rooting` (compatibility mode)** emits `DynamicDependency(All, typeof(T))` for each
  test class and accessible non-generic base type, plus the type/method registry. Its rich
  attribute and delegate dictionaries are empty.

Both modes still resolve the `MethodInfo` keys used by the adapter at module
initialization. Reflection-free mode also resolves `PropertyInfo` keys for emitted setters.
These are bounded startup lookups; test construction, test invocation, and registered
attribute reads use generated data after registration.

The `[ModuleInitializer]` runs once per test assembly when the CLR first touches that
assembly. Multiple test assemblies in the same process register independently and are
merged into a `CompositeSourceGeneratedReflectionDataProvider`.

## Provider field status

Provider fields are mode-specific. A dictionary hit is authoritative, including an empty
attribute array; a missing entry falls back to reflection.

| Field on the provider | Used by | Status |
|---|---|---|
| `Types`, `TypesByName`, `TypeMethods` | discovery and type lookup | Populated in both modes |
| `TypeAttributes` | `IReflectionOperations.GetCustomAttributes(Type)` | Complete materializable entries in reflection-free mode; empty in rooting mode |
| `TypeMethodAttributes` | `IReflectionOperations.GetCustomAttributes(MethodInfo)` | Complete materializable entries in reflection-free mode; empty in rooting mode |
| `AssemblyAttributes` | `IReflectionOperations.GetCustomAttributes(Assembly, Type)` | Materializable attributes in reflection-free mode; empty in rooting mode |
| `TypeConstructors` | `IReflectionOperations.GetDeclaredConstructors` | Always falls back |
| `TypeConstructorsInvoker` | `IReflectionOperations.CreateInstance` | Populated in reflection-free mode; empty in rooting mode |
| `TypeMethodInvokers` | test and fixture invocation | Populated in reflection-free mode; empty in rooting mode |
| `TypePropertySetters` | generated property assignment | Applicable setters in reflection-free mode; empty in rooting mode |
| `TypeProperties` | `IReflectionOperations.GetDeclaredProperties` | Always falls back |
| `TypePropertiesByName` | `IReflectionOperations.GetRuntimeProperty` | Always falls back |
| `TypeMethodLocations` | Source-location navigation | Unpopulated; navigation returns no generated location |

`TypeConstructors` and `TypeConstructorsInvoker` deliberately describe different
operations. Reflection-free mode generates constructor **invocation** delegates, but it
does not claim to support general `ConstructorInfo` enumeration; `GetDeclaredConstructors`
therefore still uses reflection. Likewise, a generated `TestContext` setter does not make
general `PropertyInfo` enumeration or name lookup reflection-free.

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

### Category A — Missing generated-entry fallback

Reflection-free mode serves registered type and method attribute entries from generated
arrays and invokes registered constructors through generated delegates. Rooting mode,
skipped/unresolved members, and incompletely materialized attributes still fall back.

- `GetCustomAttributes(MemberInfo)` for missing `Type` and `MethodInfo` entries
- `GetCustomAttributes(Assembly, Type)` when no generated assembly attributes are available
- `GetDeclaredConstructors`
- `GetDeclaredProperties`
- `GetRuntimeProperty`
- `CreateInstance` when no generated constructor delegate matches

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

Reflection-free mode emits direct attribute construction and invocation references, which
root the modeled members, while rooting mode emits
`[DynamicDependency(All, typeof(MyTests))]` per `[TestClass]` and accessible base type.
Both preserve test-related types while leaving unrelated code eligible for trimming.
Unsupported shapes and missing provider entries can still require reflection-compatible
preservation (see Category A above).

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
2. **Populate complete materializable type and method attributes** — done in reflection-free mode.
3. **Use generated constructor, method, and property-setter delegates** — done in reflection-free mode.
4. **Opt-in attribute for inherited `[TestClass]`** — a new attribute (name TBD) that
   the user applies to a derived class to add it to the generator's discovery set
   without re-applying `[TestClass]`. Replaces / refines MSTEST0069.
5. **Design generated property descriptors** before changing `TypeProperties` or
   `TypePropertiesByName`. The design must preserve declared-only and inherited lookup,
   visibility, ambiguity, and exception semantics; emitting `typeof(T).GetProperty(name)`
   would only move reflection to startup and is not a reflection-free fix.
6. **Redesign source-location data and its consumer.** `TypeMethodLocations` remains
   unpopulated because its current type/method-name representation is lossy for overloads
   and the navigation path is not wired end to end.

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
- `Type.GetMethod(name, paramTypes)` per test method remains a bounded module-init lookup
  used to create adapter-compatible dictionary keys. Reflection-free mode similarly
  resolves keys for applicable property setters.

For a large test assembly this is a real cold-start improvement.

### What remains reflective

In reflection-free mode, registered test construction and invocation use generated
delegates, registered type/method attributes use generated arrays, and applicable property
assignment uses generated setters. Rooting mode retains the reflection execution path.

Both modes still use reflection for general constructor enumeration, general property
enumeration/name lookup, contract-wide method enumeration, cross-assembly data, and any
missing or unsupported generated entry. Generated source locations are not currently
provided.

### How a delegate-based source generator (TUnit-style) differs

Frameworks like TUnit took a fundamentally different design. Instead of populating a
reflection registry that the existing execution engine consumes, they emit per-test
*delegates*:

| Operation | TUnit-style | MSTest source-gen today |
|---|---|---|
| Construct test instance | `static () => new MyTests()` | Generated constructor delegate in reflection-free mode |
| Invoke test method | `static (instance, args) => ((MyTests)instance).MyTest((int)args[0])` | Generated method delegate in reflection-free mode |
| Read `[Timeout(5000)]` | Generator reads attribute at compile time, bakes `Timeout = 5000` into a metadata record | Pre-materialized attribute in reflection-free mode |
| `DataRow` binding | Typed constants + typed casts inside the delegate | Reflection + `Convert.ChangeType` |
| `TestContext` injection | Baked property setter delegate | Generated setter when modeled; reflection fallback otherwise |

Reflection-free mode now takes the generated delegate path for modeled construction and
invocation. Parameter binding and unsupported/missing metadata can still use reflection,
so this is not a claim that the entire execution pipeline is reflection-free.

### Delegate-based reflection-free mode

**The work has already started.** A reflection-free generator that emits exactly this
shape now ships inside `src/Analyzers/MSTest.SourceGeneration/` and is selected via
`<MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>` (tracked by
[issue #1837](https://github.com/microsoft/testfx/issues/1837)). It emits a per-assembly
`MSTestReflectionMetadata` registry where each test class carries:

- `Func<object?[]?, object> Invoke` per constructor — replaces `Activator.CreateInstance`.
- `Func<object?, object?[]?, object?> Invoke` per test method — replaces
  `MethodInfo.Invoke`.
- `Func<object?, object?> Get` / `Action<object?, object?> Set` per property — replaces
  `PropertyInfo.SetValue` / `GetValue`.
- Pre-materialized `Attribute[]` arrays — replaces `GetCustomAttributes(...)`.

That registry is now wired into `SourceGeneratedReflectionDataProvider`: its complete
entries activate the generated attribute, constructor, method, and setter paths. Remaining
work includes typed data-source parameter binding and the explicitly deferred
property-descriptor and source-location designs.

The only `Activator.CreateInstance` site that does **not** fit into this story is
`TestSourceHost.CreateInstanceForType` — it instantiates arbitrary adapter host /
runner types, not user test classes, so the generator can't pre-resolve it. It is not
on the per-test hot path, so the impact is limited to host setup.

This is not a claim that every adapter reflection contract is replaced. Existing
`MethodInfo`/`PropertyInfo` keys preserve compatibility, and the explicit fallback and
deferral boundaries above remain.

### Recommended framing

The current generator's value proposition, stated honestly, is:

- ✅ Trim/Native AOT *correctness*: tests run at all after trimming/AOT publish.
- ✅ Cold-start *throughput*: skip the assembly + type scans.
- ✅ Modeled per-test construction and invocation: generated delegates in reflection-free mode.
- ⚠️ General reflection elimination: incomplete by design; property/constructor enumeration,
  cross-assembly operations, and unsupported or missing entries retain fallback.

Setting expectations this way avoids over-promising what the existing generator
delivers today while keeping the remaining reflection boundaries explicit.

### What reflection-free mode provides beyond performance

The case for reflection-free mode is **not just per-test throughput**. Its delegates and
pre-materialized attributes also improve correctness and trimming behavior for modeled
entries.

- **Source-gen mode and reflection mode converge for modeled attributes.** Reflection-free
  mode now bakes complete materializable `Attribute[]` arrays into the registry. Missing or
  unsupported entries still fall back.
- **Trim/AOT suppressions match the modeled fast paths.** Registered attributes and
  invocations do not reach the suppressed reflection operations. Suppressions remain
  necessary and must stay truthful for the explicitly documented fallback paths.
- **Direct references provide rooting in reflection-free mode.** A static delegate
  `static (instance, args) => ((MyTests)instance).MyTest((int)args[0])` roots its target.
  Rooting mode continues to emit the explicit `[DynamicDependency]` base chain.
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
- **IDE source navigation under source-gen mode remains deferred.** Although Roslyn knows
  each method's syntax, `TypeMethodLocations` is intentionally unpopulated until its lossy
  key shape and missing end-to-end consumer are redesigned.
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

The reflection-free registry is now wired to the adapter provider. A separate future
decision could integrate its packaging more deeply into `MSTest.TestAdapter`; only then
would the open-source `MSTest.SourceGeneration` package + closed-source `MSTest.Engine`
split potentially become redundant. Any sunset plan must be evaluated independently of
the runtime wiring described here:

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
