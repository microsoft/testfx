# RFC 023 - Dynamically resolved extensions

- [ ] Approved in principle
- [x] Under discussion
- [x] Implementation
- [ ] Shipped

## Summary

Add a **dynamic (late-bound) extension registration** mechanism to Microsoft.Testing.Platform (MTP).
A JSON **extension manifest** dropped next to a test application declares one or more extension
assemblies; at start-up the platform discovers those manifests, loads each declared assembly into an
isolated load context, and invokes a static hook whose signature is **identical to the existing
MSBuild `TestingPlatformBuilderHook`**:

```csharp
public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
```

This gives central infrastructure teams a way to change how tests run without modifying the build of
every test project, while keeping exactly **one** extension-registration concept in the platform: a
static `AddExtensions` hook. Only *how the hook is reached* differs — the compiler (static
registration) or a manifest (dynamic registration).

The feature is **opt-in by presence of a manifest**, **isolated by default**, and **fails loudly**
rather than silently degrading.

## Motivation

Internal infrastructure teams own the CI pipeline, not the hundreds of test projects that flow
through it. They want to add reporting, artifact collection, policy enforcement, or diagnostics to
every test run, and they cannot realistically coordinate a `PackageReference` change across every
repository and project that produces a test application.

Today MTP has no answer for this beyond static registration. That is a real gap, and the absence of a
supported mechanism pushes teams toward unsupportable workarounds (start-up hooks, profilers,
hand-patched entry points, forked SDKs).

### Why this was not done before, and what changed

Arbitrary in-process plugin loading is the mechanism behind a large share of VSTest's historical
failure modes, and MTP deliberately avoided it:

- **Dependency conflicts.** A plugin with its own dependency closure loaded into the test app's
  process can conflict with the app's dependencies or another plugin's.
- **Unversioned extension-point contracts.** Static registration lets the *compiler* verify that a
  plugin matches the host. A loader has to answer that question at run time, and VSTest never did.
- **Supportability.** With static registration the extension set is a function of the project file,
  reproducible from source. Ambient plugin state makes every bug report start with "which plugins
  were loaded, from where, built against what?"

What changed is not that these risks disappeared — it is that the design can address each one
explicitly, which is what the rest of this RFC does:

| Historical failure mode | How this design addresses it |
| --- | --- |
| Dependency conflicts | Every extension assembly is loaded into its own `AssemblyLoadContext`, resolving its dependencies from its own `.deps.json`. Only the platform assembly is shared with the host. |
| Type-identity mismatches | The platform assembly is *always* resolved from the default load context, never from the extension's folder, so `ITestApplicationBuilder` is the same type on both sides. |
| Silent degradation | Every failure — unparseable manifest, missing assembly, missing type, missing hook — fails the run. There is no "ignore and continue" path. |
| Undiscoverable plugin sets | Manifests are explicit files with explicit paths; nothing is discovered by scanning for `*.dll`. Every load decision is written to the diagnostic log. |
| No escape hatch during triage | A single environment variable disables the whole mechanism. |

### Non-goals

- **Replacing static registration.** Static registration remains the recommended mechanism, and it
  already solves a large share of requests: because `TestingPlatformBuilderHook` items flow
  transitively through `buildTransitive/`, an infrastructure team can publish one internal package
  and inject a single `PackageReference` from one central `Directory.Build.targets` (or a company
  MSBuild SDK) without touching any individual test project. Dynamic registration exists for teams
  who cannot influence the build graph at all.
- **A sandbox.** A dynamically loaded extension runs with full trust in the test process. This is a
  deployment/ownership mechanism, not a security boundary.
- **A new extension-point surface.** Dynamic extensions use exactly the same `ITestApplicationBuilder`
  API as static ones.

## Design

### 1. The manifest file

A manifest is a JSON file whose name ends with **`.testingplatformextensions.json`**.

```json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testingplatformextensions.schema.json",
  "extensions": [
    {
      "id": "8E680F4D-E423-415A-9566-855439363BC0",
      "displayName": "Contoso.TestReporting",
      "assemblyPath": "extensions/Contoso.TestReporting/Contoso.TestReporting.dll",
      "typeFullName": "Contoso.TestReporting.TestingPlatformBuilderHook",
      "enabled": true
    }
  ]
}
```

#### Schema

| Property | Required | Type | Meaning |
| --- | --- | --- | --- |
| `extensions` | yes | array | The declared extensions. May be empty. |
| `extensions[].assemblyPath` | yes | string | Path to the assembly containing the hook. Relative paths resolve against the **directory of the manifest file**. |
| `extensions[].typeFullName` | yes | string | Full name of the type declaring the static `AddExtensions` hook. |
| `extensions[].id` | no | string | Stable identifier used to de-duplicate the same extension declared by several manifests. Compared ordinally, case-insensitively. Defaults to the **resolved absolute** `assemblyPath` plus `typeFullName` when omitted. |
| `extensions[].displayName` | no | string | Human-readable name used in diagnostics. Defaults to `typeFullName`. |
| `extensions[].enabled` | no | bool | Defaults to `true`. `false` keeps the declaration in place but skips loading. |

