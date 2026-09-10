// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>
/// Validation and aggregation helpers over an attempt (sub-result) history.
/// </summary>
internal sealed partial class AzureDevOpsResultIdStore
{
    private static bool IsValidAttemptHistory([NotNullWhen(true)] IReadOnlyList<AzureDevOpsTestSubResult>? attempts)
    {
        if (attempts is null
            || attempts.Count == 0
            || attempts.Count > AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult)
        {
            return false;
        }

        int previousSequenceId = 0;
        foreach (AzureDevOpsTestSubResult attempt in attempts)
        {
            if (attempt is null
                || attempt.SequenceId <= previousSequenceId
                || attempt.SequenceId == int.MaxValue
                || attempt.DisplayName is null
                || attempt.DurationInMs is < 0
                || attempt.Outcome is not (
                    AzureDevOpsLivePublishingConstants.PassedTestOutcome
                    or AzureDevOpsLivePublishingConstants.FailedTestOutcome
                    or AzureDevOpsLivePublishingConstants.NotExecutedTestOutcome
                    or AzureDevOpsLivePublishingConstants.AbortedTestOutcome))
            {
                return false;
            }

            previousSequenceId = attempt.SequenceId;
        }

        return true;
    }

    private static long? SumDurations(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
    {
        long? total = null;
        foreach (AzureDevOpsTestSubResult attempt in attempts)
        {
            total = AddDurations(total, attempt.DurationInMs);
        }

        return total;
    }

    private static long? AddDurations(long? left, long? right)
        => left is null
            ? right
            : right is null
                ? left
                : right.Value > long.MaxValue - left.Value ? long.MaxValue : left.Value + right.Value;

    private static DateTimeOffset? GetEarliestStartedDate(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
        => attempts.Where(attempt => attempt.StartedDate is not null).Min(attempt => attempt.StartedDate);

    private static DateTimeOffset? GetLatestCompletedDate(IReadOnlyList<AzureDevOpsTestSubResult> attempts)
        => attempts.Where(attempt => attempt.CompletedDate is not null).Max(attempt => attempt.CompletedDate);

    private static void TrimAttempts(List<AzureDevOpsTestSubResult> attempts)
    {
        // Azure DevOps caps sub-results per result; keep the most recent attempts because they are the ones
        // that explain the parent outcome. A retry sequence never gets close to this.
        if (attempts.Count > AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult)
        {
            attempts.RemoveRange(0, attempts.Count - AzureDevOpsLivePublishingConstants.MaxSubResultsPerResult);
        }
    }
}
