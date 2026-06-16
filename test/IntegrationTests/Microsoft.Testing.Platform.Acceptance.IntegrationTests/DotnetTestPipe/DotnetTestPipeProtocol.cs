// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipes;
using System.Text;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Black-box reader/writer for the <c>--server dotnettestcli --dotnet-test-pipe</c> wire
/// protocol. Implements the framing and the serializer IDs / field IDs documented in
/// <c>src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/ObjectFieldIds.cs</c>
/// without referencing any of the testfx internal types (which are <c>[Embedded]</c> and
/// therefore invisible to this assembly).
/// <para>
/// Treating this as a contract reader makes the resulting tests true black-box assertions on the
/// wire format: any silent change to the message shape on the testfx side will be caught here.
/// </para>
/// </summary>
internal static class DotnetTestPipeProtocol
{
    /// <summary>
    /// Frame layout (matches <c>NamedPipeBase</c>):
    /// <code>
    ///   int32  totalPayloadLength    // = sizeof(serializerId) + bodyLength
    ///   int32  serializerId
    ///   byte[] body
    /// </code>
    /// </summary>
    public static class SerializerIds
    {
        public const int VoidResponse = 0;
        public const int TestHostCompletedRequest = 1;
        public const int TestHostProcessPIDRequest = 2;
        public const int CommandLineOptionMessages = 3;
        public const int DiscoveredTestMessages = 5;
        public const int TestResultMessages = 6;
        public const int FileArtifactMessages = 7;
        public const int TestSessionEvent = 8;
        public const int HandshakeMessage = 9;
        public const int TestInProgressMessages = 10;
    }

    public static class HandshakeProperties
    {
        public const byte PID = 0;
        public const byte Architecture = 1;
        public const byte Framework = 2;
        public const byte OS = 3;
        public const byte SupportedProtocolVersions = 4;
        public const byte HostType = 5;
        public const byte ModulePath = 6;
        public const byte ExecutionId = 7;
        public const byte InstanceId = 8;
        public const byte IsIDE = 9;
        public const byte ExecutionMode = 10;
    }

    public static class SessionEventTypes
    {
        public const byte TestSessionStart = 0;
        public const byte TestSessionEnd = 1;
    }

    public static class TestSessionEventFields
    {
        public const ushort SessionType = 1;
        public const ushort SessionUid = 2;
        public const ushort ExecutionId = 3;
    }

    /// <summary>
    /// Computes the OS-level named pipe name from a friendly identifier. Mirrors
    /// <c>NamedPipeServer.GetPipeName</c> in testfx.
    /// </summary>
    public static string GetPipeName(string name)
    {
        bool isUnix = Path.DirectorySeparatorChar == '/';
        return isUnix
            ? Path.Combine("/tmp", name)
            : $"testingplatform.pipe.{name.Replace('\\', '.')}";
    }

