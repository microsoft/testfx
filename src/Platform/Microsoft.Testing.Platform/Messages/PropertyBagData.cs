// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public abstract class PropertyBagData(string displayName, string? description) : IData
{
    public PropertyBag Properties { get; } = new();

    public string DisplayName { get; } = displayName;

    public string? Description { get; } = description;

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
