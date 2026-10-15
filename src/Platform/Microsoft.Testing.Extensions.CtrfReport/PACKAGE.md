# Microsoft.Testing.Extensions.CtrfReport

Microsoft.Testing.Extensions.CtrfReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates a test report in the [Common Test Report Format (CTRF)](https://ctrf.io) at the end of a test session.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.CtrfReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.CtrfReport
```

## About

This package extends Microsoft.Testing.Platform with:

- **CTRF (Common Test Report Format) report**: a single JSON file conforming to the immutable [CTRF 0.2.0 schema](https://github.com/ctrf-io/ctrf/blob/v0.2.0/schema/ctrf.schema.json) that can be consumed by tools supporting that specification (dashboards, CI integrations, AI agents, etc.) without requiring a TRX or JUnit XML parser.
- **Cross-tool interoperability**: same shape as outputs produced by other testing frameworks that adopt CTRF, so results from multiple test runs can be aggregated by a single consumer.

## Compatibility

The stable 1.x package line supports the public registration API, existing CLI options, and documented CTRF 0.2.0 report semantics with backward compatibility. Reports remain pinned to the immutable 0.2.0 schema; a future upstream pre-1.0 specification is not adopted automatically. An incompatible schema or identity change requires a package major version or a separate explicit opt-in. Additive report fields must remain valid for the declared schema.

This commitment does not make the upstream CTRF working draft immutable, add cross-run identity guarantees to framework-provided UIDs, or turn producer-specific `extra` metadata into a stable interchange protocol. The identity and merge limitations below still apply.

The package follows the normal Microsoft.Testing.Platform prerelease and final-release cadence while retaining its own 1.x version line. Prerelease builds remain prerelease; the normal final release produces a stable package with aligned stable platform dependencies.

## Usage

Enable the report via the `--report-ctrf` command line option. The report file name can be overridden with `--report-ctrf-filename <name>.json`.

When using MSTest.Sdk, the extension remains opt-in and is not enabled by any profile. Add `<EnableMicrosoftTestingExtensionsCtrfReport>true</EnableMicrosoftTestingExtensionsCtrfReport>` to the project to include it.

## Identity and attachment semantics

- `testId` is derived from the producing framework's full Microsoft.Testing.Platform `TestNode.Uid`. Most UIDs within the capture bound are emitted unchanged; values in the reserved `uid:`/`sha256:` namespaces are escaped, and longer UIDs use a deterministic SHA-256 identity, so neither truncation nor a hash-shaped raw UID can merge distinct tests. The CTRF extension does not add cross-run or cross-module stability guarantees beyond the framework's UID contract.
- For MSTest, that identity depends on the assembly file name, fully qualified method name, parameter types, and expanded data-row index. Moving an assembly without renaming it does not change that identity; renaming the assembly or method can. Data-driven case history is comparable only while row ordering and case definitions remain unchanged. Reordering or inserting rows can associate an existing ID with a different case; the reporter cannot detect this or infer a safe stable key from arbitrary parameter values. Other frameworks must document their own UID stability scope; a run-local UID does not provide cross-run case correlation.
- `executionId` identifies one case's execution lifecycle, including its retries. In a retry child process, the reporter reuses the retry orchestrator's existing `TESTINGPLATFORM_TRX_TESTRUN_ID` scope, which is fresh for each coordinated workflow and is also forwarded to activated packaged hosts. It combines that scope with the framework UID and case ID so each case retains the same `executionId` in its raw attempt reports and consolidated report without requiring a new orchestration protocol. Independent workflows remain distinct even when a CI job supplies the same `runId`. Outside a retry child, without a shared orchestration scope, or when duplicate UIDs make correlation ambiguous, the reporter generates independent execution IDs.
- Each physical attempt receives its own UUID. The final test object uses the standard `attemptId` field introduced in CTRF 0.2.0, including tests that were never retried; earlier attempts use `retryAttempts[].attemptId`. Older 0.1.0 reports carried the final identity in `extra.mtpAttemptId`. Retry merging still accepts that key and prefers a native `attemptId` when both are present. Legacy reports without an attempt identity remain mergeable, but the merger omits their optional `attemptId` rather than treating an execution lifecycle ID as an attempt ID.
- Retry consolidation uses both case identity and `executionId`, preserving distinct lifecycles even when they share a framework UID. Legacy rows without an execution ID can be correlated only when their case identity is unique in each input report and does not span conflicting known lifecycles. Repeated case/lifecycle pairs within one report remain separate; ambiguous results are preserved rather than guessed into another test's retry history.
- `extra.uid` continues to carry the Microsoft.Testing.Platform UID for compatibility with reports produced before first-class CTRF identity fields were available.
- `attachments[].path` is emitted unchanged as an opaque CTRF path value. It can be a local absolute file path and consumers must not assume that it is remotely accessible.

## Logical runs and retries

One coordinated invocation, including a multi-project `dotnet test` invocation, is one logical run. Its module reports, retry-attempt reports, and merged report should share a `runId`; each distinct report has its own `reportId`.

An outer launcher or CI script can supply `TESTINGPLATFORM_LOGICAL_RUN_ID` before launching the modules. Generate a fresh value for each coordinated invocation and propagate it to all contributing processes. Do not reuse a CI job ID for separate test invocations. For example, in PowerShell:

```powershell
$previousRunId = $env:TESTINGPLATFORM_LOGICAL_RUN_ID
try {
    $env:TESTINGPLATFORM_LOGICAL_RUN_ID = [Guid]::NewGuid().ToString("D")
    dotnet test
}
finally {
    $env:TESTINGPLATFORM_LOGICAL_RUN_ID = $previousRunId
}
```

Microsoft.Testing.Platform does not currently receive an automatic invocation-wide ID from `dotnet test`. When no explicit logical-run ID is available, module reports omit the optional `runId` rather than using the module-local IPC execution ID. Standalone test applications generate their own logical-run ID; standalone retry orchestration shares that ID across its attempts. A merged report preserves `runId` only when every input supplies the same value.

For a retried test, the test object represents the final attempt, including its duration. `retryAttempts` contains the preceding attempts, numbered `1..N-1`, and `retries` equals that history's length. Retry merging counts each logical test once and marks a test as flaky when its final outcome passes after an earlier failure.

## Merge lineage and consumer behavior

Producer-owned metadata lives under the root `extra["microsoft.testingplatform"]` object. It is diagnostic extension data, not a standardized CTRF provenance field or a stable interchange contract:

| Field | Meaning |
| --- | --- |
| `documentRole` | `execution` for a physical-execution report, including an in-process retry history; `merged` for a derived report. |
| `mergeMode` | Merged reports only: `concatenate` for combining module reports, or `collapseRetryAttempts` for successive retry processes. |
| `inputCount` | The number of accepted immediate input documents, not the number of expected executions or distinct report IDs. |
| `inputs` | One entry per accepted immediate input, in merge order. Each entry contains its `reportId` when supplied; an empty object means the input had no usable report ID. |
| `inputCompleteness` | Currently `unknown`: the merger has no authoritative expected-input set for the logical run. This does not mean supplied inputs were unreadable or execution was aborted. |

For nested merges, `inputs` identifies the immediate parent reports, not a flattened list of all physical executions. Retain those immutable reports to follow their lineage. Legacy or foreign reports may lack IDs or this extension metadata; do not infer lineage from matching test names or a shared `runId`.

When any input declares CTRF 0.2.0, the merged report also declares 0.2.0 regardless of input order. It accepts the reporter's historical 0.0.0 documents and the compatible 0.0.4/0.1.0 schema family alongside 0.2.0. Other or undeclared versions cannot be mixed with 0.2.0: the merge fails without overwriting its inputs or destination. Without a 0.2.0 input, the existing first-input version behavior is unchanged. Legacy final test objects retain their original identity fields; their optional native `attemptId` is not fabricated.

A schema-promoted document gets a distinct deterministic `reportId` so it cannot be mistaken for the older merger's differently versioned output. Merging those same inputs again remains idempotent.

For aggregate analysis, consume the merged report instead of also counting the input reports it represents. Retain raw inputs for diagnostics. Do not deduplicate solely by `runId`, because independent module reports legitimately share it. When input identities are unavailable, select the aggregate explicitly rather than assuming every report in a directory is disjoint.

Lineage is not proof of completeness. An input list records what contributed, not what should have contributed; neither an observed count nor the maximum retry budget proves that all expected executions are present. Known truncated module runs are not merged, and retry merging requires exactly one matching artifact per executed attempt. These safeguards remain in place. The existing `results.environment.extra.incomplete` / `runStatus: "aborted"` markers describe interrupted test execution, separately from missing merge inputs.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

For the versioned CTRF specification, see <https://github.com/ctrf-io/ctrf/blob/v0.2.0/spec/ctrf.md>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
