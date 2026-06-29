# Microsoft.Testing.Platform.Internal.DotnetTest

This is an **internal, source-only** package. It shares — **as compiled source** — the pieces of the `dotnet test`
↔ [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) integration that must
stay a **single source of truth** across the [microsoft/testfx](https://github.com/microsoft/testfx) and
[dotnet/sdk](https://github.com/dotnet/sdk) repositories, instead of being hand-copied (where any drift is a silent
break).

> ⚠️ This package is an **implementation detail** of `dotnet test` integration — that is what the `Internal` in the
> name signals. It ships as a **preview** package (`IsShipping=true`, always preview via `SuppressFinalPackageVersion`)
> on the same train as the rest of Microsoft.Testing.Platform so it can flow to dotnet/sdk, and is **not** intended for
> direct end-user consumption. (A *public* MTP client/orchestration library was proposed in
> [#5667](https://github.com/microsoft/testfx/issues/5667); that is a separate effort, closed pending a real use
> case.)

## What's inside

The package ships shared source as `contentFiles/cs/any` with `BuildAction=Compile`, so the consumer compiles it
into its **own** assembly and the `internal` types are visible without any `InternalsVisibleTo` plumbing:

- **Wire contract** (`contentFiles/cs/any/DotnetTestProtocol/`): `ObjectFieldIds.cs` (serializer/field ids) and
  `Constants.cs` (handshake property names, execution modes, session-event types, test states, protocol version).
- **Terminal reporter** (`contentFiles/cs/any/TerminalReporter/`): the reporter + rendering + state types and the
  small platform abstractions they need (`IConsole`/`IStopwatch`/`IColor`/`System*`, `RoslynString`,
  `ApplicationStateGuard`, `StackTraceHelper`, `TargetFrameworkParser`, `TestRunSummaryHelper`).
- **Terminal localized resources** (`build/TerminalReporter/`): `TerminalResources.resx` + `xlf/`, wired into the
  consumer by the auto-imported `build/Microsoft.Testing.Platform.Internal.DotnetTest.props` (it generates the
  strongly-typed `TerminalResources` accessor in the expected namespace and, where XliffTasks is present, emits
  satellite assemblies for each language).

## Consuming it (plug-in requirements)

Reference the package and the source compiles into your assembly. For the terminal reporter source the consumer
must have:

- **`ImplicitUsings` enabled** — the build props supplies the extra global usings the shared source relies on
  (`System.Text`, `System.Runtime.CompilerServices`, `System.Runtime.Versioning`, …) when implicit usings are on.
- **A `LangVersion` that supports the `field` keyword** (preview/latest).
- **The `Microsoft.CodeAnalysis.EmbeddedAttribute` polyfill** — dotnet/sdk already defines it (for its copied IPC
  transport); other consumers must supply a tiny internal copy.
- **XliffTasks** (only for localized satellites) — dotnet/sdk has it via Arcade.

The wire-contract source is zero-dependency and needs none of the above.

## Scope

The message **models** and **serializers** are not shared yet because they still depend on `TestMetadataProperty`
(`Microsoft.Testing.Platform.Extensions.Messages`) and use a different class shape than the SDK's copy; sharing them
requires decoupling/unifying first.
