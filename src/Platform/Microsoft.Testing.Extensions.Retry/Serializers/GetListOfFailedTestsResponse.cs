// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class GetListOfFailedTestsResponse(string[] failedTestIds) : IResponse
{
    public string[] FailedTestIds { get; } = failedTestIds;
}

internal sealed class GetListOfFailedTestsResponseSerializer : NamedPipeSerializer<GetListOfFailedTestsResponse>, INamedPipeSerializer
{
    public override int Id => 3;

    protected override GetListOfFailedTestsResponse DeserializeCore(Stream stream)
    {
        int totalFailedTests = ReadInt(stream);

        string[] testsId = new string[totalFailedTests];
        for (int i = 0; i < totalFailedTests; i++)
        {
            testsId[i] = ReadString(stream);
        }

        return new GetListOfFailedTestsResponse(testsId);
    }

    protected override void SerializeCore(GetListOfFailedTestsResponse objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.FailedTestIds.Length);
        foreach (string testId in objectToSerialize.FailedTestIds)
        {
            WriteString(stream, testId);
        }
    }
}
