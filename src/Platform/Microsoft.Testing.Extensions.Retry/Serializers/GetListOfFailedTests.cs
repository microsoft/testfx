// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class GetListOfFailedTestsRequest : IRequest;

internal sealed class GetListOfFailedTestsRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 2;

    public object Deserialize(Stream stream) => new GetListOfFailedTestsRequest();

    public void Serialize(object obj, Stream stream)
    {
    }
}
