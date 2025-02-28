// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents a test node.
/// </summary>
public class TestNode
{
    /// <summary>
    /// Gets the unique identifier of the test node.
    /// </summary>
    public required TestNodeUid Uid { get; init; }

    /// <summary>
    /// Gets the display name of the test node.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the properties of the test node.
    /// </summary>
    public PropertyBag Properties { get; init; } = new();

    /// <inheritdoc />
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestNode { ")
            .Append("Uid = ")
            .Append(Uid.ToString())
            .Append(", DisplayName = ")
            .Append(DisplayName)
            .Append(", Properties = [");

        bool hasAnyProperty = false;
        foreach (IProperty property in Properties)
        {
            if (!hasAnyProperty)
            {
                hasAnyProperty = true;
            }
            else
            {
                builder.Append(',');
            }

            builder.Append(' ').Append(property);
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("] }");

        return builder.ToString();
    }
}
