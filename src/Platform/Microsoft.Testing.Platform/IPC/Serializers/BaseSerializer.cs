// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal abstract partial class BaseSerializer
{
    private const int MaximumCollectionLength = 1_000_000;

    // Self-contained DEBUG assert so this shared-source type has no dependency on the rest of
    // Microsoft.Testing.Platform (e.g. RoslynDebug). Replaces RoslynDebug.Assert in the serializers.
    // No [DoesNotReturnIf(false)]: this is [Conditional("DEBUG")] and delegates to Debug.Assert, which can
    // return when the condition is false - so the annotation would be misleading and would force down-level
    // consumers to also carry the DoesNotReturnIfAttribute polyfill.
    [Conditional("DEBUG")]
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Self-contained replacement for RoslynDebug in shared IPC source.")]
    protected static void DebugAssert(bool condition, string message)
        => Debug.Assert(condition, message);

    // Internal invariant-violation diagnostic for "impossible" states (e.g. GetSize<T> called with an
    // unsupported type). Kept self-contained (no ApplicationStateGuard) so this file shares as source.
    private static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new(string.Format(CultureInfo.InvariantCulture, "This program location is thought to be unreachable. File='{0}' Line={1}", path, line));

    protected static void WriteField(Stream stream, ushort id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, ushort id, long? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<long>(stream);
        WriteLong(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, int? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<int>(stream);
        WriteInt(stream, value.Value);
    }

    protected static void WriteField(Stream stream, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, byte? value)
    {
        if (value is null)
        {
            return;
        }

        WriteByte(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, bool? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value.Value);
    }

    protected static void WriteField(Stream stream, ushort id, byte? value)
    {
        if (value is null)
        {
            return;
        }

        WriteUShort(stream, id);
        WriteSize<byte>(stream);
        WriteByte(stream, value.Value);
    }

    protected static void SetPosition(Stream stream, long position) => stream.Position = position;

    protected static void WriteAtPosition(Stream stream, int value, long position)
    {
        long currentPosition = stream.Position;
        SetPosition(stream, position);
        WriteInt(stream, value);
        SetPosition(stream, currentPosition);
    }

    /// <summary>
    /// Reads the standard field envelope (a <c>ushort</c> field count followed by that many
    /// <c>[ushort id][int size][payload]</c> triples) and dispatches each field to <paramref name="tryReadField"/>.
    /// When the callback returns <see langword="false"/> (an unrecognized field id), the field payload is skipped so
    /// that the reader stays aligned and remains forward-compatible with newer producers.
    /// </summary>
    protected static void ReadFields(Stream stream, Func<ushort, int, bool> tryReadField)
    {
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);
            if (fieldSize < 0)
            {
                throw new InvalidDataException($"Field {fieldId} has a negative size.");
            }

            long fieldEnd = checked(stream.Position + fieldSize);
            if (fieldEnd > stream.Length)
            {
                throw new EndOfStreamException($"Field {fieldId} extends beyond the end of the stream.");
            }

            if (!tryReadField(fieldId, fieldSize))
            {
                SetPosition(stream, fieldEnd);
                continue;
            }

            if (stream.Position > fieldEnd)
            {
                throw new InvalidDataException($"Field {fieldId} consumed more data than its declared size.");
            }

            SetPosition(stream, fieldEnd);
        }
    }

    /// <summary>
    /// Reads the standard field envelope while threading a value-type state through a non-capturing callback.
    /// Returning <see langword="null"/> from the callback marks the field as unrecognized.
    /// </summary>
    /// <typeparam name="TState">The value-type state accumulated while fields are read.</typeparam>
    protected static TState ReadFields<TState>(Stream stream, TState state, Func<Stream, ushort, int, TState, TState?> tryReadField)
        where TState : struct
    {
        ushort fieldCount = ReadUShort(stream);
        for (int i = 0; i < fieldCount; i++)
        {
            ushort fieldId = ReadUShort(stream);
            int fieldSize = ReadInt(stream);
            if (fieldSize < 0)
            {
                throw new InvalidDataException($"Field {fieldId} has a negative size.");
            }

            long fieldEnd = checked(stream.Position + fieldSize);
            if (fieldEnd > stream.Length)
            {
                throw new EndOfStreamException($"Field {fieldId} extends beyond the end of the stream.");
            }

            TState? updatedState = tryReadField(stream, fieldId, fieldSize, state);
            if (updatedState is null)
            {
                SetPosition(stream, fieldEnd);
                continue;
            }

            state = updatedState.Value;
            if (stream.Position > fieldEnd)
            {
                throw new InvalidDataException($"Field {fieldId} consumed more data than its declared size.");
            }

            SetPosition(stream, fieldEnd);
        }

        return state;
    }

    protected static T ReadFieldPayload<T>(Stream stream, int fieldSize, Func<Stream, T> readPayload)
    {
        ValidatePayloadSize(stream, fieldSize);
        using var payloadStream = new BoundedReadStream(stream, fieldSize);
        return readPayload(payloadStream);
    }

    protected static int ReadCollectionLength(Stream stream, int minimumBytesPerItem)
    {
        int length = ReadInt(stream);
        long remainingBytes = stream.Length - stream.Position;
        return (length < 0 ||
            length > MaximumCollectionLength ||
            minimumBytesPerItem <= 0 ||
            length > remainingBytes / minimumBytesPerItem)
            ? throw new InvalidDataException($"Collection length {length} is invalid for the remaining payload.")
            : length;
    }

    /// <summary>
    /// Writes a length-prefixed list payload using the deferred-size-backfill protocol: the field id, a reserved
    /// 4-byte size slot, the element count, then each element via <paramref name="writeItem"/>. The reserved slot is
    /// finally patched with the payload size. A <see langword="null"/> or empty list writes nothing.
    /// </summary>
    /// <typeparam name="T">The element type of the list being serialized.</typeparam>
    protected static void WriteListPayload<T>(Stream stream, ushort fieldId, T[]? list, Action<Stream, T> writeItem)
    {
        if (list is null || list.Length == 0)
        {
            return;
        }

        WriteUShort(stream, fieldId);
        // We will reserve an int (4 bytes) so that we fill the size later, once we write the payload.
        WriteInt(stream, 0);
        long before = stream.Position;
        WriteInt(stream, list.Length);
        foreach (T item in list)
        {
            writeItem(stream, item);
        }

        // NOTE: We are able to seek only if we are using a MemoryStream
        // thus, the seek operation is fast as we are only changing the value of a property.
        WriteAtPosition(stream, (int)(stream.Position - before), before - sizeof(int));
    }

    // ExecutionId and InstanceId are the two leading fields shared verbatim by the four 'dotnet test' collection
    // message envelopes that carry a list payload (DiscoveredTestMessages, TestResultMessages, TestInProgressMessages,
    // FileArtifactMessages). Their wire ids are pinned to 1 and 2 for those serializers and MUST NOT change. Note that
    // AzureDevOpsLogMessage/DisplayMessage also place ExecutionId/InstanceId at ids 1/2, but they carry scalar payloads
    // rather than a message list, so they do not use these helpers.
    private const ushort ExecutionScopedExecutionIdFieldId = 1;
    private const ushort ExecutionScopedInstanceIdFieldId = 2;

    /// <summary>
    /// Matches the two leading collection-envelope fields (<c>ExecutionId</c> id 1 / <c>InstanceId</c> id 2) shared by
    /// the four 'dotnet test' list-carrying messages payloads, assigning the value into <paramref name="executionId"/>
    /// or <paramref name="instanceId"/> and returning <see langword="true"/> when the field is one of them. The caller
    /// handles its type-specific message-list field ids when this returns <see langword="false"/>. Kept as a
    /// <see langword="ref"/>-based matcher so it adds no allocation on the hot per-test IPC read path.
    /// </summary>
    protected static bool TryReadExecutionScopedField(Stream stream, ushort fieldId, int fieldSize, ref string? executionId, ref string? instanceId)
    {
        switch (fieldId)
        {
            case ExecutionScopedExecutionIdFieldId:
                executionId = ReadStringValue(stream, fieldSize);
                return true;

            case ExecutionScopedInstanceIdFieldId:
                instanceId = ReadStringValue(stream, fieldSize);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Writes the shared collection-envelope header: a field-count prefix (the <c>ExecutionId</c> and <c>InstanceId</c>
    /// fields when non-<see langword="null"/> plus <paramref name="payloadFieldCount"/>), followed by the
    /// <c>ExecutionId</c> (id 1) and <c>InstanceId</c> (id 2) fields. The caller writes its type-specific message
    /// list(s) directly afterwards. Kept callback-free so it adds no closure/delegate allocation on the hot per-test
    /// IPC write path.
    /// </summary>
    protected static void WriteExecutionScopedHeader(Stream stream, string? executionId, string? instanceId, ushort payloadFieldCount)
    {
        DebugAssert(stream.CanSeek, "We expect a seekable stream.");

        WriteUShort(stream, (ushort)((executionId is null ? 0 : 1) + (instanceId is null ? 0 : 1) + payloadFieldCount));

        WriteField(stream, ExecutionScopedExecutionIdFieldId, executionId);
        WriteField(stream, ExecutionScopedInstanceIdFieldId, instanceId);
    }

    public static bool IsNullOrEmpty<T>(T[]? list) => list is null || list.Length == 0;
}
