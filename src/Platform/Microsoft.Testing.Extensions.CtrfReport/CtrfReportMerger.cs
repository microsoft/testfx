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
///   <item><description><c>results.tests[]</c> arrays are concatenated as-is, unless <see cref="CtrfMergeMode.CollapseRetryAttempts"/> asks for successive attempts of the same test to be folded into one row.</description></item>
///   <item><description><c>results.summary</c> counters are re-derived by counting the merged <c>tests[]</c> (so <c>summary.tests</c> always matches the array length); <c>start</c>/<c>stop</c> use the earliest/latest across inputs, <c>duration</c> is the resulting span.</description></item>
///   <item><description><c>reportFormat</c> and <c>specVersion</c> are taken from the first report; <c>reportId</c> is derived deterministically from the inputs, so identical inputs reproduce the same id (RFC 018 idempotency).</description></item>
///   <item><description><c>runId</c> is carried over when every input agrees on one, because the merged document describes the same logical run as its inputs while remaining a distinct artifact with its own <c>reportId</c> (see ctrf-io/ctrf#58).</description></item>
///   <item><description><c>tool</c> keeps a concrete identity only when every input reported the exact same tool object; otherwise (inputs disagree or any input omits it) a neutral merger identity is used, so one framework is not attributed to another's tests.</description></item>
///   <item><description><c>environment</c> keeps the first report's shared fields, but module-specific values under <c>extra</c> (<c>testApplication</c>, <c>exitCode</c>) are dropped rather than presented as describing all merged modules.</description></item>
/// </list>
/// </remarks>
internal static class CtrfReportMerger
{
    // Neutral tool identity used when merged inputs disagree on their producing test framework, so the
    // merged report does not misattribute one framework's identity to another's tests.
    private const string MergedToolName = "Microsoft.Testing.Extensions.CtrfReport (merged)";

    // Identity stamped into the merged document's 'generatedBy' — the merge is produced by this extension,
    // not by any input report.
    private const string GeneratedByName = "Microsoft.Testing.Extensions.CtrfReport";

    // Fields a CTRF retry attempt object (section 11) shares with a test object and that carry over verbatim when
    // a non-final attempt is folded into 'retryAttempts[]'. 'attempt' and 'status' are handled separately because
    // they are required, and 'attemptId' is listed here because an input that assigned one keeps it.
    private static readonly string[] RetryAttemptFields =
    [
        "attemptId",
        "duration",
        "message",
        "trace",
        "line",
        "snippet",
        "stdout",
        "stderr",
        "start",
        "stop",
        "attachments",
    ];

    internal static string Merge(IReadOnlyList<string> inputReports)
        => Merge(inputReports, CtrfMergeMode.Concatenate);

    internal static string Merge(IReadOnlyList<string> inputReports, CtrfMergeMode mode)
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

            first ??= root;
            reportCount++;
            acceptedReports.Add(reportJson);

            distinctRunIds.Add(ReadString(root, "runId") is { Length: > 0 } runIdText ? runIdText : string.Empty);

