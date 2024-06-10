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
*/
internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());
        namedPipeBase.RegisterSerializer<TestHostProcessExitRequest>(new TestHostProcessExitRequestSerializer());
        namedPipeBase.RegisterSerializer<TestHostProcessPIDRequest>(new TestHostProcessPIDRequestSerializer());
        namedPipeBase.RegisterSerializer<CommandLineOptionMessages>(new CommandLineOptionMessagesSerializer());
    }
}
