// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal enum JUnitMergeMode
{
    Concatenate,
    CollapseRetryAttempts,
}

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
        => Merge(inputReports, reportName, JUnitMergeMode.Concatenate);

    internal static XDocument Merge(
        IReadOnlyList<XDocument> inputReports,
        string reportName,
        JUnitMergeMode mode)
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

        if (mode == JUnitMergeMode.CollapseRetryAttempts)
        {
            return MergeRetryAttempts(inputReports, reportName);
        }

        long totalTests = 0;
        long totalFailures = 0;
        long totalErrors = 0;
        long totalSkipped = 0;
        double totalTime = 0;
        DateTimeOffset? earliestTimestamp = null;
        XElement? recoveryProperties = FindRecoveryProperties(inputReports);

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
                ApplyRecoveryProperties(clonedSuite, recoveryProperties);
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
        => await MergeToFileAsync(
            inputPaths,
            outputPath,
            reportName,
            JUnitMergeMode.Concatenate,
            cancellationToken).ConfigureAwait(false);

    internal static async Task MergeToFileAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        string reportName,
        JUnitMergeMode mode,
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
            throw new ArgumentException("At least one JUnit report is required to merge.", nameof(inputPaths));
        }

        // RFC 018 treats per-module inputs as read-only and requires them to remain on disk; reject an
        // output that aliases an input so a merge (which writes with a truncating File.Create) can never
        // overwrite one of its own sources.
        MergeOutputFileHelper.EnsureOutputDoesNotAliasInput(inputPaths, outputPath);

        var reports = new List<XDocument>(inputPaths.Count);
        foreach (string inputPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reports.Add(XDocument.Load(inputPath));
        }

        XDocument merged = Merge(reports, reportName, mode);

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!RoslynString.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Write to a temporary sibling, then replace the destination ENTRY, so a symlink/hardlink output
        // alias of an input has only its link removed rather than the read-only source truncated in place.
        await MergeOutputFileHelper.WriteViaTemporarySiblingAsync(outputPath, async tempPath =>
        {
            using FileStream stream = File.Create(tempPath);
#if NETCOREAPP
            await merged.SaveAsync(stream, SaveOptions.None, cancellationToken).ConfigureAwait(false);
#else
            merged.Save(stream, SaveOptions.None);
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }).ConfigureAwait(false);
    }

    private static XDocument MergeRetryAttempts(IReadOnlyList<XDocument> inputReports, string reportName)
    {
        var suites = new List<RetrySuite>();
        var suiteIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        DateTimeOffset? earliestTimestamp = null;
        XElement? finalSuiteProperties = null;
        XElement? recoveryProperties = FindRecoveryProperties(inputReports);

        foreach (XDocument report in inputReports)
        {
            XElement[] reportSuites = [.. GetSuites(report)];
            finalSuiteProperties = reportSuites
                .SelectMany(suite => suite.Elements().Where(IsProperties))
                .Select(properties => new XElement(properties))
                .FirstOrDefault()
                ?? finalSuiteProperties;

            foreach (XElement suite in reportSuites)
            {
                if (TryReadTimestamp(suite, "timestamp", out DateTimeOffset timestamp)
                    && (earliestTimestamp is null || timestamp < earliestTimestamp))
                {
                    earliestTimestamp = timestamp;
                }

                string suiteIdentity = BuildSuiteIdentity(suite);
                if (!suiteIndices.TryGetValue(suiteIdentity, out int suiteIndex))
                {
                    suiteIndex = suites.Count;
                    suiteIndices.Add(suiteIdentity, suiteIndex);
                    suites.Add(new RetrySuite(new XElement(suite)));
                }

                RetrySuite retrySuite = suites[suiteIndex];
                var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (XElement testCase in suite.Elements().Where(IsTestCase))
                {
                    string testIdentity = BuildTestIdentity(testCase);
                    occurrences.TryGetValue(testIdentity, out int occurrence);
                    occurrences[testIdentity] = occurrence + 1;
                    string occurrenceIdentity = BuildIdentity(testIdentity, occurrence.ToString(CultureInfo.InvariantCulture));
                    if (retrySuite.TestIndices.TryGetValue(occurrenceIdentity, out int testIndex))
                    {
                        retrySuite.Tests[testIndex] = new XElement(testCase);
                    }
                    else
                    {
                        retrySuite.TestIndices.Add(occurrenceIdentity, retrySuite.Tests.Count);
                        retrySuite.Tests.Add(new XElement(testCase));
                    }
                }
            }
        }

        long totalTests = 0;
        long totalFailures = 0;
        long totalErrors = 0;
        long totalSkipped = 0;
        double totalTime = 0;
        var mergedRoot = new XElement(RootElementName);

        for (int i = 0; i < suites.Count; i++)
        {
            RetrySuite retrySuite = suites[i];
            XElement mergedSuite = retrySuite.Template;
            foreach (XElement testCase in mergedSuite.Elements().Where(IsTestCase).ToArray())
            {
                testCase.Remove();
            }

            if (finalSuiteProperties is not null)
            {
                XElement[] existingProperties = [.. mergedSuite.Elements().Where(IsProperties)];
                if (existingProperties.Length == 0)
                {
                    mergedSuite.AddFirst(new XElement(finalSuiteProperties));
                }
                else
                {
                    existingProperties[0].ReplaceWith(new XElement(finalSuiteProperties));
                    foreach (XElement duplicateProperties in existingProperties.Skip(1))
                    {
                        duplicateProperties.Remove();
                    }
                }

                ApplyRecoveryProperties(mergedSuite, recoveryProperties);
            }

            long failures = 0;
            long errors = 0;
            long skipped = 0;
            double time = 0;
            foreach (XElement testCase in retrySuite.Tests)
            {
                mergedSuite.Add(testCase);
                failures += testCase.Elements().Any(element => element.Name.LocalName == "failure") ? 1 : 0;
                errors += testCase.Elements().Any(element => element.Name.LocalName == "error") ? 1 : 0;
                skipped += testCase.Elements().Any(element => element.Name.LocalName == "skipped") ? 1 : 0;
                time += ReadDouble(testCase, "time");
            }

            long tests = retrySuite.Tests.Count;
            mergedSuite.SetAttributeValue("id", i);
            mergedSuite.SetAttributeValue("tests", tests.ToString(CultureInfo.InvariantCulture));
            mergedSuite.SetAttributeValue("failures", failures.ToString(CultureInfo.InvariantCulture));
            mergedSuite.SetAttributeValue("errors", errors.ToString(CultureInfo.InvariantCulture));
            mergedSuite.SetAttributeValue("skipped", skipped.ToString(CultureInfo.InvariantCulture));
            mergedSuite.SetAttributeValue("time", time.ToString("0.000", CultureInfo.InvariantCulture));
            mergedRoot.Add(mergedSuite);

            totalTests += tests;
            totalFailures += failures;
            totalErrors += errors;
            totalSkipped += skipped;
            totalTime += time;
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

    private static IEnumerable<XElement> GetSuites(XDocument report)
        => report.Root is not XElement root
            ? []
            : string.Equals(root.Name.LocalName, RootElementName, StringComparison.Ordinal)
                ? root.Elements().Where(element => string.Equals(element.Name.LocalName, SuiteElementName, StringComparison.Ordinal))
                : string.Equals(root.Name.LocalName, SuiteElementName, StringComparison.Ordinal)
                    ? [root]
                    : [];

    private static bool IsTestCase(XElement element)
        => element.Name.LocalName == "testcase";

    private static bool IsProperties(XElement element)
        => element.Name.LocalName == "properties";

    private static XElement? FindRecoveryProperties(IReadOnlyList<XDocument> reports)
    {
        foreach (XDocument report in reports)
        {
            foreach (XElement properties in GetSuites(report).SelectMany(suite => suite.Elements().Where(IsProperties)))
            {
                if (ReadSuiteProperty(properties, "incomplete") == "true")
                {
                    var recoveryProperties = new XElement("properties");
                    recoveryProperties.Add(
                        new XElement("property", new XAttribute("name", "run-status"), new XAttribute("value", "aborted")),
                        new XElement("property", new XAttribute("name", "incomplete"), new XAttribute("value", "true")));
                    return recoveryProperties;
                }
            }
        }

        return null;
    }

    private static void ApplyRecoveryProperties(XElement suite, XElement? recoveryProperties)
    {
        if (recoveryProperties is null)
        {
            return;
        }

        XElement properties = suite.Elements().FirstOrDefault(IsProperties) ?? new XElement("properties");
        if (properties.Parent is null)
        {
            suite.AddFirst(properties);
        }

        foreach (XElement recoveryProperty in recoveryProperties.Elements("property"))
        {
            string name = recoveryProperty.Attribute("name")!.Value;
            properties.Elements("property")
                .Where(property => property.Attribute("name")?.Value == name)
                .Remove();
            properties.Add(new XElement(recoveryProperty));
        }
    }

    private static string? ReadSuiteProperty(XElement properties, string name)
        => properties.Elements("property")
            .FirstOrDefault(property => property.Attribute("name")?.Value == name)
            ?.Attribute("value")
            ?.Value;

    private static string BuildSuiteIdentity(XElement suite)
        => BuildIdentity(
            suite.Attribute("name")?.Value,
            suite.Attribute("package")?.Value,
            suite.Attribute("hostname")?.Value);

    private static string BuildTestIdentity(XElement testCase)
        => BuildIdentity(
            testCase.Attribute("classname")?.Value,
            testCase.Attribute("file")?.Value,
            ReadProperty(testCase, "uid"),
            ReadProperty(testCase, "testpath"),
            ReadProperty(testCase, "original-name") ?? testCase.Attribute("name")?.Value);

    private static string BuildIdentity(params string?[] components)
    {
        var identity = new StringBuilder();
        foreach (string? component in components)
        {
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, component);
        }

        return identity.ToString();
    }

    private static string? ReadProperty(XElement testCase, string propertyName)
        => testCase.Elements()
            .Where(element => element.Name.LocalName == "properties")
            .Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == "property"
                && element.Attribute("name")?.Value == propertyName)
            ?.Attribute("value")
            ?.Value;

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

    private sealed class RetrySuite(XElement template)
    {
        public XElement Template { get; } = template;

        public List<XElement> Tests { get; } = [];

        public Dictionary<string, int> TestIndices { get; } = [with(StringComparer.Ordinal)];
    }
}
