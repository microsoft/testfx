// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class RpcMessagesTests
{
    private static TestNode CreateTestNode(string uid, string displayName)
        => new() { Uid = new TestNodeUid(uid), DisplayName = displayName };

    [TestMethod]
    public void RunRequestArgs_ToString_RendersTestNodesReadably()
    {
        RunRequestArgs args = new(Guid.Empty, [CreateTestNode("uid1", "Test1"), CreateTestNode("uid2", "Test2")], GraphFilter: null);

        string result = args.ToString();

        Assert.StartsWith("RunRequestArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("Test1", result);
        Assert.Contains("Test2", result);
    }

    [TestMethod]
    public void DiscoverRequestArgs_ToString_RendersTestNodesReadably()
    {
        DiscoverRequestArgs args = new(Guid.Empty, [CreateTestNode("uid1", "Test1")], GraphFilter: "filter");

        string result = args.ToString();

        Assert.StartsWith("DiscoverRequestArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("Test1", result);
        Assert.Contains("filter", result);
    }

    [TestMethod]
    public void RequestArgs_ToString_WithNullTestNodes_RendersNull()
    {
        RunRequestArgs args = new(Guid.Empty, TestNodes: null, GraphFilter: null);

        string result = args.ToString();

        Assert.Contains("TestNodes = <null>", result);
        AssertNoDefaultCollectionToString(result);
    }

    [TestMethod]
    public void RunResponseArgs_ToString_RendersArtifactsReadably()
    {
        RunResponseArgs args = new([new Artifact("uri", "producer", "type", "MyArtifact")]);

        string result = args.ToString();

        Assert.StartsWith("RunResponseArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("MyArtifact", result);
    }

    [TestMethod]
    public void TestNodeStateChangedEventArgs_ToString_RendersChangesReadably()
    {
        TestNodeUpdateMessage change = new(new SessionUid("session"), CreateTestNode("uid1", "ChangedTest"));
        TestNodeStateChangedEventArgs args = new(Guid.Empty, [change]);

        string result = args.ToString();

        Assert.StartsWith("TestNodeStateChangedEventArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("ChangedTest", result);
    }

    [TestMethod]
    public void TelemetryEventArgs_ToString_RendersMetricsReadably()
    {
        TelemetryEventArgs args = new("my-event", new Dictionary<string, object> { ["metric1"] = 42 });

        string result = args.ToString();

        Assert.StartsWith("TelemetryEventArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("my-event", result);
        Assert.Contains("metric1 = 42", result);
    }

    [TestMethod]
    public void ProcessInfoArgs_ToString_RendersEnvironmentVariablesReadably()
    {
        ProcessInfoArgs args = new("program.exe", "--flag", "C:\\work", new Dictionary<string, string?> { ["VAR"] = "value" });

        string result = args.ToString();

        Assert.StartsWith("ProcessInfoArgs {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("VAR = value", result);
    }

    [TestMethod]
    public void TestsAttachments_ToString_RendersAttachmentsReadably()
    {
        TestsAttachments args = new([new RunTestAttachment("uri", "producer", "type", "MyAttachment", "desc")]);

        string result = args.ToString();

        Assert.StartsWith("TestsAttachments {", result);
        AssertNoDefaultCollectionToString(result);
        Assert.Contains("MyAttachment", result);
    }

    private static void AssertNoDefaultCollectionToString(string value)
    {
        Assert.DoesNotContain("System.Collections", value);
        Assert.DoesNotContain("System.String[]", value);
        Assert.DoesNotContain("`1[", value);
    }
}
