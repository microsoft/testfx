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
///   <item><description><c>reportFormat</c> and <c>specVersion</c> are taken from the first report; <c>reportId</c> is derived deterministically from the inputs, so identical inputs reproduce the same id (RFC 018 idempotency).</description></item>
///   <item><description><c>tool</c> keeps a concrete identity only when every input reported the exact same tool object; otherwise (inputs disagree or any input omits it) a neutral merger identity is used, so one framework is not attributed to another's tests.</description></item>
///   <item><description><c>environment</c> keeps the first report's shared fields, but module-specific values under <c>extra</c> (<c>testApplication</c>, <c>exitCode</c>) are dropped rather than presented as describing all merged modules.</description></item>
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
        // distinct *complete* tool identities (full serialized tool object, not just its name) so the
        // merged report is only stamped with a concrete framework when every input reported the exact
        // same tool; otherwise a neutral merger identity is used (see below). An input that omits 'tool'
        // counts as a distinct (missing) identity, so a mix of tagged/untagged inputs also degrades.
        var distinctToolIdentities = new HashSet<string>(StringComparer.Ordinal);
        JsonNode? firstTool = null;
        int reportCount = 0;

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

            first ??= root;
            reportCount++;

            if (root["results"]?["environment"] is JsonObject environment)
            {
                environments.Add(environment);
            }

            JsonNode? results = root["results"];
            if (results?["tests"] is JsonArray testArray)
            {
                foreach (JsonNode? test in testArray)
                {
                    mergedTests.Add(test?.DeepClone());

                    // Fall back to per-test timing so a summary-less input (which the merger explicitly
                    // supports) still contributes to the merged min/max instead of being dropped or
                    // forcing the merged timestamp back to the Unix epoch.
                    if (test is not null)
                    {
                        if (TryReadLong(test, "start", out long testStart))
                        {
                            earliestStart = Min(earliestStart, testStart);
                        }

                        if (TryReadLong(test, "stop", out long testStop))
                        {
                            latestStop = Max(latestStop, testStop);
                        }
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
        // differing value would misstate the environment for most tests. Module-specific 'extra' values
        // (the producing test application and its exit code) are always dropped.
        if (BuildCommonEnvironment(environments) is JsonObject commonEnvironment)
        {
            resultsObject["environment"] = commonEnvironment;
        }

        resultsObject["tests"] = mergedTests;

        var merged = new JsonObject
        {
            ["reportFormat"] = first["reportFormat"]?.DeepClone() ?? "CTRF",
            ["specVersion"] = first["specVersion"]?.DeepClone() ?? "0.0.0",
            ["reportId"] = CreateDeterministicReportId(inputReports),
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

        // RFC 018 treats per-module inputs as read-only and requires them to remain on disk; reject an
        // output that aliases an input so a merge can never overwrite one of its own sources.
        EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

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

        // Write to a temporary sibling, then replace the destination ENTRY. If the output path is a
        // symlink/hardlink alias of an input (which the textual alias check above cannot detect because
        // Path.GetFullPath does not resolve links), replacing the entry removes only the link and leaves
        // the read-only source intact, rather than truncating it in place via WriteAllText.
        string tempPath = GetTempSiblingPath(outputPath);
        try
        {
#if NETCOREAPP
            await File.WriteAllTextAsync(tempPath, merged, cancellationToken).ConfigureAwait(false);
#else
            File.WriteAllText(tempPath, merged);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
            ReplaceFile(tempPath, outputPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private static string GetTempSiblingPath(string outputPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) is { Length: > 0 } dir
            ? dir
            : Directory.GetCurrentDirectory();
        return Path.Combine(directory, Path.GetFileName(outputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
    }

    private static void ReplaceFile(string tempPath, string outputPath)
    {
        // Delete the destination entry (a regular file, or a symlink/hardlink alias) before moving the
        // freshly written temp file into place. Deleting a link removes only the link, never its target's
        // content, so a source aliased by the output path is never truncated. An exact (case-insensitive)
        // alias of an input has already been rejected, so this cannot delete an input in place.
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        File.Move(tempPath, outputPath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort temp cleanup: leaking a .tmp sibling is preferable to masking the primary
            // exception (or the successful result) with a cleanup failure.
        }
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

    /// <summary>
    /// Builds a merged environment containing only the fields that every input's environment agrees on
    /// (so a value that differs across CI agents is dropped rather than attributed to all tests). The
    /// module-specific <c>extra.testApplication</c> and <c>extra.exitCode</c> fields are always dropped.
    /// Returns <see langword="null"/> when no environment survives.
    /// </summary>
    private static JsonObject? BuildCommonEnvironment(IReadOnlyList<JsonObject> environments)
    {
        if (environments.Count == 0)
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
    /// Derives a stable <c>reportId</c> from the raw input reports so identical inputs reproduce the same
    /// id on every retry (RFC 018 idempotency) without a random source or reusing an input report's id.
    /// A non-cryptographic 128-bit FNV-1a fill is sufficient here — the id only needs to be deterministic
    /// and collision-resistant enough to identify a merged report, not secret.
    /// </summary>
    private static string CreateDeterministicReportId(IReadOnlyList<string> inputReports)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong hashLow = 14695981039346656037UL;
        ulong hashHigh = 0x9E3779B97F4A7C15UL;

        foreach (string report in inputReports)
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

    /// <summary>
    /// Rejects an output path that resolves to one of the input report paths, so a merge never overwrites
    /// a source report (RFC 018 keeps inputs on disk, read-only). Paths are canonicalized (symlinks
    /// resolved where the runtime supports it) and compared case-insensitively, so a differently-cased
    /// path or a symlinked parent directory that aliases an input directory is still detected.
    /// </summary>
    private static void EnsureOutputDoesNotAliasInput(IReadOnlyList<string> inputPaths, string outputPath)
    {
        string outputCanonical = GetCanonicalPath(outputPath);
        foreach (string inputPath in inputPaths)
        {
            if (string.Equals(GetCanonicalPath(inputPath), outputCanonical, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The output path '{outputPath}' cannot be one of the input report paths; inputs are treated as read-only.", nameof(outputPath));
            }
        }
    }

    /// <summary>
    /// Canonicalizes <paramref name="path"/> to a full path with symlinks/junctions resolved in every
    /// existing component (so a symlinked parent directory that aliases another location is detected). On
    /// runtimes without link resolution (netstandard/.NET Framework) it falls back to the lexical full
    /// path.
    /// </summary>
    private static string GetCanonicalPath(string path)
    {
        string full = Path.GetFullPath(path);
#if NETCOREAPP
        try
        {
            string? root = Path.GetPathRoot(full);
            if (RoslynString.IsNullOrEmpty(root))
            {
                return full;
            }

            string resolved = root;
            foreach (string part in full.Substring(root.Length).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                string next = Path.Combine(resolved, part);
                resolved = Directory.Exists(next)
                    ? new DirectoryInfo(next).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next
                    : File.Exists(next)
                        ? new FileInfo(next).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? next
                        : next;
            }

            return resolved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return full;
        }
#else
        return full;
#endif
    }

    private static long Min(long? current, long candidate)
        => current is null || candidate < current ? candidate : current.Value;

    private static long Max(long? current, long candidate)
        => current is null || candidate > current ? candidate : current.Value;
}
