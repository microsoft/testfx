// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
 * NOTE: We have the following ids used for those serializers
 * DO NOT change the IDs of the existing serializers
 * VoidResponseSerializer: 0
 * TestHostCompletedRequestSerializer: 1
 * TestHostProcessPIDRequestSerializer: 2
 * CommandLineOptionMessagesSerializer: 3
 * (4 is reserved - previously used by a removed serializer)
 * DiscoveredTestMessagesSerializer: 5
 * TestResultMessagesSerializer: 6
 * FileArtifactMessagesSerializer: 7
 * TestSessionEventSerializer: 8
 * HandshakeMessageSerializer: 9
 * TestInProgressMessagesSerializer: 10
 * AzureDevOpsLogMessageSerializer: 11
 * DisplayMessageSerializer: 12
 * WaitForServerControlRequestSerializer: 13
 * ServerControlMessageSerializer: 14
*/

[Embedded]
internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(new TestHostCompletedRequestSerializer(), typeof(TestHostCompletedRequest));
        namedPipeBase.RegisterSerializer(new TestHostProcessPIDRequestSerializer(), typeof(TestHostProcessPIDRequest));
        namedPipeBase.RegisterSerializer(new CommandLineOptionMessagesSerializer(), typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(new DiscoveredTestMessagesSerializer(), typeof(DiscoveredTestMessages));
        namedPipeBase.RegisterSerializer(new TestResultMessagesSerializer(), typeof(TestResultMessages));
        namedPipeBase.RegisterSerializer(new FileArtifactMessagesSerializer(), typeof(FileArtifactMessages));
        namedPipeBase.RegisterSerializer(new TestSessionEventSerializer(), typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(new HandshakeMessageSerializer(), typeof(HandshakeMessage));
        namedPipeBase.RegisterSerializer(new TestInProgressMessagesSerializer(), typeof(TestInProgressMessages));
        namedPipeBase.RegisterSerializer(new AzureDevOpsLogMessageSerializer(), typeof(AzureDevOpsLogMessage));
        namedPipeBase.RegisterSerializer(new DisplayMessageSerializer(), typeof(DisplayMessage));
        namedPipeBase.RegisterSerializer(new WaitForServerControlRequestSerializer(), typeof(WaitForServerControlRequest));
        namedPipeBase.RegisterSerializer(new ServerControlMessageSerializer(), typeof(ServerControlMessage));
    }
}
