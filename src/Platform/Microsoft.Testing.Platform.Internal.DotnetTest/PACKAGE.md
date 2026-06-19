# Microsoft.Testing.Platform.Internal.DotnetTest

This is an **internal, source-only** package. It shares — **as compiled source** — the pieces of the `dotnet test`
↔ [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) integration that must
stay a **single source of truth** across the [microsoft/testfx](https://github.com/microsoft/testfx) and
[dotnet/sdk](https://github.com/dotnet/sdk) repositories, instead of being hand-copied (where any drift is a silent
break).

> ⚠️ This package is an **implementation detail** of `dotnet test` integration — that is what the `Internal` in the
> name signals. It is `IsShipping=false` (not published to nuget.org) and is **not** intended for direct end-user
> consumption. (A *public* MTP client/orchestration library was proposed in
> [#5667](https://github.com/microsoft/testfx/issues/5667); that is a separate effort, closed pending a real use
> case.)

## What's inside

The package ships shared source files as `contentFiles/cs/any` with `BuildAction=Compile`, so the consumer compiles
them into its **own** assembly and the `internal` types are visible without any `InternalsVisibleTo` plumbing.

Today it ships the `dotnet test` named-pipe **wire contract**:

- `ObjectFieldIds.cs` — serializer ids and per-message field ids.
- `Constants.cs` — handshake property names, execution modes, session-event types, test states and the protocol
  version.

It is designed to grow to host the other source shared with `dotnet test` (e.g. the terminal reporter).

## Scope

Only the wire **contract** (ids + constants) is shared today. The message **models** and **serializers** are not
included yet because they still depend on `TestMetadataProperty`
(`Microsoft.Testing.Platform.Extensions.Messages`) and use a different class shape than the SDK's copy; sharing them
requires decoupling/unifying first.

