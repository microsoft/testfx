# LLM context telemetry: research and decisions

## Contract

Add two common properties to the optional Microsoft.Testing.Extensions.Telemetry
usage collector:

| Property | Value |
| --- | --- |
| `is llm` | Existing `LLMEnvironmentDetector` result as `true`/`false`; a heuristic, not proof of human/agent authorship. |
| `copilot agent session id` | Normalized `COPILOT_AGENT_SESSION_ID`, or `not_present` when absent/empty. |

Match the SDK's `CopilotAgentSessionId`: preserve canonical UUID versions 1-8
with RFC variant, plus the nil UUID, including original casing. Hash other
values as lowercase SHA-256 of their original UTF-8 bytes, without trimming.
A literal `not_present` input is hashed. A missing telemetry property means
collection support is unknown.

The signals are independent. Snapshot them before queueing each event, without
mutating caller dictionaries or accepting overrides. Preserve existing opt-outs,
session/reporter IDs, runtime restrictions, cancellation, and transport.
No new public API or dependency; no changes to console defaults, OpenTelemetry,
or JSON-RPC `ServerTelemetry` forwarding, which operates independently of usage
telemetry opt-out.

## Research and decision

[dotnet/sdk#57106](https://github.com/dotnet/sdk/pull/57106) adds cross-product
Copilot correlation. Its pinned
[normalization implementation](https://github.com/dotnet/sdk/blob/a436089fa916892516a2d1d4ed964c96d07380fa/src/Cli/dotnet/Telemetry/TelemetryCommonProperties.cs)
was checked against the referenced Copilot runtime source on 2026-11-18.

Reuse TestFx's detector, previously used only for console defaults, and collect
the fields in `AppInsightsProvider.LogEventAsync`. Construction-time capture was
rejected because queued events could receive stale context. Keep normalization
private: TestFx's existing hasher uppercases inputs and would break correlation;
`Guid.TryParse` accepts UUID forms outside Copilot's contract. Collecting agent
names/raw marker values or adding OpenTelemetry tags is outside scope.

## Test-value evidence

Validated on 2026-11-18 with SDK `12.0.100-alpha.1.26562.108`, Debug builds.
Base: `28c534d9fd47856682e8d1c14f54a10a1b4a08bf`.
Behavioral `src`/`test` patch SHA-256:
`51bb4966e403e758b6f4c331080004821ae2c87cfde414f892c915dd0f906044`.
This document is excluded from that patch identity.

| Contract | Tests |
| --- | --- |
| Detection and correlation | `LogEvent_ReportsLLMContext` (6 cases), `LogEvent_NormalizesCopilotSessionId` (17 cases: UUIDs, absence, casing, whitespace, malformed IDs, UTF-8). |
| Queueing and ownership | `LogEvent_SnapshotsContextBeforeQueueingWithoutMutatingCallerProperties`: three distinguishable events, changing context, overrides, and dictionary reuse. |
| Opt-outs | `TelemetryManager_WhenDisabled_DoesNotCreateContextCollector`, plus packaged tests with the real provider registered. |
| Shipping boundary | `Telemetry_Context_IsIncludedInEveryUsageEvent`: four contexts per runtime; checks each event's fields, identity, and unchanged platform session ID. |

| Selection | Passed / executed | Result |
| --- | --- | --- |
| Provider tests: .NET 10 / 11 / Framework 4.6.2 / 4.7.2 | 57 / 57, 57 / 57, 54 / 54, 54 / 54 | Exit 0 |
| Manager/detector tests: .NET 10 / 11 / Framework 4.6.2 | 30 / 30 on each | Exit 0 |
| Packaged acceptance: .NET 10 / 11 / Framework 4.6.2 | 15 / 15 | Exit 0 |

Total: **312 unit and 15 acceptance cases passed**, no failures/skips.
TRX/JUnit/CTRF counts were reconciled; method-level parallelization was preserved.
The acceptance host runs on .NET 12. Its custom-framework children intentionally
exit 8 (`ZeroTests`), exercising startup/shutdown telemetry rather than counting
zero-test runs as regression tests.

**Executed counterfactual:** removing both fields before enqueueing, with the
same tests retained, produced 24 intended assertion failures and 33 passes on
.NET 10 (exit 2). Restoring the provider and forcing a rebuild recovered to
57 passes (exit 0). This is feature-removal evidence, not a pristine-base run.
Mutation patch SHA-256:
`15d8df8a7812dc2fd699e6aa4e5c294e0666b50a17280a38ba111a7235058d20`.

Both changed `3.0.0-dev` packages packed successfully. All six executed consumer
DLLs matched their NuGet payload hashes, using `netstandard2.0` assets on
.NET Framework. Isolated restores and actual loaded assembly paths were checked.
The packaged scenarios verified 24 usage events across 12 context-specific launches.

## Validation notes and remaining work

The default build could not find Visual Studio 18.12; supported
`-msbuildEngine dotnet` builds passed instead. Fixture package prerequisites were
packed separately. Tests use an explicit diagnostic directory because agent-aware
banner suppression hides the diagnostic-file announcement. A stale comparison
DLL after copying older source was identified by hashes and corrected with
`-t:Rebuild`; setup/build failures were not counted as behavioral evidence.

Full commands, reports, patches, binlogs, and DLL hashes remain in session
artifacts, including `run-unit-validation.ps1` and
`consumer-provenance-complete\provenance.json`. The temporary comparison export
was removed. Fixture-only MSTest packages are retained under `fixture-packages`;
copy them to `artifacts\packages\Debug\Shipping` before a local acceptance rerun
without a full pack. Final `.\eng\verify-nupkgs.ps1 -configuration Debug` passed.

This is nonvisual metadata; backend ingestion/dashboard queries were not verified.
Before release, update the
[public telemetry documentation](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry)
and complete any required telemetry governance review.
