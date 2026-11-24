// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class GetListOfFailedTestsRequest : IRequest;

internal sealed class GetListOfFailedTestsRequestSerializer : NamedPipeSerializer<GetListOfFailedTestsRequest>, INamedPipeSerializer
{
    public override int Id => 2;

    protected override GetListOfFailedTestsRequest DeserializeCore(Stream stream) => new();

    protected override void SerializeCore(GetListOfFailedTestsRequest objectToSerialize, Stream stream)
    {
    }
}
