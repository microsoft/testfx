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

internal static class HtmlReportMerger
{
    private const string GeneratorName = "Microsoft.Testing.Extensions.HtmlReport";

    // Fields copied verbatim from a folded (non-final) test row onto its "retryAttempts[]" entry, in addition to
    // the always-present "attempt"/"outcome"/"durationMs".
    private static readonly string[] RetryAttemptDetailFields =
    [
        "errorMessage",
        "exceptionType",
        "stackTrace",
        "standardOutput",
        "standardError",
        "retryAttemptNumber",
        "isSupersededRetryAttempt",
    ];

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

        (JsonArray mergedTests, int passed, int failed, int skipped, int timedOut, int errored, double totalDurationMs, int? flaky) =
            mode == HtmlMergeMode.CollapseRetryAttempts
                ? CollapseRetryAttempts(orderedTests)
                : ConcatenateTests(orderedTests);

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

        return HtmlReportEngine.RenderReport(merged.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
    }

    /// <summary>
    /// Default (non-retry) merge: every row is kept, numbered "#N of M" via "attemptIndex"/"attemptOf" whenever more
    /// than one row shares the same test identity (e.g. the same test UID appearing in more than one shard), so
    /// nothing is silently collapsed away.
    /// </summary>
    private static (JsonArray Tests, int Passed, int Failed, int Skipped, int TimedOut, int Errored, double TotalDurationMs, int? Flaky) ConcatenateTests(
        MergedTest[] orderedTests)
    {
        var countByIdentity = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string identity in orderedTests.Select(CreateTestIdentity))
        {
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

        for (int i = 0; i < orderedTests.Length; i++)
        {
            MergedTest mergedTest = orderedTests[i];
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
            test["sourceReportStartTime"] = mergedTest.SourceReportStartTime.ToString("O", CultureInfo.InvariantCulture);

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

        return (mergedTests, passed, failed, skipped, timedOut, errored, totalDurationMs, null);
    }

    /// <summary>
    /// RetryAttempts merge: folds successive attempts of the same logical test (rows sharing the same test
    /// identity) into a single row, using the LAST occurrence in the supplied execution order as the logical
    /// result; earlier occurrences are appended, oldest first, to that row's "retryAttempts[]" with their outcome,
    /// duration, and whichever error/output detail fields they carried. A test whose final outcome is "passed"
    /// after at least one non-passed prior attempt is additionally flagged "flaky" so it stays visibly
    /// distinguishable from a test that has always passed. Logical result counts use only the final occurrence,
    /// while total duration includes every physical attempt so retry cost remains visible.
    /// </summary>
    private static (JsonArray Tests, int Passed, int Failed, int Skipped, int TimedOut, int Errored, double TotalDurationMs, int? Flaky) CollapseRetryAttempts(
        MergedTest[] orderedTests)
    {
        HashSet<(int ReportIndex, string BaseIdentity)> ambiguousIdentities =
        [
            .. orderedTests
                .GroupBy(test => (
                    test.OriginalReportIndex,
                    BaseIdentity: CreateRetryBaseIdentity(test),
                    RetryAttempt: ReadOptionalInt(test.Test, "retryAttemptNumber")))
                .Where(static group => group.Count() > 1)
                .Select(static group => (group.Key.OriginalReportIndex, group.Key.BaseIdentity)),
        ];
        var slots = new List<(MergedTest Final, List<JsonObject> Priors)>();
        var slotByIdentity = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (MergedTest mergedTest in orderedTests)
        {
            string baseIdentity = CreateRetryBaseIdentity(mergedTest);
            string identity = ambiguousIdentities.Contains((mergedTest.OriginalReportIndex, baseIdentity))
                ? $"{baseIdentity}\0ambiguous\0{mergedTest.OriginalReportIndex.ToString(CultureInfo.InvariantCulture)}\0{mergedTest.OriginalTestIndex.ToString(CultureInfo.InvariantCulture)}"
                : baseIdentity;
            if (slotByIdentity.TryGetValue(identity, out int index))
            {
                (MergedTest previousFinal, List<JsonObject> priors) = slots[index];
                priors.Add(previousFinal.Test);
                slots[index] = (mergedTest, priors);
            }
            else
            {
                slotByIdentity.Add(identity, slots.Count);
                slots.Add((mergedTest, []));
            }
        }

        var mergedTests = new JsonArray();
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedOut = 0;
        int errored = 0;
        int flaky = 0;
        double totalDurationMs = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            (MergedTest final, List<JsonObject> priors) = slots[i];
            JsonObject test = final.Test;
            string outcome = ReadRequiredString(test, "outcome");
            double durationMs = ReadRequiredDouble(test, "durationMs");

            test["rowKey"] = i;
            _ = test.Remove("attemptIndex");
            _ = test.Remove("attemptOf");
            AddOptionalString(test, "testApplication", final.ProducingTestModule);
            AddOptionalString(test, "targetFramework", final.TargetFramework);
            AddOptionalString(test, "architecture", final.Architecture);
            AddOptionalString(test, "executionId", final.ExecutionId);
            test["sourceReportStartTime"] = final.SourceReportStartTime.ToString("O", CultureInfo.InvariantCulture);

            if (priors.Count > 0)
            {
                var history = new JsonArray();
                bool anyPriorNotPassed = false;
                for (int attemptNumber = 1; attemptNumber <= priors.Count; attemptNumber++)
                {
                    JsonObject priorTest = priors[attemptNumber - 1];
                    if (!string.Equals(ReadOptionalString(priorTest, "outcome"), "passed", StringComparison.Ordinal))
                    {
                        anyPriorNotPassed = true;
                    }

                    history.Add((JsonNode)BuildRetryAttempt(priorTest, attemptNumber));
                }

                test["retryAttempts"] = history;
                test["retries"] = priors.Count;

                if (string.Equals(outcome, "passed", StringComparison.Ordinal) && anyPriorNotPassed)
                {
                    test["flaky"] = true;
                    flaky++;
                }
            }

            CountOutcome(outcome, ref passed, ref failed, ref skipped, ref timedOut, ref errored);
            totalDurationMs += durationMs;
            foreach (JsonObject prior in priors)
            {
                totalDurationMs += ReadRequiredDouble(prior, "durationMs");
            }

            mergedTests.Add((JsonNode)test);
        }

        return (mergedTests, passed, failed, skipped, timedOut, errored, totalDurationMs, flaky);
    }

