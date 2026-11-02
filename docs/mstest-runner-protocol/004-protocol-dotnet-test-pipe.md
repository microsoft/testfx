# 004 - `dotnet test` Binary Protocol

> **Current implementation note (2026-10-09):** [Host handshake/capability parsing](../../src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/DotnetTestConnection.cs),
> pipe/HTTP transport and local fake-SDK fixtures support the host-side protocol topics.
> See [MTP-011](../specifications/mtp.md#mtp-011--native-binary-channel-versus-legacy-msbuild) and
> [MTP-016](../specifications/mtp.md#mtp-016--no-handshake-and-undeclared-capability-fallback).
> The external SDK comparisons, gateway/browser deployment requirements, vendored-file alignment
> and full wire-field catalog below were not independently validated in this scoped audit.

**Experimental 1.5.0 contract.** The HTTP control and artifact-transfer entries below describe this
browser-launcher prototype, not support in a released SDK. Its source-matched SDK counterpart is pinned
in [`eng/browser-testing/sdk.json`](../../eng/browser-testing/sdk.json). These experimental allocations
must be reconciled with independently allocated wire IDs in `main` and the matching SDK before integration.

This document is the descriptive specification of the **`dotnettestcli` protocol**: the small,
versioned, **binary** protocol used between a Microsoft.Testing.Platform (MTP) test application and the
`dotnet test` implementation shipped in the .NET SDK. The protocol can be bootstrapped over the legacy
named-pipe transport or over authenticated HTTP request/reply for environments such as
`browser-wasm`.

It is a *different* protocol from the [JSON-RPC server-mode protocol](./001-protocol-intro.md):

| | JSON-RPC server mode | `dotnet test` binary protocol (this doc) |
| --- | --- | --- |
| Activation | `--server` (or `--server jsonrpc`) | `--server dotnettestcli` plus named-pipe or HTTP bootstrap options |
| Wire format | JSON-RPC 2.0 over stdio / TCP | Custom length-prefixed **binary** frames over a named pipe or HTTP POST bodies |
| Shape | Full request/response + notifications, bidirectional | **Push-only** data channel (host → SDK) + auxiliary reverse control channel |
| Role of the runner | Server | Client (connects to the SDK's named-pipe server or HTTP gateway) |

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
> message models, serializers, handshake/state constants) is shared source. The **transport**
> (framing loop, buffering, connection-loss reactions, authentication, unknown-message handling, endpoint
> or pipe-name resolution) is deliberately **not** shared and is implemented independently on each side.
> Wherever this document describes transport
> *behavior*, it describes testfx's implementation. The current `dotnet/sdk` data-pipe server implements
> its own transport and **diverges** in several places (called out inline below); such behaviors are not
> protocol guarantees and must not be assumed to be symmetric across both sides.

---

## 1. Terminology

- **Test application / test host** — the MTP-based test executable (the process being run by
  `dotnet test`). In this protocol it is the named-pipe or HTTP **client**.
- **SDK / `dotnet test`** — the .NET SDK component that launches test applications and renders their
  output. It is the named-pipe **server** (creating and listening on the data pipe) or the HTTP
  **gateway** (hosting the per-run request/reply endpoint).
- **Primary channel** — the host-initiated request/reply channel carrying all handshakes and test data.
  It is either the data pipe or the HTTP gateway endpoint.
- **Data pipe** — the legacy primary-channel named pipe, whose OS name is supplied via
  `--dotnet-test-pipe`.
- **HTTP gateway** — the SDK-side per-run endpoint that receives authenticated binary HTTP POSTs and
  returns one binary reply per request.
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

The legacy SDK bootstrap remains:

```text
<testapp> --server dotnettestcli --dotnet-test-pipe <osPipeName> [other options]
```

For HTTP the SDK starts the test application with:

```text
<testapp> --server dotnettestcli
  --dotnet-test-transport http
  --dotnet-test-http-endpoint <absoluteUrl>
  --dotnet-test-http-token <perRunBearerToken>
  [other options]
```

Rules and behavior:

- `--server` accepts zero or one argument. The value `dotnettestcli` (case-insensitive) selects this
  protocol. `--server jsonrpc` (or `--server` with no value) selects JSON-RPC server mode instead.
- Omitting `--dotnet-test-transport` preserves the legacy behavior: `--dotnet-test-pipe` implicitly
  selects the named-pipe transport.
- `--dotnet-test-transport` accepts `pipe` or `http`. Explicit `pipe` requires
  `--dotnet-test-pipe`; explicit `http` requires both HTTP options and rejects a pipe option.
- `--dotnet-test-pipe` takes **exactly one** argument: the fully-resolved OS pipe name/path the SDK is
  already listening on (see §3).
- `--dotnet-test-http-endpoint` must be an absolute HTTPS URL. Plain HTTP is accepted only for a
  loopback host. User information, query strings, and fragments are rejected.
- `--dotnet-test-http-token` is a non-empty bearer token without whitespace or control characters.
  It is per-run bootstrap material, must have enough entropy to be unguessable, and must not be logged.
- All bootstrap options are built-in and **hidden**: the platform omits them from `--help`, so they are
  only listed by `--info`. They are pre-launch SDK/host integration options, not direct end-user options.

### Environment variables

| Variable | Set by | Purpose |
| --- | --- | --- |
| `TESTINGPLATFORM_DOTNETTEST_EXECUTIONID` | The root test application on first connect (if not already set) | The Execution ID. Each root test-app process generates its own value in `AfterCommonServiceSetupAsync` (if unset) and propagates it via the environment **only to its child process tree** (its test host controller, orchestrator). So all handshakes *within one test-application execution* share it, but separate test applications launched by one multi-project `dotnet test` command normally get **different** IDs. If already present it is **not** overwritten. |
| `TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER` | Retry orchestrator | The positive, 1-based retry attempt assigned to a child test host. A normal test host reports attempt `1` when the variable is absent. Invalid values fail before the handshake instead of being silently reinterpreted. |
| `TESTINGPLATFORM_PIPE_DIRECTORY` | User (optional) | On Unix, overrides the directory used to place the domain-socket file, **for pipes created by testfx's `NamedPipeServer`** (see §3). It does **not** relocate a pipe created by the other side: the current `dotnet/sdk` data-pipe server still resolves Unix names as `Path.Combine("/tmp", name)` and does not honor this variable/`TMPDIR` (nor the 103-byte precheck). Since the SDK creates the data pipe, this variable effectively only affects pipes testfx itself creates (e.g. the sibling controller pipe). |

---

## 3. Transport bootstrap & security

| Concern | Named pipe | Authenticated HTTP |
| --- | --- | --- |
| Bootstrap | `--dotnet-test-pipe <resolvedName>`; implicit default or `--dotnet-test-transport pipe` | `--dotnet-test-transport http` + endpoint + per-run token |
| Access control | Current-user pipe ACL on .NET plus OS pipe naming | HTTPS (except loopback HTTP) plus bearer authentication on every POST |
| Protocol bytes | One complete existing frame per write/read | One complete existing frame per request/response body |
| Ordering | One request/reply under the client lock | One POST in flight under the same ordering lock |
| Versioning | Handshake negotiates the existing serializer/message contract | Identical handshake and versions; HTTP is bootstrap/transport only |
| Disconnect signal | Persistent pipe EOF/write failure | No persistent signal; failure appears on the current or next POST |
| Browser considerations | Unavailable on browser/WASI | `HttpClient`/fetch; gateway must support CORS/PNA preflight |
| Cost | Persistent local stream | HTTP headers, possible preflight, and request setup per protocol message |

### 3.1 Named pipe

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

### 3.2 Authenticated HTTP

The HTTP transport uses ordinary .NET `HttpClient`; on `browser-wasm` this flows through the runtime's
browser `fetch` implementation. It requires no custom JavaScript shim.

For each host request the client sends:

```http
POST <per-run-endpoint>
Authorization: Bearer <per-run-token>
Content-Type: application/octet-stream

<one complete protocol frame>
```

The gateway replies with a successful status and `application/octet-stream` body containing exactly one
complete reply frame. The host validates success status, framing, response type, and the absence of a
second frame. A semaphore permits exactly one request in flight, preserving the same ordering and
backpressure as the named pipe. `ConnectAsync` performs no probe: the handshake is the first POST, avoiding
an otherwise redundant browser preflight.

The SDK-side gateway contract is:

1. Create a unique endpoint and cryptographically strong bearer token for each launched test-application
   run. Do not reuse either across unrelated runs.
2. Authenticate every POST before decoding or dispatching its body. Compare credentials without logging
   them, and never place the token in a URL.
3. Accept exactly one existing protocol request frame and return exactly one existing protocol reply
   frame. Do not translate serializer IDs, field IDs, framing, or negotiated versions.
4. Serialize handling per host connection/run. The host already limits itself to one request in flight;
   the gateway must not reorder requests across asynchronous handlers.
5. Keep the endpoint available for the entire run and return explicit non-success status codes for
   authentication or gateway failures. There is no health polling.
6. Bound request and response sizes, reject malformed or trailing frames, and avoid buffering/logging
   bodies in diagnostics because protocol payloads can contain test names, paths, and output.

#### Browser CORS and Private Network Access

An actual browser applies CORS, and may apply Private Network Access (PNA), even when a Node-hosted
`browser-wasm` acceptance test does not. The gateway must:

- allow the test application's exact origin (not `*` when credentials or policy require a specific
  origin);
- allow `POST`, `Authorization`, and `Content-Type`;
- answer `OPTIONS` preflight without requiring the bearer header that the browser has not sent yet;
- include the matching CORS response headers on success and error responses;
- account for PNA preflight when a public or secure origin targets a loopback/private-network gateway
  (including `Access-Control-Allow-Private-Network: true` where the browser requires it); and
- use HTTPS for non-loopback endpoints to avoid mixed-content and bootstrap validation failures.

This adds preflight and per-request HTTP overhead compared with a persistent WebSocket or named pipe, but
substantially reduces browser-specific host code and reuses the platform `HttpClient` stack.

---

## 4. Framing

Every message — request and reply, on the primary channel and control pipe — is a single frame:

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
| raw `byte[]` | Exactly `fieldSize` bytes, without an inner length prefix | Array length; used by `FileArtifactTransferRequest.Data` |

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

All serializers are registered in one registry shared by the primary and control transports
(`RegisterSerializers.RegisterAllSerializers`). **IDs are frozen for backwards compatibility** — never
change or reuse an ID.

| ID | Message | Direction | Kind | Since |
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
| 13 | `WaitForServerControlRequest` | host → SDK (on auxiliary control transport) | Request | 1.4.0; HTTP in experimental 1.5.0 |
| 14 | `ServerControlMessage` | SDK → host reply (on auxiliary control transport) | Response | 1.4.0; HTTP in experimental 1.5.0 |
| 15 | `FileArtifactTransferRequest` | host → SDK (HTTP primary channel) | Request | Experimental 1.5.0 |
| 16 | `FileArtifactTransferResponse` | SDK → host reply (HTTP primary channel) | Response | Experimental 1.5.0 |

> IDs 1 and 2 belong to the sibling **test host controller** pipe (a monitoring channel between a test
> host controller process and the test host). They share the same framing and registry but are not part
> of the SDK data flow; they are listed here because they occupy IDs in the shared registry.
>
> ¹ `TestInProgressMessages` (ID 10) is registered and round-tripped but is **not currently emitted** on
> the data pipe — see §9.4.

---

## 7. Request/reply model

Although data flows host → SDK, the primary channel is entirely **host-initiated request/reply**: the
host sends one request frame and waits for exactly one reply frame before sending the next. This
serializes all sends behind a single lock (`RequestReplyAsync` holds a `SemaphoreSlim`), so messages are
delivered in order.

- For every data message the SDK replies with a **`VoidResponse`** (serializer ID 0, empty body). The
  host ignores the value; it only needs the reply to know the SDK consumed the message and to unblock
  the next send.
- The **handshake** receives a `HandshakeMessage` reply (§8). A `FileArtifactTransferRequest` receives
  a `FileArtifactTransferResponse` (§9.9), not a `VoidResponse`. The separate HTTP control client can
  receive a `ServerControlMessage` or a `VoidResponse` when a bounded long poll has no pending command (§12).

The protocol is therefore "push-only" from a *semantic* standpoint (the SDK never initiates a primary
channel request), but every push is acknowledged. The HTTP primary client does not poll. Only the
capability-gated auxiliary control client parks long polls, independently of primary-channel requests.

```mermaid
sequenceDiagram
    participant H as Test host (client)
    participant S as dotnet test SDK (server)
    Note over H,S: named pipe or HTTP, one in-flight message at a time
    H->>S: DiscoveredTestMessages / TestResultMessages / ...
    S-->>H: VoidResponse
    H->>S: next message
    S-->>H: VoidResponse
```

---

## 8. Handshake & version negotiation

Before any data is sent, the host performs one handshake round-trip on the selected primary transport.

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
| 16 | `SupportedTruncatedRunPostProcessorKinds` | test host, server test host, test host controller, or artifact post-processor | Subset of ID 14 whose processors can consume artifacts observed before a policy-truncated run stopped. |
| 17 | `SupportedTruncatedRunPostProcessorExtensionsLegacy` | test host, server test host, test host controller, or artifact post-processor | Subset of ID 15 whose processors can consume artifacts observed before a policy-truncated run stopped. |
| 18 | `RequiredPostProcessorKinds` | test host, server test host, test host controller, or artifact post-processor | Subset of ID 14 containing internal coordination kinds that must be post-processed even when only one matching artifact exists. |
| 19 | `RequiredPostProcessingSupported` | SDK handshake response only | Boolean capability. `true` means the SDK honors ID 18 and supplies the authoritative `runSummary` manifest block before producers defer their standalone output. |
| 20 | `ServerControlTransport` | SDK handshake response only | Experimental capability. Exactly `http` enables authenticated HTTP control on a second client; it does not activate a named pipe. |
| 21 | `FileArtifactTransfer` | SDK handshake response only | Experimental capability. Exactly `chunked-v1`, with valid limits at IDs 22-24, enables HTTP artifact export. |
| 22 | `FileArtifactTransferMaxChunkBytes` | SDK handshake response only | Positive decimal `Int32` chunk limit; the host additionally caps each chunk at 1 MiB. |
| 23 | `FileArtifactTransferMaxFileBytes` | SDK handshake response only | Positive decimal `Int64` per-file byte limit. |
| 24 | `FileArtifactTransferMaxModuleBytes` | SDK handshake response only | Positive decimal `Int64` aggregate byte limit for exported files in the module. |
| 25 | `FileArtifactTransferMaxFileCount` | SDK handshake response only | Optional positive decimal `Int32` aggregate file-count limit. Missing or invalid values use the host ceiling of 10,000; a valid value is capped at that ceiling. |

### 8.2 SDK → host: `HandshakeMessage` (reply)

The SDK replies with its own `HandshakeMessage`. The host reads these properties:

- `SupportedProtocolVersions` (ID 4): the **single** version the SDK negotiated. The host checks it is
  in its own supported set; if not, the run is treated as **incompatible** (exit code
  `IncompatibleProtocolVersion`).
- `IsIDE` (ID 9): when `"true"`, the host streams **full** discovery details and streams per-test
  in-progress updates as results (see §9).
- `ServerControlPipeName` (ID 12): when non-empty, enables the reverse control channel (§12). This is a
  **capability signal gated on the property's presence**, independent of the version string, and is
  used only with the named-pipe primary transport.
- `ServerControlTransport` (ID 20): exactly `http` enables the auxiliary HTTP control client only when
  the primary transport is HTTP (§12). An absent or unknown value leaves this capability disabled.
- `FileArtifactTransfer` and limits (IDs 21-25): `chunked-v1` plus positive limits at IDs 22-24 enable
  HTTP artifact export (§9.9). Missing or unsupported capability data does not make browser-local paths
  usable on the SDK's filesystem.

### 8.3 Negotiation algorithm

The host advertises `ProtocolConstants.SupportedVersions` (currently `"1.0.0;1.1.0;1.2.0;1.3.0;1.4.0;1.5.0"`).
The SDK picks the **highest version present in both sets** and returns that single value. The host then:

- Confirms the returned value is in its supported set (compatibility gate).
- Derives feature flags from the negotiated `Version`:
  - `IsLogForwardingSupported` = negotiated ≥ `1.2.0`
  - `IsDisplayMessageForwardingSupported` = negotiated ≥ `1.3.0`
- Derives control and artifact-transfer support from the advertised capabilities, not merely from
  negotiating `1.5.0`. Older peers that omit them do not enable these features.

```mermaid
sequenceDiagram
    participant H as Test host
    participant S as dotnet test SDK
    H->>S: HandshakeMessage(PID, OS, SupportedProtocolVersions="1.0.0;...;1.5.0", HostType, ExecutionId, InstanceId, ExecutionMode, [AttemptNumber], ...)
    S->>S: pick highest mutually-supported version
    S-->>H: HandshakeMessage(SupportedProtocolVersions=<negotiated>, [IsIDE], [ServerControlPipeName or ServerControlTransport], [FileArtifactTransfer and limits])
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
ticks), `Reason`(5), `StandardOutput`(6), `ErrorOutput`(7), `SessionUid`(8), `RetryAttemptNumber`(9,
int), `IsSuperseded`(10, bool).

`FailedTestResultMessage` fields: `Uid`(1), `DisplayName`(2), `State`(3), `Duration`(4), `Reason`(5),
`ExceptionMessageList`(6), `StandardOutput`(7), `ErrorOutput`(8), `SessionUid`(9), `Expected`(10),
`Actual`(11), `RetryAttemptNumber`(12, int), `IsSuperseded`(13, bool).

- `ExceptionMessage` fields: `ErrorMessage`(1), `ErrorType`(2), `StackTrace`(3). Exceptions are
  flattened (aggregate/inner exceptions expanded) before sending.
- `Expected`/`Actual` (added after `SessionUid`, older readers skip them) carry structured
  assertion-diff values captured by assertion libraries from `Exception.Data["assert.expected"]` /
  `["assert.actual"]`. Only failed tests populate them; error/timeout/cancelled send null.
- `RetryAttemptNumber`/`IsSuperseded` (added after `SessionUid` on successful messages and after
  `Expected`/`Actual` on failed messages; older readers skip them) carry the in-process retry metadata
  surfaced by `RetryAttemptProperty` (for example MSTest's `[Retry]`
  attribute): the 1-based attempt number within the current test host run, and whether a later attempt
  for the same test node follows this one. They accompany any outcome state (passed, failed, etc.) and
  are null when the test was not retried. Consumers that want a single row per test should ignore
  updates whose `IsSuperseded` is `true`; consumers that want the whole retry history keep them all.

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
`TestDisplayName`(5), `SessionUid`(6), `Kind`(7), `InputArtifactPaths`(8, string list). `Kind` carries
the producer-asserted artifact kind used for post-processor routing. `InputArtifactPaths` is present
only on dispatcher-produced artifacts and contains the non-empty set of manifest input paths represented
by that output. The dispatcher preserves each path string exactly as received; consumers intersect it
with the paths from that job's manifest rather than independently normalizing it. Its absence means
provenance is unavailable, including messages from older hosts. Test-scoped artifacts fill
`TestUid`/`TestDisplayName`; session and standalone artifacts leave them empty.

For HTTP, the host first exports the file bytes through the negotiated transfer capability (§9.9).
Only the SDK's committed destination paths are then reported in `FullPath` and `InputArtifactPaths`.
Without that capability, the host emits one compatibility warning and suppresses these artifact
messages rather than presenting test-host-local paths as usable SDK files. Named-pipe artifact
reporting is unchanged.

### 9.6 `CommandLineOptionMessages` (ID 3)

Sent only on the **help** path (`--help`). Carries `ModulePath`(1) and a list of
`CommandLineOptionMessage`(`Name`(1), `Description`(2), `IsHidden`(3, bool), `IsBuiltIn`(4, bool),
reserved field ID 5, `ProviderUid`(6), `MinimumArity`(7, int), `MaximumArity`(8, int)), sorted by name.
Tool-provided options (`IToolCommandLineOptionsProvider`) are excluded. This lets the SDK render
`dotnet test --help` from the test host's actual option set.

Fields 6-8 are optional additive metadata. Older readers skip these unknown fields and continue to
read fields 1-4. Newer readers treat an absent provider UID or arity as undeclared, preserving
compatibility with older hosts.

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

### 9.9 Chunked file-artifact transfer (experimental IDs 15/16)

This exchange is enabled only on HTTP when the SDK advertises `FileArtifactTransfer=chunked-v1` and
positive chunk, file, and module byte limits. The SDK owns destination paths, quotas, authentication,
and association with the handshaken execution/instance. The host supplies a filename, never a
destination path, and sends the bytes before publishing the existing artifact metadata.

`FileArtifactTransferRequest` uses the standard field envelope (§5.1):

| Field ID | Name | Encoding | Meaning |
| --: | --- | --- | --- |
| 1 | `ExecutionId` | string | Execution that owns the transfer. |
| 2 | `InstanceId` | string | Connecting host instance that owns the transfer. |
| 3 | `TransferId` | string | New GUID in `N` format for each exported file. |
| 4 | `FileName` | string | File basename, not a host-selected destination. |
| 5 | `TotalLength` | `Int64`, 8 bytes | Non-negative declared file size. |
| 6 | `Offset` | `Int64`, 8 bytes | Non-negative starting offset of this chunk. |
| 7 | `Data` | raw bytes | Chunk payload, including an empty payload for a zero-byte file. |
| 8 | `IsFinal` | `bool`, 1 byte | Whether this chunk completes the declared file. |

All eight fields must be present. The deserializer rejects missing fields, negative lengths/offsets,
incorrect scalar sizes, and chunks larger than 1 MiB before allocating the chunk buffer. `Data` contains
only the raw payload: its envelope size is the byte count; there is no nested length prefix.

`FileArtifactTransferResponse` contains required `NextOffset` (field 1, non-negative `Int64`) and
optional `FullPath` (field 2, string) and `Error` (field 3, string). For every chunk the host requires
`NextOffset` to equal the submitted offset plus payload length. The final acknowledgment must include
a non-empty committed SDK `FullPath`. A zero-byte file still sends one final request and requires a
committed-path acknowledgment.

The host applies the lower of the advertised chunk limit and 1 MiB, checks the advertised file/module
byte quotas, and caps the optional file-count limit at 10,000. It exports referenced `InputArtifactPaths`
as well as the artifact itself. A negotiated transfer failure (including quota rejection, missing
source file, peer error, wrong offset, or absent committed path) reports an error and fails the run;
it must not degrade to a successful metadata-only report. Cancellation propagates through the transfer.
This differs deliberately from an older peer with no transfer capability, which follows the warning
and suppressed-artifact compatibility path in §9.5.

---

## 10. Versioning & compatibility

`SupportedVersions = "1.0.0;1.1.0;1.2.0;1.3.0;1.4.0;1.5.0"`.

| Version | What it adds / signals |
| --- | --- |
| 1.0.0 | Base protocol. With an SDK that only supports 1.0.0, **neither side shows live output** (the SDK suppresses its reporter to avoid colliding with host output). |
| 1.1.0 | Not a wire change: signals the host no longer plugs in `TerminalOutputDevice`, so the SDK can safely keep its live `TerminalTestReporter` output on. |
| 1.2.0 | Adds `AzureDevOpsLogMessage` (ID 11). Host forwards ADO logging commands (only on an ADO agent). |
| 1.3.0 | Adds `DisplayMessage` (ID 12). Host forwards warning/error host diagnostics (always). |
| 1.4.0 | Adds the reverse **server-control** channel (`WaitForServerControlRequest` ID 13, `ServerControlMessage` ID 14). Version is bumped so negotiated state advances in lockstep, but named-pipe control is gated on `ServerControlPipeName`, not on the version alone. SDK support must be verified for the specific counterpart (see §12). |
| 1.5.0 (experimental) | Reuses IDs 13/14 on a second authenticated HTTP client, gated by `ServerControlTransport=http`, and adds bounded chunked artifact-transfer IDs 15/16, gated by `FileArtifactTransfer=chunked-v1` and the advertised limits. Neither capability follows from the version string alone. |

Additive fields on known messages do not require a protocol-version bump:

| MTP package version | Message field |
| --- | --- |
| 2.4.0 | `FileArtifactMessage.Kind` (7) |
| 2.4.0 | `FileArtifactMessage.InputArtifactPaths` (8) |

Compatibility rules / assumptions:

- Forwarding of 1.2.0/1.3.0 messages is **gated on the negotiated version** — the host never sends a
  message an older SDK can't decode.
- Unknown **field IDs** within a known message are skipped (§5.1) — a newer host can add fields without
  breaking an older SDK.
- Unknown **serializer IDs**: the testfx receiver has no serializer to look up, but the current
  `dotnet/sdk` data-pipe server is created with `skipUnknownMessages: true`, so it produces an
  `UnknownMessage`, logs and skips it, and still replies with `VoidResponse`. Either way, new *messages*
  must be **version- or capability-gated** for their semantics to be understood — do not rely on a receiver decoding an
  unknown serializer ID. (Unknown *fields* within a known message are always safe, per the previous rule.)
- The control channel is capability-gated: a named-pipe peer must advertise a pipe name, and an HTTP
  peer must advertise `ServerControlTransport=http`. An older peer that advertises neither leaves it disabled.
- Chunked artifact transfer is separately capability-gated. Absent or unsupported capability data
  suppresses HTTP artifact reporting with a warning; failure after negotiation is terminal (§9.9).

---

## 11. Connection-loss behavior (assumptions)

Connection-loss detection differs by transport and is a core integration consideration.

**Data pipe (`exitProcessOnConnectionLoss: true`).** If the SDK disconnects there is no way to recover,
so the host process **exits abnormally** with exit code `GenericFailure` (1). This happens on two paths:
the host reads EOF where a reply was expected, or a write fails with `IOException`/`ObjectDisposedException`
while sending a request. On the **read-EOF** path the host also writes a diagnostic to stderr before
exiting; the **write-failure** path calls `Exit(GenericFailure)` directly **without** that diagnostic.
This is deliberate: if the user kills `dotnet.exe`, the test host must die too rather than orphan.

**HTTP primary channel.** HTTP has no persistent session whose closure can notify an otherwise idle host.
Gateway loss, DNS/network failure, authentication expiry, or a malformed response is detected by the
current POST, or by the next POST if the host was idle. A failed request fails the host operation; there
is no transparent retry because retrying after an ambiguous network failure could duplicate a message.
Once a POST starts, any transport, authentication, framing, or response failure makes the transport
terminal and cancels queued callers because the SDK and host protocol states may be ambiguous.
Automatic redirects are disabled for the same reason: every `3xx` response fails the operation instead
of replaying the authenticated frame at another location. The WASI handler does not implement automatic
redirects and does not support configuring that property, so the host leaves its default untouched and
still rejects any returned `3xx` status.
The implicit `HttpClient` timeout is disabled; cancellation is controlled by the operation token passed
to `HttpClient.SendAsync` and response-body reads, aborting the in-flight fetch without converting
expected cancellation into a connection-loss failure. Cancellation after a request starts makes the
transport terminal so another protocol request cannot overlap a partially received reply. If a handler
does not complete its send after cancellation, the pending send is observed before its request resources
are released. Disposal cancels no completed messages and prevents later use.

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
> The host-side behavior is defined here. SDK availability must be checked against the exact
> source-matched counterpart, not inferred from a negotiated version or assumed for a released SDK.
> The experimental browser counterpart is pinned in `eng/browser-testing/sdk.json`.

Purpose: let the SDK push a control signal (today only session cancellation, e.g. from a global
`--maximum-failed-tests` or `--timeout`) to the running test host, even while the host is otherwise
silent.

This auxiliary channel is separate from the primary client's ordering lock. Named-pipe peers use the
SDK-advertised control pipe. HTTP peers use a **second authenticated HTTP client** only when
`ServerControlTransport=http` is advertised; `ServerControlPipeName` alone never activates HTTP
control. The second client uses the same per-run endpoint/token, but its parked request does not block
results or artifact transfers on the primary client. Host-local cancellation still cancels in-flight
HTTP requests normally.

### 12.1 Named-pipe control

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

### 12.2 Authenticated HTTP control (experimental 1.5.0)

After a successful compatible handshake advertising `ServerControlTransport=http`, the host starts a
background listener using its second HTTP client. It parks `WaitForServerControlRequest` (ID 13) and
accepts:

- `ServerControlMessage(Kind=1)`: `CancelSession`; invoke the existing graceful-stop reaction exactly
  once, using the same interlocked guard as named-pipe control.
- `ServerControlMessage(Kind=2)`: `CloseChannel`; finish the listener without cancelling the session.
- `VoidResponse`: the SDK's bounded long poll expired without a command; wait 50 ms before parking
  another request so an immediately replying peer cannot create a tight loop.

Unknown control kinds are ignored and the listener continues. Unlike a failure to establish the
optional named-pipe connection, an HTTP control failure while the session is live requests cooperative
cancellation and records a diagnostic. Expected listener cancellation during shutdown does not request
another session cancellation. Exit cancels and disposes the auxiliary client and bounds the listener
wait to five seconds.

---

## 13. End-to-end lifecycle

The host builder creates a `DotnetTestConnection` and calls `AfterCommonServiceSetupAsync`, which selects
the legacy named pipe or authenticated HTTP from the hidden bootstrap options, sets/reads the Execution ID
environment variable, and initializes the client. The overall flow:

```mermaid
sequenceDiagram
    participant SDK as dotnet test SDK
    participant Host as Test host

    SDK->>SDK: create data pipe or per-run HTTP gateway + token
    SDK->>Host: launch with named-pipe or HTTP bootstrap [--list-tests|--help|...]
    Host->>SDK: connect pipe, or initialize HttpClient (no probe)
    Host->>SDK: HandshakeMessage (request)
    SDK-->>Host: HandshakeMessage (reply: negotiated version, [IsIDE], [control capability], [artifact-transfer capability and limits])

    alt incompatible version
        Host->>Host: exit IncompatibleProtocolVersion
    else help mode
        Host->>SDK: CommandLineOptionMessages
        SDK-->>Host: VoidResponse
    else run / discover
        opt matching auxiliary control capability advertised
            Host->>SDK: connect control pipe or second HTTP client + park WaitForServerControlRequest
        end
        Host->>SDK: TestSessionEvent(TestSessionStart)
        SDK-->>Host: VoidResponse
        loop per test node update
            opt HTTP artifact export negotiated
                Host->>SDK: FileArtifactTransferRequest chunk(s)
                SDK-->>Host: FileArtifactTransferResponse (offset; final committed path)
            end
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
| Constants (states, versions, handshake props) | `ServerMode/DotnetTest/IPC/Constants.cs` |
| Serializer registry | `IPC/Serializers/RegisterSerializers.cs` |
| Serializer primitives / envelope | `IPC/Serializers/BaseSerializer.cs`, `NamedPipeSerializer.cs` |
| Framing / named-pipe transport | `IPC/NamedPipeConnectionBase.cs`, `NamedPipeServer.cs`, `NamedPipeClient.cs` |
| HTTP primary transport | `ServerMode/DotnetTest/Transport/DotnetTestHttpClient.cs` |
| Chunked artifact transfer | `ServerMode/DotnetTest/IPC/Models/FileArtifactTransfer*.cs`, `ServerMode/DotnetTest/IPC/Serializers/FileArtifactTransfer*Serializer.cs` |
| Pipe naming | `NamedPipeServer.GetPipeName` |
| Message models | `ServerMode/DotnetTest/IPC/Models/*.cs`, `IPC/Models/*.cs` |
| Message serializers | `ServerMode/DotnetTest/IPC/Serializers/*.cs` |
| Host connection / handshake / control channel | `ServerMode/DotnetTest/DotnetTestConnection.cs` |
| Host data consumer (state → message mapping) | `ServerMode/DotnetTest/IPC/DotnetTestDataConsumer.cs` |
| Shared-source manifest (vendored to dotnet/sdk) | `ServerMode/DotnetTest/DotnetTestProtocolContract.props` |
| Black-box protocol reference / tests | `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests/DotnetTestPipe/*`, `BrowserWasmExecutionTests.cs`, `test/UnitTests/Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests/*` |
