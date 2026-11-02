// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport.Resources;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal static partial class HtmlReportMerger
{
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

    /// <summary>
    /// Default (non-retry) merge: every row is kept, numbered "#N of M" via "attemptIndex"/"attemptOf" whenever more
    /// than one row shares the same test identity (e.g. the same test UID appearing in more than one shard), so
    /// nothing is silently collapsed away.
    /// </summary>
    private static (JsonArray Tests, int Passed, int Failed, int Skipped, int TimedOut, int Errored, int? Flaky) ConcatenateTests(
        MergedTest[] orderedTests)
    {
        var identities = new TestIdentity[orderedTests.Length];
        var countByIdentity = new Dictionary<TestIdentity, int>();
        for (int i = 0; i < orderedTests.Length; i++)
        {
            TestIdentity identity = CreateTestIdentity(orderedTests[i]);
            identities[i] = identity;
            countByIdentity[identity] = countByIdentity.TryGetValue(identity, out int existing) ? existing + 1 : 1;
        }

        var emittedByIdentity = new Dictionary<TestIdentity, int>();
        var mergedTests = new JsonArray();
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedOut = 0;
        int errored = 0;
        for (int i = 0; i < orderedTests.Length; i++)
        {
            MergedTest mergedTest = orderedTests[i];
            JsonObject test = mergedTest.Test;
            TestIdentity identity = identities[i];
            string outcome = ReadRequiredString(test, "outcome");
            _ = ReadRequiredDouble(test, "durationMs");

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
            mergedTests.Add((JsonNode)test);
        }

        return (mergedTests, passed, failed, skipped, timedOut, errored, null);
    }

    /// <summary>
    /// RetryAttempts merge: folds successive attempts of the same logical test (rows sharing the same test
    /// identity) into a single row, using the LAST occurrence in the supplied execution order as the logical
    /// result; earlier occurrences are appended, oldest first, to that row's "retryAttempts[]" with their outcome,
    /// duration, and whichever error/output detail fields they carried. A test whose final outcome is "passed"
    /// after at least one non-passed prior attempt is additionally flagged "flaky" so it stays visibly
    /// distinguishable from a test that has always passed. Logical result counts use only the final occurrence,
    /// while the merged report duration spans from the earliest input start time to the latest input end time.
    /// </summary>
    private static (JsonArray Tests, int Passed, int Failed, int Skipped, int TimedOut, int Errored, int? Flaky) CollapseRetryAttempts(
        MergedTest[] orderedTests)
    {
        var baseIdentities = new RetryIdentity[orderedTests.Length];
        for (int i = 0; i < orderedTests.Length; i++)
        {
            baseIdentities[i] = CreateRetryBaseIdentity(orderedTests[i]);
        }

        HashSet<(int ReportIndex, RetryIdentity BaseIdentity)> ambiguousIdentities =
        [
            .. orderedTests
                .Select((test, index) => (
                    test.OriginalReportIndex,
                    BaseIdentity: baseIdentities[index],
                    RetryAttempt: ReadOptionalInt(test.Test, "retryAttemptNumber")))
                .GroupBy(static entry => (entry.OriginalReportIndex, entry.BaseIdentity, entry.RetryAttempt))
                .Where(static group => group.Count() > 1)
                .Select(static group => (group.Key.OriginalReportIndex, group.Key.BaseIdentity)),
        ];
        var slots = new List<(MergedTest Final, List<JsonObject> Priors)>();
        var slotByIdentity = new Dictionary<RetrySlotIdentity, int>();

        for (int testIndex = 0; testIndex < orderedTests.Length; testIndex++)
        {
            MergedTest mergedTest = orderedTests[testIndex];
            RetryIdentity baseIdentity = baseIdentities[testIndex];
            RetrySlotIdentity identity = ambiguousIdentities.Contains((mergedTest.OriginalReportIndex, baseIdentity))
                ? new(baseIdentity, mergedTest.OriginalReportIndex, mergedTest.OriginalTestIndex)
                : new(baseIdentity, null, null);
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
        for (int i = 0; i < slots.Count; i++)
        {
            (MergedTest final, List<JsonObject> priors) = slots[i];
            JsonObject test = final.Test;
            string outcome = ReadRequiredString(test, "outcome");
            _ = ReadRequiredDouble(test, "durationMs");

            test["rowKey"] = i;
            _ = test.Remove("attemptIndex");
            _ = test.Remove("attemptOf");
            _ = test.Remove("flaky");
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
            mergedTests.Add((JsonNode)test);
        }

        return (mergedTests, passed, failed, skipped, timedOut, errored, flaky);
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
}
