// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class TestNode
{
    internal TestNode(TestNodeUid uid, string displayName, PropertyBag properties)
    {
        Uid = uid;
        DisplayName = displayName;
        Properties = properties;
    }

    public TestNodeUid Uid { get; }

    public string DisplayName { get; }

    public PropertyBag Properties { get; }

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
