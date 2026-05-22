# RFC 014 - Command-line option mappings

- [ ] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

Introduce **command-line option mappings**: a new extensibility point in Microsoft.Testing.Platform (MTP) that lets an extension declaratively accept a user-facing option (e.g. `--logger trx`, `--collect "XPlat Code Coverage"`) and rewrite it, at parse time, into one or more first-class MTP options (e.g. `--report-trx`, `--coverage`). Mappings differ from regular `CommandLineOption` instances in two ways: multiple extensions are allowed to register the *same* mapping name, and exactly one of them is expected to claim responsibility for a given argument value. The primary scenario is making the migration from VSTest to MTP feel less abrupt without polluting the canonical MTP option set.

## Motivation

### The migration pain point

VSTest has long-established, user-facing options that fan out to many independent extensions:

```text
dotnet test --logger trx --logger "console;verbosity=detailed" --collect "XPlat Code Coverage" --collect "blame"
```

In MTP, each of these capabilities is exposed by a distinct extension that registers its own option(s):

| VSTest | MTP equivalent |
|---|---|
| `--logger trx` | `--report-trx` (`Microsoft.Testing.Extensions.TrxReport`) |
| `--logger "console;verbosity=detailed"` | `--output detailed` (`Microsoft.Testing.Platform`) |
| `--collect "XPlat Code Coverage"` | `--coverage` (`Microsoft.Testing.Extensions.CodeCoverage`) |
| `--collect "blame"` | `--crashdump` / `--hangdump` (`Microsoft.Testing.Extensions.CrashDump` / `…HangDump`) |

This is a deliberate design choice: each capability is its own MTP extension with its own option schema, its own validation, and its own help entry. It is also a real source of friction for the very large population of users who already type `--logger` and `--collect` from muscle memory, who have CI pipelines that pass these flags, and who follow blog posts and Stack Overflow answers that were written for VSTest.

Today, MTP responds to these flags with `Unknown option '--logger'`. That answer is correct but unhelpful.

### Why this can't be solved with regular `CommandLineOption`

The existing `ICommandLineOptionsProvider` contract forbids two extensions from registering the same option name (`CommandLineOptionsValidator` treats duplicate names as a fatal error). That rule is essential: it guarantees that for any option, there is exactly one provider responsible for its validation, its argument arity, and the meaning of `ICommandLineOptions.TryGetOptionArgumentList`.

But `--logger` and `--collect` are intrinsically polyvalent: the *value* selects which extension is responsible. There is no extension that owns `--logger` as a whole — there is one extension that owns `--logger trx`, another that would own `--logger console`, etc. The single-owner-per-option rule that protects normal options is exactly what blocks expressing this.

### Why not just add `--logger` to a single extension

A naive workaround — making, say, the TRX extension own `--logger` and dispatch by value — collapses the moment a second VSTest-compatible extension wants to participate. It also leaks an unrelated extension's identity (TRX) into the dispatcher for unrelated values (`console`, `html`). The mapping mechanism described below is the smallest surface that lets multiple extensions cooperate without breaking the single-owner invariant for canonical MTP options.

## Naming

