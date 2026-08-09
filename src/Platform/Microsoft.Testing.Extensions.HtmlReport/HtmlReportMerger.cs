// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal static class HtmlReportMerger
{
    private const string GeneratorName = "Microsoft.Testing.Extensions.HtmlReport";

    internal static string Merge(IReadOnlyList<string> inputReports)
    {
        IReadOnlyList<string> reports = inputReports ?? throw new ArgumentNullException(nameof(inputReports));

        return Merge([.. reports.Select(report => new HtmlReportMergeInput(report, null, null, null, null))]);
    }

    private static string Merge(IReadOnlyList<HtmlReportMergeInput> inputs)
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

        foreach (ParsedHtmlReport parsedReport in parsedReports.OrderBy(report => report.StartTime).ThenBy(report => report.OriginalIndex))
        {
            HtmlReportMergeInput input = parsedReport.Input;
            JsonObject report = parsedReport.Report;
            foreach (JsonNode? test in (JsonArray)report["tests"]!)
            {
                var testObject = (JsonObject)test!.DeepClone();
                tests.Add(new MergedTest(
                    testObject,
                    ReadOptionalString(testObject, "testApplication")
                        ?? input.ProducingTestModule
                        ?? ReadOptionalString(report, "testApplication"),
                    ReadOptionalString(testObject, "targetFramework") ?? input.TargetFramework,
                    ReadOptionalString(testObject, "architecture") ?? input.Architecture,
                    ReadOptionalString(testObject, "executionId") ?? input.ExecutionId));
            }
        }

        IReadOnlyList<JsonObject> reports = [.. parsedReports.Select(report => report.Report)];
        var countByIdentity = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (MergedTest mergedTest in tests)
        {
            string identity = CreateTestIdentity(mergedTest);
            countByIdentity[identity] = countByIdentity.TryGetValue(identity, out int existing) ? existing + 1 : 1;
        }

        var emittedByIdentity = new Dictionary<string, int>(StringComparer.Ordinal);
        var mergedTests = new JsonArray();
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedOut = 0;
        int errored = 0;
        double totalDurationMs = 0;

        for (int i = 0; i < tests.Count; i++)
        {
            MergedTest mergedTest = tests[i];
            JsonObject test = mergedTest.Test;
            string identity = CreateTestIdentity(mergedTest);
            string outcome = ReadRequiredString(test, "outcome");
            double durationMs = ReadRequiredDouble(test, "durationMs");

            test["rowKey"] = i;
            _ = test.Remove("attemptIndex");
            _ = test.Remove("attemptOf");
            AddOptionalString(test, "testApplication", mergedTest.ProducingTestModule);
            AddOptionalString(test, "targetFramework", mergedTest.TargetFramework);
            AddOptionalString(test, "architecture", mergedTest.Architecture);
            AddOptionalString(test, "executionId", mergedTest.ExecutionId);

            int attemptOf = countByIdentity[identity];
            if (attemptOf > 1)
            {
                int attemptIndex = emittedByIdentity.TryGetValue(identity, out int emitted) ? emitted + 1 : 1;
                emittedByIdentity[identity] = attemptIndex;
                test["attemptIndex"] = attemptIndex;
                test["attemptOf"] = attemptOf;
            }

            CountOutcome(outcome, ref passed, ref failed, ref skipped, ref timedOut, ref errored);
            totalDurationMs += durationMs;
            mergedTests.Add((JsonNode)test);
        }

        bool hasCommonFramework = TryGetCommonFramework(reports, out string framework, out string frameworkUid, out string frameworkVersion);
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
            ["summary"] = new JsonObject
            {
                ["total"] = tests.Count,
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["timedOut"] = timedOut,
                ["errored"] = errored,
                ["totalDurationMs"] = totalDurationMs,
            },
        };

        if (TryGetCommonInt(reports, "exitCode", out int exitCode))
        {
            merged["exitCode"] = exitCode;
        }

        return HtmlReportEngine.RenderReport(merged.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }

    internal static async Task MergeToFileAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputPath,
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

        byte[] mergedBytes = Encoding.UTF8.GetBytes(Merge(reports));
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

    private static JsonObject ParseReport(string html)
    {
        JsonObject report;
        try
        {
            report = JsonNode.Parse(HtmlReportEngine.ExtractReportJson(html)) as JsonObject
                ?? throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html));
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html), ex);
        }

        bool isValidReport =
            string.Equals(ReadOptionalString(report, "schemaVersion"), "1", StringComparison.Ordinal)
            && string.Equals(ReadOptionalString(report, "generator"), GeneratorName, StringComparison.Ordinal)
            && report["tests"] is JsonArray tests
            && tests.All(test => test is JsonObject);

        return isValidReport
            ? report
            : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(html));
    }

    private static string ReadRequiredString(JsonObject owner, string propertyName)
        => ReadOptionalString(owner, propertyName)
            ?? throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static string? ReadOptionalString(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static double ReadRequiredDouble(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out double number)
            ? number
            : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static DateTimeOffset ReadRequiredTimestamp(JsonObject owner, string propertyName)
        => DateTimeOffset.TryParse(
            ReadOptionalString(owner, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset timestamp)
                ? timestamp
                : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static string CreateTestIdentity(MergedTest test)
        => string.Join(
            "\0",
            ReadRequiredString(test.Test, "uid"),
            test.ProducingTestModule ?? string.Empty,
            test.TargetFramework ?? string.Empty,
            test.Architecture ?? string.Empty);

    private static void AddOptionalString(JsonObject owner, string propertyName, string? value)
    {
        if (value is not null)
        {
            owner[propertyName] = value;
        }
    }

    private static string? GetCommonString(IReadOnlyList<JsonObject> reports, string propertyName)
    {
        string? common = ReadOptionalString(reports[0], propertyName);
        for (int i = 1; i < reports.Count; i++)
        {
            if (!string.Equals(common, ReadOptionalString(reports[i], propertyName), StringComparison.Ordinal))
            {
                return null;
            }
        }

        return common;
    }

    private static bool TryGetCommonFramework(
        IReadOnlyList<JsonObject> reports,
        out string framework,
        out string frameworkUid,
        out string frameworkVersion)
    {
        framework = GetCommonString(reports, "framework") ?? string.Empty;
        frameworkUid = GetCommonString(reports, "frameworkUid") ?? string.Empty;
        frameworkVersion = GetCommonString(reports, "frameworkVersion") ?? string.Empty;
        if (framework.Length == 0 || frameworkUid.Length == 0 || frameworkVersion.Length == 0)
        {
            return false;
        }

        foreach (JsonObject report in reports)
        {
            if (!string.Equals(ReadOptionalString(report, "framework"), framework, StringComparison.Ordinal)
                || !string.Equals(ReadOptionalString(report, "frameworkUid"), frameworkUid, StringComparison.Ordinal)
                || !string.Equals(ReadOptionalString(report, "frameworkVersion"), frameworkVersion, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetCommonInt(IReadOnlyList<JsonObject> reports, string propertyName, out int common)
    {
        if (reports[0][propertyName] is not JsonValue first || !first.TryGetValue(out common))
        {
            common = default;
            return false;
        }

        for (int i = 1; i < reports.Count; i++)
        {
            if (reports[i][propertyName] is not JsonValue value
                || !value.TryGetValue(out int candidate)
                || candidate != common)
            {
                common = default;
                return false;
            }
        }

        return true;
    }

    private static void CountOutcome(string outcome, ref int passed, ref int failed, ref int skipped, ref int timedOut, ref int errored)
    {
        switch (outcome)
        {
            case "passed": passed++; break;
            case "failed": failed++; break;
            case "skipped": skipped++; break;
            case "timedOut": timedOut++; break;
            case "errored": errored++; break;
            default: throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(outcome));
        }
    }

    private sealed record HtmlReportMergeInput(
        string Html,
        string? ProducingTestModule,
        string? TargetFramework,
        string? Architecture,
        string? ExecutionId);

    private sealed record ParsedHtmlReport(
        HtmlReportMergeInput Input,
        JsonObject Report,
        DateTimeOffset StartTime,
        int OriginalIndex);

    private sealed record MergedTest(
        JsonObject Test,
        string? ProducingTestModule,
        string? TargetFramework,
        string? Architecture,
        string? ExecutionId);
}
