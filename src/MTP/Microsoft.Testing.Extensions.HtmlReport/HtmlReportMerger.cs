// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.HtmlReport;

/// <summary>
/// Controls how <see cref="HtmlReportMerger"/> combines the "tests[]" arrays of its inputs.
/// </summary>
internal enum HtmlMergeMode
{
    /// <summary>
    /// Concatenates the inputs, which is correct when they describe disjoint sets of tests (the shard or
    /// per-module case). This is the default: MTP test UIDs are only unique WITHIN an assembly, so collapsing
    /// by identity across modules would fuse same-named tests from different assemblies.
    /// </summary>
    Concatenate,

    /// <summary>
    /// Folds rows describing the same logical test into one, which is correct when the inputs are successive
    /// attempts of the same test module (--retry-failed-tests): the LAST occurrence in the supplied execution
    /// order wins and earlier occurrences become its "retryAttempts[]". Inputs MUST be supplied in attempt order
    /// (as RetryArtifactProcessor already does) rather than re-sorted by embedded timestamp, and MUST come from
    /// the same module for identities to be comparable.
    /// </summary>
    CollapseRetryAttempts,
}

internal static partial class HtmlReportMerger
{
    private const string GeneratorName = "Microsoft.Testing.Extensions.HtmlReport";

    internal static string Merge(IReadOnlyList<string> inputReports)
        => Merge(inputReports, HtmlMergeMode.Concatenate);

    internal static string Merge(IReadOnlyList<string> inputReports, HtmlMergeMode mode)
    {
        IReadOnlyList<string> reports = inputReports ?? throw new ArgumentNullException(nameof(inputReports));

        return Merge([.. reports.Select(report => new HtmlReportMergeInput(report, null, null, null, null))], mode);
    }

    private static string Merge(IReadOnlyList<HtmlReportMergeInput> inputs, HtmlMergeMode mode)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        if (inputs.Count == 0)
        {
            throw new ArgumentException(ExtensionResources.HtmlReportsRequired, nameof(inputs));
        }

        var parsedReports = new List<ParsedHtmlReport>(inputs.Count);
        var tests = new List<MergedTest>();
        DateTimeOffset? earliestStartTime = null;
        DateTimeOffset? latestEndTime = null;

        for (int i = 0; i < inputs.Count; i++)
        {
            HtmlReportMergeInput input = inputs[i];
            JsonObject report = ParseReport(input.Html);
            DateTimeOffset startTime = ReadRequiredTimestamp(report, "startTime");
            DateTimeOffset endTime = ReadRequiredTimestamp(report, "endTime");
            parsedReports.Add(new ParsedHtmlReport(input, report, startTime, i));
            earliestStartTime = earliestStartTime is null || startTime < earliestStartTime ? startTime : earliestStartTime;
            latestEndTime = latestEndTime is null || endTime > latestEndTime ? endTime : latestEndTime;
        }

        foreach (ParsedHtmlReport parsedReport in parsedReports)
        {
            HtmlReportMergeInput input = parsedReport.Input;
            JsonObject report = parsedReport.Report;
            var reportTests = (JsonArray)report["tests"]!;
            for (int i = 0; i < reportTests.Count; i++)
            {
                JsonNode? test = reportTests[i];
                var testObject = (JsonObject)test!.DeepClone();
                tests.Add(new MergedTest(
                    testObject,
                    ReadOptionalString(testObject, "testApplication")
                        ?? input.ProducingTestModule
                        ?? ReadOptionalString(report, "testApplication"),
                    ReadOptionalString(testObject, "targetFramework") ?? input.TargetFramework,
                    ReadOptionalString(testObject, "architecture") ?? input.Architecture,
                    ReadOptionalString(testObject, "executionId") ?? input.ExecutionId,
                    TryReadTimestamp(testObject, "sourceReportStartTime", out DateTimeOffset sourceReportStartTime)
                        ? sourceReportStartTime
                        : parsedReport.StartTime,
                    parsedReport.OriginalIndex,
                    i));
            }
        }

        IReadOnlyList<JsonObject> reports = [.. parsedReports.Select(report => report.Report)];

