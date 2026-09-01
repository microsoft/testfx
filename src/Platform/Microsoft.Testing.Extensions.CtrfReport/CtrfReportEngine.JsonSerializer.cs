// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private byte[] BuildCtrfJson(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        List<ReportTestResult> preparedResults = PrepareResults(results);

        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int pending = 0;
        int other = 0;
        int flaky = 0;
        foreach (ReportTestResult result in preparedResults)
        {
            switch (result.Final.Status)
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }

            if (result.IsFlaky)
            {
                flaky++;
            }
        }

        long startMs = _testStartTime.ToUnixTimeMilliseconds();
        long stopMs = finishTime.ToUnixTimeMilliseconds();
        long durationMs = Math.Max(0, stopMs - startMs);

        using var ms = new MemoryStream(capacity: 8 * 1024);
        // We deliberately use the default Default encoder rather than
        // UnsafeRelaxedJsonEscaping: CTRF documents routinely flow into web
        // dashboards that embed JSON into HTML/JS, and test names/messages are
        // attacker-controllable. The default safe encoder keeps `<`, `>`, `&`
        // escaped so a test display name like `<script>alert(1)</script>` can't
        // become an XSS vector in downstream consumers.
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
        };

        using (var writer = new Utf8JsonWriter(ms, writerOptions))
        {
            writer.WriteStartObject();

            writer.WriteString("reportFormat", CtrfReportFormat);
            // CTRF is still in pre-1.0; the upstream spec is at "0.0.0" today
            // (see https://github.com/ctrf-io/ctrf/blob/main/spec/ctrf.md).
            // Bump this constant whenever we update against a newer schema revision.
            writer.WriteString("specVersion", CtrfSpecVersion);
            writer.WriteString("reportId", Guid.NewGuid().ToString("D"));
            // CTRF 5.4 (`runId`): identifies the logical run this document belongs to. A logical run can span
            // several documents — most notably the successive processes of `--retry-failed-tests`, where each
            // attempt writes its own document. ctrf-io/ctrf#58 confirmed that those per-execution documents (and
            // any document merged from them) SHOULD share a `runId` while each keeps its own `reportId`.
            writer.WriteString("runId", ResolveRunId());
            writer.WriteString("timestamp", finishTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString(
                "generatedBy",
                $"Microsoft.Testing.Extensions.CtrfReport@{ExtensionVersion.DefaultSemVer}");

            writer.WritePropertyName("results");
            writer.WriteStartObject();

            // results.tool
            writer.WritePropertyName("tool");
            writer.WriteStartObject();
            // CTRF spec requires `tool.name` to be a non-empty string. Fall back to
            // a sentinel rather than emitting an empty string (which would fail
            // strict schema validation by downstream CTRF consumers).
            string toolName = RoslynString.IsNullOrEmpty(_testFramework.DisplayName)
                ? "unknown"
                : _testFramework.DisplayName;
            writer.WriteString("name", toolName);
            if (!RoslynString.IsNullOrEmpty(_testFramework.Version))
            {
                writer.WriteString("version", _testFramework.Version);
            }

            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            writer.WriteString("uid", _testFramework.Uid);
            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.summary
            writer.WritePropertyName("summary");
            writer.WriteStartObject();
            writer.WriteNumber("tests", preparedResults.Count);
            writer.WriteNumber("passed", passed);
            writer.WriteNumber("failed", failed);
            writer.WriteNumber("skipped", skipped);
            writer.WriteNumber("pending", pending);
            writer.WriteNumber("other", other);
            writer.WriteNumber("flaky", flaky);
            writer.WriteNumber("start", startMs);
            writer.WriteNumber("stop", stopMs);
            writer.WriteNumber("duration", durationMs);
            writer.WriteEndObject();

            // results.environment
            writer.WritePropertyName("environment");
            writer.WriteStartObject();
            string user = _environment.GetEnvironmentVariable("UserName")
                ?? _environment.GetEnvironmentVariable("USER")
                ?? string.Empty;
            // CTRF `osPlatform` expects a short identifier such as "win32", "linux" or
            // "darwin"; the full descriptive string belongs in `osVersion`.
            writer.WriteString("osPlatform", GetCtrfOsPlatform());
            writer.WriteString("osVersion", RuntimeInformation.OSDescription);
            // CTRF `extra` MUST be an object (schema enforces additionalProperties: false
            // on environment, with `extra` typed as object). We surface the test module
            // path and process exit code here rather than as top-level environment fields
            // because there is no first-class CTRF slot for them.
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            writer.WriteString("user", user);
            writer.WriteString("machine", _environment.MachineName);
            writer.WriteNumber("exitCode", _exitCode);
            writer.WriteString("testApplication", _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            if (_isIncomplete)
            {
                writer.WriteBoolean("incomplete", true);
                writer.WriteString("runStatus", "aborted");
            }

            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.tests
            writer.WritePropertyName("tests");
            writer.WriteStartArray();

            foreach (ReportTestResult result in preparedResults)
            {
                WriteTest(writer, result);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Resolves the CTRF <c>runId</c>: the id of the logical run this document belongs to.
    /// </summary>
    /// <remarks>
    /// The retry orchestrator sets <c>TESTINGPLATFORM_LOGICAL_RUN_ID</c> before launching its attempts, so every
    /// attempt process stamps the same value; a CI job can set it too, to correlate documents this process cannot
    /// know about (the modules of a multi-project run, or shards on different machines). Failing that, the
    /// <c>dotnet test</c> execution id identifies this test application's own process tree — note it is per root
    /// test application, NOT per <c>dotnet test</c> invocation, so sibling modules legitimately get distinct ids
    /// (see <c>docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md</c>). A fresh id is the last resort:
    /// an uncorrelated run is a logical run of its own, and CTRF requires the field to be a non-empty string.
    /// </remarks>
    private string ResolveRunId()
    {
        string? runId = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_LOGICAL_RUN_ID);
        if (RoslynString.IsNullOrEmpty(runId))
        {
            runId = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
        }

        return RoslynString.IsNullOrEmpty(runId) ? Guid.NewGuid().ToString("D") : runId!;
    }
}
