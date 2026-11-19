// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Helpers for interpreting the in-process retry attribution carried by <see cref="RetryAttemptProperty"/>.
/// </summary>
/// <remarks>
/// <see cref="RetryAttemptProperty"/> is a sibling of the <see cref="TestNodeStateProperty"/> rather than part of
/// it, mirroring <see cref="TimingProperty"/> and the output properties: it describes the execution occurrence, not
/// the outcome, and applies to in-progress updates as well as terminal ones. The trade-off is that "is this the
/// test's final outcome?" is not answerable from the state property alone, so every consumer that wants exactly one
/// row per test would otherwise re-implement the same lookup. This helper is that single implementation.
/// </remarks>
internal static class RetryAttemptPropertyExtensions
{
    /// <summary>
    /// Returns <see langword="true"/> when a later attempt for the same test node supersedes this update, so it is
    /// not the test's final outcome.
    /// </summary>
    /// <remarks>
    /// Consumers that report exactly one result per test - the process exit code, TRX, JUnit, Azure DevOps - skip
    /// these updates. Consumers that present the retry history - CTRF's <c>retryAttempts[]</c>/<c>flaky</c>, the
    /// HTML report, the terminal's retried counters - keep them.
    /// </remarks>
    /// <param name="testNode">The test node to inspect.</param>
    /// <returns><see langword="true"/> if the update is a superseded retry attempt.</returns>
    public static bool IsSupersededRetryAttempt(this TestNode testNode)
        => testNode.Properties.SingleOrDefault<RetryAttemptProperty>() is { IsSuperseded: true };
}
