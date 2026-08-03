> **⚠️ DRAFT — NOT READY TO PUBLISH.**
> This is a working draft for a future devblogs.microsoft.com post, kept in-repo for review. It is
> **not** linked from any published documentation and carries no publication date.
>
> **Do not publish until both are true:**
> 1. A public `MSTest.Sdk` release (targeted: **4.4**) resolves `MSTest.SourceGeneration` through the
>    documented `PublishAot=true` entry point — **not** the discontinued `MSTest.Engine`-era generator
>    that the currently-shipped `MSTest.Sdk` 4.3.3 still resolves. The fix already exists in source on
>    `main`; it has **not** shipped in a public package yet (verified against the public NuGet feed).
> 2. A real `dotnet publish -r <rid> -p:PublishAot=true` has been run against that public release and
>    the resulting native binary has been executed and confirmed to discover and run the sample's
>    tests. Build-only verification is not sufficient — see the design-history note in
>    `docs/source-generator/design.md` and PR #9825/#9769 for why a build-time pass alone missed a
>    real regression here before.
>
> Until then, every "before/after" claim and the "one-line opt-in" framing below is **provisional**.
> Full research context, timeline, and factual-risk log: session artifact
> `files/mstest-source-generation-blog-draft.md` (not part of this repo).

# MSTest source generation: trim-safe, AOT-friendly test discovery

_Working title — alternates: "How MSTest is removing reflection from test construction and execution
(and where discovery still can't, yet)" / "MSTest's new source generator: what it does today, not what
the docs promise"_

## The problem

If you've ever published an MSTest suite as trimmed or Native AOT, you may have hit this: the build
itself is clean — no `IL2026`/`IL3050` warnings, because MSTest's own reflection call sites are
already annotated so the trimmer doesn't complain about them — but the *published, trimmed binary*
either discovers zero tests or throws `MissingMethodException` at run time. That gap is the actual
failure mode: silencing a trim warning is not the same as preserving what the trimmer would otherwise
remove. It's a direct consequence of how MSTest has always discovered tests — reflecting over your
compiled assembly at startup (`Assembly.GetTypes()`, per-class `Type.GetMethods()`, then
`Activator.CreateInstance`/`MethodInfo.Invoke` to build and run them). A trimmer can't prove ahead of
time which types and methods that reflection will touch, so unless something else roots them, it
trims them away. That same `Assembly.GetTypes()`/`GetMethods()` scan also runs on every ordinary
startup, trimmed or not — for a large suite, that's discovery work happening before a single test
executes.

## What MSTest.SourceGeneration actually does

MSTest is shipping a source generator, `MSTest.SourceGeneration`, that moves *which test classes exist
in an assembly* from a runtime `Assembly.GetTypes()` scan to a compile-time-populated registry, and
moves *construction, invocation, and (for modeled members) attribute reads* from reflection to
generated code. It's more surgical than "discovery is now compile time" suggests, though: finding
*which methods on a given class are `[TestMethod]`s* still walks every member of that class via
`Type.GetMethods()` at run time — the adapter's method-enumeration contract expects every method on a
type, and the generator's per-type test-method data is deliberately partial (only `[TestMethod]`-
annotated members), so that walk always falls back to reflection, in both modes, regardless of whether
the class is otherwise modeled. What *does* change for a method that walk turns up: in the default
`ReflectionFree` mode, a modeled test method's attributes are pre-materialized, so the adapter serves
that attribute read (and, once the method is confirmed to be a test, its construction/invocation) from
generated data instead of calling `GetCustomAttributes`/`Activator.CreateInstance`/`MethodInfo.Invoke`.
So per-class *method discovery* isn't reflection-free — the attribute reads and execution for the
methods it finds are. At the assembly level it's more binary: once even one class in an assembly is in
the generated registry, the adapter treats that registry as authoritative for *which classes exist* in
that assembly — it doesn't separately re-scan reflectively for classes the generator skipped (more on
that below).

There are two modes, selected via `<MSTestSourceGenMode>`:

