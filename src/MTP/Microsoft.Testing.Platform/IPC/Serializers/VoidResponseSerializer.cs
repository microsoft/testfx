// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class VoidResponseSerializer : NamedPipeSerializer<VoidResponse>, INamedPipeSerializer
{
    public override int Id => VoidResponseFieldsId.MessagesSerializerId;

    protected override VoidResponse DeserializeCore(Stream _)
        => VoidResponse.CachedInstance;

    protected override void SerializeCore(VoidResponse _, Stream __)
    {
    }
}
