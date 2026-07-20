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
        public const int AzureDevOpsLogMessage = 11;
        public const int DisplayMessage = 12;
        public const int WaitForServerControlRequest = 13;
        public const int ServerControlMessage = 14;
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
        public const byte OrchestratorFeature = 11;
        public const byte ServerControlPipeName = 12;
        public const byte AttemptNumber = 13;
        public const byte SupportedPostProcessorKinds = 14;
        public const byte SupportedPostProcessorExtensionsLegacy = 15;
    }

    public static class ServerControlKinds
    {
        public const byte CancelSession = 1;
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

    public static class AzureDevOpsLogMessageFields
    {
        public const ushort ExecutionId = 1;
        public const ushort InstanceId = 2;
        public const ushort LogText = 3;
    }

    public static class DisplayMessageFields
    {
        public const ushort ExecutionId = 1;
        public const ushort InstanceId = 2;
        public const ushort Level = 3;
        public const ushort Text = 4;
    }

    public static class DisplayMessageLevels
    {
        public const byte Information = 0;
        public const byte Warning = 1;
        public const byte Error = 2;
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
    /// Encodes the body of a <see cref="SerializerIds.ServerControlMessage"/> frame. Mirrors
    /// <c>ServerControlMessageSerializer</c>: a field-tagged object with a single <c>Kind</c> field (id 1)
    /// carrying one byte (<see cref="ServerControlKinds"/>).
    /// </summary>
    public static byte[] EncodeServerControlMessageBody(byte kind)
    {
        const ushort kindFieldId = 1;

        using MemoryStream stream = new();
        WriteUShort(stream, 1); // field count
        WriteUShort(stream, kindFieldId);
        WriteInt(stream, sizeof(byte)); // field size
        stream.WriteByte(kind);

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

    /// <summary>
    /// Decodes the body of a <see cref="SerializerIds.AzureDevOpsLogMessage"/> frame.
    /// Format: <c>ushort fieldCount; (ushort fieldId, int fieldSize, payload)*fieldCount</c>
    /// where every field is a length-prefixed UTF-8 string. Returns <c>null</c> for absent fields.
    /// </summary>
    public static (string? ExecutionId, string? InstanceId, string? LogText) DecodeAzureDevOpsLogMessageBody(byte[] body)
    {
        string? executionId = null;
        string? instanceId = null;
        string? logText = null;

        using MemoryStream stream = new(body, writable: false);
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case AzureDevOpsLogMessageFields.ExecutionId:
                    executionId = ReadFixedSizeString(stream, fieldSize);
                    break;
                case AzureDevOpsLogMessageFields.InstanceId:
                    instanceId = ReadFixedSizeString(stream, fieldSize);
                    break;
                case AzureDevOpsLogMessageFields.LogText:
                    logText = ReadFixedSizeString(stream, fieldSize);
                    break;
                default:
                    stream.Seek(fieldSize, SeekOrigin.Current);
                    break;
            }
        }

        return (executionId, instanceId, logText);
    }

    /// <summary>
    /// Decodes the body of a <see cref="SerializerIds.DisplayMessage"/> frame.
    /// Format: <c>ushort fieldCount; (ushort fieldId, int fieldSize, payload)*fieldCount</c>
    /// where the id fields and the text are length-prefixed UTF-8 strings and Level is a single byte.
    /// Returns <c>null</c> for absent string fields and <c>null</c> for an absent Level.
    /// </summary>
    public static (string? ExecutionId, string? InstanceId, byte? Level, string? Text) DecodeDisplayMessageBody(byte[] body)
    {
        string? executionId = null;
        string? instanceId = null;
        byte? level = null;
        string? text = null;

        using MemoryStream stream = new(body, writable: false);
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            switch (fieldId)
            {
                case DisplayMessageFields.ExecutionId:
                    executionId = ReadFixedSizeString(stream, fieldSize);
                    break;
                case DisplayMessageFields.InstanceId:
                    instanceId = ReadFixedSizeString(stream, fieldSize);
                    break;
                case DisplayMessageFields.Level:
                    // Respect the declared field size and handle truncation explicitly: only read a Level byte
                    // when the field actually carries one (fieldSize >= 1 and a byte is available), then skip any
                    // extra bytes a future wire revision may add so subsequent fields stay aligned. ReadByte
                    // returns -1 at end-of-stream, so guard against it rather than casting -1 to a byte.
                    if (fieldSize >= 1)
                    {
                        int read = stream.ReadByte();
                        if (read >= 0)
                        {
                            level = (byte)read;
                        }

                        if (fieldSize > 1)
                        {
                            stream.Seek(fieldSize - 1, SeekOrigin.Current);
                        }
                    }

                    break;
                case DisplayMessageFields.Text:
                    text = ReadFixedSizeString(stream, fieldSize);
                    break;
                default:
                    stream.Seek(fieldSize, SeekOrigin.Current);
                    break;
            }
        }

        return (executionId, instanceId, level, text);
    }

    public static IReadOnlyList<FileArtifact> DecodeFileArtifacts(byte[] body)
    {
        const ushort fileArtifactMessageListFieldId = 3;
        const ushort fullPathFieldId = 1;
        const ushort displayNameFieldId = 2;
        const ushort kindFieldId = 7;

        List<FileArtifact> artifacts = [];
        using MemoryStream stream = new(body, writable: false);
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);
            if (fieldId != fileArtifactMessageListFieldId)
            {
                stream.Seek(fieldSize, SeekOrigin.Current);
                continue;
            }

            int messageCount = ReadInt(stream);
            for (int messageIndex = 0; messageIndex < messageCount; messageIndex++)
            {
                string? fullPath = null;
                string? displayName = null;
                string? kind = null;
                ushort messageFieldCount = ReadUShort(stream);
                for (int messageFieldIndex = 0; messageFieldIndex < messageFieldCount; messageFieldIndex++)
                {
                    ushort messageFieldId = ReadUShort(stream);
                    int messageFieldSize = ReadInt(stream);
                    switch (messageFieldId)
                    {
                        case fullPathFieldId:
                            fullPath = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case displayNameFieldId:
                            displayName = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case kindFieldId:
                            kind = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        default:
                            stream.Seek(messageFieldSize, SeekOrigin.Current);
                            break;
                    }
                }

                artifacts.Add(new FileArtifact(fullPath, displayName, kind));
            }
        }

        return artifacts;
    }

    /// <summary>
    /// Decodes the tests carried by a <see cref="SerializerIds.DiscoveredTestMessages"/> frame,
    /// including the full discovery details (file path, line number, namespace, type/method name,
    /// parameter types and traits). Mirrors the wire layout produced by
    /// <c>DiscoveredTestMessagesSerializer</c>: the body is a field-tagged object whose
    /// <c>DiscoveredTestMessageList</c> field (id 3) holds a length-prefixed array of field-tagged
    /// test messages. Unknown fields are skipped via their declared size, exactly like the product
    /// deserializer.
    /// <para>
    /// Decoding the whole object (not just the display name) lets the acceptance test catch
    /// regressions where the SDK-facing metadata required to build the <c>--list-tests json</c>
    /// document stops flowing over the pipe.
    /// </para>
    /// </summary>
    public static IReadOnlyList<DiscoveredTest> DecodeDiscoveredTests(byte[] body)
    {
        const ushort discoveredTestMessageListFieldId = 3;
        const ushort uidFieldId = 1;
        const ushort displayNameFieldId = 2;
        const ushort filePathFieldId = 3;
        const ushort lineNumberFieldId = 4;
        const ushort namespaceFieldId = 5;
        const ushort typeNameFieldId = 6;
        const ushort methodNameFieldId = 7;
        const ushort traitsFieldId = 8;
        const ushort parameterTypeFullNamesFieldId = 9;

        List<DiscoveredTest> discoveredTests = [];
        using MemoryStream stream = new(body, writable: false);

        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);

            if (fieldId != discoveredTestMessageListFieldId)
            {
                // ExecutionId / InstanceId (or any future field): skip the whole payload.
                stream.Seek(fieldSize, SeekOrigin.Current);
                continue;
            }

            int messageCount = ReadInt(stream);
            for (int m = 0; m < messageCount; m++)
            {
                string? uid = null;
                string? displayName = null;
                string? filePath = null;
                int? lineNumber = null;
                string? @namespace = null;
                string? typeName = null;
                string? methodName = null;
                string[] parameterTypeFullNames = [];
                Dictionary<string, string> traits = [];

                ushort messageFieldCount = ReadUShort(stream);
                for (int f = 0; f < messageFieldCount; f++)
                {
                    ushort messageFieldId = ReadUShort(stream);
                    int messageFieldSize = ReadInt(stream);
                    switch (messageFieldId)
                    {
                        case uidFieldId:
                            uid = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case displayNameFieldId:
                            displayName = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case filePathFieldId:
                            filePath = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case lineNumberFieldId:
                            lineNumber = ReadInt(stream);
                            break;
                        case namespaceFieldId:
                            @namespace = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case typeNameFieldId:
                            typeName = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case methodNameFieldId:
                            methodName = ReadFixedSizeString(stream, messageFieldSize);
                            break;
                        case traitsFieldId:
                            foreach (KeyValuePair<string, string> trait in ReadTraits(stream))
                            {
                                traits[trait.Key] = trait.Value;
                            }

                            break;
                        case parameterTypeFullNamesFieldId:
                            parameterTypeFullNames = ReadParameterTypeFullNames(stream);
                            break;
                        default:
                            stream.Seek(messageFieldSize, SeekOrigin.Current);
                            break;
                    }
                }

                discoveredTests.Add(new DiscoveredTest(
                    uid, displayName, filePath, lineNumber, @namespace, typeName, methodName, parameterTypeFullNames, traits));
            }
        }

        return discoveredTests;
    }

    /// <summary>
    /// Reads the <c>Traits</c> payload (id 8) of a discovered test message: a length-prefixed array
    /// of field-tagged key/value pairs (<c>Key</c> id 1, <c>Value</c> id 2).
    /// </summary>
    private static IReadOnlyList<KeyValuePair<string, string>> ReadTraits(Stream stream)
    {
        const ushort keyFieldId = 1;
        const ushort valueFieldId = 2;

        int length = ReadInt(stream);
        List<KeyValuePair<string, string>> traits = [];
        for (int i = 0; i < length; i++)
        {
            string? key = null;
            string? value = null;
            ushort fieldCount = ReadUShort(stream);
            for (int f = 0; f < fieldCount; f++)
            {
                ushort fieldId = ReadUShort(stream);
                int fieldSize = ReadInt(stream);
                switch (fieldId)
                {
                    case keyFieldId:
                        key = ReadFixedSizeString(stream, fieldSize);
                        break;
                    case valueFieldId:
                        value = ReadFixedSizeString(stream, fieldSize);
                        break;
                    default:
                        stream.Seek(fieldSize, SeekOrigin.Current);
                        break;
                }
            }

            traits.Add(new KeyValuePair<string, string>(key ?? string.Empty, value ?? string.Empty));
        }

        return traits;
    }

    /// <summary>
    /// Reads the <c>ParameterTypeFullNames</c> payload (id 9) of a discovered test message: a
    /// length-prefixed array of length-prefixed UTF-8 strings.
    /// </summary>
    private static string[] ReadParameterTypeFullNames(Stream stream)
    {
        int length = ReadInt(stream);
        string[] parameterTypeFullNames = new string[length];
        for (int i = 0; i < length; i++)
        {
            parameterTypeFullNames[i] = ReadLengthPrefixedString(stream);
        }

        return parameterTypeFullNames;
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

    private static void WriteInt(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
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

internal sealed record FileArtifact(string? FullPath, string? DisplayName, string? Kind);

/// <summary>
/// A fully decoded discovered test message: the complete discovery object the SDK needs to build the
/// <c>--list-tests json</c> document (display name plus file/method location and traits).
/// </summary>
internal sealed record DiscoveredTest(
    string? Uid,
    string? DisplayName,
    string? FilePath,
    int? LineNumber,
    string? Namespace,
    string? TypeName,
    string? MethodName,
    string[] ParameterTypeFullNames,
    IReadOnlyDictionary<string, string> Traits);