- **`ReflectionFree` (the default).** Emits complete, materializable attribute arrays plus
  constructor, method, and property-setter *delegates* for modeled members — replacing
  `Activator.CreateInstance`, `MethodInfo.Invoke`, and `PropertyInfo.SetValue` on the hot path for a
  normal test run. An attribute that can't be fully materialized at compile time (or a non-public/
  init-only property setter) is simply left out of the generated data for that member, and the adapter
  falls back to reflection for it. Those direct delegate/type references also root the modeled members
  for the trimmer, and — like `Rooting` mode below — the generator additionally emits
  `[DynamicDependency(All, typeof(T))]` for each accessible, non-generic base type in a class's
  hierarchy, so shared `[ClassInitialize]`/`[TestContext]` members on a base survive trimming too.
- **`Rooting` (compatibility mode).** Emits only the type/method registry plus
  `[DynamicDependency(All, typeof(T))]` per test class and per accessible non-generic base type —
  enough to stop the trimmer removing members, but attribute reads, construction, and invocation
  always go through reflection in this mode (the attribute/delegate dictionaries stay empty).

Either way, some things are always still reflective, by design: general constructor/property
enumeration (`GetDeclaredConstructors`, `GetDeclaredProperties`), per-class method enumeration itself
(`Type.GetMethods()`, walked to find which members are `[TestMethod]`s — the generator's per-type method
data is deliberately partial, so this walk is unconditional in both modes), cross-assembly lookups like
`Type.GetType(string)`, and — within a modeled class — anything that class's registration doesn't
cover (Rooting mode's empty attribute/delegate dictionaries, or a `ReflectionFree`-mode attribute that
can't be fully materialized). Classes the generator can't discover as tests at all (unsupported
shapes — see "Where it stops today" below) are a different story: reflection only rediscovers them if
their assembly has *no* generator-registered classes at all; in a mixed assembly they're silently
absent, not reflectively recovered. The honest framing is **"compile-time-known for which classes exist
in an assembly; reflection-free for construction, invocation, and (once a member is identified as a
test) its attributes; still reflective for per-class method enumeration, general constructor/property
enumeration, cross-assembly lookups, and (within limits) anything unsupported"** — not "fully
reflection-free" and not "discovery moved to compile time" in the sense of skipping every reflective
walk.

## Before / after

**Before — ordinary MSTest, reflection-based discovery and execution (works today, unchanged):**

```csharp
[TestClass]
public class CalculatorTests
{
    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(2, 2, 4)]
    public void Add_ReturnsExpectedSum(int a, int b, int expected)
        => Assert.AreEqual(expected, a + b);
}
```

At startup, the adapter reflects over the assembly to find this class and method, reflects again per
`[DataRow]` to bind its arguments, and uses `Activator.CreateInstance`/`MethodInfo.Invoke` to run it.

**After — identical test code, the source generator wired into the build** _(see "Getting it running
today" below — as of this draft, that wiring is not yet available through the documented, supported
`MSTest.Sdk` path; don't treat the fragment below as adoption guidance)_:

```xml
<PackageReference Include="MSTest.SourceGeneration" Version="<preview version>" />
```

`CalculatorTests` doesn't change at all. At compile time, the generator (in its default
`ReflectionFree` mode) emits roughly this shape — illustrative and abridged, not a hand-callable API
(the real registration hook is an internal-use infrastructure type whose exact signature can change
between releases):

```csharp
[ModuleInitializer]
internal static void Initialize()
{
    // Class registration: which types are [TestClass]es, known at compile time — the adapter
    // won't need Assembly.GetTypes() to find this class in this assembly.
    // Method identification: still walks this class's members via Type.GetMethods() at run time
    // to find [TestMethod]s (see "Either way, some things are always still reflective" above) —
    // but once identified, this method's attributes and invocation are served from generated data.
    // Modeled execution: a generated delegate constructs CalculatorTests and invokes the test
    // method directly — no Activator.CreateInstance / MethodInfo.Invoke for this class.
    // Attribute metadata: [DataRow] instances are constructed by this generated code from
    // compile-time-captured values, not read back via GetCustomAttributes at run time.
    ReflectionMetadataHook.Register(assembly, types, testMethods, typeAttributes, assemblyAttributes,
        methodAttributes, methodInvokers, constructorInvokers, propertySetters);
}
```

The adapter has the class registry before the test host even starts scanning for classes in this
assembly — it still walks each class's members reflectively to find test methods, but the generated
delegates root the members they reference and serve attribute reads/execution for the methods that
walk turns up — so a trimmed or Native AOT build won't remove them.

**Also generator-backed:** the generated method invoker casts each argument to the test method's
declared parameter type — `(int)args![0]!`, `(double)args![1]!`, and so on — as part of the same
delegate that replaces `MethodInfo.Invoke`. That applies uniformly regardless of whether the values
came from a `[DataRow]` literal or an already-evaluated `[DynamicData]` source; there's no separate
reflective binding step in the generated path. What's still true: `[DynamicData]` *values themselves*
are evaluated at run time (calling the referenced method/property/field), since they aren't knowable at
compile time — but once evaluated, they flow through that same generated, typed-cast invoker.

## Supported today

- Ordinary `[TestClass]`/`[TestMethod]` discovery, construction, and invocation (default mode).
- `[DataRow]` — the attribute itself is pre-materialized, and argument binding runs through the same
  generated, typed-cast invoker as the method call itself — no separate reflective binding step.
- `[DynamicData]` — the values are evaluated at run time, but the surrounding construction/invocation
  is generator-backed.
- Base-class test fixtures, as long as `[TestClass]` is declared on the concrete (most-derived) type —
  the generator still walks the base chain for *inherited methods*, just not for discovering the
  `[TestClass]` attribute itself (see below).

## Where it stops today, and why

Two different kinds of "not modeled" behave very differently at run time, and the distinction matters.

**Whole classes the generator can't model as tests at all** — static classes, generic classes,
inaccessible classes (file-local types, or types nested in a `private`/`protected`/`private protected`
container — `internal` and `protected internal` nested classes are fine, since generated code in the
same assembly can still see them), and inherited `[TestClass]` (the attribute must
be declared directly on the concrete type — a base class carrying it isn't enough, since Roslyn's fast
incremental-generator API doesn't follow inheritance) — never enter the generated registry. In an
assembly that also has other generator-registered classes, that class is silently absent: it doesn't
run, and (see below) it isn't reflectively rediscovered either. `AOTSG0001`–`AOTSG0003` flag the first
three of these in `ReflectionFree` mode (the default); `MSTEST0069` flags inherited `[TestClass]`
regardless of mode — that analyzer isn't gated on `MSTestSourceGenMode` at all. (Abstract test classes
are a deliberate exception: their members stay safely rooted through the concrete-class base chain, so
no diagnostic is needed.)

**Individual methods inside an otherwise-modeled class** — generic test methods and by-ref/`out`/`in`
parameter test methods — are a different story. The class itself is still in the registry; only that
one method isn't in the generator's per-type method registry, which the adapter deliberately keeps
partial (it only needs to cover the fast path, not every method on the type). Because the adapter's
method enumeration always delegates to full reflection regardless of source-gen mode, these methods are
still reflectively discovered and executed through the ordinary reflective path — they aren't dropped,
just not fast-pathed. `AOTSG0004`–`AOTSG0005` flag these two shapes in `ReflectionFree` mode, as a
build-time signal that this particular method fell back to the slower path; `Rooting` mode suppresses
all five `AOTSG*` diagnostics — but be precise about why. For the two method-level shapes, the
suppression tracks reality: `Rooting` mode never had generated invokers for *any* method, so a
generic/by-ref test method isn't worse off there than any other method. For the three whole-class
shapes, though, suppression is a noise-reduction choice, not a fix — the class-shape validation that
excludes static/generic/inaccessible classes (and inherited `[TestClass]`, via `MSTEST0069`) runs
identically in both modes, so a skipped whole class is just as silently absent from a mixed assembly
under `Rooting` as under `ReflectionFree`; `Rooting` mode does not restore its discoverability. Either
way, having any compiler signal here at all is new — most of these used to be silent, whether that meant
"doesn't run" (whole-class case) or "runs, just reflectively" (method case), with no way to tell which
from the build output.

This list isn't exhaustive. `<TrimmerRootAssembly Include="$(AssemblyName)" />` is a related, coarser,
build-time-only lever that keeps a whole assembly's members alive for the trimmer — but it's worth
being precise about what it does and doesn't buy you here. It answers a *trimming* question ("does the
trimmer remove this member"), not a *discovery* question ("does the adapter find this test"): once an
assembly has even one source-generated test class, the adapter's per-assembly type lookup returns only
the generator's registered set for that assembly — it does not additionally re-scan reflectively for
more test classes the generator skipped. So for a test assembly that's a mix of generator-supported and
generator-skipped whole-class shapes, `TrimmerRootAssembly` alone won't make an inherited-`[TestClass]`
test start running again; the generator's own registered set is authoritative for which classes get
discovered. `TrimmerRootAssembly`'s real value today is for an assembly that has *no* source-generated
test classes at all (so the adapter falls back to a full reflective scan and needs the whole assembly
kept alive), or as generic build-time insurance against the trimmer independent of the generator.
Closing the actual discovery gap for skipped whole-class shapes is the roadmap items below, not this
lever.

## Getting it running today

_This section exists only because this draft predates its own publication gate (see the banner at the
top). It's not adoption guidance — do not follow it as a recipe._

As of this draft, the documented `MSTest.Sdk` `PublishAot=true` convenience wiring does not yet resolve
`MSTest.SourceGeneration` on the currently-public SDK version. The fix for that is already merged to
`main` and expected in a future `MSTest.Sdk` release; this post is gated on that release shipping. The
`samples/public/DemoMSTestSdk/ProjectWithNativeAOT` sample in the repo currently references
`MSTest.SourceGeneration` directly, bypassing the SDK's convenience wiring, and is explicitly marked in
its own project file as **investigation/workaround-only — not a recommended configuration**, kept
solely so the scenarios in this post stay compilable and reviewable while the supported path is still
in flight. When this post ships, this whole section should be gone, replaced by "add
`PublishAot=true`, done."

## What's stable, what's preview

| Surface | Status |
|---|---|
| MSTest core (`MSTest.TestFramework` / `MSTest.TestAdapter`) | **Stable**, normal 4.x semver |
| `MSTest.SourceGeneration` package | **Preview/alpha** — versioned independently of MSTest's stable line |
| `MSTEST0069` (inherited `[TestClass]`) | Ships in the current preview package, emitted regardless of `MSTestSourceGenMode`; still tracked internally as an unshipped analyzer rule pending a stable release |
| `AOTSG0001`–`AOTSG0005` (other unsupported shapes) | Ships in the current preview package, `ReflectionFree` mode only (suppressed in `Rooting` mode); still tracked internally as unshipped analyzer rules pending a stable release |
| Compile-time validation of `[DataRow]` argument compatibility against the method's parameters (turning a runtime mismatch into a build error) | **Roadmap** — not implemented today |
| Inherited `[TestClass]` discovery | **Roadmap** — opt-in marker attribute, design not finalized |

The `MSTest.SourceGeneration` package version shown in this post's samples is an alpha, versioned
independently from the stable `MSTest.TestFramework`/`MSTest.TestAdapter` line — don't read "MSTest
shipped a source generator" as "MSTest went alpha."

## Try it

The `samples/public/DemoMSTestSdk/ProjectWithNativeAOT` sample in the `microsoft/testfx` repo has a
runnable version of the scenarios above (`net8.0`, `PublishAot=true`, `EnableMSTestRunner=true`), plus
a `NotSourceGenerated.cs` file documenting each unsupported shape with its real diagnostic ID. As noted
above, its current wiring is workaround-only, not a configuration to copy. Building and running the
managed test host is enough to see the generated registration and passing tests; confirming the
*trimmed/AOT-published* binary behaves the same way is a separate, necessary step this post's
publication is gated on (see the banner at the top).

## What's next

Inherited `[TestClass]` support is on the roadmap via a to-be-designed opt-in marker attribute — no
committed release yet. Compile-time validation of `[DataRow]` argument compatibility against the test
method's parameters (turning today's runtime mismatch into a build error) and generated
property-descriptor support (for `GetDeclaredProperties`/`GetRuntimeProperty`, which still always fall
back to reflection today) are also on the roadmap, each its own independent, incremental change — not a
promise for a specific version.

---

_Audience: .NET developers already on MSTest who ship to trimming/Native AOT-sensitive environments
(CLI tools, containers, constrained/embedded targets), or who run large suites where avoiding a single
assembly-wide `Assembly.GetTypes()` scan is measurable in CI — not large suites in general, since
per-class method enumeration still walks reflectively either way (see "Either way, some things are
always still reflective" above). This is not primarily a "make an ordinary desktop test run faster"
post — the generator establishes the intended trim/AOT-safe path for construction, invocation, and
modeled attributes, and removes reflective execution dependencies for those modeled shapes; a real
trimmed/Native AOT publish-and-run against the shipped release is the verification still pending (see
the banner at the top), not raw execution speed on an ordinary run._