    /// <summary>
    /// Reads exactly one framed message from the pipe. Returns <see langword="null"/> on EOF
    /// (peer disconnected cleanly).
    /// </summary>
    public static async Task<RawMessage?> ReadFrameAsync(PipeStream stream, CancellationToken cancellationToken)
    {
        byte[] header = ArrayPool<byte>.Shared.Rent(sizeof(int));
        try
        {
            if (!await TryReadExactlyAsync(stream, header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            int totalPayloadLength = BitConverter.ToInt32(header, 0);

            byte[] payload = new byte[totalPayloadLength];
            if (!await TryReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            int serializerId = BitConverter.ToInt32(payload, 0);
            byte[] body = new byte[totalPayloadLength - sizeof(int)];
            Array.Copy(payload, sizeof(int), body, 0, body.Length);

            return new RawMessage(serializerId, body);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    /// <summary>
    /// Writes one framed message to the pipe.
    /// </summary>
    public static async Task WriteFrameAsync(PipeStream stream, int serializerId, byte[] body, CancellationToken cancellationToken)
    {
        int totalPayloadLength = sizeof(int) + body.Length;
        byte[] header = BitConverter.GetBytes(totalPayloadLength);
        byte[] id = BitConverter.GetBytes(serializerId);

        await stream.WriteAsync(header.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(id.AsMemory(0, sizeof(int)), cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body.AsMemory(0, body.Length), cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (OperatingSystem.IsWindows() && stream is NamedPipeServerStream serverStream)
        {
            serverStream.WaitForPipeDrain();
        }
    }

    /// <summary>
    /// Decodes the body of a <see cref="SerializerIds.HandshakeMessage"/> frame into its property
    /// dictionary. Format: <c>ushort fieldCount; (byte key, length-prefixed UTF-8 value) * fieldCount</c>.
    /// </summary>
    public static Dictionary<byte, string> DecodeHandshakeBody(byte[] body)
    {
        Dictionary<byte, string> result = [];
        using MemoryStream stream = new(body, writable: false);

        ushort count = ReadUShort(stream);
        for (int i = 0; i < count; i++)
        {
            byte key = (byte)stream.ReadByte();
            string value = ReadLengthPrefixedString(stream);
            result.Add(key, value);
        }

        return result;
    }

    /// <summary>
    /// Encodes the body of a <see cref="SerializerIds.HandshakeMessage"/> frame from a property
    /// dictionary.
    /// </summary>
    public static byte[] EncodeHandshakeBody(Dictionary<byte, string> properties)
    {
        using MemoryStream stream = new();
        WriteUShort(stream, (ushort)properties.Count);
        foreach (KeyValuePair<byte, string> kvp in properties)
        {
            stream.WriteByte(kvp.Key);
            WriteLengthPrefixedString(stream, kvp.Value);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Decodes the body of a <see cref="SerializerIds.TestSessionEvent"/> frame.
    /// Format: <c>ushort fieldCount; (ushort fieldId, int fieldSize, payload)*fieldCount</c>
    /// where payload shape is determined by fieldId. Returns <c>null</c> for fields that are absent.
    /// </summary>
    public static (byte? SessionType, string? SessionUid, string? ExecutionId) DecodeTestSessionEventBody(byte[] body)
    {
        byte? sessionType = null;
        string? sessionUid = null;
        string? executionId = null;

        using MemoryStream stream = new(body, writable: false);
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case TestSessionEventFields.SessionType:
                    sessionType = (byte)stream.ReadByte();

                    // SessionType is a single byte today, but advance past any extra bytes the
                    // wire format may carry so subsequent fields stay aligned.
                    if (fieldSize > 1)
                    {
                        stream.Seek(fieldSize - 1, SeekOrigin.Current);
                    }

                    break;
                case TestSessionEventFields.SessionUid:
                    sessionUid = ReadFixedSizeString(stream, fieldSize);
                    break;
                case TestSessionEventFields.ExecutionId:
                    executionId = ReadFixedSizeString(stream, fieldSize);
                    break;
                default:
                    // Unknown field id: skip forward.
                    stream.Seek(fieldSize, SeekOrigin.Current);
                    break;
            }
        }

        return (sessionType, sessionUid, executionId);
    }

    private static async Task<bool> TryReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }

    private static ushort ReadUShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        stream.ReadExactly(bytes);
        return BitConverter.ToUInt16(bytes);
    }

    private static int ReadInt(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt32(bytes);
    }

    private static void WriteUShort(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(bytes, value);
        stream.Write(bytes);
    }

    private static string ReadLengthPrefixedString(Stream stream)
    {
        int length = ReadInt(stream);
        return ReadFixedSizeString(stream, length);
    }

    private static string ReadFixedSizeString(Stream stream, int length)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            stream.ReadExactly(buffer, 0, length);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void WriteLengthPrefixedString(Stream stream, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        byte[] header = BitConverter.GetBytes(byteCount);
        stream.Write(header, 0, header.Length);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            int written = Encoding.UTF8.GetBytes(value, buffer);
            stream.Write(buffer, 0, written);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

/// <summary>A raw decoded pipe frame (serializer id + body bytes, no further decoding).</summary>
internal sealed record RawMessage(int SerializerId, byte[] Body);