Unknown properties (including `$schema`) are ignored so manifests stay forward-compatible, but each
one is written to the diagnostic log — at the root as `<name>` and inside an entry as
`extensions[<index>].<name>` — so typos are still discoverable. This matters most for `enabled`: a
misspelled `"enabeld": false` runs an extension its author believed was switched off, and the log
line is the only trace of why.

The same `id` appearing twice is only de-duplicated when both declarations name the same resolved
assembly path and type. Two *different* extensions sharing an `id` fails the run, because honouring
only the first would silently drop a policy somebody deliberately deployed. This mirrors how the
MSBuild task rejects `TestingPlatformBuilderHook` items whose metadata conflicts.

`id` consistency is only checked between declarations that are actually going to load. A declaration
with `enabled: false` is skipped before its `id` is considered, so it can neither collide with nor
block an enabled one. That is deliberate: nothing is silently dropped when the author has explicitly
said not to deploy it, and the alternative would let a switched-off entry hard-fail the run, which
would defeat the purpose of `enabled: false` as the per-extension escape hatch.

De-duplication does **not** span static and dynamic registration: the platform cannot see the
statically generated hook list at the point manifests are processed, so an extension delivered
through both paths has its hook invoked twice. Ship an extension through one path or the other.

The JSON schema for editor completion lives in
[`docs/testingplatformextensions.schema.json`](../testingplatformextensions.schema.json).

#### Why these fields and not fewer

`assemblyPath` and `typeFullName` are the irreducible minimum — they are exactly the information the
MSBuild `TestingPlatformBuilderHook` item carries (`TypeFullName` plus, implicitly, the referenced
assembly). The other three each pay for themselves:

- **`id`** — the moment two infrastructure teams can each drop a manifest, the same extension can be
  declared twice. Without a stable identity the platform would register it twice, which for a data
  consumer means duplicated reports. `id` mirrors the stable GUID the MSBuild item already requires.
- **`enabled`** — the realistic rollout story is "ship the manifest to every machine, turn it on for
  some". Deleting and restoring files is a worse mechanism than a flag, and `enabled: false` is also
  the per-extension escape hatch during an incident.
- **`displayName`** — every error message and log line in this feature names an extension. Without it
  the only handle is a fully-qualified type name, which is poor in a terminal error.

#### Why one file can declare several extensions

A single infrastructure team usually ships a coherent set (a reporter plus a policy hook). Requiring
one file per extension would multiply files without adding isolation, since ordering and failure
semantics are per-entry either way.

### 2. The hook contract

Identical to static registration:

```csharp
namespace Contoso.TestReporting;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        => builder.AddContosoReporting();
}
```

The platform looks up `typeFullName` in the loaded assembly and requires a **public static** method
named `AddExtensions` taking exactly `(ITestApplicationBuilder, string[])` and returning `void`
synchronously. Inherited static hooks are found. The synchronous `void` requirement is deliberate and
is enforced (§6) for both `async Task` and `async void`: the hook is invoked synchronously, so either
would return at its first `await`.

The hook receives the **real** builder, not a wrapper. Several shipped helpers — `AddOpenTelemetryProvider`,
`AddRunSettingsService`, MSTest's `AddMSTest` — reach through `ITestApplicationBuilder` to the concrete
builder, so handing a hook anything else would make them throw or, worse, silently do nothing. The one
restriction dynamic hooks are under (§4) is therefore enforced inside the builder for the duration of the
call rather than by substituting the object.

This is the single
most important property of the design: an extension author writes **one** hook and it works whether
the extension is referenced by the project or declared in a manifest, and an extension that is
currently referenced statically can be moved to a manifest with no code change.

`args` receives the same arguments that were passed to `TestApplication.CreateBuilderAsync`.

### 3. Discovery

At start-up the platform enumerates `*.testingplatformextensions.json` in the **test application's own
directory**, non-recursively. Non-recursive is deliberate: recursion would make the extension set
depend on unrelated content of the output tree.

Manifests are ordered by file name (ordinal) and entries within a manifest keep declaration order, so
the resulting registration order is deterministic and reproducible.

A later revision may add an environment variable holding an explicit list of manifest paths, for
teams that cannot write into the test application's directory. That is intentionally **not** part of
this iteration: the file-based mechanism must prove itself first, and the environment-variable form
raises additional questions (relative-path resolution, precedence, whether it replaces or augments
discovery) that are better answered with real usage data.

