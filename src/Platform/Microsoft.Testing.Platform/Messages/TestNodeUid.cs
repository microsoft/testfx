// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed class TestNodeUid(string value) : IEquatable<TestNodeUid>
{
    public string Value { get; init; } = !RoslynString.IsNullOrWhiteSpace(value)
        ? value
#pragma warning disable RS0030 // Do not use banned APIs
        : throw new ArgumentNullException(nameof(value));
#pragma warning restore RS0030 // Do not use banned APIs

    public static implicit operator string(TestNodeUid testNode)
        => testNode.Value;

    public static implicit operator TestNodeUid(string value)
        => new(value);

    public static bool operator ==(TestNodeUid left, TestNodeUid right)
        => left.Equals(right);

    public static bool operator !=(TestNodeUid left, TestNodeUid right)
        => !left.Equals(right);

    public bool Equals(TestNodeUid? other)
        => other?.Value == value;

    public override bool Equals(object? obj)
        => Equals(obj as TestNodeUid);

    public override int GetHashCode()
        => Value.GetHashCode();
}
