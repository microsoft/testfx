// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal abstract partial class BaseSerializer
{
    protected static void WriteSize<T>(Stream stream)
        where T : struct
        => WriteInt(stream, GetSize<T>());

#if NETCOREAPP
    protected static void WriteInt(Stream stream, int value) => WritePrimitive(stream, value);

    protected static int ReadInt(Stream stream) => ReadPrimitive<int>(stream);

    protected static void WriteLong(Stream stream, long value) => WritePrimitive(stream, value);

    protected static long ReadLong(Stream stream) => ReadPrimitive<long>(stream);

    protected static void WriteUShort(Stream stream, ushort value) => WritePrimitive(stream, value);

    protected static ushort ReadUShort(Stream stream) => ReadPrimitive<ushort>(stream);

    private static void WritePrimitive<T>(Stream stream, T value)
        where T : unmanaged
    {
        int size = GetSize<T>();
        Span<byte> bytes = stackalloc byte[size];
        System.Runtime.InteropServices.MemoryMarshal.Write(bytes, in value);
        stream.Write(bytes);
    }

    private static T ReadPrimitive<T>(Stream stream)
        where T : unmanaged
    {
        Span<byte> bytes = stackalloc byte[GetSize<T>()];
        stream.ReadExactly(bytes);
        return System.Runtime.InteropServices.MemoryMarshal.Read<T>(bytes);
    }
#else
    protected static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static int ReadInt(Stream stream)
    {
        byte[] bytes = new byte[sizeof(int)];
        ReadExactly(stream, bytes, 0, bytes.Length);
        return BitConverter.ToInt32(bytes, 0);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static long ReadLong(Stream stream)
    {
        byte[] bytes = new byte[sizeof(long)];
        ReadExactly(stream, bytes, 0, bytes.Length);
        return BitConverter.ToInt64(bytes, 0);
    }

    protected static void WriteUShort(Stream stream, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static ushort ReadUShort(Stream stream)
    {
        byte[] bytes = new byte[sizeof(ushort)];
        ReadExactly(stream, bytes, 0, bytes.Length);
        return BitConverter.ToUInt16(bytes, 0);
    }
#endif

    protected static void WriteBool(Stream stream, bool value)
        => stream.WriteByte(value ? (byte)1 : (byte)0);

    protected static bool ReadBool(Stream stream)
        => ReadByte(stream) != 0;

    // Reads exactly 'count' bytes into 'buffer' starting at 'offset', looping until the request is
    // satisfied or the end of the stream is reached. This centralizes the previously duplicated
    // per-primitive read logic and fixes the historical short-read bug on the non-NETCOREAPP path,
    // where a single Stream.Read could return fewer bytes than requested and silently corrupt data.
    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
#if NETCOREAPP
        stream.ReadExactly(buffer, offset, count);
#else
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            totalRead += read;
        }
#endif
    }

    protected static byte ReadByte(Stream stream)
        => stream.ReadByte() is int value and not -1
            ? (byte)value
            : throw new EndOfStreamException();

    protected static void WriteByte(Stream stream, byte value) => stream.WriteByte(value);

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
}
