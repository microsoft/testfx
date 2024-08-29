// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class TestHostProcessPIDRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestHostProcessPIDRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        int pid = ReadInt(stream);
        return new TestHostProcessPIDRequest(pid);
    }

    public void Serialize(object obj, Stream stream)
    {
        var testHostProcessPIDRequest = (TestHostProcessPIDRequest)obj;
        WriteInt(stream, testHostProcessPIDRequest.PID);
    }
}
