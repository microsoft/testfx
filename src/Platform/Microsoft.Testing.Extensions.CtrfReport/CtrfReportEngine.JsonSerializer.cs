// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private byte[] BuildCtrfJson(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        // Collapse multiple captures sharing the same UID into a single CTRF test
        // entry. The CTRF spec models retries as nested `retryAttempts[]` records
        // and exposes `flaky: true` on the final passing row; emitting separate
        // top-level rows for retries would inflate `summary.tests` and double-count
        // outcomes.
        List<CollapsedTestResult> collapsed = CollapseAttempts(results);

        // Aggregate summary counts from the collapsed (final) outcomes only.
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int pending = 0;
        int other = 0;
        int flaky = 0;
        foreach (CollapsedTestResult c in collapsed)
        {
            switch (c.Final.Status)
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }

            if (c.IsFlaky)
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
            writer.WriteNumber("tests", collapsed.Count);
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
            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.tests
            writer.WritePropertyName("tests");
            writer.WriteStartArray();

            foreach (CollapsedTestResult c in collapsed)
            {
                WriteTest(writer, c);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return ms.ToArray();
    }
}
