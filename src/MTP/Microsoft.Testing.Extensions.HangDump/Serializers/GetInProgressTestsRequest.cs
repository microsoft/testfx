// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class GetInProgressTestsRequest : IRequest;

internal sealed class GetInProgressTestsRequestSerializer : NamedPipeSerializer<GetInProgressTestsRequest>, INamedPipeSerializer
{
    public override int Id => 4;

    protected override GetInProgressTestsRequest DeserializeCore(Stream stream) => new();

    protected override void SerializeCore(GetInProgressTestsRequest objectToSerialize, Stream stream)
    {
    }
}
