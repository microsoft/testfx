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
 * SuccessfulTestResultMessageSerializer: 5
 * FailedTestResultMessageSerializer: 6
 * FileArtifactInfoSerializer: 7
 * TestSessionEventSerializer: 8
 * HandshakeInfoSerializer: 9
 * DiscoveredTestMessageSerializer: 10
*/

internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        namedPipeBase.RegisterSerializer(new TestHostProcessExitRequestSerializer(), typeof(TestHostProcessExitRequest));
        namedPipeBase.RegisterSerializer(new TestHostProcessPIDRequestSerializer(), typeof(TestHostProcessPIDRequest));
        namedPipeBase.RegisterSerializer(new CommandLineOptionMessagesSerializer(), typeof(CommandLineOptionMessages));
        namedPipeBase.RegisterSerializer(new SuccessfulTestResultMessageSerializer(), typeof(SuccessfulTestResultMessage));
        namedPipeBase.RegisterSerializer(new FailedTestResultMessageSerializer(), typeof(FailedTestResultMessage));
        namedPipeBase.RegisterSerializer(new FileArtifactInfoSerializer(), typeof(FileArtifactInfo));
        namedPipeBase.RegisterSerializer(new TestSessionEventSerializer(), typeof(TestSessionEvent));
        namedPipeBase.RegisterSerializer(new HandshakeInfoSerializer(), typeof(HandshakeInfo));
        namedPipeBase.RegisterSerializer(new DiscoveredTestMessageSerializer(), typeof(DiscoveredTestMessage));
    }
}
