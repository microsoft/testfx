// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostCompletedRequestSerializer : NamedPipeSerializer<TestHostCompletedRequest>, INamedPipeSerializer
{
    public override int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

    protected override TestHostCompletedRequest DeserializeCore(Stream stream)
    {
        int exitCode = ReadInt(stream);
        int? unfilteredExitCode = ReadOptionalInt(stream);
        if (unfilteredExitCode is null)
        {
            return new TestHostCompletedRequest(exitCode);
        }

        int? count = ReadOptionalInt(stream);
        if (count is null)
        {
            return new TestHostCompletedRequest(exitCode, unfilteredExitCode.Value);
        }

        if (count is < 0 or > 10_000)
        {
            throw new InvalidDataException("Invalid controller summary artifact count.");
        }

        var artifacts = new SessionFileArtifact[count.Value];
        for (int i = 0; i < artifacts.Length; i++)
        {
            string sessionUid = ReadString(stream);
            string path = ReadString(stream);
            string displayName = ReadString(stream);
            string? description = ReadInt(stream) switch
            {
                0 => null,
                1 => ReadString(stream),
                _ => throw new InvalidDataException("Invalid artifact description marker."),
            };
            string? kind = ReadInt(stream) switch
            {
                0 => null,
                1 => ReadString(stream),
                _ => throw new InvalidDataException("Invalid artifact kind marker."),
            };
            artifacts[i] = new SessionFileArtifact(new SessionUid(sessionUid), new FileInfo(path), displayName, description, kind);
        }

        return new TestHostCompletedRequest(exitCode, unfilteredExitCode.Value, artifacts);
    }

    private static int? ReadOptionalInt(Stream stream)
    {
        int firstByte = stream.ReadByte();
        if (firstByte == -1)
        {
            return null;
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

        return BitConverter.ToInt32(unfilteredExitCodeBytes, 0);
    }

    protected override void SerializeCore(TestHostCompletedRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.ExitCode);
        WriteInt(stream, objectToSerialize.UnfilteredExitCode);
        if (objectToSerialize.SummaryArtifacts.Length == 0)
        {
            return;
        }

        // Append-only: older peers ignore the extra payload, and new peers accept both legacy shapes.
        WriteInt(stream, objectToSerialize.SummaryArtifacts.Length);
        foreach (SessionFileArtifact artifact in objectToSerialize.SummaryArtifacts)
        {
            WriteString(stream, artifact.SessionUid.Value);
            WriteString(stream, artifact.FileInfo.FullName);
            WriteString(stream, artifact.DisplayName);
            WriteInt(stream, artifact.Description is null ? 0 : 1);
            if (artifact.Description is { } description)
            {
                WriteString(stream, description);
            }

            WriteInt(stream, artifact.Kind is null ? 0 : 1);
            if (artifact.Kind is { } kind)
            {
                WriteString(stream, kind);
            }
        }
    }
}
