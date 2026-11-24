# MTP controller data consumers: research and decisions

## Problem and contract

Coverlet.MTP publishes `TestCoverageReportMessage` from its controller-side
process-exit handler. A reporter that only registers a test-host `IDataConsumer`
cannot receive that message, while registering another process-exit handler
couples it to extension registration order.

The proposed API lets an extension observe controller-published `IData` when a
controller is already required for another reason. Registering the observer
alone must not create a controller process. It is not a child-process message
relay, a run-completion notification, or a retry-wide aggregation mechanism.
The existing force-restart consumer registration remains internal.

Producer: Coverlet.MTP 10.1.0+ (controller-side coverage report messages).
Consumer: third-party MTP reporters such as ReportGenerator.Mtp. The MTP
consumer boundary is in `Microsoft.Testing.Platform` (source version 3.0.0 at
the inspected revision); downstream package versions and release branches must
be aligned by the consuming integration.

## Research and experiments

### 2026-11-21: inspect issue and MTP lifecycle

Source state: `3bf21c761a747e2c95135401c9138dd1640888d7`.

- Issue #8904 describes ReportGenerator running before Coverlet's exit handler.
  The controller invokes exit handlers sequentially and drains its message bus
  after each handler. Coverlet publishes the report message from this controller
  lifecycle.
- Existing internal `AddDataConsumer` contributes to the controller restart
  decision. `AddDataConsumerWhenControllerStarts` is an observer and is only
  built after some other registration requires the controller.
- The internal TRX extension already uses controller-side consumers. The
  controller's test-host IPC does not forward ordinary child `IData` messages.
- ReportGenerator PR #797 used reflection and observed coverage messages/files
  in both handler orders, but did not prove populated ReportGenerator output.
- Three independent design reviews recommended an experimental observer API;
  they identified per-message aggregation, cancellation, browser behavior,
  composite-factory validation, and consumer lifetime as contract risks.

### 2026-11-21: implement and validate the observer capability

Source state: base commit `3bf21c761a747e2c95135401c9138dd1640888d7` plus the
uncommitted implementation and tests in this worktree. The final review retained
the existing internal TRX consumer's browser-unsupported behavior; only the new
public observer registration is a browser no-op.

- Added the optional experimental `ITestHostControllerDataConsumerManager`
  capability and extension methods without changing the shipped
  `ITestHostControllersManager` contract. Observer registration does not affect
  the controller restart decision. Unsupported custom managers get a localized
  `NotSupportedException`.
- Added four focused unit tests and a package-consuming acceptance test. The
  acceptance test uses a synthetic controller message producer and report
  consumer; it is not a Coverlet + ReportGenerator integration.
- An initial acceptance invocation targeted `net11.0`, but the acceptance test
  host targets `net12.0` and could not launch. The project was built and the
  same cases were rerun under `net12.0`.
- Some retained generated test-asset outputs had older package contents. Those
  were not used as provenance evidence: the final acceptance runs generated
  fresh private package caches, and their binlogs record the consumer's
  `Microsoft.Testing.Platform` 3.0.0-dev `net11.0` runtime asset and resolved
  path. The acceptance harness removes those temporary cache files after the
  run.

## Alternatives and decision

- Adding lifetime-handler priorities would expose ordering dependencies without
  guaranteeing report publication and would couple unrelated extensions.
- Exposing the existing force-restart consumer path would make a reporting
  extension change process topology.
- A coverage-specific hook duplicates the report-message contract and is too
  narrow for other controller-published data.
- A run-completion callback is not required for the issue's per-report
  consumption scenario. It would be a separate lifecycle contract and is not
  included. Consumers needing a merged, run-wide result need a separately
  designed completion surface.

Implement an optional controller-data-consumer capability and extension
methods, following the existing test-host-orchestrator middleware pattern. This
preserves implementers of the shipped `ITestHostControllersManager` interface.
The API is experimental. It receives data published on the controller's bus,
not child-host data; it is best-effort during cancellation/finalization; it is
not invoked unless another feature starts a controller; it has no cross-consumer
ordering guarantee; and `SessionUid` is not guaranteed to identify a child
session. Browser registration is a no-op because browser execution has no
controller process.

The observer is invoked per message. It does not notify consumers when all
reports for a run or retry sequence have arrived. Slow or failing consumers
participate in the existing message-bus drain and therefore can delay or affect
controller finalization.

## Validation and demonstration

