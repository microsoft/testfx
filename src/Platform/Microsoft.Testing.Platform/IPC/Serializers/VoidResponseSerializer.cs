// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
internal sealed class VoidResponseSerializer : INamedPipeSerializer
{
    public int Id => SerializerIds.VoidResponseSerializerId;

    public object Deserialize(Stream _)
        => new VoidResponse();

    public void Serialize(object _, Stream __)
    {
    }
}
