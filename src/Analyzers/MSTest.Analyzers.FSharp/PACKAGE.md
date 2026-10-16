# MSTest.Analyzers.FSharp

An opt-in, diagnostics-only analyzer package for MSTest tests written in F#. It uses
FSharp.Compiler.Service, not Roslyn, and does not change `MSTest.Analyzers` or
enable itself through `MSTest.TestFramework` or `MSTest.Sdk`.

## Rule coverage

The package ports the 77 current diagnostics whose checks apply to F#. It retains
the `MSTESTxxxx` identifiers, existing messages, severities, and disabled-by-default
design preferences. Checks use resolved F# symbols and syntax rather than matching
source text or short API names.

| Area | Diagnostics |
| --- | --- |
| Test declarations, context and fixtures | 0001-0005, 0007-0013, 0015-0016, 0019-0022, 0024, 0029-0031, 0035-0036, 0041, 0043-0045, 0050, 0056-0057, 0059-0060, 0062-0063, 0066, 0071, 0085-0086, 0088 |
| Assertions, cancellation and conditional execution | 0017, 0023, 0025, 0032, 0037-0038, 0046, 0048-0049, 0051, 0054, 0058, 0061, 0064-0065, 0067-0068, 0079-0080, 0083 |
| Data rows, data sources, member conditions and providers | 0014, 0018, 0042, 0052, 0070, 0072, 0081, 0087, 0089 |
| Parallel safety, dependencies and platform compatibility | 0073-0078, 0082, 0084 |

Every number in this table has the `MSTEST` prefix. Numeric ranges are inclusive.

### Not applicable to this host/language

| Diagnostic | Reason |
| --- | --- |
| MSTEST0026 | F# has no C#/VB conditional-access (`?.`) expression. Explicit option/null handling is not equivalent to accidentally passing a lifted null into an assertion. |
| MSTEST0027, MSTEST0028 | Suppress another Roslyn analyzer's async-suffix diagnostic; that diagnostic and suppression protocol do not run in the F# compiler. |
| MSTEST0033 | Suppresses Roslyn CS8618 for MSTest-injected properties. F# initialization/nullness diagnostics are a different compiler contract. |
| MSTEST0040 | F# does not declare C#-style `async void` methods or async-void lambdas. F# async/task computations have different return and exception semantics. |
| MSTEST0047 | Suppresses a Roslyn unused-parameter diagnostic, not an F# compiler warning. |
| MSTEST0055 | F# already warns when a non-unit result is implicitly discarded (FS0020). Explicit `ignore`, like a C# discard assignment, is intentional and should not be flagged by this rule. |
| MSTEST0069 | Belongs to the Roslyn MSTest source generator, which does not process F# projects. |

MSTEST0006, MSTEST0034, MSTEST0039 and MSTEST0053 were removed from the current
analyzer set; their identifiers are not reused.

## Installation and CLI

Use the package version matching your MSTest release:

```xml
<PackageReference Include="MSTest.Analyzers.FSharp" Version="YOUR_MSTEST_VERSION">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>analyzers</IncludeAssets>
  <GeneratePathProperty>true</GeneratePathProperty>
</PackageReference>
```

Install `fsharp-analyzers` **0.39.2** into your local tool manifest. The analyzer
targets .NET 10 and requires an SDK/CLI/editor host compatible with
**FSharp.Analyzers.SDK 0.39.2** (same major/minor). Dependencies are included in
`analyzers/dotnet/fs`; they are not compile/runtime dependencies of your tests.

Run the tool with `--project YourTests.fsproj --analyzers-path PATH`, where `PATH`
is this package's restored `analyzers/dotnet/fs` directory. Add
`--treat-as-error "MSTEST*"` to fail CI on findings. The tool, not the F# compiler,
executes these analyzers; referencing this package alone does not run analysis
during `dotnet build`.

For build integration, use the separate `FSharp.Analyzers.Build` package and its
`AnalyzeFSharpProject` target. Set `FSharpAnalyzersOtherFlags` in the project or
`Directory.Build.targets` to pass the analyzer path using
`$(PkgMSTest_Analyzers_FSharp)/analyzers/dotnet/fs`. Enable
`RunAnalyzersDuringBuild` when automatic build analysis is desired.

## Configuration

Default enablement and severities match the current Roslyn rules. To opt into
disabled design preferences or change individual severities, place
`mstest-analyzers.json` beside the `.fsproj`:

```json
{
  "MSTEST0019": "suggestion",
  "MSTEST0067": "warning",
  "MSTEST0017": "error",
  "MSTEST0032": "none"
}
```

Accepted severities are `error`, `warning`, `suggestion`/`info`, `silent`/`hint`,
and `none`. Missing files preserve defaults; malformed configuration and invalid
severity values are errors, not a silent fallback. This is project-local and
shared by the editor and CLI; it is not Roslyn `.editorconfig` integration.

The host does not provide Roslyn's MSBuild analyzer options. Build-dependent checks
can be supplied the evaluated project capabilities using the same file:

```json
{
  "IsMSTestTestAdapterReferenced": true,
  "PublishAot": false,
  "RunAOTCompilation": false,
  "MSTestHostTestClassInjectionEnabled": false,
  "mstest_parallel_safety_mode": "always"
}
```

Keep these values aligned with the consuming project's build configuration.
`mstest_parallel_safety_mode` is optional; without `always`, parallel-safety
checks require an assembly `Parallelize` attribute. Hosting checks also recognize
resolved hosting-injection calls and assembly metadata.

## Editor and suppression

FsAutoComplete/Ionide supports the editor entry point. Enable
`FSharp.enableAnalyzers` and use a compatible host. Visual Studio and Rider are
not supported by this SDK integration. Code fixes are not included.

The analyzer host supports suppression comments, for example:

```fsharp
// fsharpanalyzer: ignore-line-next MSTEST0017
Assert.AreEqual(actual, 42)
```

## F# signatures

MSTest accepts `unit`-returning instance test methods, `Task`, or `ValueTask`.
It does not accept `Async<unit>`, `Task<unit>` or `ValueTask<unit>`. Upcast a task
computation to the non-generic `Task`:

```fsharp
[<TestMethod>]
member _.Example() : Task =
    task {
        do! Task.Delay(1)
    } :> Task
```

Rules use resolved MSTest types, not short attribute or method names. Signature
files and editor snapshots without a typed tree do not produce diagnostics.
An editor snapshot without project-level information defers checks that need the
complete assembly (for example, dependency graphs and assembly-level providers).
Unresolved referenced attributes are not dereferenced; cross-version fixture
checks require the old framework dependency to be available to the compiler service.