            if (root["results"]?["environment"] is JsonObject environment)
            {
                environments.Add(environment);
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
        if (BuildCommonEnvironment(environments, reportCount) is JsonObject commonEnvironment)
        {
            resultsObject["environment"] = commonEnvironment;
        }

        resultsObject["tests"] = tests;

        var merged = new JsonObject
        {
            ["reportFormat"] = first["reportFormat"]?.DeepClone() ?? "CTRF",
            ["specVersion"] = first["specVersion"]?.DeepClone() ?? "0.0.0",
            ["reportId"] = CreateDeterministicReportId(acceptedReports),
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

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken)
        => await MergeToFileAsync(inputPaths, outputPath, CtrfMergeMode.Concatenate, cancellationToken).ConfigureAwait(false);

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CtrfMergeMode mode,
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

        // Reject an empty input list before any filesystem work (Merge throws for empty input, but only
        // after the output directory would already have been created).
        if (inputPaths.Count == 0)
        {
            throw new ArgumentException("At least one CTRF report is required to merge.", nameof(inputPaths));
        }

        // RFC 018 treats per-module inputs as read-only and requires them to remain on disk; reject an
        // output that aliases an input so a merge can never overwrite one of its own sources.
        MergeOutputFileHelper.EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

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

        string merged = Merge(reports, mode);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Write to a temporary sibling, then replace the destination ENTRY, so a symlink/hardlink output
        // alias of an input has only its link removed rather than the read-only source truncated in place.
        await MergeOutputFileHelper.WriteViaTemporarySiblingAsync(outputPath, async tempPath =>
        {
#if NETCOREAPP
            await File.WriteAllTextAsync(tempPath, merged, cancellationToken).ConfigureAwait(false);
#else
            File.WriteAllText(tempPath, merged);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Folds successive attempts of the same logical test — the tests[] rows contributed, in attempt order, by
    /// the per-attempt documents of one orchestrated retry run — into a single row per test.
    /// </summary>
    /// <remarks>
    /// The retry model confirmed in ctrf-io/ctrf#58: the last attempt's outcome IS the test object, and
    /// <c>retryAttempts[]</c> holds attempts <c>1..N-1</c> (the initial execution plus any earlier retries),
    /// numbered from 1, so <c>retries == retryAttempts.length</c> and the final attempt is <c>retries + 1</c>.
    /// The test object therefore keeps the FINAL attempt's <c>duration</c> (and <c>start</c>/<c>stop</c>), rather
    /// than a sum across attempts. Attempts an input already recorded in its own <c>retryAttempts[]</c> (in-process
    /// retries within a single attempt process) are flattened into the same history so no execution is lost.
    /// </remarks>
    private static JsonArray CollapseRetryAttempts(JsonArray tests)
    {
        var slots = new List<(JsonObject Final, List<JsonObject> Priors)>();
        var byIdentity = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (JsonNode? test in tests)
        {
            // Non-objects were already rejected during ingestion; this only re-establishes the type.
            if (test is not JsonObject testObject)
            {
                continue;
            }

            // A row we cannot identify gets its own slot: fusing unrelated rows would lose results, whereas an
            // uncollapsed duplicate is merely redundant.
            if (GetTestIdentity(testObject) is not string identity)
            {
                slots.Add((testObject, []));
                continue;
            }

            if (byIdentity.TryGetValue(identity, out int index))
            {
                (JsonObject previousFinal, List<JsonObject> priors) = slots[index];
                priors.Add(previousFinal);
                slots[index] = (testObject, priors);
            }
            else
            {
                byIdentity.Add(identity, slots.Count);
                slots.Add((testObject, []));
            }
        }

        var collapsed = new JsonArray();
        foreach ((JsonObject final, List<JsonObject> priors) in slots)
        {
            collapsed.Add(BuildCollapsedTest(final, priors));
        }

        return collapsed;
    }

    /// <summary>
    /// Computes the key identifying the logical test a row describes, preferring the CTRF <c>testId</c>, then the
    /// producer-supplied <c>extra.uid</c>, and finally the suite path plus name. Returns <see langword="null"/>
    /// when the row carries none of them.
    /// </summary>
    private static string? GetTestIdentity(JsonObject test)
    {
        if (ReadString(test, "testId") is { Length: > 0 } testId)
        {
            return $"testId\u001f{testId}";
        }

        // `extra` is free-form, so a foreign producer may well have written a string or an array there; indexing
        // anything but an object by property name throws.
        if (test["extra"] is JsonObject extra && ReadString(extra, "uid") is { Length: > 0 } uid)
        {
            return $"uid\u001f{uid}";
        }

        if (ReadString(test, "name") is not { Length: > 0 } name)
        {
            return null;
        }

        var identity = new StringBuilder("name");
        if (test["suite"] is JsonArray suite)
        {
            foreach (JsonNode? segment in suite)
            {
                // A suite segment is normally a string, but the document is untrusted. Fall back to the
                // segment's JSON text so a non-string segment still contributes a distinct, deterministic part
                // of the key instead of throwing on the string conversion.
                identity.Append('\u001f').Append(
                    segment is JsonValue segmentValue && segmentValue.TryGetValue(out string? segmentText)
                        ? segmentText
                        : segment?.ToJsonString());
            }
        }

        return identity.Append('\u001f').Append(name).ToString();
    }

    private static JsonNode BuildCollapsedTest(JsonObject final, List<JsonObject> priors)
    {
        var collapsed = (JsonObject)final.DeepClone();
        if (priors.Count == 0)
        {
            return collapsed;
        }

        var history = new JsonArray();
        foreach (JsonObject prior in priors)
        {
            AppendAttempts(history, prior);
        }

        // In-process retries observed by the final attempt precede its own outcome in the history.
        if (collapsed["retryAttempts"] is JsonArray finalAttempts)
        {
            AppendNestedAttempts(history, finalAttempts);
        }

        bool anyFailed = false;
        for (int i = 0; i < history.Count; i++)
        {
            if (history[i] is not JsonObject attempt)
            {
                continue;
            }

            attempt["attempt"] = i + 1;
            anyFailed |= ReadString(attempt, "status") == "failed";
        }

        collapsed["retryAttempts"] = history;
        collapsed["retries"] = history.Count;

        // CTRF 9.22: flaky only when the FINAL status is passed after at least one failed attempt. Recomputed
        // (rather than inherited) because an input's own flag only describes the attempt that produced it.
        if (ReadString(collapsed, "status") == "passed" && anyFailed)
        {
            collapsed["flaky"] = true;
        }
        else
        {
            collapsed.Remove("flaky");
        }

        return collapsed;
    }

    /// <summary>
    /// Appends the executions a non-final attempt row represents — the attempts it already nested, then its own
    /// outcome — to the retry history being built.
    /// </summary>
    private static void AppendAttempts(JsonArray history, JsonObject test)
    {
        if (test["retryAttempts"] is JsonArray nested)
        {
            AppendNestedAttempts(history, nested);
        }

        history.Add(ToRetryAttempt(test));
    }

    /// <summary>
    /// Copies retry attempt objects an input already recorded into the history being built, skipping entries that
    /// are not objects: a <see langword="null"/> element could not describe an execution and is not a valid retry
    /// attempt object.
    /// </summary>
    private static void AppendNestedAttempts(JsonArray history, JsonArray nested)
    {
        foreach (JsonNode? attempt in nested)
        {
            if (attempt is JsonObject attemptObject)
            {
                history.Add(attemptObject.DeepClone());
            }
        }
    }

    /// <summary>
    /// Projects a test row onto the retry attempt object of CTRF section 11, which allows a narrower set of
    /// fields than a test: everything else (name, suite, tags, labels, ...) either belongs to the collapsed test
    /// object or, like <c>rawStatus</c>, moves under <c>extra</c>, the only permitted extension point.
    /// </summary>
    private static JsonNode ToRetryAttempt(JsonObject test)
    {
        // 'attempt' is assigned by the caller, once the position of this execution in the history is known.
        var attempt = new JsonObject
        {
            ["attempt"] = 1,
            ["status"] = test["status"]?.DeepClone() ?? "other",
        };

        foreach (string field in RetryAttemptFields)
        {
            if (test[field] is JsonNode value)
            {
                attempt[field] = value.DeepClone();
            }
        }

        JsonObject? extra = test["extra"] is JsonObject testExtra ? (JsonObject)testExtra.DeepClone() : null;
        if (test["rawStatus"] is JsonNode rawStatus)
        {
            extra ??= [];
            extra["rawStatus"] = rawStatus.DeepClone();
        }

        if (extra is { Count: > 0 })
        {
            attempt["extra"] = extra;
        }

        return attempt;
    }

    /// <summary>
    /// Reads a string property, treating a value of any other JSON type as absent. The explicit
    /// <c>(string?)</c> conversion on a <see cref="JsonNode"/> THROWS for a non-string value rather than
    /// yielding <see langword="null"/>, and every document reaching the merger is untrusted.
    /// </summary>
    private static string? ReadString(JsonObject owner, string propertyName)
        => owner[propertyName] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>
    /// Reads an integral property from a JSON node, tolerating anything the node may actually be. The node comes
    /// straight out of an untrusted document — <c>results.summary</c> is not guaranteed to be an object — so a
    /// non-object must read as "absent" rather than throw: indexing a <see cref="JsonValue"/> by property name is
    /// an <see cref="InvalidOperationException"/>.
    /// </summary>
    private static bool TryReadLong(JsonNode node, string propertyName, out long value)
    {
        value = 0;
        if (node is not JsonObject jsonObject || jsonObject[propertyName] is not JsonValue jsonValue)
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

    /// <summary>
    /// Builds a merged environment containing only the fields that every input's environment agrees on
    /// (so a value that differs across CI agents is dropped rather than attributed to all tests). The
    /// module-specific <c>extra.testApplication</c> and <c>extra.exitCode</c> fields are always dropped.
    /// Returns <see langword="null"/> when no environment survives.
    /// </summary>
    private static JsonObject? BuildCommonEnvironment(IReadOnlyList<JsonObject> environments, int reportCount)
    {
        // A field can only be common to all inputs if every accepted report supplied an environment; a
        // report with no environment is a disagreement (the field is absent there).
        if (environments.Count == 0 || environments.Count != reportCount)
        {
            return null;
        }

        var merged = new JsonObject();
        foreach (KeyValuePair<string, JsonNode?> field in environments[0])
        {
            if (field.Key == "extra")
            {
                continue;
            }

            string firstValue = field.Value?.ToJsonString() ?? "null";
            if (environments.All(e => (e[field.Key]?.ToJsonString() ?? "null") == firstValue))
            {
                merged[field.Key] = field.Value?.DeepClone();
            }
        }

        if (environments[0]["extra"] is JsonObject firstExtra)
        {
            var extra = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> field in firstExtra)
            {
                if (field.Key is "testApplication" or "exitCode")
                {
                    continue;
                }

                string firstValue = field.Value?.ToJsonString() ?? "null";
                if (environments.All(e => e["extra"] is JsonObject extraObject && (extraObject[field.Key]?.ToJsonString() ?? "null") == firstValue))
                {
                    extra[field.Key] = field.Value?.DeepClone();
                }
            }

            if (extra.Count > 0)
            {
                merged["extra"] = extra;
            }
        }

        return merged.Count > 0 ? merged : null;
    }

    /// <summary>
    /// Derives a stable <c>reportId</c> from the accepted CTRF input reports so identical inputs reproduce
    /// the same id on every retry (RFC 018 idempotency) without a random source or reusing an input report's
    /// id. Only the payloads that passed CTRF validation are hashed, so a rejected non-CTRF input cannot
    /// alter the merged report's identity. A non-cryptographic 128-bit FNV-1a fill is sufficient here — the
    /// id only needs to be deterministic and collision-resistant enough to identify a merged report, not
    /// secret.
    /// </summary>
    private static string CreateDeterministicReportId(IReadOnlyList<string> acceptedReports)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong hashLow = 14695981039346656037UL;
        ulong hashHigh = 0x9E3779B97F4A7C15UL;

        foreach (string report in acceptedReports)
        {
            foreach (char c in report)
            {
                hashLow = (hashLow ^ c) * fnvPrime;
                hashHigh = (hashHigh ^ c) * fnvPrime;
            }

            // Fold in each input's length so different chunk boundaries (e.g. ["ab","c"] vs ["a","bc"])
            // never collide.
            hashLow = (hashLow ^ (ulong)report.Length) * fnvPrime;
            hashHigh = (hashHigh ^ ((ulong)report.Length + 1UL)) * fnvPrime;
        }

        byte[] bytes = new byte[16];
        BitConverter.GetBytes(hashLow).CopyTo(bytes, 0);
        BitConverter.GetBytes(hashHigh).CopyTo(bytes, 8);
        return new Guid(bytes).ToString("D");
    }

    private static long Min(long? current, long candidate)
        => current is null || candidate < current ? candidate : current.Value;

    private static long Max(long? current, long candidate)
        => current is null || candidate > current ? candidate : current.Value;
}
