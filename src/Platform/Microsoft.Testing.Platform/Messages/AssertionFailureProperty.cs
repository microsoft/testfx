// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that carries the structured <c>expected</c> and <c>actual</c> values of a failed assertion,
/// so consumers can render an expected-vs-actual diff instead of only a flat failure message.
/// </summary>
/// <remarks>
/// <para>
/// Historically this information could only travel on <see cref="Exception.Data"/> under the
/// <c>assert.expected</c> / <c>assert.actual</c> keys. That channel requires the test framework to hand the
/// platform the very exception instance the assertion library threw, which is not always possible: a framework
/// may report a failure without any exception at all (see the
/// <see cref="FailedTestNodeStateProperty(string)"/> overload), or it may only have the message and stack trace
/// as strings once the failure has crossed a process or AppDomain boundary.
/// </para>
/// <para>
/// Consumers should prefer this property and fall back to the <see cref="Exception.Data"/> keys for producers
/// that have not been updated yet.
/// </para>
/// </remarks>
public sealed class AssertionFailureProperty : IProperty, IEquatable<AssertionFailureProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssertionFailureProperty"/> class.
    /// </summary>
    /// <param name="expected">
    /// The pre-formatted text representation of the expected value, or <see langword="null"/> when the
    /// assertion has no natural expected value.
    /// </param>
    /// <param name="actual">
    /// The pre-formatted text representation of the actual value, or <see langword="null"/> when the
    /// assertion has no natural actual value.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Both <paramref name="expected"/> and <paramref name="actual"/> are <see langword="null"/>, which would
    /// make the property carry no information. Rejecting that state is what lets consumers treat the mere
    /// presence of this property as authoritative and stop consulting the legacy
    /// <see cref="Exception.Data"/> keys.
    /// </exception>
    public AssertionFailureProperty(string? expected, string? actual)
    {
        if (expected is null && actual is null)
        {
            throw new ArgumentException($"At least one of '{nameof(expected)}' or '{nameof(actual)}' must be non-null.", nameof(expected));
        }

        Expected = expected;
        Actual = actual;
    }

    /// <summary>
    /// Gets the pre-formatted text representation of the expected value, or <see langword="null"/> when the
    /// assertion has no natural expected value.
    /// </summary>
    public string? Expected { get; }

    /// <summary>
    /// Gets the pre-formatted text representation of the actual value, or <see langword="null"/> when the
    /// assertion has no natural actual value.
    /// </summary>
    public string? Actual { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(AssertionFailureProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(Expected)} = ");
        builder.Append(Expected);
        builder.Append($", {nameof(Actual)} = ");
        builder.Append(Actual);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as AssertionFailureProperty);

    /// <inheritdoc />
    public bool Equals(AssertionFailureProperty? other)
        => other is not null && Expected == other.Expected && Actual == other.Actual;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Expected, Actual);
}
