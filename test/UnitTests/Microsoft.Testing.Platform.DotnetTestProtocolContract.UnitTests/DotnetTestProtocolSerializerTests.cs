// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests;

/// <summary>
/// Round-trips the shared 'dotnet test' message serializers (ids 0-12) - models, serializers, the serializer
/// registry (<see cref="NamedPipeBase"/> + <see cref="RegisterSerializers"/>) and the decoupled
/// <see cref="BaseSerializer"/> - compiled into this independent assembly via <c>DotnetTestProtocolContract.props</c>.
/// </summary>
/// <remarks>
/// This project does NOT reference Microsoft.Testing.Platform; it compiles the same source files. The fact that this
/// assembly compiles at all proves the whole serializer stack is self-contained; the round-trips below prove the wire
/// bytes survive a serialize/deserialize cycle in a consumer with a single source of truth. This mirrors
/// <c>ProtocolTests</c> in Microsoft.Testing.Platform.UnitTests; keep both in sync.
/// </remarks>
[TestClass]
public sealed class DotnetTestProtocolSerializerTests
{
    private static T RoundTrip<T>(NamedPipeSerializer<T> serializer, T message)
        where T : notnull
    {
        using var stream = new MemoryStream();
        serializer.Serialize(message, stream);
        stream.Position = 0;
        return serializer.Deserialize(stream);
    }

    // Exposes the protected registry lookup so the test can assert every id 0-12 is registered.
    // Exposes the protected registry lookups so the test can assert both the id-based mapping (every id 0-12 is
    // registered) and the type-based mapping (GetSerializer(Type), used by the runtime serialize path).
    private sealed class SerializerRegistry : NamedPipeBase
    {
        public INamedPipeSerializer Get(int id) => GetSerializer(id);

        public INamedPipeSerializer GetByType(Type type) => GetSerializer(type);
    }

    [TestMethod]
    public void RegisterAllSerializers_RegistersEveryWireId()
    {
        var registry = new SerializerRegistry();
        registry.RegisterAllSerializers();

        Assert.AreEqual(VoidResponseFieldsId.MessagesSerializerId, registry.Get(0).Id);
        Assert.AreEqual(TestHostCompletedRequestFieldsId.MessagesSerializerId, registry.Get(1).Id);
        Assert.AreEqual(TestHostProcessPIDRequestFieldsId.MessagesSerializerId, registry.Get(2).Id);
        Assert.AreEqual(CommandLineOptionMessagesFieldsId.MessagesSerializerId, registry.Get(3).Id);
        Assert.AreEqual(DiscoveredTestMessagesFieldsId.MessagesSerializerId, registry.Get(5).Id);
        Assert.AreEqual(TestResultMessagesFieldsId.MessagesSerializerId, registry.Get(6).Id);
        Assert.AreEqual(FileArtifactMessagesFieldsId.MessagesSerializerId, registry.Get(7).Id);
        Assert.AreEqual(TestSessionEventFieldsId.MessagesSerializerId, registry.Get(8).Id);
        Assert.AreEqual(HandshakeMessageFieldsId.MessagesSerializerId, registry.Get(9).Id);
        Assert.AreEqual(TestInProgressMessagesFieldsId.MessagesSerializerId, registry.Get(10).Id);
        // The two ids that used to be dropped at the SDK because they were never registered.
        Assert.AreEqual(AzureDevOpsLogMessageFieldsId.MessagesSerializerId, registry.Get(11).Id);
        Assert.AreEqual(DisplayMessageFieldsId.MessagesSerializerId, registry.Get(12).Id);

        // Reserved id 4 (formerly ModuleSerializer) must remain unregistered.
        Assert.ThrowsExactly<KeyNotFoundException>(() => registry.Get(4));

        // Pin the concrete serializer TYPE (not just a matching Id value) for the two ids that were being
        // dropped, so an accidental id/type misregistration is caught here too - both via id lookup and via the
        // type-based lookup (GetSerializer(Type)) that the runtime serialize path actually uses.
        Assert.IsInstanceOfType<AzureDevOpsLogMessageSerializer>(registry.Get(11));
        Assert.IsInstanceOfType<DisplayMessageSerializer>(registry.Get(12));
        Assert.IsInstanceOfType<AzureDevOpsLogMessageSerializer>(registry.GetByType(typeof(AzureDevOpsLogMessage)));
        Assert.IsInstanceOfType<DisplayMessageSerializer>(registry.GetByType(typeof(DisplayMessage)));
    }

