// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private static List<ReportTestResult> PrepareResults(CapturedTestResult[] results)
    {
        var openSequencesByUid = new Dictionary<string, List<RetrySequence>>(StringComparer.Ordinal);
        var sequences = new List<RetrySequence>();

        for (int resultIndex = 0; resultIndex < results.Length; resultIndex++)
        {
            CapturedTestResult result = results[resultIndex];
            if (result.RetryAttemptNumber is not int attemptNumber)
            {
                continue;
            }

            if (attemptNumber == 1)
            {
                if (!result.IsSupersededRetryAttempt)
                {
                    continue;
                }

                var sequence = new RetrySequence(resultIndex);
                sequences.Add(sequence);
                if (!openSequencesByUid.TryGetValue(result.Uid, out List<RetrySequence>? openSequences))
                {
                    openSequences = [];
                    openSequencesByUid.Add(result.Uid, openSequences);
                }

                openSequences.Add(sequence);
                continue;
            }

            if (!openSequencesByUid.TryGetValue(result.Uid, out List<RetrySequence>? candidates))
            {
                continue;
            }

            RetrySequence? matchingSequence = null;
            int matchingSequenceCount = 0;
            for (int sequenceIndex = 0; sequenceIndex < candidates.Count; sequenceIndex++)
            {
                if (candidates[sequenceIndex].NextAttemptNumber == attemptNumber)
                {
                    matchingSequence = candidates[sequenceIndex];
                    matchingSequenceCount++;
                }
            }

            if (matchingSequenceCount != 1)
            {
                if (matchingSequenceCount > 1)
                {
                    // RetryAttemptProperty has no execution identity beyond UID and attempt
                    // number. If several overlapping executions can accept this attempt,
                    // collapsing would cross-wire their diagnostics, so preserve every row.
                    _ = candidates.RemoveAll(sequence => sequence.NextAttemptNumber == attemptNumber);
                    if (candidates.Count == 0)
                    {
                        openSequencesByUid.Remove(result.Uid);
                    }
                }

                continue;
            }

            if (matchingSequence is null)
            {
                throw ApplicationStateGuard.Unreachable();
            }

            matchingSequence.ResultIndices.Add(resultIndex);
            matchingSequence.NextAttemptNumber++;
            if (!result.IsSupersededRetryAttempt)
            {
                matchingSequence.IsComplete = true;
                candidates.Remove(matchingSequence);
                if (candidates.Count == 0)
                {
                    openSequencesByUid.Remove(result.Uid);
                }
            }
        }

        var completedByFirstIndex = new Dictionary<int, RetrySequence>();
        var consumedIndices = new HashSet<int>();
        foreach (RetrySequence sequence in sequences)
        {
            if (!sequence.IsComplete)
            {
                continue;
            }

            completedByFirstIndex.Add(sequence.ResultIndices[0], sequence);
            for (int index = 1; index < sequence.ResultIndices.Count; index++)
            {
                consumedIndices.Add(sequence.ResultIndices[index]);
            }
        }

        var prepared = new List<ReportTestResult>(results.Length - consumedIndices.Count);
        for (int resultIndex = 0; resultIndex < results.Length; resultIndex++)
        {
            if (completedByFirstIndex.TryGetValue(resultIndex, out RetrySequence? sequence))
            {
                var priorAttempts = new List<CapturedTestResult>(sequence.ResultIndices.Count - 1);
                for (int index = 0; index < sequence.ResultIndices.Count - 1; index++)
                {
                    priorAttempts.Add(results[sequence.ResultIndices[index]]);
                }

                prepared.Add(new ReportTestResult(results[sequence.ResultIndices[^1]], priorAttempts));
            }
            else if (!consumedIndices.Contains(resultIndex))
            {
                prepared.Add(new ReportTestResult(results[resultIndex], []));
            }
        }

        return prepared;
    }

    private sealed class RetrySequence(int firstResultIndex)
    {
        public List<int> ResultIndices { get; } = [firstResultIndex];

        public int NextAttemptNumber { get; set; } = 2;

        public bool IsComplete { get; set; }
    }

    private sealed class ReportTestResult(CapturedTestResult final, IReadOnlyList<CapturedTestResult> priorAttempts)
    {
        public CapturedTestResult Final { get; } = final;

        public IReadOnlyList<CapturedTestResult> PriorAttempts { get; } = priorAttempts;

        public bool IsFlaky
            => Final.Status == "passed"
            && PriorAttempts.Any(attempt => attempt.Status == "failed");
    }
}
