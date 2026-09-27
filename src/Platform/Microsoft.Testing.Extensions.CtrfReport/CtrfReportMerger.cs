// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.Testing.Extensions.CtrfReport;

/// <summary>
/// Merges several already-produced CTRF JSON reports into a single CTRF document.
/// </summary>
/// <remarks>
/// This is a pure, invocation-agnostic JSON-level merge (no I/O, no clock) that mirrors the
/// TRX and JUnit mergers, demonstrating that the same post-processing shape fits a JSON format:
/// <list type="bullet">
///   <item><description><c>results.tests[]</c> arrays are concatenated, except that elements which are not Test objects are dropped so one input's malformed row cannot invalidate the merged document; <see cref="CtrfMergeMode.CollapseRetryAttempts"/> additionally folds successive attempts of the same test into one row.</description></item>
///   <item><description><c>results.summary</c> counters are re-derived by counting the merged <c>tests[]</c> (so <c>summary.tests</c> always matches the array length); <c>start</c>/<c>stop</c> use the earliest/latest across inputs, <c>duration</c> is the resulting span.</description></item>
///   <item><description><c>reportFormat</c> and <c>specVersion</c> are taken from the first report; <c>reportId</c> is derived deterministically from the inputs AND the merge mode, so identical inputs reproduce the same id (RFC 018 idempotency) while the two modes — which produce materially different documents — get distinct ids.</description></item>
///   <item><description><c>runId</c> is carried over when every input agrees on one, because the merged document describes the same logical run as its inputs while remaining a distinct artifact with its own <c>reportId</c> (see ctrf-io/ctrf#58).</description></item>
///   <item><description><c>tool</c> keeps a concrete identity only when every input reported the exact same tool object; otherwise (inputs disagree or any input omits it) a neutral merger identity is used, so one framework is not attributed to another's tests.</description></item>
///   <item><description><c>environment</c> keeps the first report's shared fields, but module-specific values under <c>extra</c> (<c>testApplication</c>, <c>exitCode</c>) are dropped rather than presented as describing all merged modules.</description></item>
/// </list>
/// <para>
/// Validity contract. The merger guarantees the shape of what it SYNTHESIZES — the summary, the identity
/// fields, and the retry attempt objects it builds and renumbers — and PRESERVES verbatim what it merely
/// passes through. The only thing it drops is an element that cannot be a Test at all (a non-object), because
/// that alone would break the array's item type for every consumer of the merged file.
/// </para>
/// <para>
/// It deliberately does NOT validate or repair the Test rows themselves, even though an input may carry a
/// row with a missing or wrong-typed required field. Dropping such a row would lose a real result and
/// rewriting it would fabricate an outcome the producer never reported, which are both worse than relaying a
/// defect that belongs to the input document. A corollary is that merging a single document whose identities
/// are already unique leaves its <c>tests[]</c> untouched: the merger combines reports, it is not a CTRF
/// validator or linter.
/// </para>
/// </remarks>
internal static partial class CtrfReportMerger
{
    // Neutral tool identity used when merged inputs disagree on their producing test framework, so the
    // merged report does not misattribute one framework's identity to another's tests.
    private const string MergedToolName = "Microsoft.Testing.Extensions.CtrfReport (merged)";

    // Identity stamped into the merged document's 'generatedBy' — the merge is produced by this extension,
    // not by any input report.
    private const string GeneratedByName = "Microsoft.Testing.Extensions.CtrfReport";

    internal static string Merge(IReadOnlyList<string> inputReports)
        => Merge(inputReports, CtrfMergeMode.Concatenate);

    internal static string Merge(IReadOnlyList<string> inputReports, CtrfMergeMode mode)
        => Merge(inputReports, mode, requireAllReports: false);

    private static string Merge(IReadOnlyList<string> inputReports, CtrfMergeMode mode, bool requireAllReports)
        => Merge(inputReports, mode, requireAllReports, nameof(inputReports));

    private static string Merge(
        IReadOnlyList<string> inputReports,
        CtrfMergeMode mode,
        bool requireAllReports,
        string invalidInputParameterName)
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

        // Accumulate the raw JSON of every ACCEPTED CTRF input so the deterministic reportId is derived only
        // from the payloads that actually contributed to the merge. Hashing the unfiltered inputs would let a
        // rejected non-CTRF input (which is skipped below) change the merged report's identity.
        var acceptedReports = new List<string>(inputReports.Count);

