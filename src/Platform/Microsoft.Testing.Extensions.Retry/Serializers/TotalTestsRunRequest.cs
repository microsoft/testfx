// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class TotalTestsRunRequest(int totalTests) : IRequest
{
    public int TotalTests { get; } = totalTests;
}

internal sealed class TotalTestsRunRequestSerializer : NamedPipeSerializer<TotalTestsRunRequest>, INamedPipeSerializer
{
    public override int Id => 4;

    protected override TotalTestsRunRequest DeserializeCore(Stream stream)
        => new(ReadInt(stream));

    protected override void SerializeCore(TotalTestsRunRequest objectToSerialize, Stream stream)
        => WriteInt(stream, objectToSerialize.TotalTests);
}
