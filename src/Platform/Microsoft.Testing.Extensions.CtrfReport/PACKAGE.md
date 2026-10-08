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
- For MSTest, that identity depends on the assembly file name, fully qualified method name, parameter types, and expanded data-row index. Moving an assembly without renaming it does not change that identity; renaming the assembly or method can. Data-driven case history is comparable only while row ordering and case definitions remain unchanged. Reordering or inserting rows can associate an existing ID with a different case; the reporter cannot detect this or infer a safe stable key from arbitrary parameter values. Other frameworks must document their own UID stability scope; a run-local UID does not provide cross-run case correlation.
- `executionId` identifies one case's execution lifecycle, including its retries. In a retry child process, the reporter reuses the retry orchestrator's existing `TESTINGPLATFORM_TRX_TESTRUN_ID` scope, which is fresh for each coordinated workflow and is also forwarded to activated packaged hosts. It combines that scope with the framework UID and case ID so each case retains the same `executionId` in its raw attempt reports and consolidated report without requiring a new orchestration protocol. Independent workflows remain distinct even when a CI job supplies the same `runId`. Outside a retry child, without a shared orchestration scope, or when duplicate UIDs make correlation ambiguous, the reporter generates independent execution IDs.
- Each physical attempt receives its own UUID. Earlier attempts represented in `retryAttempts` use the standard `attemptId` field. CTRF 0.1.0 does not allow `attemptId` on the final test object, so that attempt's identity is carried in `extra.mtpAttemptId` instead; retry merging preserves it when promoting a raw result into history. Legacy reports without an attempt identity remain mergeable, but the merger omits their optional `attemptId` rather than treating an execution lifecycle ID as an attempt ID.
- `extra.uid` continues to carry the Microsoft.Testing.Platform UID for compatibility with reports produced before first-class CTRF identity fields were available.
- `attachments[].path` is emitted unchanged as an opaque CTRF path value. It can be a local absolute file path and consumers must not assume that it is remotely accessible.

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

For the versioned CTRF specification, see <https://github.com/ctrf-io/ctrf/blob/v0.1.0/spec/ctrf.md>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
