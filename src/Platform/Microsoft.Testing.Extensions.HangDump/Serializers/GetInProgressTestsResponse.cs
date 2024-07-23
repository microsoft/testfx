// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class GetInProgressTestsResponse(IReadOnlyList<(string, int)> tests) : IResponse
{
    public IReadOnlyList<(string, int)> Tests { get; } = tests;
}

internal sealed class GetInProgressTestsResponseSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 5;

    public object Deserialize(Stream stream)
    {
        int readCount = ReadInt(stream);
        List<(string, int)> tests = new(readCount);
        for (int i = 0; i < readCount; i++)
        {
            string testName = ReadString(stream);
            int unixTimeSeconds = ReadInt(stream);
            tests.Add((testName, unixTimeSeconds));
        }

        return new GetInProgressTestsResponse(tests);
    }

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var getInProgressTestsResponse = (GetInProgressTestsResponse)objectToSerialize;
        WriteInt(stream, getInProgressTestsResponse.Tests.Count);
        foreach ((string testName, int seconds) in getInProgressTestsResponse.Tests)
        {
            WriteString(stream, testName);
            WriteInt(stream, seconds);
        }
    }
}