    [TestMethod]
    public void AzureDevOpsLogMessage_RoundTrips()
    {
        var message = new AzureDevOpsLogMessage("MyExecId", "MyInstId", "##[group]Tests: MyAssembly (net9.0)");

        AzureDevOpsLogMessage actual = RoundTrip(new AzureDevOpsLogMessageSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.AreEqual(message.LogText, actual.LogText);
    }

    [TestMethod]
    public void DisplayMessage_RoundTrips()
    {
        var message = new DisplayMessage("MyExecId", "MyInstId", 2, "A warning from the host");

        DisplayMessage actual = RoundTrip(new DisplayMessageSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.AreEqual(message.Level, actual.Level);
        Assert.AreEqual(message.Text, actual.Text);
    }

    [TestMethod]
    public void HandshakeMessage_RoundTrips()
    {
        var message = new HandshakeMessage(new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.PID] = "1234",
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = ProtocolConstants.SupportedVersions,
        });

        HandshakeMessage actual = RoundTrip(new HandshakeMessageSerializer(), message);

        Assert.IsNotNull(actual.Properties);
        Assert.AreEqual("1234", actual.Properties[HandshakeMessagePropertyNames.PID]);
        Assert.AreEqual(ProtocolConstants.SupportedVersions, actual.Properties[HandshakeMessagePropertyNames.SupportedProtocolVersions]);
    }