| Requirement / risk | Automated E2E evidence | Rendered check / gap |
| --- | --- | --- |
| Consumer is created only for an existing controller | Unit tests: observer alone does not construct the consumer or request a process restart; with a lifetime handler, the consumer is included in controller configuration. | Nonvisual lifecycle contract. |
| Consumer registration works through the shipped manager surface | Unit tests call the public extension methods through `ITestHostControllersManager`; include both factory forms and duplicate/null validation where supported. | Nonvisual API. |
| Controller-published report reaches an observer in either registration order | Packed-consumer acceptance cases `ControllerDataConsumerObserver_ConsumesCoverageReportInEitherRegistrationOrder`: consumer before/after producer; both assert exact coverage XML and synthetic HTML output. The generated consumer resolves package `Microsoft.Testing.Platform` 3.0.0-dev, `net11.0`, as recorded in the acceptance build binlog. | Synthetic HTML only. Actual Coverlet + ReportGenerator output remains unverified. |
| Failure, cancellation, retry and host-mode behavior | Existing message-bus/finalization tests remain relevant; focused local tests cover observer activation only. Cancellation, retry, and server-mode composition remain follow-up integration evidence. | No visual changes in MTP itself. |

### Test-value evidence

The green and behavior-removal runs used the same two acceptance test UIDs:
`116ce3d9-05dd-85cb-9e01-872ec54ccc12` (consumer after producer) and
`19916861-de63-8c00-9cdb-a4aee95eed7c` (consumer before producer). Both selected
and executed two tests; neither run skipped tests. The generated consumer
asserts report-file availability and exact HTML content, not merely process
success or text presence.

| State | Source and package | Command and outcome |
| --- | --- | --- |
| Green | Base `3bf21c761a747e2c95135401c9138dd1640888d7` plus this worktree's implementation and test changes. `Microsoft.Testing.Platform.3.0.0-dev` package SHA-256: `EC53017C663A98B64B18494B7D3DEE4F8972EE52BE861B61D376B9B05965967D`. The acceptance build binlog records the package's `net11.0` runtime asset resolved from a fresh private cache. | `.\build.cmd -pack -c Release -bl` — exit 0, 0 warnings/errors. `.\build.cmd -projects "test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj" -c Release -bl` — exit 0, 0 warnings/errors. `.\.dotnet\dotnet.exe test test\UnitTests\Microsoft.Testing.Platform.UnitTests\Microsoft.Testing.Platform.UnitTests.csproj -c Debug -f net10.0 --filter "FullyQualifiedName~ControllerDataConsumerObserver" -p:UsingDotNetTest=true` — exit 0, 4 passed, 0 failed/skipped. `.\.dotnet\dotnet.exe test test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-build -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\ControllerDataConsumer -- --filter-uid 116ce3d9-05dd-85cb-9e01-872ec54ccc12 --filter-uid 19916861-de63-8c00-9cdb-a4aee95eed7c` — exit 0, 2 passed, 0 failed/skipped. |
| Red (behavior-removal check, not pristine baseline) | Same base, API, tests, and support changes in the task-owned comparison worktree; removed only the observer factory build/register call from `TestHostControllersManager.BuildAsync`. Repacked that state and retained the test assertions. Comparison package SHA-256: `59D1A0D5DCCFFFE008680E7123537D8766D107A4D0D9A1CA687EE179AF20FD39`. Its acceptance build binlog also records `Microsoft.Testing.Platform` 3.0.0-dev and the `net11.0` runtime asset. | `.\build.cmd -pack -c Release -bl` — exit 0. `.\.dotnet\dotnet.exe test test\IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests\Microsoft.Testing.Platform.Acceptance.IntegrationTests.csproj -c Release -f net12.0 --no-build -p:UsingDotNetTest=true -p:ArtifactsTestResultsDir=artifacts\TestResults\Release\ControllerDataConsumer-Red -- --filter-uid 116ce3d9-05dd-85cb-9e01-872ec54ccc12 --filter-uid 19916861-de63-8c00-9cdb-a4aee95eed7c` — exit 2, 2 failed, 0 passed/skipped. Both fail at the intended `File.Exists(htmlPath)` assertion because the observer did not create the synthetic report. |

CI selection was inspected, but CI was not run: the general Windows Release
suite (`TestFx.slnx`) and Linux/macOS suite (`NonWindowsTests.slnf`) include the
MTP acceptance project. The Windows application-model-only category lane is
not the relevant lane for these tests.

The package test proves the MTP observer contract with a synthetic producer
and consumer. It does not prove that downstream Coverlet and ReportGenerator
packages cooperate, populate a real report, or aggregate multiple reports or
retry attempts. No browser runtime, cancellation, server-mode, or downstream
integration run was performed. No commit or GitHub publication was made.

## Open questions

- Downstream adoption must prove a populated report from aligned shipping
  packages and decide how a consumer handles multiple reports or retry attempts.
- The current API is intentionally not an end-of-run aggregation mechanism.
- No release-branch servicing or backport decision is made here; the owning
  feature team should make it before downstream enablement.
