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
        StringBuilder builder = new();
        builder.AppendLine("Test node update:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.Append("Session UID: ").AppendLine(SessionUid.Value);
        builder.Append("Parent test node UID: ").AppendLine(ParentTestNodeUid?.Value ?? "null");
        builder.AppendLine("Test node: ").AppendLine("{").AppendLine(TestNode.ToString()).AppendLine("}");

        return builder.ToString();
    }
}