    /// <summary>
    /// Projects a folded (non-final) test row onto its "retryAttempts[]" entry: the attempt number assigned by the
    /// caller, its own outcome/duration, and whichever of <see cref="RetryAttemptDetailFields"/> it carries.
    /// </summary>
    private static JsonObject BuildRetryAttempt(JsonObject test, int attemptNumber)
    {
        var attempt = new JsonObject
        {
            ["attempt"] = attemptNumber,
            ["outcome"] = ReadRequiredString(test, "outcome"),
            ["durationMs"] = ReadRequiredDouble(test, "durationMs"),
        };

        foreach (string field in RetryAttemptDetailFields)
        {
            if (test[field] is JsonNode value)
            {
                attempt[field] = value.DeepClone();
            }
        }

        return attempt;
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
        => TryReadTimestamp(owner, propertyName, out DateTimeOffset timestamp)
                ? timestamp
                : throw new ArgumentException(ExtensionResources.HtmlReportInputIsNotValid, nameof(owner));

    private static bool TryReadTimestamp(JsonObject owner, string propertyName, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParse(
            ReadOptionalString(owner, propertyName),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);

    private static string CreateTestIdentity(MergedTest test)
        => string.Join(
            "\0",
            ReadRequiredString(test.Test, "uid"),
            test.ProducingTestModule ?? string.Empty,
            test.TargetFramework ?? string.Empty,
            test.Architecture ?? string.Empty);

    private static string CreateRetryBaseIdentity(MergedTest test)
        => string.Join(
            "\0",
            CreateTestIdentity(test),
            ReadRequiredString(test.Test, "displayName"));

    private static int? ReadOptionalInt(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out int result)
            ? result
            : null;

    private static bool TryGetInt(JsonObject owner, string propertyName, out int value)
    {
        if (owner[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue(out value))
        {
            return true;
        }

        value = default;
        return false;
    }

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
        string? ExecutionId,
        DateTimeOffset SourceReportStartTime,
        int OriginalReportIndex,
        int OriginalTestIndex);
}