### 4. Ordering relative to static extensions

**Dynamic extensions register first, static ones second.**

That is easiest to see from the generated entry point, where the two registration steps are separate
calls:

```csharp
var builder = await TestApplication.CreateBuilderAsync(args);  // 1. dynamic extensions register in here
builder.AddSelfRegisteredExtensions(args);                     // 2. static extensions register here
using var app = await builder.BuildAsync();                    // 3.
```

The loading runs as the last thing inside `CreateBuilderAsync`, just before it hands the builder back
— but that method returns *before* `AddSelfRegisteredExtensions` is called, so a manifest-declared
extension is registered ahead of every statically referenced one.

This is the only placement that behaves identically for the generated entry point and for a
hand-written `Main`, because `CreateBuilderAsync` is the single point every host goes through. The
alternative — registering during `BuildAsync` — would run after the test framework registration check
and would behave differently depending on what the user's `Main` did in between.

The practical consequence is that a dynamic extension cannot take the "must be registered last"
position that CrashDump and TrxReport occupy. That is acceptable: those extensions are last because
of a build-time ordering guarantee that a manifest cannot participate in anyway.

Running first has one consequence that must be closed explicitly: a dynamic extension would otherwise
be able to call `RegisterTestFramework` before the test application does, claim the framework slot,
and make the *application's* own registration fail. That would let a manifest silently decide which
tests run and what results they report — exactly what this design must not allow. The builder
therefore refuses `RegisterTestFramework` for the duration of a dynamic hook, with an error naming
the extension and its manifest. Every other member behaves exactly as it does for a statically
registered extension, because the hook holds the real builder (§2).

Because the platform re-executes the **same executable** as an out-of-process test host whenever a
test host controller extension is active — and likewise for each attempt of the retry orchestrator —
manifests are discovered again in every such process. That is the same behaviour statically
registered extensions have, and it is what makes controller-side extensions work at all, but an
extension hook must therefore be safe to run once per process rather than once per logical run.

### 5. Assembly loading and isolation

**On .NET (`net8.0`+):** each distinct extension assembly path is loaded into its own
`AssemblyLoadContext`:

- The context resolves the extension's dependencies with `AssemblyDependencyResolver` over the
  extension's own `.deps.json`, so the extension gets the versions it was published with.
- **The platform contract assemblies are shared with the default context**, matched by simple name:
  `Microsoft.Testing.Platform` and `Microsoft.Testing.Extensions.TrxReport.Abstractions`. Without the
  first, the extension would load a second copy of the platform and `ITestApplicationBuilder` would be
  a different type, failing at the hook invocation. The second is there because its types are
  *exchanged* between extensions — `ITrxReportCapability` is implemented by one extension and queried
  for by another — so a private copy would make the capability silently invisible. Matching by name
  rather than relying on what happens to sit next to the extension keeps identity independent of the
  deployment layout. If the host does not carry a shared assembly at all (an abstractions package the
  test application never referenced), the extension falls back to its own copy rather than failing to
  load: an isolated copy is worse than a shared one, but far better than not running. Any future
  abstractions assembly whose types cross the boundary must be added to
  `DynamicExtensionConstants.SharedContractAssemblyNames`; implementation assemblies must not be.
- If `AssemblyDependencyResolver` cannot resolve a reference (for example the extension was xcopied
  without its `.deps.json`), the context falls back to probing the extension's own directory —
  including the culture sub-directory for satellite assemblies — and only then to the default context.
- Contexts are cached by resolved assembly path, so two manifests pointing at the same assembly share
  one context instead of loading it twice.
- Contexts are not collectible and extensions are never unloaded. An extension lives for the lifetime
  of the process, exactly like a statically registered one.

**Deploy extensions in their own directory.** Isolation is about assembly *identity*, not file
layout: if an extension is dropped into the test application's own output folder, its `.deps.json`
resolves to the very files the application also uses, so an isolated context is loaded from shared
files. Giving each extension its own published folder is what makes the isolation meaningful.

**On .NET Framework (the `netstandard2.0` asset):** `AssemblyLoadContext` does not exist. The
extension is loaded with `Assembly.LoadFrom`, which probes the extension's directory but provides no
isolation. This is a documented limitation, not a silent one — the diagnostic log records that the
extension was loaded without isolation.

**Under NativeAOT / when dynamic code is not supported:** loading an assembly from disk is impossible.
If any manifest declares an enabled extension, the platform fails with an explicit error rather than
silently skipping, because silently skipping is precisely the "the infra policy did not apply and
nobody noticed" failure this design exists to avoid. Teams publishing NativeAOT test apps must set
`enabled: false`, remove the manifest, or use the kill switch.

### 6. Failure policy

Every one of the following **fails the run** with a message naming the manifest file and the
extension:

