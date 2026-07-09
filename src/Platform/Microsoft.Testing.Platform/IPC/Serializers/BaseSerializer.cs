// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal abstract class BaseSerializer
{
    // Self-contained DEBUG assert so this shared-source type has no dependency on the rest of
    // Microsoft.Testing.Platform (e.g. RoslynDebug). Replaces RoslynDebug.Assert in the serializers.
    // No [DoesNotReturnIf(false)]: this is [Conditional("DEBUG")] and delegates to Debug.Assert, which can
    // return when the condition is false - so the annotation would be misleading and would force down-level
    // consumers to also carry the DoesNotReturnIfAttribute polyfill.
    [Conditional("DEBUG")]
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Self-contained replacement for RoslynDebug in shared IPC source.")]
    protected static void DebugAssert(bool condition, string message)
        => Debug.Assert(condition, message);

    // Internal invariant-violation diagnostic for "impossible" states (e.g. BitConverter.TryWriteBytes into a
    // correctly-sized buffer). Kept self-contained (no ApplicationStateGuard) so this file shares as source.
    private static InvalidOperationException Unreachable([CallerFilePath] string? path = null, [CallerLineNumber] int line = 0)
        => new(string.Format(CultureInfo.InvariantCulture, "This program location is thought to be unreachable. File='{0}' Line={1}", path, line));

#if NETCOREAPP
    protected static string ReadString(Stream stream)
    {
        Span<byte> len = stackalloc byte[sizeof(int)];
        stream.ReadExactly(len);
        int stringLen = BitConverter.ToInt32(len);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringLen);
        try
        {
            stream.ReadExactly(bytes, 0, stringLen);
            return Encoding.UTF8.GetString(bytes, 0, stringLen);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static string ReadStringValue(Stream stream, int size)
    {
        byte[] bytes = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            stream.ReadExactly(bytes, 0, size);
            return Encoding.UTF8.GetString(bytes, 0, size);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteString(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringutf8TotalBytes);
        try
        {
            Span<byte> len = stackalloc byte[sizeof(int)];
            if (!BitConverter.TryWriteBytes(len, stringutf8TotalBytes))
            {
                throw Unreachable();
            }

            stream.Write(len);

            Encoding.UTF8.GetBytes(str, bytes);
            stream.Write(bytes, 0, stringutf8TotalBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        Span<byte> len = stackalloc byte[sizeof(int)];

        if (!BitConverter.TryWriteBytes(len, sizeInBytes))
        {
            throw Unreachable();
        }

        stream.Write(len);
    }

    protected static void WriteInt(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        if (!BitConverter.TryWriteBytes(bytes, value))
        {
            throw Unreachable();
        }

        stream.Write(bytes);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        if (!BitConverter.TryWriteBytes(bytes, value))
        {
            throw Unreachable();
        }

        stream.Write(bytes);
    }

    protected static void WriteUShort(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        if (!BitConverter.TryWriteBytes(bytes, value))
        {
            throw Unreachable();
        }

        stream.Write(bytes);
    }

    protected static void WriteBool(Stream stream, bool value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        if (!BitConverter.TryWriteBytes(bytes, value))
        {
            throw Unreachable();
        }

        stream.Write(bytes);
    }

    protected static int ReadInt(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt32(bytes);
    }

    protected static long ReadLong(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        stream.ReadExactly(bytes);
        return BitConverter.ToInt64(bytes);
    }

    protected static ushort ReadUShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        stream.ReadExactly(bytes);
        return BitConverter.ToUInt16(bytes);
    }

    protected static bool ReadBool(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        stream.ReadExactly(bytes);
        return BitConverter.ToBoolean(bytes);
    }

#else
    protected static string ReadString(Stream stream)
    {
        byte[] len = new byte[sizeof(int)];
        _ = stream.Read(len, 0, len.Length);
        int length = BitConverter.ToInt32(len, 0);
        byte[] bytes = new byte[length];
        _ = stream.Read(bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(bytes);
    }

    protected static string ReadStringValue(Stream stream, int size)
    {
        byte[] bytes = new byte[size];
        _ = stream.Read(bytes, 0, bytes.Length);

        return Encoding.UTF8.GetString(bytes);
    }

    protected static void WriteString(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] len = BitConverter.GetBytes(bytes.Length);
        stream.Write(len, 0, len.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        byte[] len = BitConverter.GetBytes(sizeInBytes);
        stream.Write(len, 0, len.Length);
    }

    protected static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static int ReadInt(Stream stream)
    {
        byte[] bytes = new byte[sizeof(int)];
        _ = stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToInt32(bytes, 0);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteUShort(Stream stream, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static long ReadLong(Stream stream)
    {
        byte[] bytes = new byte[sizeof(long)];
        _ = stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToInt64(bytes, 0);
    }

    protected static ushort ReadUShort(Stream stream)
    {
        byte[] bytes = new byte[sizeof(ushort)];
        _ = stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToUInt16(bytes, 0);
    }

    protected static void WriteBool(Stream stream, bool value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static bool ReadBool(Stream stream)
    {
        byte[] bytes = new byte[sizeof(bool)];
        _ = stream.Read(bytes, 0, bytes.Length);
        return BitConverter.ToBoolean(bytes, 0);
    }
#endif

    protected static byte ReadByte(Stream stream) => (byte)stream.ReadByte();

    protected static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

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
            if (!tryReadField(fieldId, fieldSize))
            {
                // If we don't recognize the field id, skip the payload corresponding to that field.
                SetPosition(stream, stream.Position + fieldSize);
            }
        }
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

    private static int GetSize<T>() => typeof(T) switch
    {
        Type type when type == typeof(int) => sizeof(int),
        Type type when type == typeof(long) => sizeof(long),
        Type type when type == typeof(short) => sizeof(short),
        Type type when type == typeof(ushort) => sizeof(ushort),
        Type type when type == typeof(bool) => sizeof(bool),
        Type type when type == typeof(byte) => sizeof(byte),
        _ => throw Unreachable(),
    };

    public static bool IsNullOrEmpty<T>(T[]? list) => list is null || list.Length == 0;
}
