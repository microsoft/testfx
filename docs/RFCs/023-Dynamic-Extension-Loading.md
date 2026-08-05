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

The feature is **off by default** and must be turned on per run with `--enable-dynamic-extensions`;
what it loads is then reported on the run's output, not just in the diagnostic log. It is **isolated
by default**, and **fails loudly** rather than silently degrading.

Manifests are read from the **test application's own directory** — never the working directory. That
is the only security-relevant property of this design; see [Trust](#trust).

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
| Dependency conflicts | Every extension assembly is loaded into its own `AssemblyLoadContext`, resolving its dependencies from its own `.deps.json`. Only an explicit list of contract assemblies is shared with the host: `Microsoft.Testing.Platform` and `Microsoft.Testing.Extensions.TrxReport.Abstractions` (§5). |
| Type-identity mismatches | Those shared contracts are *always* resolved from the default load context, never from the extension's folder, so `ITestApplicationBuilder` is the same type on both sides. |
| Silent degradation | Every failure — unparseable manifest, missing assembly, missing type, missing hook — fails the run. There is no "ignore and continue" path. |
| Undiscoverable plugin sets | The feature is off unless a run passes `--enable-dynamic-extensions`. Manifests are explicit files with explicit paths; nothing is discovered by scanning for `*.dll`, and everything loaded is echoed to the run's output. |
| No escape hatch during triage | Removing `--enable-dynamic-extensions` from the invocation disables the whole mechanism, and `enabled: false` disables one extension without deleting its manifest. |

### Non-goals

- **Replacing static registration.** Static registration remains the recommended mechanism, and it
  already solves a large share of requests: because `TestingPlatformBuilderHook` items flow
  transitively through `buildTransitive/`, an infrastructure team can publish one internal package
  and inject a single `PackageReference` from one central `Directory.Build.targets` (or a company
  MSBuild SDK) without touching any individual test project. Dynamic registration exists for teams
  who cannot influence the build graph at all.
- **A sandbox.** A dynamically loaded extension runs with full trust in the test process, as does
  every statically referenced one. This is a deployment/ownership mechanism; .NET has no
  intra-process security boundary to offer here (see [Trust](#trust)).
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
| `extensions[].displayName` | no | string | Human-readable name for the extension. Used in the loaded-extensions report, the diagnostic log, and assembly-level errors. Defaults to `typeFullName`. |
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
- **`displayName`** — gives an extension a handle that is not a fully-qualified type name. It identifies
  the extension in the loaded-extensions report, in the diagnostic log, and in the errors that do not
  already name the type (a missing or unloadable assembly). Errors that are *about* the type already
  print `typeFullName`, along with the assembly and the declaring manifest, so they identify the
  extension without it — and since `displayName` defaults to `typeFullName`, adding it there would
  usually just print the same string twice.

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

### 3. Opt-in and discovery

**The feature is off unless the run explicitly asks for it.** Nothing is discovered, parsed or loaded
without `--enable-dynamic-extensions` on the command line.

This is a **product decision, not a security control** — see [Trust](#trust) for why it cannot be one.
It is made for predictability and supportability: a manifest that happens to be in an output
directory should not silently change how a run behaves, and a run that did load extensions should be
identifiable from the invocation itself rather than by inspecting the output tree afterwards. A
command-line switch serves that better than an environment variable, because it lives in the CI
definition next to the rest of the invocation instead of in ambient machine state. Neither is more
trustworthy than the other; both are fully trusted control-plane inputs.

Once enabled, the platform enumerates `*.testingplatformextensions.json` in the **test application's
own directory**, non-recursively. Non-recursive is deliberate: recursion would make the extension set
depend on unrelated content of the output tree.

Manifests are ordered by file name (ordinal) and entries within a manifest keep declaration order, so
the resulting registration order is deterministic and reproducible.

A later revision may add a way to point at manifests outside that directory, for teams that cannot
write into it. That is intentionally **not** part of this iteration: the file-based mechanism must
prove itself first, and an explicit path list raises additional questions (relative-path resolution,
precedence, whether it replaces or augments discovery) that are better answered with real usage data.

#### Trust

This design is analysed against the [.NET baseline security
assumptions][baseline], which apply by default and are not restated here.

**The only security-relevant property of this feature is where manifests are read from.** They are
read from the directory containing the test application executable, which is a fully trusted
application folder (baseline §2.1) whose contents can already influence execution flow. Anyone able
to write a manifest there could equally replace an assembly the application already loads, so
discovery adds no authority that the location did not already confer.

Discovery deliberately never uses the **current working directory**, which the baseline explicitly
excludes from that guarantee: users expect the working directory to hold data rather than
instructions, so reading manifests from it would widen what a run treats as code. This rule is the
one behaviour here that a future change must not break, and it is pinned by a unit test.

Everything else follows from the baseline rather than from anything this design does:

- Extensions run with the full privileges of the test process. In-process composition is not a
  security boundary and .NET has no intra-process sandbox (baseline §3.3), so this is true of a
  statically referenced extension and of any NuGet package the test project consumes. It is not a
  property peculiar to dynamic loading.
- Isolation (§5) exists to stop extensions *colliding* with each other and with the application. It
  is a compatibility mechanism and provides no containment.
- The opt-in (§3) is a product decision. **It is not a security control** and should not be described
  as one: an actor who can write to the application directory is already fully trusted, so gating on
  a flag does not restrict them.

[baseline]: https://github.com/dotnet/core/blob/main/Documentation/security-foundations/baseline-security-assumptions.md

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
  deployment layout. If the host does not carry a shared assembly at all — the ordinary manifest-only
  deployment, where the extension brings its own copy — that copy is **promoted into the default
  context** rather than loaded privately, so it becomes the one canonical identity for every extension
  that follows. Loading it into the extension's own context instead would give each extension its own
  copy, and a capability implemented by one dynamic extension would be invisible to another. Note also
  that "carried by the host" is not the same as "already loaded": because dynamic hooks run before
  static ones (§4), a contract is often present in the application's dependency graph but untouched,
  so the platform asks the default context to load it by simple name before falling back. Any future
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

**Under NativeAOT:** loading an assembly from disk is impossible. If a manifest declares an enabled
extension, the platform fails with an explicit error rather than silently skipping, because silently
skipping is precisely the "the infra policy did not apply and nobody noticed" failure this design
exists to avoid. Teams publishing NativeAOT test apps must set `enabled: false`, remove the manifest,
or stop passing `--enable-dynamic-extensions`.

This is detected by *attempting* the load and translating the resulting
`PlatformNotSupportedException`, deliberately **not** by pre-checking
`RuntimeFeature.IsDynamicCodeSupported`. That switch answers a different question — whether new code
can be generated — and `<PublishAot>true</PublishAot>` turns it off even for builds whose managed
output still runs normally on CoreCLR (see `PublishAotNonNativeTests`). Gating on it would refuse to
load extensions for applications that are perfectly capable of loading them.

**Under `PublishTrimmed`:** the extension assembly itself is external to the application, so nothing
trims it, and it loads normally. What it *calls* is a different matter. The trimmer removes any code
the application does not reference — not just platform API, but BCL types and members too — so an
extension that calls something no statically referenced code uses can load and then fail at run time
with `MissingMethodException` or `TypeLoadException`. An extension does not have to be exotic to hit
this: any BCL member the host application happens not to use is a candidate.

Rooting the whole extension-facing surface is not an option — that surface is unbounded, since it
includes the BCL, so rooting it would defeat trimming entirely. Nor can this be detected at load
time: the extension's IL is not inspected, and the member is only missing once it is called. It is
therefore documented as a limitation rather than worked around: **do not combine `PublishTrimmed`
with dynamic extensions.** The two features want opposite things — trimming removes what is not
statically reachable, and dynamic extensions are by definition not statically reachable.

### 6. Failure policy

Every one of the following **fails the run** with a message naming the manifest file and the
extension:

- the manifest is not valid JSON, its root is not an object, or it carries content after the root
  object (the netstandard2.0 parser stops at the root value, so this is checked explicitly to keep
  both readers strict in the same way);
- `extensions` is missing or is not an array, or an entry is not an object;
- a recognized property is declared twice, at the root or inside an entry, so one declaration would be
  discarded (**.NET only** — see below);
- `assemblyPath` or `typeFullName` is missing or empty;
- the same `id` names two different extensions;
- the test application's directory cannot be searched for manifests, so the platform cannot even tell
  whether an extension had to be loaded (a directory that simply does not exist is *not* an error — it
  declares nothing, and the two are distinguished rather than collapsed by a `Directory.Exists` check,
  which returns false for both);
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

**Duplicate keys are the one rule the two readers do not share.** JSON permits a repeated key and
most parsers resolve it last-wins, which here would discard extensions somebody deliberately
declared. `System.Text.Json` exposes every occurrence, so the .NET reader rejects the manifest. The
netstandard2.0 reader cannot: Jsonite fills a dictionary by indexer assignment, so the earlier value
is already gone before the reader runs, and detecting it would mean forking a vendored parser that
the server-mode JSON-RPC stack also uses. Matching that limitation on .NET was considered and
rejected — parity is not worth keeping quiet on the platform that *can* see the problem. Unknown
properties are exempt on both: repeating one cannot change what gets loaded, so rejecting it would
only punish forward-compatible manifests.

Because these failures happen inside `CreateBuilderAsync`, before the platform owns the application,
they surface as an exception out of the entry point rather than as formatted platform output. The
diagnostic log is flushed before the exception propagates, so the trace of which manifests were read
and which extension failed survives. Improving that first-run experience is left to a follow-up; the
message itself always names the manifest and the extension.

The counterweight to a strict policy is that turning the feature off is always one flag away: every
error names `--enable-dynamic-extensions` as the way to skip discovery entirely.

### 7. Diagnostics

**Loading is never silent.** Whenever at least one extension is loaded, the platform writes to
standard output the number loaded and, for each, its display name, the resolved absolute assembly
path, the hook type, and the manifest that declared it. This is not
gated on `--diagnostic`: running foreign code inside the test process is something the person reading
the log should see without having opted into extra logging.

The exception is any mode that reserves standard output for a machine-readable stream: server mode,
where it is a protocol channel, and `--list-tests json`, where the JSON document must be the sole
content of standard output. Both already suppress the platform banner for the same reason. The
diagnostic log still records everything there.

With `--diagnostic` enabled, the diagnostic log additionally records: every manifest read, every
property the platform did not understand, every entry that was skipped (disabled or duplicate `id`)
and why, whether each extension was loaded with or without isolation, and the fact that its hook
completed.

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

- Startup failures currently escape `CreateBuilderAsync` as an unhandled exception rather than as
  formatted platform output (§6). Should the generated entry point grow a controlled startup-error
  path, which would also help the existing configuration-file failures?
- Should de-duplication span static and dynamic registration, so an extension shipped through both
  paths registers once? That needs a registration identity the generated `SelfRegisteredExtensions`
  can also carry.
- A way to point at manifests outside the test application's directory is deferred; §3 lists the
  questions it raises.
- **Should an unresolvable non-shared dependency fail, rather than fall back to the application?**
  When an extension's `AssemblyLoadContext` cannot resolve a reference, it delegates to the default
  context — the standard `AssemblyDependencyResolver` pattern, and mandatory for framework assemblies,
  which the resolver never returns a path for. The same fallback also means a *missing* private
  dependency binds to the application's copy of that name if it has one. This is **not** a security
  question (see [Trust](#trust)): both directories are fully trusted application folders. It is a
  usability one, and the two cases are not distinguishable where the decision is made — `System.Runtime`
  and a missing `Contoso.Shared` both simply fail to resolve, so "fall back only for framework
  assemblies" has nothing to test against. A heuristic exists — treat a name the application directory
  carries but the extension could not resolve as an error — but it would forbid an extension from
  relying on a library the host ships. This iteration takes the permissive option because it is the
  standard pattern and the reversible one; a deployment mistake surfaces as an ordinary load error at
  the point of use.

### Settled

- **Version compatibility between an extension and the platform is the user's responsibility.** The
  platform shares the contract assemblies by simple name and does not validate that the extension was
  built against a compatible version. A genuine mismatch surfaces as the same `MissingMethodException`
  a statically referenced extension would produce. Introducing a declared contract version was
  considered and rejected for this iteration: it would need the extension-surface `InternalsVisibleTo`
  story ([#7739](https://github.com/microsoft/testfx/issues/7739)) resolved first, and it is not worth
  gating the feature on.

## References

- [#7739 — IVT story for MTP and extensions](https://github.com/microsoft/testfx/issues/7739)
- [#3494 / #3525 — Platform.MSBuild should allow generating a helper for registration of extensions](https://github.com/microsoft/testfx/issues/3494)
- [RFC 017 — Custom test host launcher](./017-TestHost-Launcher.md)
- [MSTest runner protocol](../mstest-runner-protocol/001-protocol-intro.md)
