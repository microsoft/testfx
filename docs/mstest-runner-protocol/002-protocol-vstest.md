# 002 - Changes from `vstest`

> **Current implementation note (2026-10-09):** Capability exchange and request cancellation are
> implemented by [ServerTestHost](../../src/Platform/Microsoft.Testing.Platform/Hosts/ServerTestHost.RequestExecution.cs)
> and its request-handling lifecycle; see [MTP-010](../specifications/mtp.md#mtp-010--json-rpc-initialization-and-request-scope).
> The claimed serialization-overhead reduction and non-.NET runner possibility below are
> motivation/design claims, not measured performance or interoperability evidence from this audit.

These are the main changes as a result of the use of the new protocol and self-contained executables:

- Direct communication between the IDE and the test runner over JSON, reducing the serialization/deserialization overhead.
- Future possibility of writing the test runners in their own respective languages, as long as they can start a server that uses the same protocol.
- The capabilities system. It allows client/runner to handshake which features are supported. For instance not all runners need to support the [IDE integration extensions](./003-protocol-ide-integration-extensions.md).
- Each individual request is cancellable.
