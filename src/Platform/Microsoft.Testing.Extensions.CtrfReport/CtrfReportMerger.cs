// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// Merges several already-produced CTRF JSON reports into a single CTRF document.
/// </summary>
/// <remarks>
/// This is a pure, invocation-agnostic JSON-level merge (no I/O, no clock) that mirrors the
/// TRX and JUnit mergers, demonstrating that the same post-processing shape fits a JSON format:
/// <list type="bullet">
///   <item><description><c>results.tests[]</c> arrays are concatenated as-is.</description></item>
///   <item><description><c>results.summary</c> counters are re-derived by counting the merged <c>tests[]</c> (so <c>summary.tests</c> always matches the array length); <c>start</c>/<c>stop</c> use the earliest/latest across inputs, <c>duration</c> is the resulting span.</description></item>
///   <item><description><c>reportFormat</c>, <c>specVersion</c>, <c>tool</c> and <c>environment</c> are taken from the first report; <c>reportId</c> is freshly generated.</description></item>
/// </list>
/// </remarks>
internal static class CtrfReportMerger
{
    // Neutral tool identity used when merged inputs disagree on their producing test framework, so the
    // merged report does not misattribute one framework's identity to another's tests.
    private const string MergedToolName = "Microsoft.Testing.Extensions.CtrfReport (merged)";

    internal static string Merge(IReadOnlyList<string> inputReports)
    {
        if (inputReports is null)
        {
            throw new ArgumentNullException(nameof(inputReports));
        }

        if (inputReports.Count == 0)
        {
            throw new ArgumentException("At least one CTRF report is required to merge.", nameof(inputReports));
        }

        JsonObject? first = null;
        var mergedTests = new JsonArray();

        long? earliestStart = null;
        long? latestStop = null;

        // A same-kind merge can combine modules produced by different test frameworks. Track the
        // distinct tool identities so the merged report is only stamped with a single framework when
        // every input actually used it; otherwise a neutral merger identity is used (see below).
        var distinctToolNames = new HashSet<string>(StringComparer.Ordinal);
        JsonNode? firstTool = null;

        foreach (string reportJson in inputReports)
        {
            if (JsonNode.Parse(reportJson) is not JsonObject root)
            {
                continue;
            }

            first ??= root;

            JsonNode? results = root["results"];
            if (results?["tests"] is JsonArray testArray)
            {
                foreach (JsonNode? test in testArray)
                {
                    mergedTests.Add(test?.DeepClone());
                }
            }

            if (results?["tool"] is JsonNode toolNode)
            {
                firstTool ??= toolNode;
                distinctToolNames.Add((string?)toolNode["name"] ?? string.Empty);
            }

            JsonNode? summary = results?["summary"];
            if (summary is not null)
            {
                if (TryReadLong(summary, "start", out long start) && (earliestStart is null || start < earliestStart))
                {
                    earliestStart = start;
                }

                if (TryReadLong(summary, "stop", out long stop) && (latestStop is null || stop > latestStop))
                {
                    latestStop = stop;
                }
            }
        }

        if (first is null)
        {
            throw new ArgumentException("None of the provided inputs were valid CTRF reports.", nameof(inputReports));
        }

        long startMs = earliestStart ?? 0;
        long stopMs = latestStop ?? startMs;

        // Counters are derived from the merged tests[] rather than trusting each input's summary, so
        // summary.tests always equals the array length even when an input omitted or under-reported
        // its summary.
        long passed = 0, failed = 0, skipped = 0, pending = 0, other = 0, flaky = 0;
        foreach (JsonNode? test in mergedTests)
        {
            if (test is null)
            {
                continue;
            }

            switch ((string?)test["status"])
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }

            if (test["flaky"] is JsonValue flakyValue && flakyValue.TryGetValue(out bool isFlaky) && isFlaky)
            {
                flaky++;
            }
        }

        var summaryObject = new JsonObject
        {
            ["tests"] = mergedTests.Count,
            ["passed"] = passed,
            ["failed"] = failed,
            ["skipped"] = skipped,
            ["pending"] = pending,
            ["other"] = other,
            ["flaky"] = flaky,
            ["start"] = startMs,
            ["stop"] = stopMs,
            ["duration"] = Math.Max(0, stopMs - startMs),
        };

        var resultsObject = new JsonObject();

        // Only carry a concrete tool identity when every input reported the same one (the common
        // single-framework case). When inputs disagree, stamping the first framework onto all tests
        // would misattribute the others, so use a neutral merger identity instead.
        if (distinctToolNames.Count == 1 && firstTool is not null)
        {
            resultsObject["tool"] = firstTool.DeepClone();
        }
        else
        {
            resultsObject["tool"] = new JsonObject { ["name"] = MergedToolName };
        }

        resultsObject["summary"] = summaryObject;

        // The environment is taken from the first report for shared fields (OS, user, machine), but
        // module-specific values under 'extra' (the producing test application and its exit code) cannot
        // describe all merged modules, so they are dropped rather than misattributed.
        if (first["results"]?["environment"] is JsonNode environment && environment.DeepClone() is JsonObject mergedEnvironment)
        {
            if (mergedEnvironment["extra"] is JsonObject environmentExtra)
            {
                environmentExtra.Remove("testApplication");
                environmentExtra.Remove("exitCode");
            }

            resultsObject["environment"] = mergedEnvironment;
        }

        resultsObject["tests"] = mergedTests;

        var merged = new JsonObject
        {
            ["reportFormat"] = first["reportFormat"]?.DeepClone() ?? "CTRF",
            ["specVersion"] = first["specVersion"]?.DeepClone() ?? "0.0.0",
            ["reportId"] = Guid.NewGuid().ToString("D"),
            ["timestamp"] = DateTimeOffset.FromUnixTimeMilliseconds(stopMs).ToString("O", CultureInfo.InvariantCulture),
            ["results"] = resultsObject,
        };

        if (first["generatedBy"] is JsonNode generatedBy)
        {
            merged["generatedBy"] = generatedBy.DeepClone();
        }

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (inputPaths is null)
        {
            throw new ArgumentNullException(nameof(inputPaths));
        }

        if (outputPath is null)
        {
            throw new ArgumentNullException(nameof(outputPath));
        }

        var reports = new List<string>(inputPaths.Count);
        foreach (string inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NETCOREAPP
            reports.Add(await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false));
#else
            reports.Add(File.ReadAllText(inputPath));
#endif
        }

        string merged = Merge(reports);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

#if NETCOREAPP
        await File.WriteAllTextAsync(outputPath, merged, cancellationToken).ConfigureAwait(false);
#else
        File.WriteAllText(outputPath, merged);
        await Task.CompletedTask.ConfigureAwait(false);
#endif
    }

    private static bool TryReadLong(JsonNode summary, string propertyName, out long value)
    {
        value = 0;
        if (summary[propertyName] is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue(out long longValue))
        {
            value = longValue;
            return true;
        }

        if (jsonValue.TryGetValue(out double doubleValue))
        {
            value = (long)doubleValue;
            return true;
        }

        return false;
    }
}
