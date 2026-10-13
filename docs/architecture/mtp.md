# Microsoft.Testing.Platform architecture

## Baseline and assurance

- Status: **current implementation baseline (scoped)**, not a release specification.
- Inspected revision: `c5e1f5c613fa02ffa507db333e4702da6c97e488`.
- Inspected date: **2026-10-09**.
- Scope: `src/Platform`, corresponding unit/acceptance tests, and the MTP design documents.
- Assurance: source inspection and existing test mapping only; **no tests were executed** for this documentation change.

Read the [documentation guide](../specifications/README.md) and [observable contracts](../specifications/mtp.md) for maintenance rules, stable IDs and test evidence.
RFCs record decisions/proposals, not proof that every described feature shipped. External SDK,
IDE, coverage-collector, and CI-service behavior is not verified by this baseline.

## Product boundaries

MTP embeds a host in a test application rather than requiring a central runner to discover arbitrary adapters.
The application registers one framework and selected extensions, builds, and runs. Frameworks own
discovery/execution; MTP owns dispatch, extension composition, messages, output and application policies.

The [core package](../../src/Platform/Microsoft.Testing.Platform/PACKAGE.md) describes the BCL-only dependency boundary.
Optional bridges, AI, reports, retry and diagnostics are separately packaged under `src/Platform`, not implicit capabilities of every host.
The [MSBuild package](../../src/Platform/Microsoft.Testing.Platform.MSBuild/PACKAGE.md) supplies entry-point/configuration generation and legacy test-target integration.

## Construction and ownership

[`TestApplication.CreateBuilderAsync`](../../src/Platform/Microsoft.Testing.Platform/Builder/TestApplication.cs) parses arguments
(including response files), sets up early diagnostics, and optionally invokes dynamic hooks before returning.
[`TestApplicationBuilder`](../../src/Platform/Microsoft.Testing.Platform/Builder/TestApplicationBuilder.cs) holds factories
and delegates construction to `TestHostBuilder`; framework registration is singular, not a Microsoft.Extensions DI container.

[`SetupCommonServicesAsync`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostBuilder.CommonServices.cs) assembles configuration,
logging/output proxies, cancellation, capabilities and policies, then validates options.
[`BuildHostAsync`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostBuilder.Modes.cs) selects an informational/tool host,
orchestrator, controller, or console/JSON-RPC test host. Discovery skips controller restart.

[`ServiceProvider`](../../src/Platform/Microsoft.Testing.Platform/Services/ServiceProvider.cs) is an ordered instance registry with
assignable-type lookup. Clones normally share references, with exceptions such as coverage capabilities:
not process serialization or a Microsoft.Extensions scope. Server requests add/replace owned services
and avoid disposing borrowed application services. See MTP-001, MTP-002 and MTP-009.

## Extension composition and execution flow

```text
Application entry point
  -> CreateBuilderAsync / dynamic hooks
  -> framework + static extension registration
  -> BuildAsync / common services + option validation
  -> selected host
       -> session-start notifications / drain
       -> framework CreateTestSessionAsync
       -> discovery OR run request + filter
       -> ExecuteRequestAsync / explicit completion
       -> framework CloseTestSessionAsync (normal path)
       -> drain / session-finishing notifications / drain
       -> verdict, final output, service cleanup
```

This is the normal console path, **not an unconditional callback guarantee after exceptions**.
[`ExtensionBuilderHelper`](../../src/Platform/Microsoft.Testing.Platform/Helpers/ExtensionBuilderHelper.cs) implements enabled/initializable
construction; composite factories share one instance across roles. Managers validate UID/type uniqueness.
[`TestHostTestFrameworkInvoker`](../../src/Platform/Microsoft.Testing.Platform/Requests/TestHostTestFrameworkInvoker.cs) waits for explicit
request completion, not just the return of the framework's task.

