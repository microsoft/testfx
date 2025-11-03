// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class VoidResponseSerializer : INamedPipeSerializer
{
    public int Id => VoidResponseFieldsId.MessagesSerializerId;

    public object Deserialize(Stream _)
        => new VoidResponse();

    public void Serialize(object _, Stream __)
    {
    }
}
