// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class ActivityIndicatorMutexNameRequest(string mutexName) : IRequest
{
    public string MutexName { get; } = mutexName;
}

internal sealed class ActivityIndicatorMutexNameRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 1;

    public object Deserialize(Stream stream)
    {
        string mutexName = ReadString(stream);
        return new ActivityIndicatorMutexNameRequest(mutexName);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var request = (ActivityIndicatorMutexNameRequest)objectToSerialize;
        WriteString(stream, request.MutexName);
    }
}
