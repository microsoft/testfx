# Microsoft.Testing.Extensions.CtrfReport

Microsoft.Testing.Extensions.CtrfReport is an extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that generates a test report in the [Common Test Report Format (CTRF)](https://ctrf.io) at the end of a test session.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.CtrfReport` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.CtrfReport
```

## About

> **⚠️ Experimental:** This extension is currently experimental. The API, CLI options and on-disk format may change in future releases without notice. The CTRF specification itself is also pre-1.0 and may evolve.

This package extends Microsoft.Testing.Platform with:

- **CTRF (Common Test Report Format) report**: a single JSON file conforming to the immutable [CTRF 0.1.0 schema](https://github.com/ctrf-io/ctrf/blob/v0.1.0/schema/ctrf.schema.json) that can be consumed by any tool that understands CTRF (dashboards, CI integrations, AI agents, etc.) without requiring a TRX or JUnit XML parser.
- **Cross-tool interoperability**: same shape as outputs produced by other testing frameworks that adopt CTRF, so results from multiple test runs can be aggregated by a single consumer.

## Usage

Enable the report via the `--report-ctrf` command line option. The report file name can be overridden with `--report-ctrf-filename <name>.json`.

## Identity and attachment semantics

- `testId` is derived from the producing framework's full Microsoft.Testing.Platform `TestNode.Uid`. Most UIDs within the capture bound are emitted unchanged; values in the reserved `uid:`/`sha256:` namespaces are escaped, and longer UIDs use a deterministic SHA-256 identity, so neither truncation nor a hash-shaped raw UID can merge distinct tests. The CTRF extension does not add cross-run or cross-module stability guarantees beyond the framework's UID contract.
- `executionId` is a new UUID for each reported CTRF execution lifecycle. Earlier attempts represented in `retryAttempts` receive their own `attemptId`.
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

Producer-owned metadata lives under the root `extra["microsoft.testingplatform"]` object. It is experimental extension data, not a standardized CTRF provenance field:

| Field | Meaning |
| --- | --- |
| `documentRole` | `execution` for a physical-execution report, including an in-process retry history; `merged` for a derived report. |
| `mergeMode` | Merged reports only: `concatenate` for combining module reports, or `collapseRetryAttempts` for successive retry processes. |
| `inputCount` | The number of accepted immediate input documents, not the number of expected executions or distinct report IDs. |
| `inputs` | One entry per accepted immediate input, in merge order. Each entry contains its `reportId` when supplied; an empty object means the input had no usable report ID. |
| `inputCompleteness` | Currently `unknown`: the merger has no authoritative expected-input set for the logical run. This does not mean supplied inputs were unreadable or execution was aborted. |

For nested merges, `inputs` identifies the immediate parent reports, not a flattened list of all physical executions. Retain those immutable reports to follow their lineage. Legacy or foreign reports may lack IDs or this extension metadata; do not infer lineage from matching test names or a shared `runId`.

For aggregate analysis, consume the merged report instead of also counting the input reports it represents. Retain raw inputs for diagnostics. Do not deduplicate solely by `runId`, because independent module reports legitimately share it. When input identities are unavailable, select the aggregate explicitly rather than assuming every report in a directory is disjoint.

Lineage is not proof of completeness. An input list records what contributed, not what should have contributed; neither an observed count nor the maximum retry budget proves that all expected executions are present. Known truncated module runs are not merged, and retry merging requires exactly one matching artifact per executed attempt. These safeguards remain in place. The existing `results.environment.extra.incomplete` / `runStatus: "aborted"` markers describe interrupted test execution, separately from missing merge inputs.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

For the versioned CTRF specification, see <https://github.com/ctrf-io/ctrf/blob/v0.1.0/spec/ctrf.md>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
