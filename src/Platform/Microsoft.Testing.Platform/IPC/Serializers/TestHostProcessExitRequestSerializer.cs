// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class TestHostProcessExitRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestHostProcessExitRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        int exitCode = ReadInt(stream);
        return new TestHostProcessExitRequest(exitCode);
    }

    public void Serialize(object obj, Stream stream)
    {
        var testHostProcessExitRequest = (TestHostProcessExitRequest)obj;
        WriteInt(stream, testHostProcessExitRequest.ExitCode);
    }
}