Earlier discussion (issue [#7249](https://github.com/microsoft/testfx/issues/7249)) proposed the term **alias**. Feedback noted that "alias" in CLI conventions usually means a pure rename (`-v` ≡ `--verbose`) with identical arity and semantics, whereas this feature actually rewrites one option into one *or more* options with potentially different arity. Shell aliases (`alias ll='ls -la'`) and Git aliases are precedents for the broader meaning, but the ambiguity is real for API consumers.

This RFC uses **mapping** in the public API surface (`ICommandLineOptionMapping`, `CommandLineOptionMapping`, `CommandLineMappings`). Rationale:

- "Mapping" honestly conveys *one user-facing token mapped to one or more canonical options*.
- It avoids the rename connotation of "alias".
- It avoids the compiler-pass connotation of "transformation".
- It avoids the deprecation connotation of "shim" or "legacy", leaving room for non-VSTest uses.

User-facing documentation may still refer to these informally as "compatibility aliases" or "VSTest-style options" — the public API name does not constrain documentation prose.

## Design principles

1. **Mappings never replace canonical options.** Every capability addressable through a mapping is *also* addressable through its canonical MTP option. Mappings are purely additive sugar.
2. **Mappings are resolved before any option provider sees the command line.** A mapping turns `--logger trx` into `--report-trx` *before* `ICommandLineOptionsProvider.ValidateOptionArgumentsAsync` is called on any provider. Providers therefore never need to know that mappings exist.
3. **Exactly one mapping handles each occurrence.** Zero handlers → error. Multiple handlers → error. There is no "first match wins" silent ambiguity.
4. **Mappings cannot loop.** A mapping rewrites to canonical options only, never to other mapping names. This is a property checked at registration / validation time.
5. **Mappings have no service dependencies.** They run at command-line parse time, before the DI container is built. The contract is intentionally narrow.
6. **Mappings are opt-in for extension authors.** Existing extensions are unaffected. A new MTP project that does not register any mapping behaves exactly as today.

## Detailed design

### Public API surface

A new namespace under the existing command-line extensibility surface:

```csharp
namespace Microsoft.Testing.Platform.Extensions.CommandLine;

/// <summary>
/// Provides one or more <see cref="CommandLineOptionMapping"/> entries that rewrite a user-facing
/// option name into one or more canonical MTP command-line options at parse time.
/// </summary>
public interface ICommandLineOptionMappingProvider : IExtension
{
    IReadOnlyCollection<CommandLineOptionMapping> GetCommandLineOptionMappings();
}

/// <summary>
/// A single mapping registration. Multiple providers may register mappings with the same
/// <see cref="Name"/>; exactly one of them is expected to return true from
/// <see cref="TryMap"/> for any given argument list.
/// </summary>
public sealed class CommandLineOptionMapping
{
    public CommandLineOptionMapping(
        string name,
        string description,
        ArgumentArity arity,
        CommandLineOptionMapper map);

    /// <summary>The user-facing option name (without leading dashes), e.g. "logger" or "collect".</summary>
    public string Name { get; }

    /// <summary>Help-text description. Multiple providers registering the same name MAY have different descriptions; the help renderer concatenates them.</summary>
    public string Description { get; }

    /// <summary>Arity of the user-facing option (typically <see cref="ArgumentArity.ExactlyOne"/>).</summary>
    public ArgumentArity Arity { get; }

    /// <summary>The rewriter delegate. See <see cref="CommandLineOptionMapper"/>.</summary>
    public CommandLineOptionMapper Map { get; }
}

/// <summary>
/// Attempts to rewrite a single occurrence of a mapped option.
/// </summary>
/// <param name="arguments">The argument list provided to that occurrence. Never null; length respects <see cref="CommandLineOptionMapping.Arity"/>.</param>
/// <param name="result">When the method returns true, the canonical options this occurrence expands into; otherwise empty.</param>
/// <returns>True if this mapping claims the occurrence; false otherwise.</returns>
public delegate bool CommandLineOptionMapper(
    ReadOnlySpan<string> arguments,
    out IReadOnlyList<CommandLineOptionMappingResult> result);

/// <summary>
/// A single canonical option produced by a mapping.
/// </summary>
public sealed class CommandLineOptionMappingResult
{
    public CommandLineOptionMappingResult(string optionName, params string[] arguments);

    /// <summary>The canonical MTP option name (without leading dashes).</summary>
    public string OptionName { get; }

    /// <summary>Arguments for the canonical option. May be empty for switch-style options.</summary>
    public IReadOnlyList<string> Arguments { get; }
}
```

Registration mirrors the existing `ICommandLineOptionsProvider` pattern:

```csharp
namespace Microsoft.Testing.Platform.Builder;

public interface ICommandLineMappingsManager
{
    void AddProvider(Func<IServiceProvider, ICommandLineOptionMappingProvider> providerFactory);
}

public interface ITestApplicationBuilder
{
    // existing members ...
    ICommandLineMappingsManager CommandLineMappings { get; }
}
```

### Example: TRX

```csharp
testApplicationBuilder.CommandLineMappings.AddProvider(_ => new TrxLoggerMapping());

internal sealed class TrxLoggerMapping : ICommandLineOptionMappingProvider
{
    public string Uid => nameof(TrxLoggerMapping);
    public string Version => "1.0.0";
    public string DisplayName => "TRX logger compatibility mapping";
    public string Description => "Accepts the VSTest '--logger trx[;LogFileName=...]' syntax.";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOptionMapping> GetCommandLineOptionMappings() =>
    [
        new(
            name: "logger",
            description: "VSTest-compatible logger selector. Supported: trx[;LogFileName=<name>].",
            arity: ArgumentArity.ExactlyOne,
            map: TryMap),
    ];

    private static bool TryMap(ReadOnlySpan<string> arguments, out IReadOnlyList<CommandLineOptionMappingResult> result)
    {
        string value = arguments[0];

        if (value == "trx")
        {
            result = [new CommandLineOptionMappingResult("report-trx")];
            return true;
        }

        if (value.StartsWith("trx;", StringComparison.OrdinalIgnoreCase))
        {
            List<CommandLineOptionMappingResult> expanded = [new CommandLineOptionMappingResult("report-trx")];
            foreach (string segment in value.AsSpan(4).ToString().Split(';'))
            {
                if (segment.StartsWith("LogFileName=", StringComparison.OrdinalIgnoreCase))
                {
                    expanded.Add(new CommandLineOptionMappingResult("report-trx-filename", segment.Substring("LogFileName=".Length)));
                }
                // unknown segments fall through; see "Unknown sub-options" below.
            }

            result = expanded;
            return true;
        }

        result = [];
        return false;
    }
}
```

### Example: Code coverage

```csharp
internal sealed class CodeCoverageCollectMapping : ICommandLineOptionMappingProvider
{
    // ... metadata omitted ...

    public IReadOnlyCollection<CommandLineOptionMapping> GetCommandLineOptionMappings() =>
    [
        new(
            name: "collect",
            description: "VSTest-compatible data collector. Supported: 'XPlat Code Coverage', 'Code Coverage'.",
            arity: ArgumentArity.ExactlyOne,
            map: static (args, out result) =>
            {
                if (string.Equals(args[0], "XPlat Code Coverage", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[0], "Code Coverage", StringComparison.OrdinalIgnoreCase))
                {
                    result = [new CommandLineOptionMappingResult("coverage")];
                    return true;
                }

                result = [];
                return false;
            }),
    ];
}
```

### Resolution algorithm

`CommandLineParser` is augmented with a post-parse pass that runs **after** tokenization and response-file expansion, **before** option validation:

1. Build a lookup table `MappingsByName : string → List<CommandLineOptionMapping>` from every registered provider.
2. For each parsed option occurrence `--X v1 v2`:
   - If `X` is not in `MappingsByName`, keep the occurrence unchanged.
   - Otherwise, for every mapping registered under `X`, call `TryMap` exactly once.
     - **Zero claimants** → emit an error: `No registered mapping for '--X' can handle value 'v1'. Run '--help' to see canonical alternatives.`
     - **Multiple claimants** → emit an error: `Multiple mappings claim '--X v1': <provider Uids>. This is a configuration bug — at most one mapping may handle a given value.`
     - **Exactly one claimant** → replace the occurrence with the canonical occurrences it returned.
3. After all occurrences are resolved, re-run aggregation and arity validation against the resulting canonical-option set (existing `CommandLineOptionsValidator` code path, unchanged).

The user's original `string[] args` is preserved alongside the expanded set for diagnostics (`--info`, error messages, telemetry).

### Cross-cutting validation rules

Enforced at platform startup, before any user input is processed:

- A mapping name MUST NOT collide with any registered canonical `CommandLineOption.Name`. Diagnostic: `Mapping '--X' conflicts with the canonical option '--X' provided by <provider>. Rename one.`
- A `CommandLineOptionMappingResult.OptionName` MUST refer to a registered canonical `CommandLineOption`. Diagnostic: `Mapping '--X' rewrites to unknown option '--Y'.`
- A `CommandLineOptionMappingResult.OptionName` MUST NOT itself be a mapping name (no mapping-to-mapping rewriting; prevents loops and makes the resolution algorithm finite by construction).
- Mapping names follow the same character set as canonical option names (letters, digits, `-`, `?`).

### Interaction with existing surfaces

| Surface | Behaviour |
|---|---|
| `ICommandLineOptions.IsOptionSet("report-trx")` | Returns `true` if the user passed `--report-trx` *or* `--logger trx`. Providers don't need to know which path was taken. |
| `ICommandLineOptions.TryGetOptionArgumentList` | Same — the canonical option is what providers query. |
| `--help` | Help renderer adds a new section, **VSTest-style options (compatibility)**, listing every mapping with its description. Canonical options remain the primary listing. |
| `--info` | Echoes the original `args` *and* the resolved canonical form, so users can self-diagnose how a mapping expanded. |
| Response files (`@file.rsp`) | Mappings run after response-file expansion, so `--logger trx` works whether it was typed at the prompt or pulled from a `.rsp` file. |
| Error messages | "Unknown option '--logger'" becomes either a mapping error (if no provider claims the value) or a missing-extension hint (if the *value* is not recognised). |
| Telemetry | The resolved canonical options are what is reported. The fact that a mapping was used is reported as a single boolean (`used_compat_mapping`) without value-level detail, to avoid PII risk. |

### Backwards compatibility for mapping authors

The mapping API is additive. Existing `ICommandLineOptionsProvider` implementations are untouched. A test app that does not register `ICommandLineOptionMappingProvider` is byte-for-byte identical in behaviour to today.

### What about `--logger console;verbosity=detailed`?

In VSTest, the console logger options govern terminal output. In MTP, terminal output is governed by `--output` and the terminal-test-reporter options. A mapping for `--logger console` would naturally live in the same package that owns `--output`, and would translate `verbosity=detailed` to `--output detailed`, `verbosity=normal` to `--output normal`, etc. This is exemplary, not normative for this RFC — the mechanism is what we're shipping, not a fixed list of mappings.

### Sub-option handling and the `trx;LogFileName=` shape

`--logger trx;LogFileName=foo.trx` parses as a single argument value `trx;LogFileName=foo.trx` from MTP's standpoint (the `;` is inside the value, not a separator). The mapping is responsible for splitting on `;` and producing the right canonical options (`--report-trx --report-trx-filename foo.trx`). The platform doesn't prescribe a sub-option syntax — mappings are free to mimic VSTest's `name;k=v;k=v` exactly.

**Unknown sub-options** (`trx;UnsupportedKnob=42`): the mapping author decides. Recommended default is to ignore unknown sub-options and surface a warning via the `IOutputDisplay` available *after* resolution; mappings themselves cannot warn synchronously because they run pre-DI. A future iteration could add a structured warning channel to the `CommandLineOptionMapper` delegate.

## Drawbacks

1. **Two ways to do the same thing.** `--report-trx` and `--logger trx` will both be valid. We accept this as the price of migration ergonomics; mitigation is the dedicated "compatibility" section in `--help` and `--info`'s "resolved-to" output.
2. **Extension surface gains a concept.** Authors must now distinguish between "register an option" and "register a mapping". Mitigated by the fact that most extensions will never need a mapping — only those interested in courting VSTest users will.
3. **Mappings run before DI.** Mapping authors cannot read configuration, call services, or log diagnostics from inside `TryMap`. This is a deliberate constraint to keep the resolution step early and side-effect-free.
4. **Cross-extension cooperation by convention.** Two extensions both registering a `--collect` mapping must agree on disjoint value sets (`XPlat Code Coverage` vs. `blame`). The platform detects collisions and refuses to start, but it cannot prevent a future third party from grabbing a value another extension expected. This is identical to the existing risk for any extension namespace.
5. **VSTest semantic drift.** Some VSTest options (`--collect "blame"` collecting hangs *and* crashes, `--collect "blame;CollectHangDump"` toggling between them) have multi-extension semantics that no single mapping can express. Documented as "best-effort compatibility, not bug-for-bug compatibility".

## Alternatives

### Alternative 1 — Per-option ownership in a single dispatcher extension

Have one extension own `--logger` and dispatch to a registry. Rejected: collapses the moment a second extension wants to participate, and couples unrelated extensions' identities to the dispatcher.

### Alternative 2 — `init`/append style multi-ownership of regular options

Allow multiple providers to register the same `CommandLineOption` and call all of their `ValidateOptionArgumentsAsync` methods. Rejected: breaks the single-owner invariant for canonical options, which would force every existing provider to defensively check that *its* extension is the one responsible — the same dispatching logic, but smeared across every option in the platform.

### Alternative 3 — Pre-process `args` in the user's `Program.cs`

Document the recommended rewriting in prose and let users add a `if (args.Contains("--logger")) { … }` shim in their `Main`. Rejected: every consumer has to re-implement the same logic, every test framework's MSBuild integration needs a separate plan, and IDEs that invoke the test host directly don't get the benefit.

### Alternative 4 — Don't do this; tell users to migrate

Reject the proposal entirely and rely on documentation. This is the current state. It works for greenfield projects but is a real source of friction for the very large body of existing CI pipelines, scripts, and tribal knowledge that says "pass `--logger trx`".

### Alternative 5 — Naming variants considered

| Considered name | Why rejected |
|---|---|
| `CommandLineAlias` | Misleading; "alias" implies a pure rename. (See **Naming** above.) |
| `CommandLineOptionTransformation` | Accurate but evokes compiler passes / heavy machinery. |
| `CommandLineOptionExpander` | Accurate for 1→N case but awkward for the 1→1 case. |
| `CommandLineOptionShim` | "Shim" carries negative / temporary connotation; we may want non-VSTest uses. |
| `LegacyCommandLineOption` | Too narrow; precludes any non-compat use. |

`CommandLineOptionMapping` was chosen as the least misleading and most honest about 1→N behaviour.

## Compatibility

- **Not a breaking change.** All new types live in `Microsoft.Testing.Platform.Extensions.CommandLine` and are opt-in. Existing `ICommandLineOptionsProvider` implementations are untouched. A test app that doesn't reference any mapping provider behaves identically to today.
- **Public API additions** are listed in `PublicAPI.Unshipped.txt` of `Microsoft.Testing.Platform` per repo policy.
- **Help-text additions** require updating the wildcard expectations in:
  - `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoTests.cs`
  - `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/HelpInfoAllExtensionsTests.cs`
  - `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/MSBuild.KnownExtensionRegistration.cs`
  - `test/IntegrationTests/MSTest.Acceptance.IntegrationTests/HelpInfoTests.cs`
- **VSTest behavioural compatibility is best-effort.** Where VSTest's `--logger` / `--collect` have semantics that no single mapping can express (cross-extension `blame`, free-form data-collector keys), the mapping documents the supported subset and produces a clear error for unsupported values. We do not promise bug-for-bug compatibility.
- **No init accessors** introduced (per repo public-API guidelines).

## Phasing

| Phase | Deliverable | Owner |
|---|---|---|
| 1 | Mapping infrastructure (`ICommandLineOptionMappingProvider`, parser integration, validation, help/info plumbing). | `Microsoft.Testing.Platform` |
| 2 | First mapping shipped: `--logger trx` in `Microsoft.Testing.Extensions.TrxReport`. | TRX extension |
| 3 | `--collect "XPlat Code Coverage"` / `--collect "Code Coverage"` in `Microsoft.Testing.Extensions.CodeCoverage`. | Code coverage extension |
| 4 | `--logger console;verbosity=…` mapping co-located with the terminal test reporter options. | `Microsoft.Testing.Platform` |
| 5 | `--collect "blame"` shape mapping in `Microsoft.Testing.Extensions.CrashDump` / `…HangDump` (split-extension story documented). | Crash/Hang dump extensions |

Each phase is independently shippable. Phase 1 alone is useless to users; phase 1 + phase 2 already covers the single most common VSTest pipeline.

## Unresolved questions

1. **Mapping-author warning channel.** Should `TryMap` be able to surface a non-fatal warning (e.g. "ignored unknown sub-option `Foo`")? A pre-DI synchronous callback into a buffered `List<string>` is the cheapest design; this RFC defers the decision but reserves the option of adding a `warnings` out-parameter later in an additive way.
2. **Help integration depth.** Should mappings appear interleaved with the canonical options that they expand to, or in a separate "VSTest-style options" section? Current proposal is a separate section; reviewers may prefer interleaving for discoverability.
3. **MSBuild / `RunSettings` interplay.** VSTest's `--logger`/`--collect` can also be specified through `.runsettings`. Whether MTP's MSBuild integration should translate `.runsettings` data-collector entries into mapping invocations is out of scope for this RFC but is the natural follow-up.
4. **Telemetry granularity.** Reporting the *value* a mapping handled (e.g. `trx`, `XPlat Code Coverage`) is potentially valuable for prioritising which mappings to invest in, but mapping authors might receive arbitrary user-supplied strings. The current proposal logs only a boolean; an allowlist of well-known values could be added later.
5. **Should mappings be allowed to opt into running after configuration is loaded?** Some compatibility scenarios (e.g. respecting a `runsettings` opt-out) would benefit from access to configuration. The current proposal keeps mappings pre-DI for simplicity; a future RFC could add a "deferred mapping" tier if a concrete need arises.
