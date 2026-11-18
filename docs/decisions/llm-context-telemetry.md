# LLM context telemetry: research and decisions

## Contract

Add two common properties to the optional Microsoft.Testing.Extensions.Telemetry
usage collector:

| Property | Value |
| --- | --- |
| `is llm` | Existing `LLMEnvironmentDetector` result as `true`/`false`; a heuristic, not proof of human/agent authorship. |
| `CopilotAgentSessionId` | Normalized `COPILOT_AGENT_SESSION_ID`, or `not_present` when absent/empty. |

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

Use the SDK's exact correlation key to simplify cross-product queries. Keep
`is llm` distinct from SDK's `llm`, which contains agent names rather than a boolean.

## Test-value evidence

SDK-key alignment validated on 2026-11-18, Debug, SDK `12.0.100-alpha.1.26562.108`.
Base: `53b0b9bd9df4f856498e18dec978ca86c9753cfc`.
Behavioral `src`/`test` patch SHA-256:
`c6abdb455adf3e0e0634e1dfedb99d8397ff21d98d640e8a7408618de3a41fbf`.
This document is excluded from that patch identity.

| Contract | Tests |
| --- | --- |
| Detection and correlation | `LogEvent_ReportsLLMContext` (6 cases), `LogEvent_NormalizesCopilotSessionId` (17 cases: UUIDs, absence, casing, whitespace, malformed IDs, UTF-8). |
| Queueing and ownership | `LogEvent_SnapshotsContextBeforeQueueingWithoutMutatingCallerProperties`: three distinguishable events, changing context, overrides, and dictionary reuse. |
| Opt-outs | Packaged environment-variable and `TestApplicationOptions` tests with the real provider registered. |
| Shipping boundary | `Telemetry_Context_IsIncludedInEveryUsageEvent`: four contexts per runtime; checks each event's fields, identity, and unchanged platform session ID. |

| Selection | Passed / executed | Result |
| --- | --- | --- |
| Provider tests: .NET 10 / 11 / Framework 4.6.2 / 4.7.2 | 57 / 57, 57 / 57, 54 / 54, 54 / 54 | Exit 0 |
| Packaged acceptance: .NET 10 / 11 / Framework 4.6.2 | 15 / 15 | Exit 0 |

Current alignment: **222 unit and 15 acceptance cases passed**, no failures/skips.
TRX/JUnit/CTRF counts were reconciled; method-level parallelization was preserved.
The acceptance host runs on .NET 12. Its custom-framework children intentionally
exit 8 (`ZeroTests`), exercising startup/shutdown telemetry rather than counting
zero-test runs as regression tests.

**Executed regression check:** the same 17 `LogEvent_NormalizesCopilotSessionId`
cases ran in an isolated output copy with the preserved pre-rename provider.
All 17 failed specifically because `CopilotAgentSessionId` was missing (exit 2).
Replacing only that DLL with the rebuilt provider yielded 17 passes (exit 0);
test assembly bytes were unchanged. Both runs also checked report counts.
The initial feature's separate field-removal check detected 24 failures before
key alignment; its earlier reports remain archived.

Both changed `3.0.0-dev` packages packed successfully. All six executed consumer
DLLs matched their NuGet payload hashes, using `netstandard2.0` assets on
.NET Framework. Isolated restores and actual loaded assembly paths were checked.
The packaged scenarios verified 24 usage events across 12 context-specific launches,
including the SDK key's presence and the old space-separated key's absence.

## Validation notes and remaining work

CI exposed a Release-only failure: the namespace import for `RoslynString` was
guarded by `#if DEBUG`. The import is now unconditional. Release packing passes
for all three product TFMs, and 190 Release provider cases pass across .NET 10/11
and Framework 4.6.2/4.7.2 (49/49/46/46). This fixes compilation, not runtime behavior.

Full commands, reports, patches, and DLL hashes remain in session artifacts under
`sdk-key-alignment`: `validate.ps1`, `comparison-provenance.json`, and
`consumer-provenance-complete\provenance.json`. Fixture-only packages were restored
temporarily for acceptance and removed afterward; the isolated comparison output
is also cleaned up. Final `.\eng\verify-nupkgs.ps1 -configuration Debug` passed.

This is nonvisual metadata; backend ingestion/dashboard queries were not verified.
Before release, update the
[public telemetry documentation](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry)
and complete any required telemetry governance review.
