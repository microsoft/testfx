// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestNodeUpdateMessageTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToString_MessagePropertiesPreserveReverseInsertionOrderAndParentIdentity(bool parent)
    {
        TestNode node = new()
        {
            Uid = "node-17",
            DisplayName = "Visible case",
            Properties = new(new StandardOutputProperty("node payload")),
        };
        TestNodeUpdateMessage message = new(new SessionUid("session-17"), node, parent ? new TestNodeUid("parent-3") : null);
        message.Properties.Add(new TestMetadataProperty("message", "first"));
        message.Properties.Add(new TestMetadataProperty("message", "second"));

        string expected = parent
            ? "TestNodeUpdateMessage { DisplayName = TestNode update, Description = This data is used to report a TestNode state change., Properties = [ TestMetadataProperty { Key = message, Value = second }, TestMetadataProperty { Key = message, Value = first } ], ParentTestNodeUid = TestNodeUid { Value = parent-3 }, TestNode = TestNode { Uid = TestNodeUid { Value = node-17 }, DisplayName = Visible case, Properties = [ StandardOutputProperty { StandardOutput = node payload } ] } }"
            : "TestNodeUpdateMessage { DisplayName = TestNode update, Description = This data is used to report a TestNode state change., Properties = [ TestMetadataProperty { Key = message, Value = second }, TestMetadataProperty { Key = message, Value = first } ], ParentTestNodeUid = <null>, TestNode = TestNode { Uid = TestNodeUid { Value = node-17 }, DisplayName = Visible case, Properties = [ StandardOutputProperty { StandardOutput = node payload } ] } }";
        Assert.AreEqual(expected, message.ToString());
        Assert.AreSame(node, message.TestNode);
        Assert.AreEqual("session-17", message.SessionUid.Value);
    }

    [TestMethod]
    public void ToString_CoverageMessagePropertiesAreOrderedAndDelimited()
    {
        TestCoverageMessage message = new(new SessionUid("session-17"), CoverageScope.Overall, CoverageMetric.Line, 3, 8, "collector");
        message.Properties.Add(new TestMetadataProperty("Category", "fast"));
        message.Properties.Add(new TestMetadataProperty("Owner", "team"));

        Assert.AreEqual(
            "DataWithSessionUid { DisplayName = Test coverage, Description = Reports a code coverage measurement for a scope., Properties = [ TestMetadataProperty { Key = Owner, Value = team }, TestMetadataProperty { Key = Category, Value = fast } ] }",
            message.ToString());
        Assert.AreEqual("session-17", message.SessionUid.Value);
    }
}
