// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class SessionEndSerializerRequest() : IRequest;

internal sealed class SessionEndSerializerRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 2;

    public object Deserialize(Stream stream)
        => new SessionEndSerializerRequest();

    public void Serialize(object _, Stream __)
    {
    }
}
