// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitSuiteBuilder(
    ITestApplicationModuleInfo testApplicationModuleInfo,
    DateTimeOffset testStartTime)
{
    // Hard upper bound on parent-chain walks to defend against cycles or hostile
    // frameworks emitting self-referential parent UIDs.
    private const int MaxParentChainDepth = 1024;

    private const string TestPathSeparator = "/";

    public SuiteSet BuildSuites(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        DateTimeOffset finishTime)
    {
        string moduleName = Path.GetFileNameWithoutExtension(testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

        // Preserve assembly order and group testcases by classname. The fallback
        // bucket uses the immediate parent's display name (so MTP-native tests fan
        // out into multiple suites rather than one giant "__unknown__"), falling
        // back to the module name when even that is missing.
        var orderedKeys = new List<string>();
        var suiteBuckets = new Dictionary<string, List<TestCase>>(StringComparer.Ordinal);
        var nameCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (CapturedTestResult result in results)
        {
            string suiteName = ResolveSuiteName(result, parentChain, moduleName);

            if (!suiteBuckets.TryGetValue(suiteName, out List<TestCase>? bucket))
            {
                bucket = [];
                suiteBuckets[suiteName] = bucket;
                orderedKeys.Add(suiteName);
#pragma warning disable IDE0028 // Collection initialization cannot be simplified — Dictionary requires StringComparer.Ordinal
                nameCounts.Add(suiteName, new Dictionary<string, int>(StringComparer.Ordinal));
#pragma warning restore IDE0028
            }

            string baseName = result.MethodName ?? result.DisplayName;
            string testPath = BuildTestPath(result, parentChain);

            bucket.Add(new TestCase
            {
                ClassName = suiteName,
                Name = baseName,
                OriginalName = baseName,
                TestPath = testPath,
                Result = result,
                DuplicateIndex = 0,
                DuplicateOf = 0,
            });
        }

        // Second mini-pass: detect duplicate (classname, name) pairs and disambiguate
        // them with " [attempt N]" suffixes plus original-name / attempt-index /
        // attempt-of properties. We never drop rows: parameterized tests, theory
        // data, and intentional retries all need to survive into the JUnit report.
        foreach (string suiteKey in orderedKeys)
        {
            List<TestCase> bucket = suiteBuckets[suiteKey];
            Dictionary<string, int> counts = nameCounts[suiteKey];
            foreach (TestCase tc in bucket)
            {
                counts[tc.OriginalName] = counts.TryGetValue(tc.OriginalName, out int total) ? total + 1 : 1;
            }

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (TestCase tc in bucket)
            {
                int total = counts[tc.OriginalName];
                if (total <= 1)
                {
                    continue;
                }

                seen.TryGetValue(tc.OriginalName, out int index);
                index++;
                seen[tc.OriginalName] = index;
                tc.DuplicateIndex = index;
                tc.DuplicateOf = total;
                tc.Name = $"{tc.OriginalName} [attempt {index.ToString(CultureInfo.InvariantCulture)}]";
            }
        }

        var suites = new List<Suite>(orderedKeys.Count);
        long totalTests = 0;
        long totalFailures = 0;
        long totalErrors = 0;
        long totalSkipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;

        foreach (string suiteKey in orderedKeys)
        {
            List<TestCase> bucket = suiteBuckets[suiteKey];
            int failures = 0;
            int errors = 0;
            int skipped = 0;
            TimeSpan duration = TimeSpan.Zero;
            DateTimeOffset suiteStart = finishTime;
            bool sawStart = false;

            foreach (TestCase tc in bucket)
            {
                duration += tc.Result.Duration;
                switch (tc.Result.Outcome)
                {
                    case "failed":
                        failures++;
                        break;
                    case "errored":
                    case "timedOut":
                    case "cancelled":
                        errors++;
                        break;
                    case "skipped":
                        skipped++;
                        break;
                }

                if (tc.Result.StartTime is DateTimeOffset st && (!sawStart || st < suiteStart))
                {
                    suiteStart = st;
                    sawStart = true;
                }
            }

            suites.Add(new Suite
            {
                Name = suiteKey,
                Tests = bucket,
                Failures = failures,
                Errors = errors,
                Skipped = skipped,
                TotalDuration = duration,
                Timestamp = sawStart ? suiteStart : testStartTime,
            });

            totalTests += bucket.Count;
            totalFailures += failures;
            totalErrors += errors;
            totalSkipped += skipped;
            totalDuration += duration;
        }

        return new SuiteSet
        {
            Name = moduleName,
            Suites = suites,
            TotalTests = totalTests,
            TotalFailures = totalFailures,
            TotalErrors = totalErrors,
            TotalSkipped = totalSkipped,
            TotalDuration = totalDuration,
            Timestamp = testStartTime,
        };
    }

    private static string ResolveSuiteName(
        CapturedTestResult result,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        string moduleName)
    {
        if (!RoslynString.IsNullOrEmpty(result.ClassName))
        {
            return result.ClassName!;
        }

        TestResultCapture.ParentChainEntry parent = default!;
        bool hasParentDisplayName = result.ParentRawUid is string parentUid
            && parentChain.TryGetValue(parentUid, out parent)
            && !RoslynString.IsNullOrEmpty(parent.DisplayName);

        return hasParentDisplayName ? parent.DisplayName : moduleName;
    }

    private static string BuildTestPath(
        CapturedTestResult result,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain)
    {
        // Per RFC 016, testpath is the "/"-joined display names from the root down
        // to and including this node (e.g. "Root/Container/Subcontainer/MyTest").
        // The leaf is therefore always the test's own display name; the parent
        // chain (if any) is prepended in root-first order.
        var segments = new List<string>();

        if (result.ParentRawUid is not null)
        {
            string? current = result.ParentRawUid;
            int depth = 0;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current is not null && depth < MaxParentChainDepth && visited.Add(current))
            {
                if (!parentChain.TryGetValue(current, out TestResultCapture.ParentChainEntry entry))
                {
                    // Parent UID present but missing from the chain (truncated capture
                    // window, framework bug, ...). Stop walking; the leaf below still
                    // gives a usable, non-empty path.
                    break;
                }

                segments.Add(entry.DisplayName);
                current = entry.ParentRawUid;
                depth++;
            }

            segments.Reverse();
        }

        // Always include the test's own display name as the leaf so root-level
        // tests still get a non-empty testpath.
        segments.Add(result.DisplayName);

        var sb = new StringBuilder();

        // Compute the full untruncated testpath length up front so the truncation
        // marker reports the real original length (not the partially-built buffer
        // length at the moment we exceeded the cap, which omits remaining segments).
        int totalLength = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                totalLength += TestPathSeparator.Length;
            }

            totalLength += segments[i].Length;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(TestPathSeparator);
            }

            sb.Append(segments[i]);
            if (sb.Length > JUnitReportEngine.MaxTestPathLength)
            {
                int cut = JUnitReportEngine.MaxTestPathLength;

                // Don't split a surrogate pair when truncating: drop the high surrogate too.
                if (cut > 0 && char.IsHighSurrogate(sb[cut - 1]))
                {
                    cut--;
                }

                sb.Length = cut;
                sb.Append("\n…[truncated, original length: ").Append(totalLength.ToString(CultureInfo.InvariantCulture)).Append(']');
                break;
            }
        }

        return sb.ToString();
    }
}
