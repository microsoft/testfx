// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class ConsumerPipeNameRequest(string pipeName) : IRequest
{
    public string PipeName { get; } = pipeName;
}

internal sealed class ConsumerPipeNameRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 3;

    public object Deserialize(Stream stream)
    {
        string mutexName = ReadString(stream);
        return new ConsumerPipeNameRequest(mutexName);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var request = (ConsumerPipeNameRequest)objectToSerialize;
        WriteString(stream, request.PipeName);
    }
}
