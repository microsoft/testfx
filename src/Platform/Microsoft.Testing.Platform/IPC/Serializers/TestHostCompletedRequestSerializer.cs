// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostCompletedRequestSerializer : NamedPipeSerializer<TestHostCompletedRequest>, INamedPipeSerializer
{
    public override int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

    protected override TestHostCompletedRequest DeserializeCore(Stream stream)
        => new(ReadInt(stream), ReadInt(stream));

    protected override void SerializeCore(TestHostCompletedRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.ExitCode);
        WriteInt(stream, objectToSerialize.UnfilteredExitCode);
    }
}
