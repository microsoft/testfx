// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

/*
 * NOTE: We have the following ids used for those serializers
 * DO NOT change the IDs of the existing serializers
 * VoidResponseSerializer: 0
 * TestHostProcessExitRequestSerializer: 1
 * TestHostProcessPIDRequestSerializer: 2
 * CommandLineOptionMessagesSerializer: 3
 * ModuleSerializer: 4
 * DiscoveredTestMessageSerializer: 5
 * TestResultMessageSerializer: 6
 * FileArtifactMessageSerializer: 7
 * TestSessionEventSerializer: 8
 * HandshakeMessageSerializer: 9
*/

internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(new TestHostProcessExitRequestSerializer(), typeof(TestHostProcessExitRequest));
        namedPipeBase.RegisterSerializer(new TestHostProcessPIDRequestSerializer(), typeof(TestHostProcessPIDRequest));
        namedPipeBase.RegisterSerializer(new CommandLineOptionMessagesSerializer(), typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(new DiscoveredTestMessagesSerializer(), typeof(DiscoveredTestMessages));
        namedPipeBase.RegisterSerializer(new TestResultMessagesSerializer(), typeof(TestResultMessages));
        namedPipeBase.RegisterSerializer(new FileArtifactMessagesSerializer(), typeof(FileArtifactMessages));
        namedPipeBase.RegisterSerializer(new TestSessionEventSerializer(), typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(new HandshakeMessageSerializer(), typeof(HandshakeMessage));
    }
}