[`AsynchronousMessageBus`](../../src/Platform/Microsoft.Testing.Platform/Messages/AsynchronousMessageBus.cs) routes declared concrete types;
ordinary publication queues work, while blocking consumers and single-threaded runtimes consume inline.
[`CommonHost`](../../src/Platform/Microsoft.Testing.Platform/Hosts/CommonTestHost.SessionNotifications.cs) drains before finishing,
runs producer-only handlers before consumers, then drains between consumer finishers so final artifacts reach reports.
Cancellation bounds shutdown; a still-running consumer is not disposed underneath its callback. See MTP-003 and MTP-004.

## Configuration, selection and verdicts

Configuration sources use priority (lower `Order` first), with the first matching provider winning.
CLI presence/arguments also flow through configuration; passive defaults do not enable an option.
JSON environment declarations configure a child, not the current process. See MTP-005 and MTP-006.

Discovery and execution requests carry UID/tree/no-op representations; console provider constraints
compose with AND and the framework interprets the filter. JSON-RPC selection remains client-owned.
Graph properties match `TestMetadataProperty`, not arbitrary properties. See MTP-007.

Application cancellation, request cancellation, and graceful stop have different boundaries.
Verdicts include session/test failures, cancellation/truncation and count policies; coverage gates
override only success. All-skipped success is not proof of execution. See MTP-008 and MTP-013.

## Process and protocol boundaries

| Boundary | Local responsibility | Not established by this audit |
| --- | --- | --- |
| Console host | In-process framework/session dispatch | Every framework's internal scheduling |
| Controller/child | Environment preparation, control pipe, launcher handle, bounded cancellation | Remote/container/MSIX deployments from illustrative RFC examples |
| JSON-RPC | Initialization, request IDs/cancellation, test/artifact notifications | IDE UI behavior or capability consumption |
| Native `dotnet test` binary channel | Host-initiated handshake/data request-reply over pipe or HTTP; optional reverse pipe | External SDK election, aggregation, transport symmetry or HTTP gateway deployment |
| Legacy MSBuild | `InvokeTestingPlatform` task and separate MSBuild consumer IPC | Equivalence to native SDK mode |

The controller uses either the default process handler or an `ITestHostLauncher`; it still owns
IPC and lifecycle reconciliation. Live services and factories do not cross the process boundary.
See MTP-009 and [launcher RFC history](../RFCs/017-TestHost-Launcher.md).

[JSON-RPC server mode](../mstest-runner-protocol/001-protocol-intro.md) and the
[`dotnettestcli` binary protocol](../mstest-runner-protocol/004-protocol-dotnet-test-pipe.md)
are separate transports/contracts, not two names for the same server.
Native pipe acceptance fixtures use a **fake SDK**: valuable host-contract coverage, not a real
composed SDK validation. See MTP-010 and MTP-011.

## Reports and optional integrations

Reports consume result/artifact messages and publish files; report generation is distinct from
multi-module consolidation. The artifact dispatcher matches declared kinds (extension fallback
only when kind is absent) and returns attributed outputs over the binary channel.
Some CI summary extensions defer publishing only after explicit required-postprocessing support
is acknowledged; no connection or a missing/false capability retains local ownership.
See MTP-012 and MTP-016; external orchestrator behavior remains unverified.

Microsoft.Extensions bridges borrow/import explicitly owned host services without converting
the MTP registry. Dynamic hooks operate on the real builder but cannot replace the framework.
AI contracts live in the optional AI package. See MTP-014, MTP-015 and MTP-017.

## Deliberate coverage limits

This is not a full API catalog or exhaustive extension specification. Detailed retry/hot-reload scheduling,
telemetry privacy/resource identity, dump/deployment matrices, report schemas/recovery/attachments, video,
AI authentication, CLI/configuration grammar and wire catalogs remain with owning designs/future contracts.
Release alignment, real SDK multi-module cancellation/fallback, servicing/backports and shipping-package
provenance require composed-product validation.