        long? earliestStart = null;
        long? latestStop = null;

        // A same-kind merge can combine modules produced by different test frameworks. Track the
        // distinct *complete* tool identities (full serialized tool object, not just its name) so the
        // merged report is only stamped with a concrete framework when every input reported the exact
        // same tool; otherwise a neutral merger identity is used (see below). An input that omits 'tool'
        // counts as a distinct (missing) identity, so a mix of tagged/untagged inputs also degrades.
        var distinctToolIdentities = new HashSet<string>(StringComparer.Ordinal);
        JsonNode? firstTool = null;
        int reportCount = 0;

        // The merged document belongs to the same logical run as its inputs only when they all belong to the
        // same one, so a run id is carried over only when every input reported the very same value.
        var distinctRunIds = new HashSet<string>(StringComparer.Ordinal);

        // Collect each input's environment so shared fields can be retained and module- or agent-specific
        // ones (values that differ across inputs) dropped, rather than attributing the first report's
        // environment to every merged test.
        var environments = new List<JsonObject>();
        bool isIncomplete = false;

        foreach (string reportJson in inputReports)
        {
            if (JsonNode.Parse(reportJson) is not JsonObject root)
            {
                continue;
            }

            // Only accept genuine CTRF documents: reportFormat is the required format discriminator, so an
            // object without it (or with a non-CTRF value), or with no results object, must not become
            // 'first' and have CTRF-shaped data emitted under its label.
            if (root["results"] is not JsonObject)
            {
                continue;
            }

            string? format = ReadString(root, "reportFormat");
            if (!string.Equals(format, "CTRF", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // The permissive manual merger drops non-object test entries and tolerates a missing tests array.
            // Artifact post-processing cannot do that because its output must represent every supplied input,
            // so strict mode rejects precisely the shapes the merger could not carry through. Test objects
            // themselves remain pass-through data under the validity contract documented above.
            if (requireAllReports
                && (root["results"]?["tests"] is not JsonArray strictTests
                    || strictTests.Any(test => test is not JsonObject)))
            {
                throw new ArgumentException("Every input must be a valid CTRF report.", invalidInputParameterName);
            }

            first ??= root;
            reportCount++;
            acceptedReports.Add(reportJson);

            distinctRunIds.Add(ReadString(root, "runId") is { Length: > 0 } runIdText ? runIdText : string.Empty);

            if (root["results"]?["environment"] is JsonObject environment)
            {
                environments.Add(environment);
                isIncomplete |= environment["extra"] is JsonObject extra
                    && extra["incomplete"] is JsonValue incompleteValue
                    && incompleteValue.TryGetValue(out bool incomplete)
                    && incomplete;
            }

            JsonNode? results = root["results"];
            if (results?["tests"] is JsonArray testArray)
            {
                foreach (JsonNode? test in testArray)
                {
                    // Only a Test object belongs in tests[]: the CTRF schema types the array's items as objects
                    // with required members, so carrying a malformed element (a bare string, or a JSON null left
                    // by a lazy producer) through would turn a defect localized to one input into an invalid
                    // MERGED document — the artifact consumers actually read. This mirrors the check above, where
                    // an input that fails the CTRF shape test is rejected outright rather than passed along.
                    if (test is not JsonObject testObject)
                    {
                        continue;
                    }

                    mergedTests.Add(testObject.DeepClone());

                    // Fall back to per-test timing so a summary-less input (which the merger explicitly
                    // supports) still contributes to the merged min/max instead of being dropped or
                    // forcing the merged timestamp back to the Unix epoch.
                    if (TryReadLong(testObject, "start", out long testStart))
                    {
                        earliestStart = Min(earliestStart, testStart);
                    }

                    if (TryReadLong(testObject, "stop", out long testStop))
                    {
                        latestStop = Max(latestStop, testStop);
                    }
                }
            }

            if (results?["tool"] is JsonNode toolNode)
            {
                firstTool ??= toolNode;
                distinctToolIdentities.Add(toolNode.ToJsonString());
            }
            else
            {
                distinctToolIdentities.Add(string.Empty);
            }

            JsonNode? summary = results?["summary"];
            if (summary is not null)
            {
                if (TryReadLong(summary, "start", out long start))
                {
                    earliestStart = Min(earliestStart, start);
                }

                if (TryReadLong(summary, "stop", out long stop))
                {
                    latestStop = Max(latestStop, stop);
                }
            }
        }

        if (requireAllReports && reportCount != inputReports.Count)
        {
            throw new ArgumentException("Every input must be a valid CTRF report.", invalidInputParameterName);
        }

        if (first is null)
        {
            throw new ArgumentException("None of the provided inputs were valid CTRF reports.", nameof(inputReports));
        }

        long startMs = earliestStart ?? 0;
        long stopMs = latestStop ?? startMs;

        // In retry mode the inputs are successive attempts of the same suite, so the same logical test can
        // appear in several of them. Collapse those repeats into one row before counting, otherwise the same
        // test would be reported (and counted) several times.
        JsonArray tests = mode == CtrfMergeMode.CollapseRetryAttempts
            ? CollapseRetryAttempts(mergedTests)
            : mergedTests;

        // Counters are derived from the merged tests[] rather than trusting each input's summary, so
        // summary.tests always equals the array length even when an input omitted or under-reported
        // its summary. Every element is a Test object: non-objects were rejected during ingestion.
        long passed = 0, failed = 0, skipped = 0, pending = 0, other = 0, flaky = 0;
        foreach (JsonNode? test in tests)
        {
            if (test is not JsonObject testObject)
            {
                continue;
            }

            switch (ReadString(testObject, "status"))
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }

            if (testObject["flaky"] is JsonValue flakyValue && flakyValue.TryGetValue(out bool isFlaky) && isFlaky)
            {
                flaky++;
            }
        }

        var summaryObject = new JsonObject
        {
            ["tests"] = tests.Count,
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

        // Only carry a concrete tool identity when every input reported the exact same one (the common
        // single-framework case). When inputs disagree — different tool objects, or a mix of tagged and
        // untagged inputs — stamping the first framework onto all tests would misattribute the others,
        // so use a neutral merger identity instead.
        bool allInputsShareTool = distinctToolIdentities.Count == 1 && firstTool is not null && reportCount > 0;
        resultsObject["tool"] = allInputsShareTool
            ? firstTool!.DeepClone()
            : new JsonObject { ["name"] = MergedToolName };

        resultsObject["summary"] = summaryObject;

        // Retain only environment fields that every input agrees on: OS/user/machine are shared when the
        // merge is same-machine, but invocation-agnostic inputs can come from different CI agents, so a
        // differing value would misstate the environment for most tests. A report that supplies no
        // environment at all counts as disagreement (its fields are absent), so a common field requires
        // every accepted report to provide it. Module-specific 'extra' values (the producing test
        // application and its exit code) are always dropped.
        JsonObject? commonEnvironment = BuildCommonEnvironment(environments, reportCount);
        if (isIncomplete)
        {
            commonEnvironment ??= [];
            JsonObject extra = commonEnvironment["extra"] as JsonObject ?? [];
            extra["incomplete"] = true;
            extra["runStatus"] = "aborted";
            commonEnvironment["extra"] = extra;
        }

        if (commonEnvironment is not null)
        {
            resultsObject["environment"] = commonEnvironment;
        }

        resultsObject["tests"] = tests;

        var merged = new JsonObject
        {
            ["reportFormat"] = first["reportFormat"]?.DeepClone() ?? "CTRF",
            ["specVersion"] = first["specVersion"]?.DeepClone() ?? "0.0.0",
            ["reportId"] = CreateDeterministicReportId(acceptedReports, mode),
        };

        // A merged document is a new artifact (hence its own reportId) but it still describes the same logical
        // run as the documents it was built from, so it carries their runId — provided they all agree on one.
        // Inputs that disagree, or an input with no runId, contribute the empty sentinel and suppress the field
        // rather than picking an arbitrary run to represent the whole merge.
        if (distinctRunIds.Count == 1 && distinctRunIds.First() is { Length: > 0 } sharedRunId)
        {
            merged["runId"] = sharedRunId;
        }

        merged["timestamp"] = DateTimeOffset.FromUnixTimeMilliseconds(stopMs).ToString("O", CultureInfo.InvariantCulture);

        // The merged document is produced by this merger, not by any input, so stamp its own identity
        // rather than carrying the first input's 'generatedBy' (which could report a different producer
        // or version when merging reports from different tool versions).
        merged["generatedBy"] = GeneratedByName;
        merged["results"] = resultsObject;

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
