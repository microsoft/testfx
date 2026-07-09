// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class WaitForServerControlRequestSerializer : NamedPipeSerializer<WaitForServerControlRequest>, INamedPipeSerializer
{
    public override int Id => WaitForServerControlRequestFieldsId.MessagesSerializerId;

    protected override WaitForServerControlRequest DeserializeCore(Stream _)
        => WaitForServerControlRequest.CachedInstance;

    protected override void SerializeCore(WaitForServerControlRequest _, Stream __)
    {
    }
}