- the manifest is not valid JSON, or its root is not an object;
- `extensions` is missing or is not an array, or an entry is not an object;
- `assemblyPath` or `typeFullName` is missing or empty;
- the same `id` names two different extensions;
- the test application's directory cannot be searched for manifests, so the platform cannot even tell
  whether an extension had to be loaded;
- the assembly file does not exist, or cannot be loaded;
- the type is not found in the assembly;
- the type has no public static `AddExtensions(ITestApplicationBuilder, string[])`;
- that method does not return `void`, or is declared `async void` — the hook is invoked synchronously,
  so either shape would return at its first `await`, its registrations would race `BuildAsync`, and its
  failures would be swallowed;
- the hook throws, or tries to register a test framework.

Failing loudly is the deliberate choice. A manifest exists because someone decided that every run
must be affected by it; a run that quietly ignores it produces results that look valid but are not
the results that were asked for. This mirrors how the platform already treats a missing
`--config-file`.

Because these failures happen inside `CreateBuilderAsync`, before the platform owns the application,
they surface as an exception out of the entry point rather than as formatted platform output. The
diagnostic log is flushed before the exception propagates, so the trace of which manifests were read
and which extension failed survives. Improving that first-run experience is left to a follow-up; the
message itself always names the manifest and the extension.

The counterweight to a strict policy is a fast escape hatch, which is the next section.

### 7. Kill switch

Setting `TESTINGPLATFORM_NODYNAMICEXTENSIONS` to `1` or `true` (case-insensitively) skips discovery
entirely. This is the first triage step when a run misbehaves: re-run with the variable set to
establish whether a dynamically loaded extension is implicated.

The name follows the existing `TESTINGPLATFORM_NOBANNER` convention, and the accepted values match
the platform's existing boolean environment-variable handling.

### 8. Diagnostics

With `--diagnostic` enabled, the diagnostic log records: every manifest read, every property the
platform did not understand, every entry that was skipped (disabled or duplicate `id`) and why, the
resolved absolute assembly path of every extension that is loaded, whether it was loaded with or
without isolation, and the fact that its hook completed.

Extensions registered through a manifest are ordinary extensions, so they appear in `--info`
alongside statically registered ones — a manifest-declared `ICommandLineOptionsProvider`, for
instance, shows up under "Registered command line providers".

## Alternatives considered

- **Directory probing (`*.dll` scan).** Rejected. This is the VSTest model: a run's behaviour becomes
  a function of whatever happens to be on disk, and there is no declaration to diff or review.
- **A new dedicated interface (`IDynamicExtension`) instead of the existing hook shape.** Rejected:
  it would create a second extension-registration concept, force authors to choose up front whether
  an extension is static or dynamic, and prevent moving an existing extension to a manifest without a
  code change.
- **An out-of-process extension host.** Strictly safer — a wire protocol has no assembly identity and
  no dependency graph — and it remains the right long-term shape if in-process extensions prove
  problematic. It is much larger, requires an extension point to survive a process boundary, and does
  not serve extensions that need to observe in-process state. Recorded as the fallback if this
  iteration goes badly.
- **Registering during `BuildAsync` instead of `CreateBuilderAsync`.** Discussed in §4; rejected
  because behaviour would depend on what a hand-written `Main` did.
- **Silently skipping bad manifests.** Rejected; see §6.

## Open questions

- Should the platform eventually require a declared contract version in the manifest, so it can
  reject an extension built against a newer platform with a clear message instead of a
  `MissingMethodException`? This depends on resolving the extension-surface `InternalsVisibleTo`
  story ([#7739](https://github.com/microsoft/testfx/issues/7739)) first.
- Should `--info` grow a dedicated section listing dynamically loaded extensions and their manifest
  origin, rather than relying on them appearing among registered providers?
- Startup failures currently escape `CreateBuilderAsync` as an unhandled exception rather than as
  formatted platform output (§6). Should the generated entry point grow a controlled startup-error
  path, which would also help the existing configuration-file failures?
- Should de-duplication span static and dynamic registration, so an extension shipped through both
  paths registers once? That needs a registration identity the generated `SelfRegisteredExtensions`
  can also carry.
- The environment-variable form of discovery (an explicit list of manifest paths, for teams that
  cannot write into the test application's directory) is deferred; §3 lists the questions it raises.

## References

- [#7739 — IVT story for MTP and extensions](https://github.com/microsoft/testfx/issues/7739)
- [#3494 / #3525 — Platform.MSBuild should allow generating a helper for registration of extensions](https://github.com/microsoft/testfx/issues/3494)
- [RFC 017 — Custom test host launcher](./017-TestHost-Launcher.md)
- [MSTest runner protocol](../mstest-runner-protocol/001-protocol-intro.md)
