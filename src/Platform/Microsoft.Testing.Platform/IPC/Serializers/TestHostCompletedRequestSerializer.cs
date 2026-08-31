// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostCompletedRequestSerializer : NamedPipeSerializer<TestHostCompletedRequest>, INamedPipeSerializer
{
    public override int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

    protected override TestHostCompletedRequest DeserializeCore(Stream stream)
    {
        int exitCode = ReadInt(stream);
        int firstByte = stream.ReadByte();
        if (firstByte == -1)
        {
            // Serializer ID 1 originally carried only ExitCode. Treat exactly zero remaining bytes as a
            // legacy payload whose filtered and unfiltered verdicts are identical.
            return new TestHostCompletedRequest(exitCode);
        }

        byte[] unfilteredExitCodeBytes = new byte[sizeof(int)];
        unfilteredExitCodeBytes[0] = (byte)firstByte;
        int remainingBytes = sizeof(int) - 1;
        while (remainingBytes > 0)
        {
            int bytesRead = stream.Read(unfilteredExitCodeBytes, sizeof(int) - remainingBytes, remainingBytes);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException();
            }

            remainingBytes -= bytesRead;
        }

        return new TestHostCompletedRequest(exitCode, BitConverter.ToInt32(unfilteredExitCodeBytes, 0));
    }

    protected override void SerializeCore(TestHostCompletedRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.ExitCode);
        WriteInt(stream, objectToSerialize.UnfilteredExitCode);
    }
}
