// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal static class RegisterSerializers
{
    public static void RegisterAllSerializers(this NamedPipeBase namedPipeBase)
    {
        namedPipeBase.RegisterSerializer<VoidResponse>(new VoidResponseSerializer());
        namedPipeBase.RegisterSerializer<TestHostProcessExitRequest>(new TestHostProcessExitRequestSerializer());
        namedPipeBase.RegisterSerializer<TestHostProcessPIDRequest>(new TestHostProcessPIDRequestSerializer());
    }
}
