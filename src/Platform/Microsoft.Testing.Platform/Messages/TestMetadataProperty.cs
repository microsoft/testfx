// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that represents a generic test metadata property in the shape of a key-value pair associated with a <see cref="TestNode"/>.
/// </summary>
public sealed class TestMetadataProperty : IProperty, IEquatable<TestMetadataProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataProperty"/> class with a key and value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public TestMetadataProperty(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestMetadataProperty"/> class with a key and an empty value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    public TestMetadataProperty(string key)
        : this(key, string.Empty)
    {
    }

    /// <summary>
    /// Gets the metadata key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the metadata value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(TestMetadataProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(Key)} = ");
        builder.Append(Key);
        builder.Append($", {nameof(Value)} = ");
        builder.Append(Value);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as TestMetadataProperty);

    /// <inheritdoc />
    public bool Equals(TestMetadataProperty? other)
        => other is not null && Key == other.Key && Value == other.Value;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(Key, Value);
}
