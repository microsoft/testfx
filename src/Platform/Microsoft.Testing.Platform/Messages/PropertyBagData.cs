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
        StringBuilder builder = new();
        builder.AppendLine("Generic data:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            builder.AppendLine(property.ToString());
        }

        builder.AppendLine("]");

        return builder.ToString();
    }
}
