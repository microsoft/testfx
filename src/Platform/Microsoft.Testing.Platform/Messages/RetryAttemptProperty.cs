// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that identifies a test node update as one attempt of an in-process retry sequence, i.e. several
/// executions of the same test that a test framework performed within a single test host run and reported under the
/// same <see cref="TestNode.Uid"/>.
/// </summary>
/// <remarks>
/// A test framework that retries a test in-process (for example MSTest's <c>[Retry]</c> attribute) publishes one
/// <see cref="TestNodeUpdateMessage"/> per attempt, each carrying this property. Consumers that want a single row
/// per test - TRX, JUnit, the process exit code - ignore updates whose <see cref="IsSuperseded"/> is
/// <see langword="true"/>. Consumers that want the whole history - CTRF's <c>retryAttempts[]</c> / <c>flaky</c>,
/// the HTML report, the terminal's retried/flaky counters - keep them all.
/// <para>
/// This is independent from the out-of-process <c>--retry-failed-tests</c> orchestrator, which re-invokes the whole
/// test host and attributes attempts per host instance instead. When both are in play the two notions compose: the
/// host attempt is the major number and <see cref="AttemptNumber"/> the minor one.
/// </para>
/// </remarks>
public sealed class RetryAttemptProperty : IProperty, IEquatable<RetryAttemptProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttemptProperty"/> class.
    /// </summary>
    /// <param name="attemptNumber">The 1-based attempt number within the current test host run.</param>
    /// <param name="isSuperseded">
    /// <see langword="true"/> when a later attempt for the same test node follows this one, so this result is not
    /// the test's final outcome.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attemptNumber"/> is less than 1.
    /// </exception>
    public RetryAttemptProperty(int attemptNumber, bool isSuperseded)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        AttemptNumber = attemptNumber;
        IsSuperseded = isSuperseded;
    }

    /// <summary>
    /// Gets the 1-based attempt number within the current test host run. The first (non-retry) execution is 1.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets a value indicating whether a later attempt for the same test node follows this one.
    /// </summary>
    public bool IsSuperseded { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(RetryAttemptProperty)} {{ {nameof(AttemptNumber)} = {AttemptNumber}, {nameof(IsSuperseded)} = {IsSuperseded} }}";

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as RetryAttemptProperty);

    /// <inheritdoc />
    public bool Equals(RetryAttemptProperty? other)
        => other is not null && AttemptNumber == other.AttemptNumber && IsSuperseded == other.IsSuperseded;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(AttemptNumber, IsSuperseded);
}
