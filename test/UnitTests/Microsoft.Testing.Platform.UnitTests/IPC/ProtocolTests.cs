// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ProtocolTests
{
    [TestMethod]
    public void TestResultMessagesSerializeDeserialize()
    {
        var success = new SuccessfulTestResultMessage("uid", "displayName", 1, 100, "reason", "standardOutput", "errorOutput", "sessionUid");
        var fail = new FailedTestResultMessage("uid", "displayName", 2, 200, "reason", [new ExceptionMessage("errorMessage", "errorType", "stackTrace")], "standardOutput", "errorOutput", "sessionUid");
        var message = new TestResultMessages("executionId", "instanceId", [success], [fail]);

        var stream = new MemoryStream();
        new TestResultMessagesSerializer().Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestResultMessages)new TestResultMessagesSerializer().Deserialize(stream);
        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
#if NET8_0_OR_GREATER
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(message), System.Text.Json.JsonSerializer.Serialize(actual));
#endif
        Assert.AreEqual(message.FailedTestMessages?[0].Exceptions?[0].ErrorMessage, actual.FailedTestMessages?[0].Exceptions?[0].ErrorMessage);
    }

    [TestMethod]
    public void DiscoveredTestMessagesSerializeDeserialize()
    {
        var serializer = new DiscoveredTestMessagesSerializer();
        var stream = new MemoryStream();

        var message = new DiscoveredTestMessages(
            "MyExecId",
            "MyInstId",
            [
                new DiscoveredTestMessage("MyFirstUid", "DispName1", "path/to/file1.cs", 19, "MyNamespace1", "FirstType", "TM1", [], []),
                new DiscoveredTestMessage("My2ndUid", "SecondDisplay", "file2.cs", 21, string.Empty, null, string.Empty, [], []),
                new DiscoveredTestMessage("My3rdUid", "3rdDisplay", null, null, "MyNamespace3", "TestClass3", "TM3", [], [new("Key1", "Value"), new("Key2", string.Empty)]),
                new DiscoveredTestMessage("My4thUid", "DispName1", "path/to/file1.cs", 19, "MyNamespace1", "FirstType", "TM1", ["paramtype1", "paramtype2"], []),
                new DiscoveredTestMessage("My5thUid", "SecondDisplay", "file2.cs", 21, string.Empty, null, string.Empty, ["paramtype1", "paramtype2", "paramtype3"], []),
                new DiscoveredTestMessage("My5thUid", "3rdDisplay", null, null, "MyNamespace3", "TestClass3", "TM3", ["paramtype1", "paramtype2", "paramtype3"], [new("Key1", "Value"), new("Key2", string.Empty)]),
            ]);

        serializer.Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);

        var deserialized = (DiscoveredTestMessages)serializer.Deserialize(stream);
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(message.ExecutionId, deserialized.ExecutionId);
        Assert.AreEqual(message.InstanceId, deserialized.InstanceId);
        Assert.HasCount(message.DiscoveredMessages.Length, deserialized.DiscoveredMessages);
        for (int i = 0; i < message.DiscoveredMessages.Length; i++)
        {
            DiscoveredTestMessage expected = message.DiscoveredMessages[i];
            DiscoveredTestMessage actual = deserialized.DiscoveredMessages[i];
            Assert.AreEqual(expected.Uid, actual.Uid);
            Assert.AreEqual(expected.DisplayName, actual.DisplayName);
            Assert.AreEqual(expected.FilePath, actual.FilePath);
            Assert.AreEqual(expected.LineNumber, actual.LineNumber);
            Assert.AreEqual(expected.Namespace, actual.Namespace);
            Assert.AreEqual(expected.TypeName, actual.TypeName);
            Assert.AreEqual(expected.MethodName, actual.MethodName);

            Assert.IsNotNull(expected.ParameterTypeFullNames);
            Assert.IsNotNull(actual.ParameterTypeFullNames);
            Assert.HasCount(expected.ParameterTypeFullNames.Length, actual.ParameterTypeFullNames);
            for (int j = 0; j < expected.ParameterTypeFullNames.Length; j++)
            {
                Assert.AreEqual(expected.ParameterTypeFullNames[j], actual.ParameterTypeFullNames[j]);
            }

            Assert.HasCount(expected.Traits.Length, actual.Traits);
            for (int j = 0; j < expected.Traits.Length; j++)
            {
                Assert.AreEqual(expected.Traits[j].Key, actual.Traits[j].Key);
                Assert.AreEqual(expected.Traits[j].Value, actual.Traits[j].Value);
            }
        }
    }

    [TestMethod]
    public void HandshakeMessageWithProperties()
    {
        var message = new HandshakeMessage(new Dictionary<byte, string>
        {
            { 10, "Ten" },
            { 35, "ThirtyFive" },
            { 48, "FortyEight" },
        });

        var stream = new MemoryStream();
        new HandshakeMessageSerializer().Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)new HandshakeMessageSerializer().Deserialize(stream);

        Assert.IsNotNull(actual.Properties);
        Assert.IsNotNull(message.Properties);

        Assert.HasCount(3, actual.Properties);
        Assert.HasCount(3, message.Properties);

        Assert.AreEqual("Ten", actual.Properties[10]);
        Assert.AreEqual("Ten", message.Properties[10]);

        Assert.AreEqual("ThirtyFive", actual.Properties[35]);
        Assert.AreEqual("ThirtyFive", message.Properties[35]);

        Assert.AreEqual("FortyEight", actual.Properties[48]);
        Assert.AreEqual("FortyEight", message.Properties[48]);
    }

    [TestMethod]
    public void HandshakeMessageWithEmptyProperties()
    {
        var message = new HandshakeMessage([]);

        var stream = new MemoryStream();
        new HandshakeMessageSerializer().Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)new HandshakeMessageSerializer().Deserialize(stream);

        Assert.IsNotNull(actual.Properties);
        Assert.IsNotNull(message.Properties);

        Assert.IsEmpty(actual.Properties);
        Assert.IsEmpty(message.Properties);
    }

    [TestMethod]
    public void HandshakeMessageWithNullProperties()
    {
        var message = new HandshakeMessage(null);

        var stream = new MemoryStream();
        new HandshakeMessageSerializer().Serialize(message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)new HandshakeMessageSerializer().Deserialize(stream);

        Assert.IsNotNull(actual.Properties);
        Assert.IsNull(message.Properties);

        Assert.IsEmpty(actual.Properties);
    }
}
