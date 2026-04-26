// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents a test node unique identifier.
/// </summary>
/// <param name="value">The UID.</param>
public sealed class TestNodeUid(string value) : IEquatable<TestNodeUid>
{
    /// <summary>
    /// Gets the UID.
    /// </summary>
    public string Value { get; init; } = Ensure.NotNullOrWhiteSpace(value);

    /// <summary>
    /// Implicitly converts a <see cref="TestNodeUid"/> to a <see cref="string"/>.
    /// </summary>
    /// <param name="testNode">The <see cref="TestNodeUid"/> value to convert.</param>
    public static implicit operator string(TestNodeUid testNode)
        => testNode.Value;

    /// <summary>
    /// Implicitly converts a string to a <see cref="TestNodeUid"/>.
    /// </summary>
    /// <param name="value">The <see cref="TestNodeUid"/> value as <see cref="string"/>.</param>
    public static implicit operator TestNodeUid(string value)
        => new(value);

    /// <summary>
    /// Checks whether two <see cref="TestNodeUid"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="TestNodeUid"/>.</param>
    /// <param name="right">The second <see cref="TestNodeUid"/>.</param>
    /// <returns><c>true</c> if the two instances are equal; <c>false</c> otherwise.</returns>
    public static bool operator ==(TestNodeUid left, TestNodeUid right)
        => left.Equals(right);

    /// <summary>
    /// Checks whether two <see cref="TestNodeUid"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="TestNodeUid"/>.</param>
    /// <param name="right">The second <see cref="TestNodeUid"/>.</param>
    /// <returns><c>true</c> if the two instances are not equal; <c>false</c> otherwise.</returns>
    public static bool operator !=(TestNodeUid left, TestNodeUid right)
        => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(TestNodeUid? other)
        => other?.Value == value;

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestNodeUid);

    /// <inheritdoc />
    public override int GetHashCode()
        => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"TestNodeUid {{ Value = {Value} }}";
}
