// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostProcessPIDRequestSerializer : NamedPipeSerializer<TestHostProcessPIDRequest>, INamedPipeSerializer
{
    public override int Id => TestHostProcessPIDRequestFieldsId.MessagesSerializerId;

    protected override TestHostProcessPIDRequest DeserializeCore(Stream stream)
        => new(ReadInt(stream));

    protected override void SerializeCore(TestHostProcessPIDRequest objectToSerialize, Stream stream)
        => WriteInt(stream, objectToSerialize.PID);
}
