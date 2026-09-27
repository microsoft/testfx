// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher
{
#if NET9_0_OR_GREATER
    private readonly Lock _inProcessRetryAttemptsLock = new();
#else
    private readonly object _inProcessRetryAttemptsLock = new();
#endif
    private readonly Dictionary<(string AutomatedTestName, string TestCaseTitle), List<InProcessRetrySequence>> _inProcessRetrySequences = [];

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_publishConfiguration is null || _runIdCoordinator is null || _coordinatedRun is null || CurrentRunId is null)
        {
            return;
        }

        try
        {
            switch (value)
            {
                case TestNodeUpdateMessage testNodeUpdateMessage:
                    AzureDevOpsTestCaseResultWithAttachments? testCaseResult = CreateTestCaseResult(testNodeUpdateMessage.TestNode, _publishConfiguration.AutomatedTestStorage);
                    if (testCaseResult is null)
                    {
                        return;
                    }

                    RetryAttemptProperty? retryAttempt = testNodeUpdateMessage.TestNode.Properties.SingleOrDefault<RetryAttemptProperty>();
                    if (retryAttempt is not null)
                    {
                        testCaseResult = AggregateInProcessRetryAttempt(retryAttempt, testCaseResult);
                        if (testCaseResult is null)
                        {
                            return;
                        }
                    }

                    // Enqueue before renewing the lease: RenewLeaseAsync does file I/O that can throw
                    // (e.g. a sharing violation while another process reads the lease). If it threw
                    // first, the result would be dropped without ever reaching a queue, so the
                    // end-of-session "results dropped" count could not see it.
                    _pendingResults.Enqueue(testCaseResult);
                    await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
                    await FlushPendingResultsAsync(force: false, cancellationToken).ConfigureAwait(false);
                    break;

                case SessionFileArtifact sessionFileArtifact when TryCreateRunAttachment(sessionFileArtifact) is { } runAttachment:
                    _pendingRunAttachments.Enqueue(runAttachment);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TryLogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }
    }

    private AzureDevOpsTestCaseResultWithAttachments? AggregateInProcessRetryAttempt(
        RetryAttemptProperty retryAttempt,
        AzureDevOpsTestCaseResultWithAttachments attempt)
    {
        (string AutomatedTestName, string TestCaseTitle) key = (attempt.Result.AutomatedTestName, attempt.Result.TestCaseTitle);
        lock (_inProcessRetryAttemptsLock)
        {
            if (retryAttempt.AttemptNumber == 1)
            {
                if (!retryAttempt.IsSuperseded)
                {
                    return attempt;
                }

                if (!_inProcessRetrySequences.TryGetValue(key, out List<InProcessRetrySequence>? sequences))
                {
                    sequences = [];
                    _inProcessRetrySequences.Add(key, sequences);
                }

                sequences.Add(new InProcessRetrySequence(attempt));
                return null;
            }

            if (!_inProcessRetrySequences.TryGetValue(key, out List<InProcessRetrySequence>? candidates))
            {
                return attempt;
            }

            InProcessRetrySequence? matchingSequence = null;
            int matchingSequenceCount = 0;
            foreach (InProcessRetrySequence candidate in candidates)
            {
                if (candidate.NextAttemptNumber == retryAttempt.AttemptNumber)
                {
                    matchingSequence = candidate;
                    matchingSequenceCount++;
                }
            }

            if (matchingSequenceCount != 1)
            {
                if (matchingSequenceCount > 1)
                {
                    // Retry metadata has no row identity beyond the test name, title, and attempt number.
                    // Preserve every execution independently rather than cross-wire diagnostics between
                    // folded data-driven rows that share all three values.
                    foreach (InProcessRetrySequence candidate in candidates)
                    {
                        if (candidate.NextAttemptNumber != retryAttempt.AttemptNumber)
                        {
                            continue;
                        }

                        foreach (AzureDevOpsTestCaseResultWithAttachments previousAttempt in candidate.Attempts)
                        {
                            _pendingResults.Enqueue(previousAttempt);
                        }
                    }

                    _ = candidates.RemoveAll(candidate => candidate.NextAttemptNumber == retryAttempt.AttemptNumber);
                    if (candidates.Count == 0)
                    {
                        _inProcessRetrySequences.Remove(key);
                    }
                }

                return attempt;
            }

            matchingSequence!.Attempts.Add(attempt);
            matchingSequence.NextAttemptNumber++;
            if (retryAttempt.IsSuperseded)
            {
                return null;
            }

            candidates.Remove(matchingSequence);
            if (candidates.Count == 0)
            {
                _inProcessRetrySequences.Remove(key);
            }

            return attempt with { PreviousAttempts = [.. matchingSequence.Attempts.Take(matchingSequence.Attempts.Count - 1)] };
        }
    }

    private sealed class InProcessRetrySequence(AzureDevOpsTestCaseResultWithAttachments firstAttempt)
    {
        public List<AzureDevOpsTestCaseResultWithAttachments> Attempts { get; } = [firstAttempt];

        public int NextAttemptNumber { get; set; } = 2;
    }

    private void DrainIncompleteInProcessRetrySequences()
    {
        lock (_inProcessRetryAttemptsLock)
        {
            foreach (List<InProcessRetrySequence> sequences in _inProcessRetrySequences.Values)
            {
                foreach (InProcessRetrySequence sequence in sequences)
                {
                    foreach (AzureDevOpsTestCaseResultWithAttachments attempt in sequence.Attempts)
                    {
                        _pendingResults.Enqueue(attempt);
                    }
                }
            }

            _inProcessRetrySequences.Clear();
        }
    }
}
