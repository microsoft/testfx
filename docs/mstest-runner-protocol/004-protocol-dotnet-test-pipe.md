# 004 - `dotnet test` Named-Pipe Protocol

This document is the descriptive specification of the **`dotnet test` pipe protocol** (a.k.a. the
`dotnettestcli` protocol): the small, versioned, **binary** protocol used between a
Microsoft.Testing.Platform (MTP) test application and the `dotnet test` implementation shipped in the
.NET SDK.

> [!IMPORTANT]
> **Protocol vs. transport.** The wire protocol described in this document - message shapes, serializer
> IDs, field IDs, the handshake, versioning - is **transport-neutral**. It is defined purely in terms of a
> duplex byte stream: whoever creates the stream, and how, is a separate concern. Two transports exist
> today:
>
> - **Named pipe** (`System.IO.Pipes`, §3) - the original and still-default transport, activated by
>   `--dotnet-test-pipe <name>`. Everything in §3 through §13 below describes this transport unless noted.
> - **WebSocket** (§15) - required on runtimes that cannot open named pipes (`browser-wasm`), and optional
>   elsewhere, activated by `--dotnet-test-transport websocket` plus `--dotnet-test-websocket-endpoint`
>   and `--dotnet-test-websocket-token`.
>
> Sections 4 ("Framing"), 5 ("Body serialization format"), 6 ("Serializer registry"), 7 ("Request/reply
> model"), 8 ("Handshake & version negotiation"), 9 ("Message catalog"), and 10 ("Versioning &
> compatibility") apply **identically** to both transports - they operate on "the stream", not
> specifically "the pipe". Only §3 ("Pipe naming & transport") and §11-§12 (connection-loss and the
> reverse control channel) describe named-pipe-specific behavior; see §15 for the WebSocket equivalents
> and the (currently pipe-only) gaps.

It is a *different* protocol from the [JSON-RPC server-mode protocol](./001-protocol-intro.md):

| | JSON-RPC server mode | `dotnet test` pipe protocol (this doc) |
| --- | --- | --- |
| Activation | `--server` (or `--server jsonrpc`) | `--server dotnettestcli --dotnet-test-pipe <name>` |
| Wire format | JSON-RPC 2.0 over stdio / TCP | Custom length-prefixed **binary** framing over a named pipe |
| Shape | Full request/response + notifications, bidirectional | **Push-only** data channel (host → SDK) + auxiliary reverse control channel |
| Role of the runner | Server | Client (connects out to the SDK's pipe server) |

> [!IMPORTANT]
> **Source of truth.** The wire contract (serializer IDs, field IDs, handshake/session/state
> constants, message models, and serializers) lives in
> `src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/` and
> `src/Platform/Microsoft.Testing.Platform/IPC/`. Those files are **vendored (hand-copied) into
> `dotnet/sdk`**. testfx is authoritative; any change to the shared files is a coordinated,
> potentially breaking, wire-protocol change. The enumeration of shared files is
> `ServerMode/DotnetTest/DotnetTestProtocolContract.props`.
>
> **Shared wire contract vs. per-repo transport.** Only the *wire contract* (serializer/field IDs,
> message models, serializers, handshake/state constants) is shared source. The **named-pipe transport**
> (framing loop, buffering, connection-loss reactions, unknown-message handling, pipe-name resolution) is
> deliberately **not** shared and is implemented independently on each side (`NamedPipeConnectionBase`,
> `NamedPipeServer`, `NamedPipeClient` are local to testfx). Wherever this document describes transport
> *behavior*, it describes testfx's implementation. The current `dotnet/sdk` data-pipe server implements
> its own transport and **diverges** in several places (called out inline below); such behaviors are not
> protocol guarantees and must not be assumed to be symmetric across both sides.

---

## 1. Terminology

- **Test application / test host** — the MTP-based test executable (the process being run by
  `dotnet test`). In this protocol it is the **pipe client**.
- **SDK / `dotnet test`** — the .NET SDK component that launches test applications and renders their
  output. It is the **pipe server** (it creates and listens on the data pipe).
- **Data pipe** — the primary named pipe, whose OS name is supplied via `--dotnet-test-pipe`. All
  test data flows host → SDK on it.
- **Server-control pipe** — an optional *reverse* named pipe, created and listened on by the SDK, used
  to push control signals (today only session cancellation) SDK → host. Its name is advertised in the
  handshake reply.
- **Execution ID** — a GUID (`"N"` format) identifying one **test-application execution** (one root
  test-app process and its child process tree). It is shared across that test host, its test host
  controller, and its orchestrator processes via an environment variable. It is **not** a per-CLI-command
  identifier: a single `dotnet test` command that launches several test applications (e.g. a
  multi-project run) gives each root test application its **own** Execution ID by default.
- **Instance ID** — a GUID (`"N"` format) identifying one specific connecting process/`DotnetTestConnection`.
- **Attempt number** — a 1-based retry generation within one Execution ID. Attempt `1` is the initial run.
  Multiple test-host instances (for example, shards) can belong to the same attempt; a new Instance ID does
  not by itself imply a retry.
- **Session UID** — the MTP test session identifier for a run.

---

## 2. Activation & configuration

The SDK starts the test application with:

```text
<testapp> --server dotnettestcli --dotnet-test-pipe <osPipeName> [other options]
```

Rules and behavior:

- `--server` accepts zero or one argument. The value `dotnettestcli` (case-insensitive) selects this
  protocol. `--server jsonrpc` (or `--server` with no value) selects JSON-RPC server mode instead.
- `--server dotnettestcli` **requires** exactly one pre-launch transport to be fully specified: either
  `--dotnet-test-pipe` (the default named-pipe transport, §3) or `--dotnet-test-transport websocket`
  together with `--dotnet-test-websocket-endpoint` and `--dotnet-test-websocket-token` (§15). Specifying
  neither, both, or an incomplete WebSocket option set fails command-line validation with an actionable
  message (`PlatformCommandLineDotnetTestCliRequiresPipe`, `PlatformCommandLineDotnetTestTransportConflict`,
  `PlatformCommandLineDotnetTestWebSocketRequiresEndpointAndToken`, ...). Selecting the named-pipe transport
  on a runtime that cannot open one (`browser-wasm`, `wasi-wasm`) also fails validation early instead of
  crashing deep inside the connection bootstrap.
- `--dotnet-test-pipe` takes **exactly one** argument: the fully-resolved OS pipe name/path the SDK is
  already listening on (see §3).
- All of these options are built-in and **hidden**: the platform omits them from `--help`, so they are
  only listed by `--info`. They are not meant for direct end-user use.

### Environment variables

| Variable | Set by | Purpose |
| --- | --- | --- |
| `TESTINGPLATFORM_DOTNETTEST_EXECUTIONID` | The root test application on first connect (if not already set) | The Execution ID. Each root test-app process generates its own value in `AfterCommonServiceSetupAsync` (if unset) and propagates it via the environment **only to its child process tree** (its test host controller, orchestrator). So all handshakes *within one test-application execution* share it, but separate test applications launched by one multi-project `dotnet test` command normally get **different** IDs. If already present it is **not** overwritten. |
| `TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER` | Retry orchestrator | The positive, 1-based retry attempt assigned to a child test host. A normal test host reports attempt `1` when the variable is absent. Invalid values fail before the handshake instead of being silently reinterpreted. |
| `TESTINGPLATFORM_PIPE_DIRECTORY` | User (optional) | On Unix, overrides the directory used to place the domain-socket file, **for pipes created by testfx's `NamedPipeServer`** (see §3). It does **not** relocate a pipe created by the other side: the current `dotnet/sdk` data-pipe server still resolves Unix names as `Path.Combine("/tmp", name)` and does not honor this variable/`TMPDIR` (nor the 103-byte precheck). Since the SDK creates the data pipe, this variable effectively only affects pipes testfx itself creates (e.g. the sibling controller pipe). |

---

## 3. Pipe naming & transport

The pipe is a `System.IO.Pipes` named pipe opened as `PipeDirection.InOut`,
`PipeTransmissionMode.Byte`, `PipeOptions.Asynchronous`, and — on .NET (Core) — `PipeOptions.CurrentUserOnly`
(hardens the ACL so only the current user can connect).

**Pipe name resolution invariant:** the process that *creates* the pipe (the SDK for the data pipe; the
SDK for the control pipe) resolves the name locally and hands the **fully-resolved** value to the peer.
The peer uses it verbatim and never recomputes it. This keeps the SDK and host versions decoupled.

`NamedPipeServer.GetPipeName(name)` (testfx's server-side helper) computes the OS name:

- **Windows:** `testingplatform.pipe.<name-with-'\'-replaced-by-'.'>`.
- **Unix:** a domain-socket **file path** `Path.Combine(<directory>, name)` where `<directory>` is
  resolved with precedence:
  1. `TESTINGPLATFORM_PIPE_DIRECTORY` (explicit override; created & write-probed, fails fast with an
     actionable message if unusable),
  2. `Path.GetTempPath()` (honors `TMPDIR`),
  3. `/tmp` (legacy default).
  The path is normalized to an absolute path. Its UTF-8 byte length must be ≤ **103** bytes
  (`sockaddr_un.sun_path` budget: 104 − 1 for the NUL terminator, using macOS' smaller limit for
  portability); otherwise creation fails fast.

> [!NOTE]
> This resolution describes testfx's `NamedPipeServer`. On the **data** pipe the SDK is the creator, and
> the current `dotnet/sdk` server resolves Unix names as `/tmp/<name>` only (no
> `TESTINGPLATFORM_PIPE_DIRECTORY`/`TMPDIR` relocation and no 103-byte precheck). Because the peer always
> uses the fully-resolved name verbatim, this divergence is harmless for interop — it only changes *where*
> the SDK's socket file lands. The test host simply opens a `NamedPipeClient` to `.`/`<name>`.

---

## 4. Framing

Every message — request and reply, on both the data and control pipes — is a single frame:

```text
+-------------------------+------------------------+------------------------+
| int32  payloadLength    | int32  serializerId    | byte[] body            |
| (little-endian)         | (little-endian)        | (serializerId's shape) |
+-------------------------+------------------------+------------------------+
        4 bytes                   4 bytes                 N bytes

payloadLength = 4 (serializerId) + body.Length
```

- All multi-byte integers use `BitConverter` (little-endian on all supported platforms).
- The reader first reads exactly 4 bytes (`payloadLength`), then reads exactly `payloadLength` bytes
  (which contain the 4-byte `serializerId` followed by the body), looping over partial reads.
- `serializerId` selects the deserializer from the shared registry (§6).
- **EOF handling:** a `0`-byte read (clean disconnect, whether at a frame boundary or mid-message) makes
  the reader return `null`, which the transport interprets as "peer disconnected". See §11 for the
  behavioral consequences.
- On Windows, testfx's frame writer calls `WaitForPipeDrain()` after each write to ensure the frame is
  flushed to the peer before returning. This is a **testfx transport detail, not a bidirectional
  requirement**: it applies to the testfx client's *request* writes (and testfx's server); the current
  `dotnet/sdk` reply writer only writes + flushes and has no drain call. A conforming reader must not
  depend on the peer draining.
- The read buffer is 250,000 bytes; larger bodies are streamed in chunks.

```mermaid
flowchart LR
    A["Message object"] --> B["Serialize body<br/>(field envelope, §5)"]
    B --> C["Prepend int32 serializerId"]
    C --> D["Prepend int32 payloadLength"]
    D --> E["Write frame to PipeStream + Flush<br/>(WaitForPipeDrain on Windows)"]
```

---

## 5. Body serialization format

Bodies use a **self-describing, forward-compatible field envelope**. The design follows the concept of
**optional properties**: each field carries an ID and a size, unknown IDs are skipped, and new fields
are added with new IDs. Existing IDs are **never** reused or repurposed.

### 5.1 Standard field envelope

Most messages are:

```text
uint16  fieldCount
repeat fieldCount times:
    uint16  fieldId
    int32   fieldSize      // size in bytes of the following value
    byte[]  value          // interpretation depends on fieldId
```

- `fieldCount` counts only the fields actually present. **Optional (null) fields are omitted entirely**
  — the writer computes `fieldCount` from the non-null fields. A reader must therefore treat any absent
  field as "not provided" and apply its own default.
- On an **unrecognized `fieldId`**, the reader skips exactly `fieldSize` bytes and continues. This is
  what makes older readers forward-compatible with newer producers.

```mermaid
flowchart TD
    S["Read uint16 fieldCount"] --> L{"more fields?"}
    L -- yes --> R["Read uint16 fieldId<br/>Read int32 fieldSize"]
    R --> K{"fieldId recognized?"}
    K -- yes --> H["Read value per field type"]
    K -- no --> Z["Seek forward fieldSize bytes"]
    H --> L
    Z --> L
    L -- no --> D["Return deserialized object"]
```

### 5.2 Primitive value encodings

| Type | Encoding | `fieldSize` written |
| --- | --- | --- |
| `string` | `int32 byteLength` + UTF-8 bytes | For a scalar field written via `WriteField(id, string)`, the emitted `int32` **is** the envelope `fieldSize` (the UTF-8 byte length), followed directly by the raw UTF-8 bytes — there is no second length prefix. Strings that are **list elements** carry their own `int32 byteLength` prefix instead. See note below. |
| `int` | 4 bytes | `4` |
| `long` | 8 bytes | `8` |
| `ushort` | 2 bytes | `2` |
| `bool` | 1 byte | `1` |
| `byte` | 1 byte | `1` |

> [!NOTE]
> **Two string conventions coexist and are both part of the contract.**
>
> - Scalar string fields written with `WriteField(stream, id, value)` emit `uint16 id`, then an
>   `int32` byte length, then the raw UTF-8 bytes. That `int32` is exactly the envelope `fieldSize`,
>   so the reader reads it as the size and calls `ReadStringValue(stream, size)` — there is **no**
>   second length prefix inside the value.
> - Strings inside a **list payload element** (e.g. `ParameterTypeFullNames`) are written with
>   `WriteString` (a self-contained `int32 len` + bytes) and read with `ReadString`.
>
> Implementers must follow the exact per-serializer layout (each serializer file has a byte-layout
> comment). The round-trip contract tests
> (`test/UnitTests/Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests`) pin the bytes.

### 5.3 List payloads (deferred size back-fill)

List-valued fields (e.g. the message lists inside the "*Messages" envelopes) are written as:

```text
uint16  fieldId
int32   payloadSize        // reserved, back-filled after writing the payload
int32   listLength
repeat listLength times:
    <element>              // usually a nested field envelope (§5.1)
```

The writer reserves the 4-byte `payloadSize`, writes `listLength` + elements, then seeks back and
patches `payloadSize` with the number of bytes written. This requires a **seekable** stream
(a `MemoryStream` is used for buffering before the frame hits the pipe). A `null`/empty list writes
nothing at all (the field is omitted and not counted in `fieldCount`).

### 5.4 Execution-scoped header

The four list-carrying `dotnet test` messages (`DiscoveredTestMessages`, `TestResultMessages`,
`TestInProgressMessages`, `FileArtifactMessages`) share two leading fields with pinned IDs:

- `ExecutionId` — field ID **1**
- `InstanceId` — field ID **2**

Both are optional strings; the message-specific list field(s) follow. `AzureDevOpsLogMessage` and
`DisplayMessage` also place `ExecutionId`/`InstanceId` at IDs 1/2 but carry scalar payloads (not lists).

---

## 6. Serializer registry (message IDs)

All serializers are registered in one registry shared by both pipes
(`RegisterSerializers.RegisterAllSerializers`). **IDs are frozen for backwards compatibility** — never
change or reuse an ID.

| ID | Message | Direction (data pipe) | Kind | Since |
| --: | --- | --- | --- | --- |
| 0 | `VoidResponse` | SDK → host (reply) | Response | 1.0.0 |
| 1 | `TestHostCompletedRequest` | *test host controller pipe* | Request | 1.0.0 |
| 2 | `TestHostProcessPIDRequest` | *test host controller pipe* | Request | 1.0.0 |
| 3 | `CommandLineOptionMessages` | host → SDK | Request | 1.0.0 |
| 4 | *(reserved — removed serializer)* | — | — | — |
| 5 | `DiscoveredTestMessages` | host → SDK | Request | 1.0.0 |
| 6 | `TestResultMessages` | host → SDK | Request | 1.0.0 |
| 7 | `FileArtifactMessages` | host → SDK | Request | 1.0.0 |
| 8 | `TestSessionEvent` | host → SDK | Request | 1.0.0 |
| 9 | `HandshakeMessage` | host → SDK, and SDK → host reply | Request + Response | 1.0.0 |
| 10 | `TestInProgressMessages` | host → SDK¹ | Request | 1.0.0 |
| 11 | `AzureDevOpsLogMessage` | host → SDK | Request | 1.2.0 |
| 12 | `DisplayMessage` | host → SDK | Request | 1.3.0 |
| 13 | `WaitForServerControlRequest` | host → SDK (on control pipe) | Request | 1.4.0 |
| 14 | `ServerControlMessage` | SDK → host reply (on control pipe) | Response | 1.4.0 |

> IDs 1 and 2 belong to the sibling **test host controller** pipe (a monitoring channel between a test
> host controller process and the test host). They share the same framing and registry but are not part
> of the SDK data flow; they are listed here because they occupy IDs in the shared registry.
>
> ¹ `TestInProgressMessages` (ID 10) is registered and round-tripped but is **not currently emitted** on
> the data pipe — see §9.4.

---

## 7. Request/reply model

Although data flows host → SDK, the transport is still **request/reply** at the frame level: the client
(host) writes one request frame and blocks reading exactly one reply frame before sending the next. This
serializes all sends behind a single lock (`RequestReplyAsync` holds a `SemaphoreSlim`), so messages are
delivered in order.

- For every data message the SDK replies with a **`VoidResponse`** (serializer ID 0, empty body). The
  host ignores the value; it only needs the reply to know the SDK consumed the message and to unblock
  the next send.
- The one exception is the **handshake**: the SDK replies with a `HandshakeMessage` (§9), not a
  `VoidResponse`.

The protocol is therefore "push-only" from a *semantic* standpoint (the SDK never initiates a data
request), but every push is acknowledged.

```mermaid
sequenceDiagram
    participant H as Test host (client)
    participant S as dotnet test SDK (server)
    Note over H,S: data pipe, one in-flight message at a time
    H->>S: DiscoveredTestMessages / TestResultMessages / ...
    S-->>H: VoidResponse
    H->>S: next message
    S-->>H: VoidResponse
```

---

## 8. Handshake & version negotiation

Before any data is sent, the host performs one handshake round-trip on the data pipe.

### 8.1 Host → SDK: `HandshakeMessage` (request)

A `HandshakeMessage` body is a map `byte -> string`:

```text
uint16  propertyCount
repeat propertyCount times:
    byte    propertyId
    string  value          // int32 len + UTF-8 bytes
```

Property IDs (`HandshakeMessagePropertyNames`):

| ID | Name | Set by host | Meaning |
| --: | --- | --- | --- |
| 0 | `PID` | yes | Host process ID. |
| 1 | `Architecture` | yes | `RuntimeInformation.ProcessArchitecture`. |
| 2 | `Framework` | yes | `RuntimeInformation.FrameworkDescription`. |
| 3 | `OS` | yes | `RuntimeInformation.OSDescription`. |
| 4 | `SupportedProtocolVersions` | yes | Semicolon-separated list the host supports (see §10). |
| 5 | `HostType` | yes | `TestHost`, `TestHostController`, `ServerTestHost`, `TestHostOrchestrator`, or `ArtifactPostProcessor`. |
| 6 | `ModulePath` | yes | Full path of the test application. |
| 7 | `ExecutionId` | yes | The Execution ID (from the env var). |
| 8 | `InstanceId` | yes | The per-connection Instance ID. |
| 9 | `IsIDE` | **reply-only** | Consumer requests full discovery details (see §9). |
| 10 | `ExecutionMode` | yes | `run`, `help`, `discover`, or `tool` — lets the SDK detect mismatches (e.g. `--help` leaking into a run). |
| 11 | `OrchestratorFeature` | orchestrator only | The orchestrator extension Uid (e.g. retry). |
| 12 | `ServerControlPipeName` | **reply-only** | OS name of the reverse control pipe (see §12). |
| 13 | `AttemptNumber` | test host only | Positive, 1-based retry attempt. Multiple Instance IDs may share one attempt. |
| 14 | `SupportedPostProcessorKinds` | test host, server test host, test host controller, or artifact post-processor | Semicolon-separated reverse-DNS artifact kinds supported by registered post-processors. |
| 15 | `SupportedPostProcessorExtensionsLegacy` | test host, server test host, test host controller, or artifact post-processor | Semicolon-separated lowercase file extensions used as a fallback for untagged artifacts. |
| 16 | `Transport` | yes | Which transport carried this handshake: `NamedPipe` or `WebSocket` (`HandshakeMessageTransportNames`). Diagnostic/negotiation-only - the wire protocol itself never varies by transport. |

### 8.2 SDK → host: `HandshakeMessage` (reply)

The SDK replies with its own `HandshakeMessage`. The host reads these properties:

- `SupportedProtocolVersions` (ID 4): the **single** version the SDK negotiated. The host checks it is
  in its own supported set; if not, the run is treated as **incompatible** (exit code
  `IncompatibleProtocolVersion`).
- `IsIDE` (ID 9): when `"true"`, the host streams **full** discovery details and streams per-test
  in-progress updates as results (see §9).
- `ServerControlPipeName` (ID 12): when non-empty, enables the reverse control channel (§12). This is a
  **capability signal gated on the property's presence**, independent of the version string.

### 8.3 Negotiation algorithm

The host advertises `ProtocolConstants.SupportedVersions` (currently `"1.0.0;1.1.0;1.2.0;1.3.0;1.4.0;1.5.0"`).
The SDK picks the **highest version present in both sets** and returns that single value. The host then:

- Confirms the returned value is in its supported set (compatibility gate).
- Derives feature flags from the negotiated `Version`:
  - `IsLogForwardingSupported` = negotiated ≥ `1.2.0`
  - `IsDisplayMessageForwardingSupported` = negotiated ≥ `1.3.0`

```mermaid
sequenceDiagram
    participant H as Test host
    participant S as dotnet test SDK
    H->>S: HandshakeMessage(PID, OS, SupportedProtocolVersions="1.0.0;...;1.4.0", HostType, ExecutionId, InstanceId, ExecutionMode, [AttemptNumber], ...)
    S->>S: pick highest mutually-supported version
    S-->>H: HandshakeMessage(SupportedProtocolVersions=<negotiated>, [IsIDE], [ServerControlPipeName])
    H->>H: validate compatibility; set IsIDE / forwarding / control-channel flags
    alt incompatible
        H->>H: exit IncompatibleProtocolVersion
    else compatible
        H->>H: proceed to run/discover/help
    end
```

---

## 9. Message catalog (data pipe)

Every message below is a host → SDK request answered with a `VoidResponse`. `ExecutionId`/`InstanceId`
are the execution-scoped header fields (IDs 1/2) unless noted.

### 9.1 `TestSessionEvent` (ID 8)

Sent at the boundaries of the test session.

| Field ID | Name | Type | Notes |
| --: | --- | --- | --- |
| 1 | `SessionType` | byte | `SessionEventTypes`: `0` = TestSessionStart, `1` = TestSessionEnd. |
| 2 | `SessionUid` | string | The MTP session UID. |
| 3 | `ExecutionId` | string | Execution ID. |

The host sends `TestSessionStart` on session start and `TestSessionEnd` on session finish. (Note: this
message's field IDs 1/2/3 are `SessionType`/`SessionUid`/`ExecutionId` — it does **not** use the shared
execution-scoped header helper.)

### 9.2 `DiscoveredTestMessages` (ID 5)

Emitted for every discovered test (state `Discovered`). Carries a list of `DiscoveredTestMessage`:

| Field ID | Name | Type | Populated when |
| --: | --- | --- | --- |
| 1 | `Uid` | string | always |
| 2 | `DisplayName` | string | always |
| 3 | `FilePath` | string | IDE only |
| 4 | `LineNumber` | int | IDE only |
| 5 | `Namespace` | string | IDE only |
| 6 | `TypeName` | string | IDE only |
| 7 | `MethodName` | string | IDE only |
| 8 | `Traits` | list of `TraitMessage`(`Key`,`Value`) | IDE only |
| 9 | `ParameterTypeFullNames` | list of string | IDE only |

> **`IsIDE` gating.** In non-IDE runs (e.g. plain `dotnet test`) only `Uid` + `DisplayName` are sent to
> keep the payload minimal. When the SDK set `IsIDE=true` in its handshake reply (an IDE, or
> `dotnet test --list-tests json`) the host sends the full location/identifier/traits details.

### 9.3 `TestResultMessages` (ID 6)

Carries two lists: `SuccessfulTestMessageList` (ID 3) and `FailedTestMessageList` (ID 4).

Mapping from MTP node state → which list is used:

| MTP state | `State` byte (`TestStates`) | Sent as |
| --- | --: | --- |
| Passed | 1 | Successful list |
| Skipped | 2 | Successful list |
| InProgress (IDE only) | 7 | Successful list |
| Failed | 3 | Failed list |
| Error | 4 | Failed list |
| Timeout | 5 | Failed list |
| Cancelled | 6 | Failed list |

`SuccessfulTestResultMessage` fields: `Uid`(1), `DisplayName`(2), `State`(3, byte), `Duration`(4, long
ticks), `Reason`(5), `StandardOutput`(6), `ErrorOutput`(7), `SessionUid`(8).

`FailedTestResultMessage` fields: `Uid`(1), `DisplayName`(2), `State`(3), `Duration`(4), `Reason`(5),
`ExceptionMessageList`(6), `StandardOutput`(7), `ErrorOutput`(8), `SessionUid`(9), `Expected`(10),
`Actual`(11).

- `ExceptionMessage` fields: `ErrorMessage`(1), `ErrorType`(2), `StackTrace`(3). Exceptions are
  flattened (aggregate/inner exceptions expanded) before sending.
- `Expected`/`Actual` (added after `SessionUid`, older readers skip them) carry structured
  assertion-diff values captured by assertion libraries from `Exception.Data["assert.expected"]` /
  `["assert.actual"]`. Only failed tests populate them; error/timeout/cancelled send null.

### 9.4 `TestInProgressMessages` (ID 10)

Each `TestInProgressMessage` carries `Uid`(1) + `DisplayName`(2), scoped to report a test that is
currently running (rendered by non-IDE consumers as a "currently running tests" panel).

> [!NOTE]
> **Currently a registered wire shape only.** `DotnetTestDataConsumer` constructs a
> `TestInProgressMessages` for non-IDE `InProgress` node updates, but `DotnetTestConnection.SendMessageAsync`
> has no `case` for it today, so this message is **not actually sent** on the data pipe in the current
> source. Its serializer (ID 10) is registered and round-tripped, so the wire shape is fixed; a future
> change can wire it into `SendMessageAsync` without a protocol bump. In IDE mode, `InProgress` updates
> are sent as `TestResultMessages` (see §9.3) instead.

### 9.5 `FileArtifactMessages` (ID 7)

Reports produced files (per-test artifacts, session artifacts, and standalone `FileArtifact`s). Each
`FileArtifactMessage`: `FullPath`(1), `DisplayName`(2), `Description`(3), `TestUid`(4),
`TestDisplayName`(5), `SessionUid`(6), `Kind`(7). `Kind` carries the producer-asserted artifact kind
used for post-processor routing. Test-scoped artifacts fill `TestUid`/`TestDisplayName`; session and
standalone artifacts leave them empty.

### 9.6 `CommandLineOptionMessages` (ID 3)

Sent only on the **help** path (`--help`). Carries `ModulePath`(1) and a list of
`CommandLineOptionMessage`(`Name`(1), `Description`(2), `IsHidden`(3, bool), `IsBuiltIn`(4, bool)),
sorted by name. Tool-provided options (`IToolCommandLineOptionsProvider`) are excluded. This lets the
SDK render `dotnet test --help` from the test host's actual option set.

### 9.7 `AzureDevOpsLogMessage` (ID 11, ≥ 1.2.0)

`ExecutionId`(1), `InstanceId`(2), `LogText`(3). Under the pipe protocol the host installs a forwarding
output device that discards regular output, so Azure DevOps logging commands (`##[group]`, `##vso[...]`)
produced by the AzureDevOpsReport extension would otherwise be lost. When ≥ 1.2.0 is negotiated **and**
the host is running on an Azure DevOps agent, those marked lines are forwarded verbatim for the SDK to
write to its reporter (and thus the pipeline log).

### 9.8 `DisplayMessage` (ID 12, ≥ 1.3.0)

`ExecutionId`(1), `InstanceId`(2), `Level`(3, byte), `Text`(4). A generic host diagnostic forwarded so
warning/error messages produced **outside** test results (hang/crash dump diagnostics, retry summaries,
generic extension/framework warnings and errors) are not swallowed. `Level` is `DisplayMessageLevels`:
`0` Information, `1` Warning, `2` Error. The SDK maps them to `WriteMessage` / `WriteWarningMessage` /
`WriteErrorMessage`. Regular (informational) host output is still discarded by the forwarding device;
only warning/error levels are relayed. Unlike the Azure DevOps path, this is **not** gated on an ADO
agent.

---

## 10. Versioning & compatibility

`SupportedVersions = "1.0.0;1.1.0;1.2.0;1.3.0;1.4.0"`.

| Version | What it adds / signals |
| --- | --- |
| 1.0.0 | Base protocol. With an SDK that only supports 1.0.0, **neither side shows live output** (the SDK suppresses its reporter to avoid colliding with host output). |
| 1.1.0 | Not a wire change: signals the host no longer plugs in `TerminalOutputDevice`, so the SDK can safely keep its live `TerminalTestReporter` output on. |
| 1.2.0 | Adds `AzureDevOpsLogMessage` (ID 11). Host forwards ADO logging commands (only on an ADO agent). |
| 1.3.0 | Adds `DisplayMessage` (ID 12). Host forwards warning/error host diagnostics (always). |
| 1.4.0 | Adds the reverse **server-control** channel (`WaitForServerControlRequest` ID 13, `ServerControlMessage` ID 14). Version is bumped so negotiated state advances in lockstep, but the feature itself is gated on the `ServerControlPipeName` handshake property, not on the version. **testfx-side / pending SDK support:** the current `dotnet/sdk` advertises only `1.0.0`–`1.3.0` and does not vendor serializers 13/14, so it cannot advertise `ServerControlPipeName` or drive the channel yet. In practice the negotiated version tops out at 1.3.0 until the coordinated SDK change lands (see §12). |

Compatibility rules / assumptions:

- Forwarding of 1.2.0/1.3.0 messages is **gated on the negotiated version** — the host never sends a
  message an older SDK can't decode.
- Unknown **field IDs** within a known message are skipped (§5.1) — a newer host can add fields without
  breaking an older SDK.
- Unknown **serializer IDs**: the testfx receiver has no serializer to look up, but the current
  `dotnet/sdk` data-pipe server is created with `skipUnknownMessages: true`, so it produces an
  `UnknownMessage`, logs and skips it, and still replies with `VoidResponse`. Either way, new *messages*
  must be **version-gated** for their semantics to be understood — do not rely on a receiver decoding an
  unknown serializer ID. (Unknown *fields* within a known message are always safe, per the previous rule.)
- The control channel is capability-gated (presence of the pipe-name property) so an older SDK that
  never advertises the pipe simply leaves it disabled.

---

## 11. Connection-loss behavior (assumptions)

The connection-loss semantics differ by pipe and are a core part of the contract.

**Data pipe (`exitProcessOnConnectionLoss: true`).** If the SDK disconnects there is no way to recover,
so the host process **exits abnormally** with exit code `GenericFailure` (1). This happens on two paths:
the host reads EOF where a reply was expected, or a write fails with `IOException`/`ObjectDisposedException`
while sending a request. On the **read-EOF** path the host also writes a diagnostic to stderr before
exiting; the **write-failure** path calls `Exit(GenericFailure)` directly **without** that diagnostic.
This is deliberate: if the user kills `dotnet.exe`, the test host must die too rather than orphan.

**Server-control pipe (`exitProcessOnConnectionLoss: false`).** A dropped control pipe must **not** kill
the test host. This listener runs *inside the test host* as the control-pipe **client**, so an
unexpected early close means the **SDK/server peer went away**; the listener treats that as a
cooperative cancel (see §12) rather than an error. Consequently it is a **protocol requirement that the
SDK keep the control pipe open for the entire data session** — closing it early is interpreted as a
cancel request.

**Server side (testfx `NamedPipeServer`).** If the *client* (host) disconnects while testfx's server is
writing a reply, it catches the `IOException`/`ObjectDisposedException`, treats it as a graceful
disconnect, and exits its read loop without crashing. This is a **testfx transport behavior**; the
current `dotnet/sdk` data-pipe server does **not** wrap its reply writes this way, so a disconnect there
faults its server loop instead. Neither behavior is a protocol guarantee.

---

## 12. Reverse server-control channel (≥ 1.4.0)

> [!NOTE]
> **testfx-side / pending SDK support.** The host side of this channel is implemented in testfx, but the
> current `dotnet/sdk` does not advertise `ServerControlPipeName` or vendor serializers 13/14, so the
> channel is **not yet driven end-to-end by `dotnet test`**. It activates only once a coordinated SDK
> change ships. The section below describes the intended/host-side behavior.

Purpose: let the SDK push a control signal (today only session cancellation, e.g. from a global
`--maximum-failed-tests` or `--timeout`) to the running test host, even while the host is otherwise
silent.

Setup and flow:

1. In its handshake reply the SDK advertises `ServerControlPipeName` (a pipe it is already listening on).
   Each connecting process (test host, controller, orchestrator) that receives this property opens its
   **own** client to that name, so the SDK must accept one control connection per connecting process.
2. On a successful, compatible handshake with the property present, the host — on a **background task**,
   so test start is never blocked — connects to the control pipe (bounded by a 30s connect timeout) and
   parks a long-poll `WaitForServerControlRequest` (ID 13, empty body).
3. When the SDK wants to signal, it completes that request with a `ServerControlMessage` (ID 14) whose
   `Kind`(1) is a `ServerControlKinds` value (today only `1` = `CancelSession`). The host then invokes
   its cancel reaction **exactly once** (idempotent via an interlocked flag), preferring a graceful stop
   so trx/artifacts are still produced. Unknown `Kind` values are ignored and the host keeps parking.
4. If the control pipe drops unexpectedly while the session is live (i.e. the SDK/server peer went
   away), the host also treats it as a cancel (see §11).

```mermaid
sequenceDiagram
    participant H as Test host
    participant S as dotnet test SDK
    Note over H,S: after handshake advertised ServerControlPipeName
    H->>S: connect control pipe (background, 30s timeout)
    H->>S: WaitForServerControlRequest (parked long-poll)
    Note over S: user cancels / global limit hit
    S-->>H: ServerControlMessage(Kind=CancelSession)
    H->>H: request graceful session stop (once)
    Note over H,S: host keeps control pipe open until data session ends
```

The auxiliary channel is best-effort: if it cannot be established, the run continues unaffected with the
feature simply disabled.

---

## 13. End-to-end lifecycle

The host builder creates a `DotnetTestConnection` and calls `AfterCommonServiceSetupAsync`, which — when
`--server dotnettestcli` + `--dotnet-test-pipe` are present — sets/reads the Execution ID env var and
connects the data-pipe client. The overall flow:

```mermaid
sequenceDiagram
    participant SDK as dotnet test SDK
    participant Host as Test host

    SDK->>SDK: create & listen on data pipe
    SDK->>Host: launch: --server dotnettestcli --dotnet-test-pipe <name> [--list-tests|--help|...]
    Host->>SDK: connect data pipe
    Host->>SDK: HandshakeMessage (request)
    SDK-->>Host: HandshakeMessage (reply: negotiated version, [IsIDE], [ServerControlPipeName])

    alt incompatible version
        Host->>Host: exit IncompatibleProtocolVersion
    else help mode
        Host->>SDK: CommandLineOptionMessages
        SDK-->>Host: VoidResponse
    else run / discover
        opt control channel advertised
            Host->>SDK: connect control pipe + park WaitForServerControlRequest
        end
        Host->>SDK: TestSessionEvent(TestSessionStart)
        SDK-->>Host: VoidResponse
        loop per test node update
            Host->>SDK: Discovered / Result / FileArtifact / Display / ADO
            SDK-->>Host: VoidResponse
        end
        Host->>SDK: TestSessionEvent(TestSessionEnd)
        SDK-->>Host: VoidResponse
    end
    Host->>Host: OnExit: cancel & dispose control listener
```

### Multi-process runs (one test-application execution)

A single **test-application execution** may involve several MTP processes that each perform the
handshake on the **same** Execution ID:

- **Test host** — `HostType = TestHost` (or `ServerTestHost`); actually runs tests.
- **Test host controller** — `HostType = TestHostController`; monitors/restarts the test host.
- **Test host orchestrator** — `HostType = TestHostOrchestrator`; drives one or more test host
  executions (e.g. the retry orchestrator, which also sends `OrchestratorFeature`). A control-channel
  cancel maps to cancelling its application token, which propagates to the orchestrated hosts.

These are the root test-app process and its child process tree. Each advertises its own `InstanceId`;
the shared `ExecutionId` lets the SDK correlate the processes **of that one execution**. Test hosts also
advertise `AttemptNumber`: retry creates a later attempt, while sharding can create several Instance IDs
within the same attempt. A multi-project `dotnet test` command launches several such trees, each with its
own Execution ID (see §1/§2).

---

## 14. Implementation references

| Concern | File(s) |
| --- | --- |
| Wire contract (IDs, field IDs) | `ServerMode/DotnetTest/IPC/ObjectFieldIds.cs` |
| Constants (states, versions, handshake props, transport names) | `ServerMode/DotnetTest/IPC/Constants.cs` |
| Serializer registry | `IPC/Serializers/RegisterSerializers.cs` |
| Serializer primitives / envelope | `IPC/Serializers/BaseSerializer.cs`, `NamedPipeSerializer.cs` |
| Shared duplex-stream framing (both transports) | `IPC/NamedPipeConnectionBase.cs` |
| Named-pipe transport | `IPC/NamedPipeServer.cs`, `IPC/NamedPipeClient.cs` |
| Pipe naming | `NamedPipeServer.GetPipeName` |
| WebSocket transport | `ServerMode/DotnetTest/Transport/DotnetTestWebSocketClient.cs`, `ClientWebSocketDuplexStream.cs`, `BrowserWebSocketDuplexStream.cs` |
| Transport selection / CLI options | `CommandLine/PlatformCommandLineProvider.cs`, `ServerMode/DotnetTest/DotnetTestHelper.cs` (`DotnetTestTransportKind`) |
| Message models | `ServerMode/DotnetTest/IPC/Models/*.cs`, `IPC/Models/*.cs` |
| Message serializers | `ServerMode/DotnetTest/IPC/Serializers/*.cs` |
| Host connection / handshake / control channel / transport dispatch | `ServerMode/DotnetTest/DotnetTestConnection.cs` |
| Host data consumer (state → message mapping) | `ServerMode/DotnetTest/IPC/DotnetTestDataConsumer.cs` |
| Shared-source manifest (vendored to dotnet/sdk) | `ServerMode/DotnetTest/DotnetTestProtocolContract.props` |
| Black-box protocol reference / tests | `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/*`, `test/UnitTests/Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests/*` |

---

## 15. WebSocket transport

> [!NOTE]
> This section covers the **transport bootstrap** for the WebSocket alternative to the named pipe. The
> wire protocol carried over it (framing, serializers, handshake, message catalog, versioning) is exactly
> what §4-§10 already describe - nothing in this section changes a single byte on the wire.

### 15.1 Why a second transport exists

`System.IO.Pipes` (and therefore the entire transport described in §3) is unavailable on `browser-wasm`
(and `wasi-wasm`): there is no OS-level named-pipe primitive to open. Before this transport existed, the
SDK had no way to run a browser-wasm test application through the live `dotnet test` pipe protocol at all
and had to fall back to a degraded, standalone launch mode. The WebSocket transport gives `dotnet test` a
bootstrap path that works on any runtime with a WebSocket implementation, so the *same* wire protocol
(same serializers, same handshake, same message catalog) can be used everywhere.

Protocol and transport are deliberately decoupled: `DotnetTestConnection` talks to an `IClient` (a small
connect/request-reply abstraction implemented identically in shape by `NamedPipeClient` and
`DotnetTestWebSocketClient`), and the shared framing in `NamedPipeConnectionBase` operates on a plain
`Stream` rather than a `PipeStream` specifically. Only `NamedPipeClient`'s pipe-specific behavior (pipe
naming, `WaitForPipeDrain`) stays isolated to the named-pipe implementation.

### 15.2 Activation

```text
<testapp> --server dotnettestcli --dotnet-test-transport websocket \
          --dotnet-test-websocket-endpoint <wsUri> --dotnet-test-websocket-token <token> [other options]
```

- `--dotnet-test-transport websocket` selects this transport. Omitting `--dotnet-test-transport` (or
  passing `--dotnet-test-transport pipe`) keeps the named-pipe transport (§2, §3), which remains the
  default for exact backward compatibility.
- `--dotnet-test-websocket-endpoint` takes the fully-resolved `ws://` (or `wss://`) URI the SDK/gateway is
  already listening on - the test host is always the one that *initiates* the outbound connection, exactly
  mirroring the named-pipe transport where the SDK creates the pipe and the host connects out to it. This
  matters specifically for `browser-wasm`: the page/test host runs inside the browser sandbox and can only
  make outbound connections; it cannot accept inbound ones.
- `--dotnet-test-websocket-token` takes a per-run opaque secret the SDK generated for this connection (see
  §15.4).
- All three options are hidden and built-in, like `--dotnet-test-pipe`.
- Command-line validation (`PlatformCommandLineProvider.ValidateCommandLineOptionsAsync`) rejects
  impossible or incomplete combinations before any connection is attempted: the named-pipe transport on
  `browser-wasm`/`wasi-wasm`, `--dotnet-test-transport websocket` without both endpoint and token,
  specifying both a pipe and a WebSocket transport, or WebSocket-only options without
  `--dotnet-test-transport websocket`. `wasi-wasm` currently has **no** working transport at all (see
  §15.6) and is rejected regardless of which transport was requested.

### 15.3 Framing over WebSocket

Every `NamedPipeConnectionBase.WriteMessageAsync` call writes one already-assembled frame (4-byte length +
4-byte serializer ID + body) in a single `Stream.WriteAsync`. The WebSocket adapters
(`ClientWebSocketDuplexStream` for non-browser runtimes, `BrowserWebSocketDuplexStream` for `browser-wasm`)
map that 1:1 to one **binary** WebSocket message per frame (`endOfMessage: true`). Reads are treated as a
plain byte stream - consecutive receives are handed back verbatim and buffered across calls - rather than
relying on WebSocket message boundaries, so the adapters stay correct regardless of how a given frame is
fragmented on the wire by the peer or an intermediary.

`System.Net.WebSockets.ClientWebSocket` throws `PlatformNotSupportedException` on `browser-wasm`, so
`browser-wasm` uses a dedicated adapter that talks to the browser's native `WebSocket` object through
`[JSImport]`/`[JSExport]` JS interop (the same pattern `OutputDevice.BrowserOutputDevice` already uses for
`console.*`). The companion JS module is embedded as a string constant and imported via a `data:` URL, so
no changes to the generated wasm bootstrap or published asset set are required.

> [!NOTE]
> **CSP caveat.** The `data:` URL dynamic import relies on the page's Content-Security-Policy permitting it.
> A strict `script-src` that omits `data:` (and lacks `'unsafe-inline'`/a matching `'nonce-'`/`'sha256-'`
> hash for the embedded module, or `'strict-dynamic'`) will block the import, and the WebSocket transport will
> fail to initialize on that page. This is a real constraint under a locked-down CSP, not merely a theoretical
> one - "no additional assets are required" (above) is true for the wasm bootstrap/publish output, but it is
> not the same as "works under any CSP". A future revision could ship the module as a conventional `.js` asset
> (with a `nonce`/hash-friendly `<script>` tag) instead of a `data:` import if this becomes a blocker for a
> real deployment; that is a follow-up, not something this document should currently claim to solve.

### 15.4 Authentication and security

There is no OS-level ACL equivalent to `PipeOptions.CurrentUserOnly` for a TCP/WebSocket endpoint, so the
transport authenticates every connection explicitly:

- The SDK generates a per-run opaque token and passes it via `--dotnet-test-websocket-token`.
- The test host appends it to the connection URI as a query-string parameter (e.g.
  `?dotnetTestToken=<token>`) before connecting - see `DotnetTestWebSocketClient.BuildAuthenticatedUri`.
  This is deliberate, not a workaround: the browser's native `WebSocket` constructor only accepts a URL and
  an optional subprotocol list, so it **cannot** set an `Authorization` header (or any other custom
  header) on the upgrade handshake. A query-string token is the same approach ASP.NET Core SignalR uses
  for its WebSocket transport for the identical reason.
- **CORS is not authentication and must not be relied on as one.** CORS governs which browser *origins* may
  *read* a cross-origin response; it says nothing about which *process* may *open* a WebSocket connection
  to a loopback port in the first place. Any local process that can reach the port can attempt the
  handshake, so the token is the actual access check, not a defense-in-depth extra.
- The SDK is expected to bind the listener to loopback (`127.0.0.1`/`::1`) only and to reject connections
  whose token does not match the one it generated for that run/endpoint.
- **Never log the authenticated URI (or the token) verbatim.** Diagnostics should log only the endpoint's
  host/port/path, never the query string. This is enforced for the platform's own `--diagnostic` log too:
  the "Command line arguments" line masks the value of `--dotnet-test-websocket-token` (in both its
  space-separated and `--option=value`/`--option:value` inline forms) via
  `CommandLineArgumentsRedactor.Redact`, so simply enabling verbose diagnostics can never write the secret to
  disk. Any future option that carries a secret should be added to that redactor's sensitive-option list
  rather than relying on callers to remember to mask it themselves.

### 15.5 Handshake capability signal

The handshake (§8) gained one additive property, `Transport` (ID 16, `HandshakeMessagePropertyNames.Transport`),
always sent by the host as `NamedPipe` or `WebSocket` (`HandshakeMessageTransportNames`). It exists purely
for diagnostics and future negotiation - the wire protocol itself is identical either way - so an older SDK
that does not look at it is entirely unaffected. `ProtocolConstants.SupportedVersions` was bumped to
include `1.5.0` alongside it, following the same convention as every other additive capability in §10.

### 15.6 Known gaps and follow-ups (out of scope here)

- **Reverse server-control channel (§12) is named-pipe-only.** The SDK advertises
  `ServerControlPipeName` as a plain OS pipe name regardless of which transport carried the handshake.
  `DotnetTestConnection` only opens the reverse channel when the current runtime supports named pipes, so
  on `browser-wasm` (and `wasi-wasm`) server-initiated cancellation (`--timeout`,
  `--maximum-failed-tests`, ...) simply stays disabled - the same graceful degradation already used when an
  older SDK never advertises the pipe at all. A WebSocket-based (or transport-agnostic) reverse control
  channel is a natural follow-up.
- **`wasi-wasm` has no transport yet.** It cannot use named pipes (no `System.IO.Pipes` support) and, as of
  this writing, has neither a working `System.Net.WebSockets.ClientWebSocket` implementation nor an
  established JS-interop equivalent in this repo (unlike `browser-wasm`, `wasi-wasm` has no browser/JS host
  to interop with in the first place). Command-line validation rejects both transports on `wasi-wasm`
  outright with an actionable error rather than silently falling back to a degraded mode. A `wasi-wasm`
  transport (e.g. once/if a WASI sockets story matures in the .NET runtime) is a separate follow-up.
- **stdio was considered and deliberately not implemented.** Multiplexing the binary dotnettestcli protocol
  onto the same stdio streams a test host also uses for its own console output (and that other extensions
  may write to) would require either a second framing layer on top of the existing one or careful
  interleaving that risks corrupting either stream. Rather than ship that as a bolted-on special case, it
  is left as a candidate for a future, separately-designed transport if a concrete need arises.
- **The SDK side of this transport (loopback listener/gateway, token generation and validation) is not part
  of this repository.** This document only specifies what the MTP-side client requires from that gateway;
  see the coordinating `dotnet/sdk` change for the server implementation.
- **`BrowserWebSocketDuplexStream`'s JS-interop cancellation path has compile/publish coverage, not
  behavioral test coverage.** `ReadAsync`/`WriteAsync` correctly observe `CancellationToken` (`ReadAsync` races
  the JS `receive()` promise against the token and calls a `cancelReceive` JS function to clear the abandoned
  waiter so a message arriving after cancellation is queued for the next call instead of being silently lost;
  `WriteAsync` rejects an already-cancelled token before the - synchronous, uncancellable-once-issued -
  `WebSocket.send()` call). This code path is exercised by `dotnet publish -r browser-wasm` (confirming the
  `[JSImport]`/`[JSExport]` signatures are valid for the wasm JS-interop source generator) but **not** by an
  automated test that actually runs the JS module under a browser/`node` runtime and asserts the
  cancel-mid-flight and message-not-lost behavior, because doing so end-to-end needs the SDK-side WebSocket
  gateway from the previous bullet, which does not exist yet. The non-browser adapter's equivalent behavior
  (`ClientWebSocketDuplexStream`/`DotnetTestWebSocketClient`) *is* covered by a real loopback-socket test
  (`DotnetTestWebSocketClientTests`), since that path needs no browser/JS host. Once the SDK-side gateway
  exists, extending `BrowserWasmExecutionTests`-style node-hosted acceptance tests to drive this transport is
  the natural way to close this gap.
