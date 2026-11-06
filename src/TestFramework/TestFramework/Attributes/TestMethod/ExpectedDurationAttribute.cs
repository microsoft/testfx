// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies an expected maximum execution duration for a test method.
/// </summary>
/// <remarks>
/// A completed test result that exceeds this duration produces a warning without changing its outcome.
/// Unlike <see cref="TimeoutAttribute"/>, this attribute does not cancel or interrupt execution.
/// <para>
/// The budget applies independently to each data row and to the final retry attempt. Superseded retry
/// attempts and retry delays are not accumulated. Only passed and failed results are evaluated;
/// skipped, inconclusive, timed-out, aborted, and infrastructure-error results do not produce this warning.
/// </para>
/// <para>
/// The comparison uses the reported <see cref="TestResult.Duration"/>. For ordinary execution this includes
/// per-test initialization, construction, execution, cleanup, and disposal, but excludes assembly and class
/// initialization and cleanup. Custom test-method executors control their reported result durations.
/// A duration equal to the expected maximum is within budget.
/// </para>
/// <para>
/// Use generous budgets that account for machine load and cold starts. This diagnostic is not a substitute
/// for controlled performance benchmarks. Matching framework and adapter versions that support this
/// attribute are required.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ExpectedDurationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpectedDurationAttribute"/> class.
    /// </summary>
    /// <param name="milliseconds">The expected maximum execution duration, in milliseconds. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="milliseconds"/> is not positive.</exception>
    public ExpectedDurationAttribute(int milliseconds)
    {
#pragma warning disable CA1512 // Use ArgumentOutOfRangeException throw helper
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }
#pragma warning restore CA1512 // Use ArgumentOutOfRangeException throw helper

        Milliseconds = milliseconds;
    }

    /// <summary>
    /// Gets the expected maximum execution duration, in milliseconds.
    /// </summary>
    public int Milliseconds { get; }
}
