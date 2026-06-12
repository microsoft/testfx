// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The decision returned by an <see cref="ITestFilter"/> for a single test.
/// </summary>
/// <remarks>
/// Designed as a <see langword="readonly"/> <see langword="struct"/> so the filter hot path stays
/// allocation-free. Use the static <see cref="Run"/> / <see cref="Drop"/> properties (one shared
/// value each) or the parameterized <see cref="Skip(string)"/> factory to create explicit results.
/// The default value also represents <see cref="Run"/>.
/// </remarks>
public readonly struct TestFilterResult : IEquatable<TestFilterResult>
{
    private TestFilterResult(TestFilterAction action, string? skipReason)
    {
        Action = action;
        SkipReason = skipReason;
    }

    /// <summary>
    /// Gets the action MSTest should apply for the test that produced this result.
    /// </summary>
    public TestFilterAction Action { get; }

    /// <summary>
    /// Gets the reason supplied to <see cref="Skip(string)"/>. Non-<see langword="null"/> only when
    /// <see cref="Action"/> is <see cref="TestFilterAction.Skip"/>.
    /// </summary>
    public string? SkipReason { get; }

    /// <summary>
    /// Gets the result indicating that the test should run normally.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>default(TestFilterResult)</c>; <see cref="TestFilterAction.Run"/> is the
    /// default enum value so a filter that forgets to assign a result still defaults to running
    /// the test.
    /// </remarks>
    public static TestFilterResult Run { get; } = new(TestFilterAction.Run, null);

    /// <summary>
    /// Gets the result indicating that the test should be silently dropped. Matches the semantics
    /// of the command-line <c>--filter</c> option: no test result is emitted and the test is not
    /// counted.
    /// </summary>
    public static TestFilterResult Drop { get; } = new(TestFilterAction.Drop, null);

    /// <summary>
    /// Creates a result indicating that the test should be reported as Skipped with the given reason.
    /// </summary>
    /// <param name="reason">A non-empty human-readable explanation surfaced in TRX / console / IDE output.</param>
    /// <returns>A <see cref="TestFilterResult"/> with <see cref="Action"/> equal to <see cref="TestFilterAction.Skip"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is empty or whitespace only.</exception>
    public static TestFilterResult Skip(string reason)
        => reason is null
            ? throw new ArgumentNullException(nameof(reason))
            : string.IsNullOrWhiteSpace(reason)
                ? throw new ArgumentException("Value cannot be empty or whitespace.", nameof(reason))
                : new TestFilterResult(TestFilterAction.Skip, reason);

    /// <inheritdoc />
    public bool Equals(TestFilterResult other)
        => Action == other.Action && string.Equals(SkipReason, other.SkipReason, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is TestFilterResult other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (int)Action;
            hash = (hash * 31) + (SkipReason?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(TestFilterResult left, TestFilterResult right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(TestFilterResult left, TestFilterResult right) => !left.Equals(right);
}
