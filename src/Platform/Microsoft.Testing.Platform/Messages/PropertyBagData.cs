// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents a property bag data.
/// </summary>
/// <param name="displayName">The display name.</param>
/// <param name="description">The description.</param>
public abstract class PropertyBagData(string displayName, string? description) : IData
{
    /// <summary>
    /// Gets the properties.
    /// </summary>
    public PropertyBag Properties { get; } = new();

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; } = description;

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("PropertyBagData { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
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