    [TestMethod]
    public void TestResultMessages_RoundTrips()
    {
        var success = new SuccessfulTestResultMessage("uid", "displayName", TestStates.Passed, 100, "reason", "standardOutput", "errorOutput", "sessionUid");
        var fail = new FailedTestResultMessage("uid2", "displayName2", TestStates.Failed, 200, "reason", [new ExceptionMessage("errorMessage", "errorType", "stackTrace")], "standardOutput", "errorOutput", "sessionUid");
        var message = new TestResultMessages("executionId", "instanceId", [success], [fail]);

        TestResultMessages actual = RoundTrip(new TestResultMessagesSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.AreEqual(message.InstanceId, actual.InstanceId);
        Assert.AreEqual("uid", actual.SuccessfulTestMessages[0].Uid);
        Assert.AreEqual("errorMessage", actual.FailedTestMessages[0].Exceptions?[0].ErrorMessage);
    }

    [TestMethod]
    public void DiscoveredTestMessages_RoundTripsWithTraitsAndParameters()
    {
        var message = new DiscoveredTestMessages(
            "executionId",
            "instanceId",
            [
                new DiscoveredTestMessage("Uid1", "Display1", "file1.cs", 19, "NS1", "Type1", "TM1", ["p1", "p2"], [new TraitMessage("Key1", "Value1"), new TraitMessage("Key2", string.Empty)]),
                new DiscoveredTestMessage("Uid2", "Display2", null, null, "NS2", "Type2", "TM2", [], []),
            ]);

        DiscoveredTestMessages actual = RoundTrip(new DiscoveredTestMessagesSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.HasCount(2, actual.DiscoveredMessages);

        DiscoveredTestMessage first = actual.DiscoveredMessages[0];
        Assert.AreEqual("Uid1", first.Uid);
        Assert.HasCount(2, first.ParameterTypeFullNames!);
        Assert.HasCount(2, first.Traits);
        Assert.AreEqual("Key1", first.Traits[0].Key);
        Assert.AreEqual("Value1", first.Traits[0].Value);
        Assert.AreEqual("Key2", first.Traits[1].Key);
        Assert.AreEqual(string.Empty, first.Traits[1].Value);

        DiscoveredTestMessage second = actual.DiscoveredMessages[1];
        Assert.AreEqual("Uid2", second.Uid);
        Assert.HasCount(0, second.Traits);
    }

    [TestMethod]
    public void VoidResponse_RoundTrips()
    {
        VoidResponse actual = RoundTrip(new VoidResponseSerializer(), VoidResponse.CachedInstance);

        Assert.IsNotNull(actual);
    }

    [TestMethod]
    public void TestHostCompletedRequest_RoundTrips()
    {
        TestHostCompletedRequest actual = RoundTrip(new TestHostCompletedRequestSerializer(), new TestHostCompletedRequest(42));

        Assert.AreEqual(42, actual.ExitCode);
    }

    [TestMethod]
    public void TestHostProcessPIDRequest_RoundTrips()
    {
        TestHostProcessPIDRequest actual = RoundTrip(new TestHostProcessPIDRequestSerializer(), new TestHostProcessPIDRequest(1234));

        Assert.AreEqual(1234, actual.PID);
    }

    [TestMethod]
    public void CommandLineOptionMessages_RoundTrips()
    {
        var message = new CommandLineOptionMessages(
            "path/to/module.dll",
            [
                new CommandLineOptionMessage("--filter", "Filter tests", false, true),
                new CommandLineOptionMessage("--hidden", null, true, false),
            ]);

        CommandLineOptionMessages actual = RoundTrip(new CommandLineOptionMessagesSerializer(), message);

        Assert.AreEqual(message.ModulePath, actual.ModulePath);
        Assert.HasCount(2, actual.CommandLineOptionMessageList!);
        Assert.AreEqual("--filter", actual.CommandLineOptionMessageList![0].Name);
        Assert.AreEqual("Filter tests", actual.CommandLineOptionMessageList[0].Description);
        Assert.IsFalse(actual.CommandLineOptionMessageList[0].IsHidden);
        Assert.IsTrue(actual.CommandLineOptionMessageList[0].IsBuiltIn);
        Assert.IsNull(actual.CommandLineOptionMessageList[1].Description);
        Assert.IsTrue(actual.CommandLineOptionMessageList[1].IsHidden);
    }

    [TestMethod]
    public void FileArtifactMessages_RoundTrips()
    {
        var message = new FileArtifactMessages(
            "executionId",
            "instanceId",
            [
                new FileArtifactMessage("full/path.txt", "artifact", "desc", "testUid", "testDisplay", "sessionUid"),
                new FileArtifactMessage("other.txt", "other", null, null, null, null),
            ]);

        FileArtifactMessages actual = RoundTrip(new FileArtifactMessagesSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.HasCount(2, actual.FileArtifacts);
        Assert.AreEqual("full/path.txt", actual.FileArtifacts[0].FullPath);
        Assert.AreEqual("sessionUid", actual.FileArtifacts[0].SessionUid);
        Assert.AreEqual("other.txt", actual.FileArtifacts[1].FullPath);
        Assert.IsNull(actual.FileArtifacts[1].Description);
    }

    [TestMethod]
    public void TestSessionEvent_RoundTrips()
    {
        var message = new TestSessionEvent(SessionEventTypes.TestSessionStart, "sessionUid", "executionId");

        TestSessionEvent actual = RoundTrip(new TestSessionEventSerializer(), message);

        Assert.AreEqual(SessionEventTypes.TestSessionStart, actual.SessionType);
        Assert.AreEqual("sessionUid", actual.SessionUid);
        Assert.AreEqual("executionId", actual.ExecutionId);
    }

    [TestMethod]
    public void TestInProgressMessages_RoundTrips()
    {
        var message = new TestInProgressMessages(
            "executionId",
            "instanceId",
            [new TestInProgressMessage("uid1", "display1"), new TestInProgressMessage("uid2", "display2")]);

        TestInProgressMessages actual = RoundTrip(new TestInProgressMessagesSerializer(), message);

        Assert.AreEqual(message.ExecutionId, actual.ExecutionId);
        Assert.HasCount(2, actual.InProgressMessages);
        Assert.AreEqual("uid1", actual.InProgressMessages[0].Uid);
        Assert.AreEqual("display2", actual.InProgressMessages[1].DisplayName);
    }

    [TestMethod]
    public void AzureDevOpsLogMessage_RoundTripsWithNullOptionalFields()
    {
        var message = new AzureDevOpsLogMessage(null, null, "##[endgroup]");

        AzureDevOpsLogMessage actual = RoundTrip(new AzureDevOpsLogMessageSerializer(), message);

        Assert.IsNull(actual.ExecutionId);
        Assert.IsNull(actual.InstanceId);
        Assert.AreEqual("##[endgroup]", actual.LogText);
    }

    [TestMethod]
    public void DisplayMessage_RoundTripsWithNullOptionalFields()
    {
        var message = new DisplayMessage(null, null, 0, null);

        DisplayMessage actual = RoundTrip(new DisplayMessageSerializer(), message);

        Assert.IsNull(actual.ExecutionId);
        Assert.IsNull(actual.InstanceId);
        Assert.AreEqual(0, actual.Level);
        Assert.IsNull(actual.Text);
    }
}
