// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class FailedTestRequest(string uid) : IRequest
{
    public string Uid { get; } = uid;
}

internal sealed class FailedTestRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 1;

    public object Deserialize(Stream stream)
    {
        string uid = ReadString(stream);
        return new FailedTestRequest(uid);
    }

    public void Serialize(object obj, Stream stream)
    {
        var testHostProcessExitRequest = (FailedTestRequest)obj;
        WriteString(stream, testHostProcessExitRequest.Uid);
    }
}
