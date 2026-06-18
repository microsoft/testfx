# Microsoft.Testing.Platform.DotnetTestProtocol

This package shares — **as compiled source** — the `dotnet test` named-pipe **wire contract** used between a
[Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) test host and the
`dotnet test` command line.

It exists so the protocol's serializer/field ids and handshake/session/state constants have a **single source of
truth** across the [microsoft/testfx](https://github.com/microsoft/testfx) and
[dotnet/sdk](https://github.com/dotnet/sdk) repositories, instead of being hand-copied (where any drift is a silent
wire-protocol break).

> This package is an implementation detail of `dotnet test` integration. It is **not** intended for direct
> end-user consumption.

## What's inside

The package ships two zero-dependency source files as `contentFiles` (compiled into the consuming assembly):

- `ObjectFieldIds.cs` — serializer ids and per-message field ids.
- `Constants.cs` — handshake property names, execution modes, session-event types, test states and the protocol
  version.

Because they are delivered as `contentFiles/cs/any` with `BuildAction=Compile`, the consumer compiles them into its
**own** assembly, so the `internal` types are visible without any `InternalsVisibleTo` plumbing.

## Scope

Only the wire **contract** (ids + constants) is shared here. The message **models** and **serializers** are not
included yet because they still depend on `TestMetadataProperty`
(`Microsoft.Testing.Platform.Extensions.Messages`) and use a different class shape than the SDK's copy; sharing them
requires decoupling/unifying first.
