// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.DotnetTestProtocolContract.UnitTests;

/// <summary>
/// Verifies the shared 'dotnet test' wire contract (serializer/field ids in <c>ObjectFieldIds</c> + the
/// handshake/session/state values in <c>Constants</c>), compiled into this independent assembly via
/// <c>DotnetTestProtocolContract.props</c> alongside the rest of the shared serializer stack.
/// </summary>
/// <remarks>
/// This project does not reference Microsoft.Testing.Platform's protocol types; it compiles the same source files
/// (the whole serializer stack for ids 0-12 - see <see cref="DotnetTestProtocolSerializerTests"/> for the round-trip
/// coverage). The assertions here pin every value that flows over the pipe, so an accidental change to the contract -
/// or drift from the copy dotnet/sdk consumes from this same source - fails the build here. The fact that this
/// assembly compiles at all is the proof that the contract is self-contained and consumable with a single source of
/// truth.
/// </remarks>
[TestClass]
public sealed class DotnetTestProtocolContractTests
{
    // Uniqueness of the serializer ids is enforced by the assertions below, not by the indexer
    // initializer (a duplicate id would silently overwrite the earlier entry). This test is mirrored
    // by ProtocolTests.SerializerIds_AreStable in Microsoft.Testing.Platform.UnitTests; keep both in sync.
    [TestMethod]
    public void SerializerIds_AreStable()
    {
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
            [AzureDevOpsLogMessageFieldsId.MessagesSerializerId] = nameof(AzureDevOpsLogMessageFieldsId),
            [DisplayMessageFieldsId.MessagesSerializerId] = nameof(DisplayMessageFieldsId),
            [WaitForServerControlRequestFieldsId.MessagesSerializerId] = nameof(WaitForServerControlRequestFieldsId),
            [ServerControlMessageFieldsId.MessagesSerializerId] = nameof(ServerControlMessageFieldsId),
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
        Assert.AreEqual(nameof(AzureDevOpsLogMessageFieldsId), serializerIds[11]);
        Assert.AreEqual(nameof(DisplayMessageFieldsId), serializerIds[12]);
        Assert.AreEqual(nameof(WaitForServerControlRequestFieldsId), serializerIds[13]);
        Assert.AreEqual(nameof(ServerControlMessageFieldsId), serializerIds[14]);
    }

    [TestMethod]
    public void HandshakeMessagePropertyNames_AreStable()
    {
        Dictionary<byte, string> properties = new()
        {
            [HandshakeMessagePropertyNames.PID] = nameof(HandshakeMessagePropertyNames.PID),
            [HandshakeMessagePropertyNames.Architecture] = nameof(HandshakeMessagePropertyNames.Architecture),
            [HandshakeMessagePropertyNames.Framework] = nameof(HandshakeMessagePropertyNames.Framework),
            [HandshakeMessagePropertyNames.OS] = nameof(HandshakeMessagePropertyNames.OS),
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = nameof(HandshakeMessagePropertyNames.SupportedProtocolVersions),
            [HandshakeMessagePropertyNames.HostType] = nameof(HandshakeMessagePropertyNames.HostType),
            [HandshakeMessagePropertyNames.ModulePath] = nameof(HandshakeMessagePropertyNames.ModulePath),
            [HandshakeMessagePropertyNames.ExecutionId] = nameof(HandshakeMessagePropertyNames.ExecutionId),
            [HandshakeMessagePropertyNames.InstanceId] = nameof(HandshakeMessagePropertyNames.InstanceId),
            [HandshakeMessagePropertyNames.IsIDE] = nameof(HandshakeMessagePropertyNames.IsIDE),
            [HandshakeMessagePropertyNames.ExecutionMode] = nameof(HandshakeMessagePropertyNames.ExecutionMode),
            [HandshakeMessagePropertyNames.OrchestratorFeature] = nameof(HandshakeMessagePropertyNames.OrchestratorFeature),
            [HandshakeMessagePropertyNames.ServerControlPipeName] = nameof(HandshakeMessagePropertyNames.ServerControlPipeName),
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
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.OrchestratorFeature), properties[11]);
        Assert.AreEqual(nameof(HandshakeMessagePropertyNames.ServerControlPipeName), properties[12]);
    }

    [TestMethod]
    public void HandshakeMessageExecutionModes_AreStable()
    {
        string[] modes = [HandshakeMessageExecutionModes.Run, HandshakeMessageExecutionModes.Help, HandshakeMessageExecutionModes.Discover];

        Assert.AreEqual("run", modes[0]);
        Assert.AreEqual("help", modes[1]);
        Assert.AreEqual("discover", modes[2]);
    }

    [TestMethod]
    public void SessionEventTypes_AreStable()
    {
        Dictionary<byte, string> sessionEventTypes = new()
        {
            [SessionEventTypes.TestSessionStart] = nameof(SessionEventTypes.TestSessionStart),
            [SessionEventTypes.TestSessionEnd] = nameof(SessionEventTypes.TestSessionEnd),
        };

        Assert.AreEqual(nameof(SessionEventTypes.TestSessionStart), sessionEventTypes[0]);
        Assert.AreEqual(nameof(SessionEventTypes.TestSessionEnd), sessionEventTypes[1]);
    }

    [TestMethod]
    public void TestStates_AreStable()
    {
        Dictionary<byte, string> testStates = new()
        {
            [TestStates.Discovered] = nameof(TestStates.Discovered),
            [TestStates.Passed] = nameof(TestStates.Passed),
            [TestStates.Skipped] = nameof(TestStates.Skipped),
            [TestStates.Failed] = nameof(TestStates.Failed),
            [TestStates.Error] = nameof(TestStates.Error),
            [TestStates.Timeout] = nameof(TestStates.Timeout),
            [TestStates.Cancelled] = nameof(TestStates.Cancelled),
            [TestStates.InProgress] = nameof(TestStates.InProgress),
        };

        Assert.AreEqual(nameof(TestStates.Discovered), testStates[0]);
        Assert.AreEqual(nameof(TestStates.Passed), testStates[1]);
        Assert.AreEqual(nameof(TestStates.Skipped), testStates[2]);
        Assert.AreEqual(nameof(TestStates.Failed), testStates[3]);
        Assert.AreEqual(nameof(TestStates.Error), testStates[4]);
        Assert.AreEqual(nameof(TestStates.Timeout), testStates[5]);
        Assert.AreEqual(nameof(TestStates.Cancelled), testStates[6]);
        Assert.AreEqual(nameof(TestStates.InProgress), testStates[7]);
    }

    [TestMethod]
    public void ProtocolVersion_IsStable()
    {
        // Indirect through a collection so the MSTest analyzer does not flag the comparison of a compile-time
        // constant as "always true" (MSTEST0032).
        string[] versions = [ProtocolConstants.SupportedVersions];
        Assert.AreEqual("1.0.0;1.1.0;1.2.0;1.3.0;1.4.0", versions[0]);
    }
}