        // TestModules concatenation sorts by embedded start time for deterministic chronological display across
        // independently-timestamped shards. RetryAttempts collapsing must NOT do this: RetryArtifactProcessor
        // already supplies inputs in true execution/attempt order, and an attempt's own embedded start/end time is
        // not a reliable substitute for that order (clock skew, retried attempts sharing a machine, ...).
        MergedTest[] orderedTests = mode == HtmlMergeMode.CollapseRetryAttempts
            ? [.. tests]
            :
            [
                .. tests
                    .OrderBy(test => test.SourceReportStartTime)
                    .ThenBy(test => test.OriginalReportIndex)
                    .ThenBy(test => test.OriginalTestIndex),
            ];

        (JsonArray mergedTests, int passed, int failed, int skipped, int timedOut, int errored, int? flaky) =
            mode == HtmlMergeMode.CollapseRetryAttempts
                ? CollapseRetryAttempts(orderedTests)
                : ConcatenateTests(orderedTests);
        double totalDurationMs = (latestEndTime!.Value - earliestStartTime!.Value).TotalMilliseconds;

        bool hasCommonFramework = TryGetCommonFramework(reports, out string framework, out string frameworkUid, out string frameworkVersion);
        var summary = new JsonObject
        {
            ["total"] = mergedTests.Count,
            ["passed"] = passed,
            ["failed"] = failed,
            ["skipped"] = skipped,
            ["timedOut"] = timedOut,
            ["errored"] = errored,
            ["totalDurationMs"] = totalDurationMs,
        };
        if (flaky is int flakyCount)
        {
            summary["flaky"] = flakyCount;
        }

        var merged = new JsonObject
        {
            ["schemaVersion"] = "1",
            ["generator"] = GeneratorName,
            ["generatorVersion"] = ExtensionVersion.DefaultSemVer,
            ["testApplication"] = GetCommonString(reports, "testApplication") ?? ExtensionResources.HtmlMergedReportName,
            ["machineName"] = GetCommonString(reports, "machineName") ?? string.Empty,
            ["userName"] = GetCommonString(reports, "userName") ?? string.Empty,
            ["framework"] = hasCommonFramework ? framework : string.Empty,
            ["frameworkUid"] = hasCommonFramework ? frameworkUid : string.Empty,
            ["frameworkVersion"] = hasCommonFramework ? frameworkVersion : string.Empty,
            ["startTime"] = earliestStartTime!.Value.ToString("O", CultureInfo.InvariantCulture),
            ["endTime"] = latestEndTime!.Value.ToString("O", CultureInfo.InvariantCulture),
            ["tests"] = mergedTests,
            ["summary"] = summary,
        };

        bool hasExitCode = mode == HtmlMergeMode.CollapseRetryAttempts
            ? TryGetInt(reports[^1], "exitCode", out int exitCode)
            : TryGetCommonInt(reports, "exitCode", out exitCode);
        if (hasExitCode)
        {
            merged["exitCode"] = exitCode;
        }

        if (reports.Any(IsIncomplete))
        {
            merged["incomplete"] = true;
            merged["runStatus"] = "aborted";
        }

        return HtmlReportEngine.RenderReport(merged.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }

    internal static async Task MergeToFileAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputPath,
        HtmlMergeMode mode,
        CancellationToken cancellationToken)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        if (outputPath is null)
        {
            throw new ArgumentNullException(nameof(outputPath));
        }

        if (inputs.Count == 0)
        {
            throw new ArgumentException(ExtensionResources.HtmlReportsRequired, nameof(inputs));
        }

        string[] inputPaths = [.. inputs.Select(input => input.Path)];
        MergeOutputFileHelper.EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

        var reports = new List<HtmlReportMergeInput>(inputs.Count);
        foreach (InputArtifact input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
#if NETCOREAPP
            string html = await File.ReadAllTextAsync(input.Path, cancellationToken).ConfigureAwait(false);
#else
            string html = File.ReadAllText(input.Path);
#endif
            reports.Add(new HtmlReportMergeInput(
                html,
                input.ProducingTestModule,
                input.TargetFramework,
                input.Architecture,
                input.ExecutionId));
        }

        byte[] mergedBytes = Encoding.UTF8.GetBytes(Merge(reports, mode));
        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (outputDirectory is { Length: > 0 })
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await MergeOutputFileHelper.WriteViaTemporarySiblingAsync(outputPath, async tempPath =>
        {
#if NETCOREAPP
            await File.WriteAllBytesAsync(tempPath, mergedBytes, cancellationToken).ConfigureAwait(false);
#else
            File.WriteAllBytes(tempPath, mergedBytes);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }).ConfigureAwait(false);
    }
}
