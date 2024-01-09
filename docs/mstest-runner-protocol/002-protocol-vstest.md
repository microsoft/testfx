# 002 - Changes from `vstest`

These are the main changes as a result of the use of the new protocol and self-contained executables:

- Direct communication between the IDE and the test runner over JSON, reducing the serialization/deserialization overhead.
- Future possibility of writing the test runners in their own respective languages, as long as they can start a server that uses the same protocol.
- The capabilities system. It allows client/runner to handshake which features are supported. For instance not all runners need to support the [IDE integration extensions](./003-protocol-ide-integration-extensions.md).
- Each individual request is cancellable.
