// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostCompletedRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

    public object Deserialize(Stream stream)
    {
        return new TestHostCompletedRequest();
    }

    public void Serialize(object obj, Stream stream)
    {
    }
}