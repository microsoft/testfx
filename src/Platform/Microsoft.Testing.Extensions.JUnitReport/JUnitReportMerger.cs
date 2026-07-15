// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.JUnitReport;

/// <summary>
/// Merges several already-produced JUnit XML reports into a single JUnit document.
/// </summary>
/// <remarks>
/// This is a pure, invocation-agnostic XML-level merge (no I/O, no clock) that mirrors the
/// approach used for TRX: a user-facing merge tool and an SDK-orchestrated post-processor can
/// share it and, given the same inputs and <c>reportName</c>, produce deterministic output.
/// <para>
/// Merge rules:
/// <list type="bullet">
///   <item><description>Every <c>&lt;testsuite&gt;</c> element is unioned as-is and re-assigned a sequential <c>id</c>. Both <c>&lt;testsuites&gt;</c>-rooted documents and bare <c>&lt;testsuite&gt;</c>-rooted documents are supported; any other root is skipped.</description></item>
///   <item><description>Root <c>tests</c>/<c>failures</c>/<c>errors</c>/<c>skipped</c>/<c>time</c> counters are derived by summing the per-suite counters, so they are correct even when an input's root aggregates are missing.</description></item>
///   <item><description>The root <c>timestamp</c> is the earliest across all merged suites.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class JUnitReportMerger
{
    private const string RootElementName = "testsuites";
    private const string SuiteElementName = "testsuite";

    internal static XDocument Merge(IReadOnlyList<XDocument> inputReports, string reportName)
    {
        if (inputReports is null)
        {
            throw new ArgumentNullException(nameof(inputReports));
        }

        if (reportName is null)
        {
            throw new ArgumentNullException(nameof(reportName));
        }

        if (inputReports.Count == 0)
        {
            throw new ArgumentException("At least one JUnit report is required to merge.", nameof(inputReports));
        }

        long totalTests = 0;
        long totalFailures = 0;
        long totalErrors = 0;
        long totalSkipped = 0;
        double totalTime = 0;
        DateTimeOffset? earliestTimestamp = null;

        var mergedRoot = new XElement(RootElementName);
        int suiteId = 0;

        foreach (XDocument report in inputReports)
        {
            XElement? root = report.Root;
            if (root is null)
            {
                continue;
            }

            // Support both <testsuites>-rooted documents and a bare <testsuite> root (a valid,
            // common JUnit shape); any other root has no suites to contribute and is skipped.
            IEnumerable<XElement> suites = string.Equals(root.Name.LocalName, RootElementName, StringComparison.Ordinal)
                ? root.Elements().Where(e => string.Equals(e.Name.LocalName, SuiteElementName, StringComparison.Ordinal))
                : string.Equals(root.Name.LocalName, SuiteElementName, StringComparison.Ordinal)
                    ? [root]
                    : [];

            foreach (XElement suite in suites)
            {
                var clonedSuite = new XElement(suite);
                clonedSuite.SetAttributeValue("id", suiteId++);
                mergedRoot.Add(clonedSuite);

                // Derive aggregates from the per-suite counters rather than trusting the (optional)
                // root aggregates, so a merge cannot silently under-count.
                totalTests += ReadLong(suite, "tests");
                totalFailures += ReadLong(suite, "failures");
                totalErrors += ReadLong(suite, "errors");
                totalSkipped += ReadLong(suite, "skipped");
                totalTime += ReadDouble(suite, "time");

                if (TryReadTimestamp(suite, "timestamp", out DateTimeOffset timestamp)
                    && (earliestTimestamp is null || timestamp < earliestTimestamp))
                {
                    earliestTimestamp = timestamp;
                }
            }
        }

        mergedRoot.SetAttributeValue("name", reportName);
        mergedRoot.SetAttributeValue("tests", totalTests.ToString(CultureInfo.InvariantCulture));
        mergedRoot.SetAttributeValue("failures", totalFailures.ToString(CultureInfo.InvariantCulture));
        mergedRoot.SetAttributeValue("errors", totalErrors.ToString(CultureInfo.InvariantCulture));
        mergedRoot.SetAttributeValue("skipped", totalSkipped.ToString(CultureInfo.InvariantCulture));
        mergedRoot.SetAttributeValue("time", totalTime.ToString("0.000", CultureInfo.InvariantCulture));
        if (earliestTimestamp is { } stamp)
        {
            mergedRoot.SetAttributeValue("timestamp", stamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), mergedRoot);
    }

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        string reportName,
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
        // output that aliases an input so a merge (which writes with a truncating File.Create) can never
        // overwrite one of its own sources.
        EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

        var reports = new List<XDocument>(inputPaths.Count);
        foreach (string inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(XDocument.Load(inputPath));
        }

        XDocument merged = Merge(reports, reportName);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Write to a temporary sibling, then replace the destination ENTRY. If the output path is a
        // symlink/hardlink alias of an input (which the textual alias check above cannot detect because
        // Path.GetFullPath does not resolve links), replacing the entry removes only the link and leaves
        // the read-only source intact, rather than truncating it in place via File.Create.
        string tempPath = GetTempSiblingPath(outputPath);
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
#if NETCOREAPP
                await merged.SaveAsync(stream, SaveOptions.None, cancellationToken).ConfigureAwait(false);
#else
                merged.Save(stream, SaveOptions.None);
                await Task.CompletedTask.ConfigureAwait(false);
#endif
            }

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

    private static long ReadLong(XElement element, string attributeName)
        => long.TryParse(element.Attribute(attributeName)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : 0;

    private static double ReadDouble(XElement element, string attributeName)
        => double.TryParse(element.Attribute(attributeName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : 0;

    private static bool TryReadTimestamp(XElement element, string attributeName, out DateTimeOffset result)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (RoslynString.IsNullOrEmpty(value))
        {
            result = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }

    /// <summary>
    /// Rejects an output path that resolves to one of the input report paths, so a merge never overwrites
    /// a source report (RFC 018 keeps inputs on disk, read-only). Compares case-insensitively on every
    /// platform: Windows and the default macOS volume are case-insensitive, and treating a case-differing
    /// path as an alias only makes this guard more conservative on a case-sensitive volume.
    /// </summary>
    private static void EnsureOutputDoesNotAliasInput(IReadOnlyList<string> inputPaths, string outputPath)
    {
        string outputFull = Path.GetFullPath(outputPath);
        foreach (string inputPath in inputPaths)
        {
            if (string.Equals(Path.GetFullPath(inputPath), outputFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"The output path '{outputPath}' cannot be one of the input report paths; inputs are treated as read-only.", nameof(outputPath));
            }
        }
    }
}
