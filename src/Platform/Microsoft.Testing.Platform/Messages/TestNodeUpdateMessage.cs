// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public sealed class TestNodeUpdateMessage(SessionUid sessionUid, TestNode testNode, TestNodeUid? parentTestNodeUid = null)
    : DataWithSessionUid("TestNode update", "This data is used to report a TestNode state change.", sessionUid)
{
    public TestNode TestNode { get; } = testNode;

    public TestNodeUid? ParentTestNodeUid { get; } = parentTestNodeUid;

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestNodeUpdateMessage { DisplayName = ")
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

        builder.Append("], ParentTestNodeUid = ")
            .Append(ParentTestNodeUid?.ToString() ?? "<null>")
            .Append(", TestNode = ")
            .Append(TestNode)
            .Append(" }");

        return builder.ToString();
    }
}
