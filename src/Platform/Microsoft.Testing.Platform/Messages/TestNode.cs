// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class TestNode
{
    public required TestNodeUid Uid { get; init; }

    public required string DisplayName { get; init; }

    public PropertyBag Properties { get; init; } = new();

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append("UID: ");
        sb.AppendLine(Uid.ToString());
        sb.Append("DisplayName: ");
        sb.AppendLine(DisplayName);
        sb.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            sb.AppendLine(property.ToString());
        }

        sb.AppendLine("]");

        return sb.ToString();
    }
}
