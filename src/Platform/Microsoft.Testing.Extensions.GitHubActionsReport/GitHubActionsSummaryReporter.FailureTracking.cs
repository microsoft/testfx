// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    private void ClearFlakyRecords(string uid)
    {
        if (!_flakyRecordIndicesByUid.TryGetValue(uid, out List<int>? indices))
        {
            return;
        }

        _flakyRecordIndicesByUid.Remove(uid);
        foreach (int index in indices)
        {
            (string existingUid, string key, TestRecord record) = _records[index];
            _records[index] = (existingUid, key, new TestRecord(
                record.DisplayName,
                record.FullyQualifiedName,
                record.Kind,
                record.Duration,
                isFlaky: false,
                record.Failure));
        }
    }

    /// <summary>
    /// What rendering a failure's diagnostics needs, without retaining the test node it came from.
    /// </summary>
    internal readonly struct PendingFailure
    {
        internal PendingFailure(string fullyQualifiedName, string displayName, string? explanation, Exception? exception, string? declaredFilePath, int declaredLine)
        {
            FullyQualifiedName = fullyQualifiedName;
            DisplayName = displayName;
            Explanation = explanation;
            Exception = exception;
            DeclaredFilePath = declaredFilePath;
            DeclaredLine = declaredLine;
        }

        internal string FullyQualifiedName { get; }

        internal string DisplayName { get; }

        internal string? Explanation { get; }

        internal Exception? Exception { get; }

        internal string? DeclaredFilePath { get; }

        internal int DeclaredLine { get; }
    }

    /// <summary>
    /// Orders failures the way the rendered summary does, so "the ones that will be shown" is decidable before
    /// the session ends.
    /// </summary>
    /// <remarks>
    /// The uid is the final tie-break, and it is what makes the order total. Distinct tests can share both names —
    /// duplicate parameterized cases most obviously — and without it their relative order comes from dictionary
    /// enumeration in one caller and an unstable sort in the other. Retention and rendering could then disagree
    /// about which of the tied failures falls inside the first <see cref="MaxFailures"/>, so one would be shown
    /// stripped of its diagnostics while the other held diagnostics that are never rendered.
    /// </remarks>
    private static int CompareForRendering(
        string leftUid,
        string leftFullyQualifiedName,
        string leftDisplayName,
        string rightUid,
        string rightFullyQualifiedName,
        string rightDisplayName)
    {
        int result = StringComparer.Ordinal.Compare(leftFullyQualifiedName, rightFullyQualifiedName);
        if (result != 0)
        {
            return result;
        }

        result = StringComparer.Ordinal.Compare(leftDisplayName, rightDisplayName);
        return result != 0 ? result : StringComparer.Ordinal.Compare(leftUid, rightUid);
    }

    /// <summary>
    /// Records the diagnostics for a test's latest terminal state, keeping retention bounded to
    /// <paramref name="keep"/> entries.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> <paramref name="failure"/> clears whatever was held for the UID. In-process retries
    /// reuse the same UID, so a test that failed and then recovered has to give its retained slot back — leaving it
    /// would let a passing test evict the diagnostics of one that is still failing, and that failure would then
    /// render as a bare line with nothing explaining why its details are missing.
    /// </remarks>
    internal static /* for testing */ void ApplyPendingFailure(
        Dictionary<string, PendingFailure> pending,
        string uid,
        PendingFailure? failure,
        int keep)
    {
        if (failure is { } value)
        {
            pending[uid] = value;
            TrimToRenderedFailures(pending, keep);
        }
        else
        {
            pending.Remove(uid);
        }
    }

    /// <summary>
    /// Drops the entries that sort last once more than <paramref name="keep"/> are held, so retention stays
    /// bounded however many tests fail.
    /// </summary>
    internal static /* for testing */ void TrimToRenderedFailures(Dictionary<string, PendingFailure> pending, int keep)
    {
        while (pending.Count > keep)
        {
            string? worstKey = null;
            PendingFailure worst = default;
            foreach (KeyValuePair<string, PendingFailure> candidate in pending)
            {
                if (worstKey is null
                    || CompareForRendering(candidate.Key, candidate.Value.FullyQualifiedName, candidate.Value.DisplayName, worstKey, worst.FullyQualifiedName, worst.DisplayName) > 0)
                {
                    worstKey = candidate.Key;
                    worst = candidate.Value;
                }
            }

            pending.Remove(worstKey!);
        }
    }

    /// <summary>
    /// Extracts the explanation and exception a failing state carries, or <see langword="null"/> for a state
    /// that is not a failure. Separate from <see cref="CaptureFailureDetails"/> so each state shape can be
    /// covered without standing up a whole reporter.
    /// </summary>
    internal static /* for testing */ (string? Explanation, Exception? Exception)? TryGetFailureInfo(TestNodeStateProperty? state)
        => state switch
        {
            FailedTestNodeStateProperty failed => (failed.Explanation, failed.Exception),
            ErrorTestNodeStateProperty error => (error.Explanation, error.Exception),
            TimeoutTestNodeStateProperty timeout => (timeout.Explanation, timeout.Exception),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            CancelledTestNodeStateProperty cancelled => (cancelled.Explanation, cancelled.Exception),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            _ => null,
        };

    /// <summary>
    /// Captures the diagnostics of a failing test — explanation/exception message, exception type, stack trace and
    /// the source location — so the job summary can expand the failure beyond its name.
    /// </summary>
    /// <remarks>
    /// The location is resolved the same way <see cref="GitHubActionsAnnotationReporter"/> resolves it: prefer the
    /// exception's call site (it pinpoints the failing statement) and fall back to the location the test framework
    /// reported for the test itself, so frameworks without a usable stack trace still get a location. Values are
    /// clipped here rather than at render time so an enormous stack trace never reaches the aggregation fragment
    /// written to disk.
    /// </remarks>
    private TestFailureDetails? CaptureFailureDetails(PendingFailure failure)
    {
        Exception? exception = failure.Exception;
        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        (string RelativeNormalizedPath, int LineNumber)? stackLocation = StackTraceSourceLocationResolver.TryResolve(
            exception?.StackTrace,
            repoRoot,
            _fileSystem,
            _logger,
            StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);
        GitHubActionsSourceLocation? location = stackLocation is { } resolved
            ? new GitHubActionsSourceLocation(resolved.RelativeNormalizedPath, resolved.LineNumber)
            : ResolveDeclaredLocation(failure, repoRoot);

        // Fall back on whitespace, not just null: Clip treats a whitespace-only value as absent, so an empty
        // explanation would otherwise discard a perfectly good exception message.
        string? explanation = RoslynString.IsNullOrWhiteSpace(failure.Explanation)
            ? exception?.Message
            : failure.Explanation;

        return new TestFailureDetails(
            GitHubActionsFailureDetails.Clip(explanation, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows),
            exception?.GetType().FullName,
            GitHubActionsFailureDetails.Clip(exception?.StackTrace, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows),
            location?.RelativeNormalizedPath,
            location?.LineNumber ?? 0);
    }

    /// <summary>
    /// Resolves the location the test framework declared for the test, from the file and line read out of its
    /// property bag at capture time. Mirrors <see cref="GitHubActionsAnnotationReporter.TryResolveDeclaredLocation"/>,
    /// but works from the two small values kept rather than from the node, which is not retained.
    /// </summary>
    private GitHubActionsSourceLocation? ResolveDeclaredLocation(PendingFailure failure, string repoRoot)
    {
        if (RoslynString.IsNullOrWhiteSpace(failure.DeclaredFilePath))
        {
            return null;
        }

        string? relativeNormalizedPath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(failure.DeclaredFilePath!, repoRoot, _fileSystem);

        // A framework that knows the file but not the line reports a sentinel (-1) or 0; GitHub accepts a
        // 'file'-only annotation, so drop the line rather than emitting an invalid one.
        return relativeNormalizedPath is null
            ? null
            : new GitHubActionsSourceLocation(relativeNormalizedPath, failure.DeclaredLine > 0 ? failure.DeclaredLine : 0);
    }
}
