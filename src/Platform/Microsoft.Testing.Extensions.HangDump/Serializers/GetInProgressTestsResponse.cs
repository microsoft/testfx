// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class GetInProgressTestsResponse((string, int)[] tests) : IResponse
{
    public (string, int)[] Tests { get; } = tests;
}

internal sealed class GetInProgressTestsResponseSerializer : NamedPipeSerializer<GetInProgressTestsResponse>, INamedPipeSerializer
{
    public override int Id => 5;

    protected override GetInProgressTestsResponse DeserializeCore(Stream stream)
    {
        int readCount = ReadInt(stream);
        List<(string, int)> tests = [with(readCount)];
        for (int i = 0; i < readCount; i++)
        {
            string testName = ReadString(stream);
            int unixTimeSeconds = ReadInt(stream);
            tests.Add((testName, unixTimeSeconds));
        }

        return new GetInProgressTestsResponse([.. tests]);
    }

    protected override void SerializeCore(GetInProgressTestsResponse objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.Tests.Length);
        foreach ((string testName, int seconds) in objectToSerialize.Tests)
        {
            WriteString(stream, testName);
            WriteInt(stream, seconds);
        }
    }
}
