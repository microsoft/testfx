// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class ConsumerPipeNameRequest(string pipeName) : IRequest
{
    public string PipeName { get; } = pipeName;
}

internal sealed class ConsumerPipeNameRequestSerializer : NamedPipeSerializer<ConsumerPipeNameRequest>, INamedPipeSerializer
{
    public override int Id => 3;

    protected override ConsumerPipeNameRequest DeserializeCore(Stream stream)
        => new(ReadString(stream));

    protected override void SerializeCore(ConsumerPipeNameRequest objectToSerialize, Stream stream)
        => WriteString(stream, objectToSerialize.PipeName);
}
