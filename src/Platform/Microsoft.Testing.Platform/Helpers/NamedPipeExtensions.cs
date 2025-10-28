// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;
using EasyNamedPipes.GeneratedSerializers.TestHostProtocol;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.Helpers;

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

[Embedded]
internal static class NamedPipeExtensions
{
    public static void RegisterPlatformSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(VoidResponseSerializer.Instance, typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(TestHostProcessExitRequestSerializer.Instance, typeof(TestHostProcessExitRequest));
        namedPipeBase.RegisterSerializer(TestHostProcessPIDRequestSerializer.Instance, typeof(TestHostProcessPIDRequest));
        namedPipeBase.RegisterSerializer(CommandLineOptionMessagesSerializer.Instance, typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(DiscoveredTestMessagesSerializer.Instance, typeof(DiscoveredTestMessages));
        namedPipeBase.RegisterSerializer(TestResultMessagesSerializer.Instance, typeof(TestResultMessages));
        namedPipeBase.RegisterSerializer(FileArtifactMessagesSerializer.Instance, typeof(FileArtifactMessages));
        namedPipeBase.RegisterSerializer(TestSessionEventSerializer.Instance, typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(HandshakeMessageSerializer.Instance, typeof(HandshakeMessage));
    }
}
