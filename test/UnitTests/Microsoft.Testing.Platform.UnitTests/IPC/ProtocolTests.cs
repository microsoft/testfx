// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ProtocolTests
{
    [TestMethod]
    public void TestResultMessagesSerializeDeserialize()
    {
        object serializer = new TestResultMessagesSerializer();
        var success = new SuccessfulTestResultMessage("uid", "displayName", 1, 100, "reason", "standardOutput", "errorOutput", "sessionUid");
        var fail = new FailedTestResultMessage("uid", "displayName", 2, 200, "reason", [new ExceptionMessage("errorMessage", "errorType", "stackTrace")], "standardOutput", "errorOutput", "sessionUid");
        var message = new TestResultMessages("executionId", "instanceId", [success], [fail]);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestResultMessages)Deserialize(serializer, stream);
        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
#if NET8_0_OR_GREATER
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(message), System.Text.Json.JsonSerializer.Serialize(actual));
#endif
        Assert.AreEqual(message.FailedTestMessages?[0].Exceptions?[0].ErrorMessage, actual.FailedTestMessages?[0].Exceptions?[0].ErrorMessage);
    }

    [TestMethod]
    public void DiscoveredTestMessagesSerializeDeserialize()
    {
        object serializer = new DiscoveredTestMessagesSerializer();
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

        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);

        var deserialized = (DiscoveredTestMessages)Deserialize(serializer, stream);
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
        object serializer = new HandshakeMessageSerializer();
        var message = new HandshakeMessage(new Dictionary<byte, string>
        {
            { 10, "Ten" },
            { 35, "ThirtyFive" },
            { 48, "FortyEight" },
        });

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)Deserialize(serializer, stream);

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
        object serializer = new HandshakeMessageSerializer();
        var message = new HandshakeMessage([]);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)Deserialize(serializer, stream);

        Assert.IsNotNull(actual.Properties);
        Assert.IsNotNull(message.Properties);

        Assert.IsEmpty(actual.Properties);
        Assert.IsEmpty(message.Properties);
    }

    [TestMethod]
    public void HandshakeMessageWithNullProperties()
    {
        object serializer = new HandshakeMessageSerializer();
        var message = new HandshakeMessage(null);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (HandshakeMessage)Deserialize(serializer, stream);

        Assert.IsNotNull(actual.Properties);
        Assert.IsNull(message.Properties);

        Assert.IsEmpty(actual.Properties);
    }

    // The HandshakeMessagePropertyNames values are part of the wire protocol
    // shared with dotnet test in the dotnet/sdk repository. Changing any
    // existing value is a binary-breaking change for older platform <-> SDK
    // pairings, so it is intentionally pinned by this test.
    [TestMethod]
    public void HandshakeMessagePropertyNames_ValuesAreStable()
    {
        // Indirect the comparisons through a dictionary lookup so the MSTest analyzer
        // does not flag compile-time constant comparisons as "always true / always failing".
        Dictionary<byte, string> properties = new()
        {
            { HandshakeMessagePropertyNames.PID, nameof(HandshakeMessagePropertyNames.PID) },
            { HandshakeMessagePropertyNames.Architecture, nameof(HandshakeMessagePropertyNames.Architecture) },
            { HandshakeMessagePropertyNames.Framework, nameof(HandshakeMessagePropertyNames.Framework) },
            { HandshakeMessagePropertyNames.OS, nameof(HandshakeMessagePropertyNames.OS) },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, nameof(HandshakeMessagePropertyNames.SupportedProtocolVersions) },
            { HandshakeMessagePropertyNames.HostType, nameof(HandshakeMessagePropertyNames.HostType) },
            { HandshakeMessagePropertyNames.ModulePath, nameof(HandshakeMessagePropertyNames.ModulePath) },
            { HandshakeMessagePropertyNames.ExecutionId, nameof(HandshakeMessagePropertyNames.ExecutionId) },
            { HandshakeMessagePropertyNames.InstanceId, nameof(HandshakeMessagePropertyNames.InstanceId) },
            { HandshakeMessagePropertyNames.IsIDE, nameof(HandshakeMessagePropertyNames.IsIDE) },
            { HandshakeMessagePropertyNames.ExecutionMode, nameof(HandshakeMessagePropertyNames.ExecutionMode) },
        };

        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.PID), properties[0]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.Architecture), properties[1]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.Framework), properties[2]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.OS), properties[3]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.SupportedProtocolVersions), properties[4]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.HostType), properties[5]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.ModulePath), properties[6]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.ExecutionId), properties[7]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.InstanceId), properties[8]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.IsIDE), properties[9]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.ExecutionMode), properties[10]);
    }

    // The HandshakeMessageExecutionModes string values flow over IPC to
    // dotnet test in the dotnet/sdk repository and are compared by value
    // there. Renaming any of them is a wire-protocol break.
    [TestMethod]
    public void HandshakeMessageExecutionModes_ValuesAreStable()
    {
        // Indirect the comparisons through a collection so the MSTest analyzer
        // does not flag string compile-time equalities as "always true".
        string[] modes = [HandshakeMessageExecutionModes.Run, HandshakeMessageExecutionModes.Help, HandshakeMessageExecutionModes.Discover];

        Assert.AreEqual("run", modes[0]);
        Assert.AreEqual("help", modes[1]);
        Assert.AreEqual("discover", modes[2]);
    }

    [TestMethod]
    public void TestInProgressMessagesSerializeDeserialize()
    {
        object serializer = new TestInProgressMessagesSerializer();

        var message = new TestInProgressMessages(
            "MyExecId",
            "MyInstId",
            [
                new TestInProgressMessage("uid-1", "Display 1"),
                new TestInProgressMessage("uid-2", null),
                new TestInProgressMessage(null, "Display 3"),
            ]);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestInProgressMessages)Deserialize(serializer, stream);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.HasCount(message.InProgressMessages.Length, actual.InProgressMessages);
        for (int i = 0; i < message.InProgressMessages.Length; i++)
        {
            Assert.AreEqual(message.InProgressMessages[i].Uid, actual.InProgressMessages[i].Uid);
            Assert.AreEqual(message.InProgressMessages[i].DisplayName, actual.InProgressMessages[i].DisplayName);
        }
    }

    [TestMethod]
    public void TestInProgressMessagesSerializeDeserialize_EmptyList()
    {
        object serializer = new TestInProgressMessagesSerializer();

        var message = new TestInProgressMessages("execId", "instId", []);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestInProgressMessages)Deserialize(serializer, stream);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.IsNotNull(actual.InProgressMessages);
        Assert.HasCount(0, actual.InProgressMessages);
    }

    [TestMethod]
    public void CommandLineOptionMessagesSerializeDeserialize()
    {
        object serializer = new CommandLineOptionMessagesSerializer();
        var message = new CommandLineOptionMessages(
            "path/to/module.dll",
            [
                new CommandLineOptionMessage("filter", "Filters the tests", false, true),
                new CommandLineOptionMessage("hidden-option", null, true, false),
                new CommandLineOptionMessage(null, "no name", null, null),
            ]);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (CommandLineOptionMessages)Deserialize(serializer, stream);

        Assert.AreEqual(message.ModulePath, actual.ModulePath);
        Assert.IsNotNull(message.CommandLineOptionMessageList);
        Assert.IsNotNull(actual.CommandLineOptionMessageList);
        Assert.HasCount(message.CommandLineOptionMessageList.Length, actual.CommandLineOptionMessageList);
        for (int i = 0; i < message.CommandLineOptionMessageList.Length; i++)
        {
            CommandLineOptionMessage expected = message.CommandLineOptionMessageList[i];
            CommandLineOptionMessage actualOption = actual.CommandLineOptionMessageList[i];
            Assert.AreEqual(expected.Name, actualOption.Name);
            Assert.AreEqual(expected.Description, actualOption.Description);
            Assert.AreEqual(expected.IsHidden, actualOption.IsHidden);
            Assert.AreEqual(expected.IsBuiltIn, actualOption.IsBuiltIn);
        }
    }

    [TestMethod]
    public void FileArtifactMessagesSerializeDeserialize()
    {
        object serializer = new FileArtifactMessagesSerializer();
        var message = new FileArtifactMessages(
            "MyExecId",
            "MyInstId",
            [
                new FileArtifactMessage("/full/path/artifact1.txt", "artifact1", "description1", "uid-1", "Test 1", "session-1"),
                new FileArtifactMessage("/full/path/artifact2.coverage", "artifact2", null, null, null, null),
            ]);

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (FileArtifactMessages)Deserialize(serializer, stream);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.HasCount(message.FileArtifacts.Length, actual.FileArtifacts);
        for (int i = 0; i < message.FileArtifacts.Length; i++)
        {
            FileArtifactMessage expected = message.FileArtifacts[i];
            FileArtifactMessage actualArtifact = actual.FileArtifacts[i];
            Assert.AreEqual(expected.FullPath, actualArtifact.FullPath);
            Assert.AreEqual(expected.DisplayName, actualArtifact.DisplayName);
            Assert.AreEqual(expected.Description, actualArtifact.Description);
            Assert.AreEqual(expected.TestUid, actualArtifact.TestUid);
            Assert.AreEqual(expected.TestDisplayName, actualArtifact.TestDisplayName);
            Assert.AreEqual(expected.SessionUid, actualArtifact.SessionUid);
        }
    }

    [TestMethod]
    public void TestSessionEventSerializeDeserialize()
    {
        object serializer = new TestSessionEventSerializer();
        var message = new TestSessionEvent(SessionEventTypes.TestSessionStart, "session-uid", "exec-id");

        var stream = new MemoryStream();
        Serialize(serializer, message, stream);
        stream.Seek(0, SeekOrigin.Begin);
        var actual = (TestSessionEvent)Deserialize(serializer, stream);

        Assert.AreEqual(message.SessionType, actual.SessionType);
        Assert.AreEqual(message.SessionUid, actual.SessionUid);
        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
    }

    // The serializer ids registered in RegisterSerializers flow over IPC to dotnet test in the
    // dotnet/sdk repository (see ObjectFieldIds.cs, which carries a "must be kept aligned" warning).
    // Changing any existing id is a wire-protocol break for older platform <-> SDK pairings, so the
    // full registry is intentionally pinned here. Note id 4 is permanently reserved (was ModuleSerializer).
    [TestMethod]
    public void SerializerIds_AreStable()
    {
        // Building the map through the constants (instead of asserting on the literals directly)
        // keeps the MSTest analyzer from flagging compile-time constant comparisons, and the
        // dictionary initializer additionally guarantees every id is unique.
        Dictionary<int, string> serializerIds = new()
        {
            [VoidResponseFieldsId.MessagesSerializerId] = nameof(VoidResponseFieldsId),
            [TestHostCompletedRequestFieldsId.MessagesSerializerId] = nameof(TestHostCompletedRequestFieldsId),
            [TestHostProcessPIDRequestFieldsId.MessagesSerializerId] = nameof(TestHostProcessPIDRequestFieldsId),
            [CommandLineOptionMessagesFieldsId.MessagesSerializerId] = nameof(CommandLineOptionMessagesFieldsId),
            [DiscoveredTestMessagesFieldsId.MessagesSerializerId] = nameof(DiscoveredTestMessagesFieldsId),
            [TestResultMessagesFieldsId.MessagesSerializerId] = nameof(TestResultMessagesFieldsId),
            [FileArtifactMessagesFieldsId.MessagesSerializerId] = nameof(FileArtifactMessagesFieldsId),
            [TestSessionEventFieldsId.MessagesSerializerId] = nameof(TestSessionEventFieldsId),
            [HandshakeMessageFieldsId.MessagesSerializerId] = nameof(HandshakeMessageFieldsId),
            [TestInProgressMessagesFieldsId.MessagesSerializerId] = nameof(TestInProgressMessagesFieldsId),
        };

        Assert.AreEqual(nameof(VoidResponseFieldsId), serializerIds[0]);
        Assert.AreEqual(nameof(TestHostCompletedRequestFieldsId), serializerIds[1]);
        Assert.AreEqual(nameof(TestHostProcessPIDRequestFieldsId), serializerIds[2]);
        Assert.AreEqual(nameof(CommandLineOptionMessagesFieldsId), serializerIds[3]);
        // Id 4 is reserved (formerly ModuleSerializer) and must never be reused.
        Assert.IsFalse(serializerIds.ContainsKey(4), "Serializer id 4 is reserved and must not be reassigned.");
        Assert.AreEqual(nameof(DiscoveredTestMessagesFieldsId), serializerIds[5]);
        Assert.AreEqual(nameof(TestResultMessagesFieldsId), serializerIds[6]);
        Assert.AreEqual(nameof(FileArtifactMessagesFieldsId), serializerIds[7]);
        Assert.AreEqual(nameof(TestSessionEventFieldsId), serializerIds[8]);
        Assert.AreEqual(nameof(HandshakeMessageFieldsId), serializerIds[9]);
        Assert.AreEqual(nameof(TestInProgressMessagesFieldsId), serializerIds[10]);
    }

    private static void Serialize<TMessage>(object serializer, TMessage message, Stream stream)
        => serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(Serialize) && method.GetParameters() is [{ ParameterType: var messageType }, { ParameterType: var streamType }] && messageType == typeof(TMessage) && streamType == typeof(Stream))
            .Invoke(serializer, [message!, stream]);

    private static object Deserialize(object serializer, Stream stream)
        => serializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(Deserialize) && method.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(Stream))
            .Invoke(serializer, [stream])!;
}
