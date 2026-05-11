// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

internal sealed class SerializableKeyValuePairStringProperty : IProperty, IEquatable<SerializableKeyValuePairStringProperty>
{
    public SerializableKeyValuePairStringProperty(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }

    public string Value { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(SerializableKeyValuePairStringProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(Key)} = ");
        builder.Append(Key);
        builder.Append($", {nameof(Value)} = ");
        builder.Append(Value);
        builder.Append(" }");
        return builder.ToString();
    }

    public override bool Equals(object? obj)
        => Equals(obj as SerializableKeyValuePairStringProperty);

    public bool Equals(SerializableKeyValuePairStringProperty? other)
        => other is not null && Key == other.Key && Value == other.Value;

    public override int GetHashCode()
        => RoslynHashCode.Combine(Key, Value);
}
