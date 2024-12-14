// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class GetListOfFailedTestsResponse(string[] failedTestIds) : IResponse
{
    public string[] FailedTestIds { get; } = failedTestIds;
}

internal sealed class GetListOfFailedTestsResponseSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 3;

    public object Deserialize(Stream stream)
    {
        int totalFailedTests = ReadInt(stream);

        string[] testsId = new string[totalFailedTests];
        for (int i = 0; i < totalFailedTests; i++)
        {
            testsId[i] = ReadString(stream);
        }

        return new GetListOfFailedTestsResponse(testsId);
    }

    public void Serialize(object obj, Stream stream)
    {
        var getListOfFailedTestsResponse = (GetListOfFailedTestsResponse)obj;
        WriteInt(stream, getListOfFailedTestsResponse.FailedTestIds.Length);
        foreach (string testId in getListOfFailedTestsResponse.FailedTestIds)
        {
            WriteString(stream, testId);
        }
    }
}
