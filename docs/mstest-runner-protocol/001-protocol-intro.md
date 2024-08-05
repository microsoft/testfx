# 001 - MSTest Runner Protocol

MSTest Runner projects builds into a self-contained executable that can be invoked to run tests.
The protocol describes the communication between the client (IDE/CLI/CI) any the MSTest Runner executable (also refered to as the server).

The communication is based on JSON-RPC and describes the RPC messages sent in order to support running of tests.

> [!TIP]
> Here is the summary of [differences compared to vstest](./002-protocol-vstest.md).

## API overview

Here's the current list of APIs that supported by the client.

- Initialization
  - [initialize](#capabilities-overview) - Request sent to server to establish the client/server configuration and handshake the set of features
- Test discovery and run requests
  - [testing/discoverTests](#discovery-of-tests) - Requests a server to discover tests without executing them
  - [testing/runTests](#execution-of-tests) - Requests a server to execute tests and report their results
  - [testing/testUpdates/tests](#discovery-of-tests) - Notifies client about test updates (test cases and test results)
  - [testing/testUpdates/attachments](#execution-of-tests) - Notifies client about additional attachments (trx/coverage)
- Client notifications updates
  - [client/launchDebugger](#launch-debugger) - Requests a client to launch a process with debugger attached to it
  - [client/attachDebugger](#attach-debugger) - Requests a client to attach a debugger to a process by pid
  - [client/log](#logging-of-messages) - Notifies a client to logs a message to the output window
- Miscellaneous requests
  - [telemetry/update](#telemetry) - Sends telemetry data to the client
  - [exit](#exit) - Notifies the server to stop the server process
  - [$/cancelRequest](#cancellation-of-requests) - Notifies the client/server to cancel an in-flight request

## API extension overview

The protocol is extensible, where the client and the server handshake the capabilities they support.

For instance not all servers need to support [IDE integration](./003-protocol-ide-integration-extensions.md) or new versions
of the MSTest runner, might support additional RPC requests/responses or add additional properties to the payloads.

[IDE integration](./003-protocol-ide-integration-extensions.md) describes the protocol extensions used to support features like run from editor and how to control the values shown in the test explorer.

### Basic protocol format

The messages are based on the JSON-RPC protocol.

There's three kinds of messages: requests, responses and notifications.

For requests and responses the caller sends a request with an `id`, while the callee responds back with a response setting the same `id` and a `result`/`error`.

If the server is expected to corelate multiple messages to the request, an additional guid needs to be sent.
For instance, the `testing/discoverTests` API specifies a `RUN_ID` token and this token is later also
specified in the `testing/testUpdates` notifications. This way, multiple discovery requests can be processed
in parallel and the client can map back the notifications to the corresponding originating request.

An example of a request:

```json
{
    "jsonrpc": "2.0",

    /*
     * The id is the unique identity of the request.
     * It is used to correlate the request/response and cancellation messages.
     *
     * Note: The `id` field should not be used inside of the `params`/`result`, since it's an implementation detail of the RPC. Instead if the API expects some shared token the client should generate a guid and pass that across multiple related requests.
     */
    "id": 1,
    "method": "some/method",
    "params": { "prop": "a" }
}
```

An example of a response:

```json
{
    "jsonrpc": "2.0",
    "id": 1,
    "result": { "some": "data" }
}
```

An example of an error:

```json
{
    "jsonrpc": "2.0",
    "id": 1,
    "error": {
        "code": 12,
        "data": { "some": "data" },
        "message": "this request has failed"
    }
}
```

For notifications the `id` is omitted, to signify that the callee does not respond to that message.

An example of a notification:

```json
{
    "jsonrpc": "2.0",
    "method": "some/event",
    "params": { "tag": "value" }
}
```

### Error codes

If a request fails part of the response is the `error.code`.
The error code should reflect the type of the exception that caused the RPC failure.

These are the known error codes:

```typescript
namespace ErrorCodes {
    // Defined by JSON-RPC
    export const ParseError: integer = -32700;
    export const InvalidRequest: integer = -32600;
    export const MethodNotFound: integer = -32601;
    export const InvalidParams: integer = -32602;
    export const InternalError: integer = -32603;

    /**
     * This is the start range of JSON-RPC reserved error codes.
     * It doesn't denote a real error code. No LSP error codes should
     * be defined between the start and end range. For backwards
     * compatibility the `ServerNotInitialized` and the `UnknownErrorCode`
     * are left in the range.
     *
     * @since 3.16.0
     */
    export const jsonrpcReservedErrorRangeStart = -32099;

    // Error code to be thrown if the server has not yet been initialized.
    export const serverNotInitialized = -32002;

    /**
     * This is the end range of JSON-RPC reserved error codes.
     * It doesn't denote a real error code.
     *
     * @since 3.16.0
     */
    export const jsonrpcReservedErrorRangeEnd = -32000;

    export const testingPlatformErrorRangeStart = -31700;

    // If a top level assertion has failed when running tests.
    // TODO: Decide if we can forward all assertion failures and attach them to test nodes.
    export const AssertionFailed: integer = -31001;

    export const testingPlatformErrorRangeEnd = -31000;
}
```

### Request, Notification and Response Ordering

> [!NOTE]
> This is taken and paraphrased from [LSP Specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#capabilities).

Responses to requests should be sent in roughly the same order as the requests appear on the server or client side. So for example if a server receives a `testing/discoverTests` request and then a `testing/runTests` request it will usually first return the response for the `testing/discoverTests` and then the response for `testing/runTests`.

However, the server may decide to use a parallel execution strategy and may wish to return responses in a different order than the requests were received. The server may do so as long as this reordering doesn’t affect the correctness of the responses. For example, reordering the result of `testing/discoverTests` and `testing/runTests` is allowed, as each of these requests usually won’t affect the output of the other. On the other hand, the server most likely should not reorder `testing/testUpdates/tests` updates where the result for the test is sent before it is discovered.

Similarly the `testing/testUpdates/tests` completed notification must be send after all of the updates have been sent.
This way a client can use the completed message to know that there will be no more updates sent for that run.

## Capabilities overview

After starting the test runner, the first thing that the client should do
is to exchange with the server the capabilities.

Given that there could be many different clients IDEs/CIs/CLIs and servers this allows
the client to understand what the server supports and the server to understand
what the client supports and limit functionality based on unsupported features.

> [!NOTE]
> Since the capabilities are fetched by sending an RPC request to a started executable, these can only be queried by the client after
> the project was successfully built.

### Determine capabilities during test runner initialization

> [!NOTE]
> Capabilities initialiation is based on the `initialize` request from LSP.
> In theory this allows any LSP server to also act as a test runner server, as long as it handles the additional RPC requests.

Request:

- method: `initialize`
- params: `InitializeParams` defined as follows:

```typescript
interface InitializeParams {
    // The process Id of the parent process that started the server. Is null if
    // the process has not been started by another process. If the parent
    // process is not alive then the server should exit (see exit notification)
    // its process.
    processId: PID,

    clientInfo: {
        // The name of the client.
        name: string,

        // This is the version of the client's protocol and should follow
        // semver.
        // The client and the server should be able to communicate
        // only if the major version stays the same.
        // The client can attempt to fallback on the older protocol
        // version if it has such a fallback and send another INITIALIZE
        // request.
        version: string,
    },

    capabilities: {
        // Note: Since the initialize message is compatible with the LSP protocol,
        // we should make sure that the capability paths for the testing features are unique.
        // As such, we put all of them under a single testing namespace.
        // This reduces collisions with other LSP capabilities.
        testing: {
            // If true, the client supports the client/attachDebugger and client/launchDebugger requests.
            debuggerProvider: true,

            // If true, the client can receive a batch of log messages under client/log request.
            batchLoggingSupport: true,

            // If true, the client supports the testing/testUpdates/attachments request.
            attachmentsSupport: true,

            // If true, the client support a port to which child processes
            // can connect to.
            // Note: The test runner is expected to ensure the synchronization of messages
            // for instance if additional processes are sending test updates
            // or attachment updates, these must complete before the
            // test runner sends the completion notification.
            callbackProvider: {
                port: integer
            }
        },
    }
}
```

Response:

- result: `InitializeResponse` defined as follows:

```typescript
interface InitializeResponse {
    serverInfo: {
        // The name of the server.
        name: string,

        // The server's protocol version.
        version: string
    },
    capabilities: {
        testing: {
            // Experimental: The client currently uses this variable to determine if the test runner process can
            // handle multiple discover/run requests. If true, then the client can keep the process alive.
            // This has a potential performance benefit, where startup time and time to load test assemblies/sources
            // only needs to occur once.
            experimental_multiRequestSupport: boolean;

            // If true, the server will send attachments, on top
            // of test updates during test runs.
            // The client will then wait on both to complete,
            // before it marks a test run as completed.
            attachmentsProvider?: boolean;
        },
    }
}
```

#### Versioning capabilities

Any behaviour changes should be announced via capabilities, where the client/server
should announce what kinds of additional RPC requests and responses they can handle.

For instance, let's say the server would like to be make the file/line location lazy.
The client should announce that is supports lazy locations.
If the capability is present the server can then send lazy location data.
If the capability is missing/or false the server should assume that the client would not handle
lazy locations and send the full location.

> [!NOTE]
> Discovery/Run requests, as well as TestNode format specified in the intial release of the protocol
> should be supported by all clients/servers.
> As such, they're not expressed via capabilities.

## Callback provider

In some cases the test runner might want to start additional child processes. If client has the `testing.callbackProvider` capability, the client
will provide a port for multiple connections.

This allows for instance for the test runner to start multiple child processes that it distributes test run over, while each of these child processes can directly send callbacks to the client with test node updates, rather than have to relay all information via the main test runner node.

Another use case is the collection of hang dumps, crash dumps, in which case the test runner node might crash, while the hang dump watcher process can still send back the hang dump/crash dump attachment, even after the crash.

## Discovery and run requests

The two most basic features that the MSTest runner should provide is the capability to discover and run tests.

Since, in most cases the runner loads and runs user code in the runner's process, the expected lifetime of
the test runner is a single run.

This is the typical lifetime of a test runner process:

- The client starts a test runner.
- The client sends an `initialize` request.
  - **Note:** This is part of the [capabilities check](#determine-capabilities-during-test-runner-initialization) and is used to handshake the client's and the server's capabilities.
  - The test runner sends an `initialize` response.
- The client sends multiple `testing/discoverTests`/`testing/runTests` requests.
  - The test runner sends notifications, such as `testing/testUpdates/tests` for these requests.
  - The test runner can also send requests such as `client/attachProcess` as a result
      of processing these requests.
  - The test runner sends a response for these requests.
  - **Note:** At any point of time the client can cancel these requests.
  - **Note:** If server does not have `experimental_multiRequestSupport` capability, the process needs to be exited after each individual request.
- The client shuts down the test runner.

### Data format

#### Test node

A core object for a test runner is a test node. It encompasses the information about the test
(such as its display name, source information), as well as the result information (such as
the duration, outcome).

Test nodes are used to display the test results in a UI and also are used in the `testing/runTests`
tests API. The client may request a list of tests via the `testing/discoverTests` request,
then start a new process and request a run via `testing/runTests` request. As a result
the test node must be serializable.

> [!NOTE]
> Additional TestNode's properties are
> explained in the [IDE integration](./003-protocol-ide-integration-extensions.md#test-nodes-property-extensions).

```typescript
interface TestNode {
    // Unique name (id) of the test.
    // Used by client for equality of tests to detect test
    // updates.
    'uid': string,

    // The string with which the client should display the node.
    // Example: "display-name": "SomeTestMethod()",
    'display-name': string,

    // The type of the node. It's used by the client to determine,
    // which nodes should be displayed if grouped by something else that
    // the default hierarchy.
    // In particular either an empty theory node, or the corresponding tests
    // should be displayed.
    // Currently the two supported node types are action (a unit test) or a group
    // (a namespace/class/test suite).
    // Example: "node-type": "action",
    'node-type': 'action' | 'group',

    // Note: Additional properties may be provided by the framework.
    // Used to serialize/deserialize the test case between runs.

    // Location properties:

    // Example: "location.file": "C:\\Users\\mabdullah\\source\\repos\\Demos\\TestAnywhere\\test1\\UnitTest1.cs",
    'location.file'?: string;

    // Example: "location.line-start": 6,
    'location.line-start'?: number;

    // Example: "location.line-end": 9,
    'location.line-end'?: number;

    // Result properties:

    // Example: "execution-state": "failed",
    'execution-state': ExecutionState;

    // While the execution-state is meant to be interpreted by the client,
    // for instance to count how many tests have passed/failed, to show the
    // relevant icon under the test, an outcome kind allows the adapter to
    // send the more specific outcome name. For instance the execution-state
    // should report a test as "passed" whereas the outcome kind can report
    // the test as passed-with-retries.
    // This is only used for searching/filtering on the client side and should
    // not be interpreted in any way.
    // Example: "outcome-kind": "passed-with-retries"
    'outcome-kind'?: string;

    // Example: "time.duration-ms": 45.8143,
    'time.duration-ms'?: number;

    // Example: "time.start-utc": "2023-06-20T11:09:41.6882661+00:00"
    'time.start-utc'?: string;

    // Example: "time.stop-utc": "2023-06-20T11:09:41.6882661+00:00"
    'time.stop-utc'?: string;

    // Note: The error consists of the stacktrace, error message and also the assertion properties.
    // If assertion properties are missing the exception would show in the UI as:
    // Message:
    //   ERROR MESSAGE
    // Stack trace:
    //   STACK TRACE
    //
    // If assertion properties are defined the exception would show in the UI as:
    // Message:
    //   ERROR MESSAGE
    // Actual:
    //   ACTUAL ASSERT VALUE
    // Expected:
    //   EXPECTED ASSERT VALUE
    // Stack trace:
    //   STACK TRACE

    // Example: "error.message": "Exception of type 'Microsoft.Testing.Framework.AssertFailedException'"
    'error.message'?: string;

    // Example: "error.stacktrace": "   at Microsoft.Testing.Framework.Assert.IsTrue(Boolean condition) in /_/src/Microsoft.Testing.Framework/Assertions/Assert.cs:line 36\r\n
    'error.stacktrace'?: string;

    // Example: "assert.actual": "false"
    'assert.actual'?: string;

    // Example: "assert.expected": "true"
    'assert.expected'?: string;

    // Key value pairs of traits. Traits have name and value. Frameworks that only support categories,
    // can leave the value null, or empty.
    // Example: "traits": [{ "trait1": "traitValue1"}, {"trait2": "traitValue2" }, { "category1": null }]
    'traits': Trait[];
}

// The adapter can specify an outcome for the test.
// Commonly used outcomes are specified here (to avoid
// having some adapters report timed-out outcome).
// That said custom outcomes can be provided as long as the client
// lists in its capabilities that it supports such an outcome.
// Note: The client might not support these outcomes, in which
// case the server should report one of the TestNodeBaseOutcome.
// For instance it should report "passedWithRetries" tests as "passed"
// if the client does not support a "passedWithRetries" outcome.
type ExecutionState =
    'discovered'
    | 'in-progress'
    | 'passed'
    | 'skipped'
    | 'failed'
    | 'timed-out'
    | 'error'
    | "cancelled"

interface Trait {
    key: string;
    value: string?;
}
```

### Discovery of tests

> Message direction: Client -> Server

Discovery allows the clients to understand what are all tests available in a
given project.

A client might want to list all tests in a given test runner
without running them so that it can show these tests in various UI elements.

Request:

- method: `testing/discoverTests`
- params: `DiscoverTestsParams` defined as follows:

```typescript
interface DiscoverTestsParams {
    runId: GUID
}
```

Notifications:

- Test update notification
  - method: `testing/testUpdates/tests`
  - params: `TestUpdateNotificationParams` defined as follows:

    ```typescript
    // Note: These notifications contain a stream of updates and the client should
    // aggregate these. For instance, a server might split tests discovered from within
    // a single class into multiple notifications.
    interface TestUpdateNotificationParams {
        // List of test node changes.
        // These should be processed in order and also should be complete,
        // i.e. a server will send an update for the test, if it already sent updates
        // for all of the parent nodes.
        changes: TestUpdateChange[]

        // Run id for which the notification is sent. It should match the id sent during the discovery request.
        runId: GUID
    }

    interface TestUpdateChange {
        // The id of the parent node.
        // If null, this is the top-level node.
        // For example, if a test node belongs to a suite, this will
        // specify the id of the test suite.
        parent?: string;

        // The node being updated.
        node: TestNode;
    }
    ```

- Test update complete notification
  - This notification is sent after all test update notifications are sent beforehand.
      As soon as the client processes this notification it is guaranteed to be done
      processing all node update notifications.
  - Implementation detail: if a server sends callback notifications and returns a JSON-RPC response, vs-streamjsonrpc may resolve them in any order. As such, awaiting of the response is insufficient to guarantee that all updates were processed by the callback handler.
  - method: `testing/testUpdates/tests`
  - params: `TestUpdateNotificationParams` where `params.children == null`.

Response:

- result: `void`
- error: code and message set in case an exception happens during the discovery request

### Execution of tests

> Message direction: Client -> Server

Execution allows the clients to run tests available to a given runner
and report on their results.

The client can pass in a scope that needs to be run or a list of test cases.
The test cases should have been previously discovered and reported
in the same way back to the server.

The scope defines the location that the client has selected, that isn't a test,
for instance a file, a class, a project.

Request:

- method: `testing/runTests`
- params: `RunTestsParams` defined as follows:

```typescript
interface RunTestsParams {
    // The set of tests selected by the user to run.
    // If not specified all tests will run.
    testCases?: TestNode[],

    // Token which should be specified for all update notifications.
    // This way the client can under which the update notifications should be reported.
    runId: GUID
}
```

Notifications:

- Test node updates
  - method: `testing/testUpdates/tests`
  - params: `TestUpdateNotificationParams` as described under [discovery request](#discovery-of-tests)
- Test node updates completed
  - This notification is sent after all test update notifications are sent beforehand.
      As soon as the client processes this notification it is guaranteed to be done
      processing all node update notifications.
  - method: `testing/testUpdates/tests`
  - params: `TestUpdateNotificationParams` where `params.testCases == null`.
- Attachment updates
  - method: `testing/testUpdates/attachments`
  - params: `AttachmentUpdatesParams` defined as follows:

    ```typescript
    interface AttachmentUpdatesParams {
        attachments?: Attachment[],
        runId: GUID
    }
    ```

- Attachment updates completes
    method: `testing/testUpdates/attachments`
    params: `AttachmentUpdatesParams` where `params.attachments == null`

Response:

- result: `RunTestsResponse` defined as follows:

```typescript
interface RunTestsResponse {
    // Note: These are the attachments related to the entire run (such as a trx file)
    // rather than to a single test result.
    // Note: If multiple data collectors are generating attachments, they should send separate
    // attachment events.
    attachments?: Attachment[]
}

interface Attachment {
    // OPTIONAL: If the attachment is based on a file (and the client can show a hyperlink to it)
    //           the file's location should be specified by this property.
    // Example: "uri": "file://some/coverage.trx"
    uri: string,

    // The name of the extension that generated the attachment.
    // Note: For the time being the client does not special case attachments
    //       based on their producer.
    // Example: "producer": "TrxReportGeneratorProcessLifetimeHandler"
    producer: string,

    // OPTIONAL: The file extension can be used to resolve the attachment type
    //           if that isn't ambiguous.
    // Note: For the time being the client does not special case attachments
    //       based on their type.
    // Example: "type": "file"
    type: string,

    // How the attachment can be displayed as by the client.
    // Example: "display-name": "Code Coverage"
    displayName: string;

    description: string;
}
```

- error: code and message set in case an exception happens during the discovery request

## Miscellaneous requests

### Telemetry

> Message direction: Server -> Client

Requests the client to report telemetry data about the given run.
This is specific to the server and the client.

Request:

- method: `telemetry/update`
- params: `TelemetryUpdateParams` defined as follows:

```typescript
interface TelemetryUpdateParams {
    // The name of the telemetry event to report.
    // For instance a Microsoft Testing Platform runner might report an event for `dotnet/testingplatform/execution/TestsRun`.
    eventName: string;

    // A dictionary that maps multiple metrics for telemetry.
    // For instance run telemetry might report, how much time the handler took vs how much time
    // the test adapter took to run the tests.
    metrics: any;
}
```

### Exit

> Message direction: Client -> Server

Requests the server to complete processing of all its requests
and gracefully terminate.

Notification:

- method: `exit`
- params: `{}`

### Cancellation of requests

> Message direction: Both ways

There should be a generic way of cancelling requests,
whether it's a discovery request or a run request.

This would make it possible to cancel discovery/run requests,
or request the client to cancel launching of a debugger process if necesary.

Notification:

- method: `$/cancelRequest`
- params: `CancelParams` defined as follows:

```typescript
interface CancelParams {
    id: number;
}
```

## Host requests

### Launch debugger

> Message direction: Server -> Client

Requests a client to attach a debugger to the spawned child process.

Request:

- method: `client/launchDebugger`
- params: `LaunchDebuggerParams` defined as follows:

```typescript
interface LaunchDebuggerParams {
    // The name of the program to launch.
    // Example: 'a.exe'
    program?: string;

    // Arguments to be passed into the process.
    // Example: '--verbose --output a.txt'
    args?: string;

    // Working directory of the started process.
    // Example: 'C:\Users\me\Code\projectA'
    workingDirectory?: string;

    // Any additional environment variables to be passed in.
    // Example: { 'C_LANG': 'EN_US' }
    environmentVariables?: any;
}
```

### Attach debugger

> Message direction: Server -> Client

Requests a client to attach a debugger to the spawned child process.

Request:

- method: `client/attachDebugger`
- params: `AttachDebuggerParams` defined as follows:

```typescript
interface AttachDebuggerParams {
    // The id of the process to which to attach the debugger.
    // Note: Currently the assumption is that the language of the project for which the
    //       tests were started implies the language of the process under processId.
    processId: number;
}
```

Response:

- result: `AttachDebuggerResponse` defined as follows:

```typescript
interface AttachDebuggerResponse {
    success: boolean;
}
```

### Logging of messages

> Message direction: Server -> Client

Notifies the client to log a message.
Messages are logged to the output window.

Notification:

- method: `client/log`
- params: `LogMessageParams | BatchLogMessageParams` defined as follows:
- capability: If `testing.batchLoggingSupport` is true, the server can send BatchLogMessageParams instead.
  The client will check the existence of `messages` property to determine if a batch was sent instead of a single
  message. If `testing.batchLoggingSupport` is false, the server cannot send `BatchLogMessageParams` messages to the client.

```typescript
interface LogMessageParams {
    level: TestingPlatformLogLevel;
    message: string;
}
```

```typescript
interface BatchLogMessageParams {
    // If specified the message batch should be attributed to a specific run.
    // Specifically, combined with the nodeUid, property the client should attribute
    // the messages to a specific TestNode, rather than render them globally.
    runId?: GUID;

    // List of messages to log.
    messages: LogMessage[];
}

interface LogMessage {
    // If a log message should be attributed to a single node, rather than be global.
    nodeUid?: GUID;

    // The level of a single log message.
    // Messages can have different log levels within a batch.
    level: TestingPlatformLogLevel;

    message: string;
}
```

```typescript
enum TestingPlatformLogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}
```
