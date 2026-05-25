// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class FailedTestRequest(string uid) : IRequest
{
    public string Uid { get; } = uid;
}

internal sealed class FailedTestRequestSerializer : NamedPipeSerializer<FailedTestRequest>, INamedPipeSerializer
{
    public override int Id => 1;

    protected override FailedTestRequest DeserializeCore(Stream stream)
        => new(ReadString(stream));

    protected override void SerializeCore(FailedTestRequest objectToSerialize, Stream stream)
        => WriteString(stream, objectToSerialize.Uid);
}
