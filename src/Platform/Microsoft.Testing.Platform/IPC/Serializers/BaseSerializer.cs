// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Buffers;
#endif

#if NET
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
#endif

using System.Text;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal abstract class BaseSerializer
{
#if NETCOREAPP
    protected static string ReadString(Stream stream)
    {
        Span<byte> len = stackalloc byte[sizeof(int)];
#if NET7_0_OR_GREATER
        stream.ReadExactly(len);
#else
        _ = stream.Read(len);
#endif
        int stringLen = BitConverter.ToInt32(len);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(stringLen);
        try
        {
#if NET7_0_OR_GREATER
            stream.ReadExactly(bytes, 0, stringLen);
#else
            _ = stream.Read(bytes, 0, stringLen);
#endif
            return Encoding.UTF8.GetString(bytes, 0, stringLen);
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
            ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(len, stringutf8TotalBytes), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);
            stream.Write(len);

            Encoding.UTF8.GetBytes(str, bytes);
            stream.Write(bytes, 0, stringutf8TotalBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    protected static void WriteStringSize(Stream stream, string str)
    {
        int stringutf8TotalBytes = Encoding.UTF8.GetByteCount(str);
        Span<byte> len = stackalloc byte[sizeof(int)];

        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(len, stringutf8TotalBytes), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(len);
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        Span<byte> len = stackalloc byte[sizeof(int)];

        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(len, sizeInBytes), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(len);
    }

    protected static void WriteInt(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, value), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(bytes);
    }

    protected static void WriteLong(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, value), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(bytes);
    }

    protected static void WriteShort(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, value), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(bytes);
    }

    protected static void WriteBool(Stream stream, bool value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
        ApplicationStateGuard.Ensure(BitConverter.TryWriteBytes(bytes, value), PlatformResources.UnexpectedExceptionDuringByteConversionErrorMessage);

        stream.Write(bytes);
    }

    protected static int ReadInt(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
#if NET7_0_OR_GREATER
        stream.ReadExactly(bytes);
#else
        _ = stream.Read(bytes);
#endif
        return BitConverter.ToInt32(bytes);
    }

    protected static long ReadLong(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
#if NET7_0_OR_GREATER
        stream.ReadExactly(bytes);
#else
        _ = stream.Read(bytes);
#endif
        return BitConverter.ToInt64(bytes);
    }

    protected static ushort ReadShort(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
#if NET7_0_OR_GREATER
        stream.ReadExactly(bytes);
#else
        _ = stream.Read(bytes);
#endif
        return BitConverter.ToUInt16(bytes);
    }

    protected static bool ReadBool(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[sizeof(bool)];
#if NET7_0_OR_GREATER
        stream.ReadExactly(bytes);
#else
        _ = stream.Read(bytes);
#endif
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

    protected static void WriteString(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] len = BitConverter.GetBytes(bytes.Length);
        stream.Write(len, 0, len.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    protected static void WriteStringSize(Stream stream, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        byte[] len = BitConverter.GetBytes(bytes.Length);
        stream.Write(len, 0, len.Length);
    }

    protected static void WriteSize<T>(Stream stream)
        where T : struct
    {
        int sizeInBytes = GetSize<T>();
        byte[] len = BitConverter.GetBytes(sizeInBytes);
        stream.Write(len, 0, sizeInBytes);
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

    protected static void WriteShort(Stream stream, ushort value)
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

    protected static ushort ReadShort(Stream stream)
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

    protected static void WriteField(Stream stream, ushort id, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteStringSize(stream, value);
        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, string? value)
    {
        if (value is null)
        {
            return;
        }

        WriteString(stream, value);
    }

    protected static void WriteField(Stream stream, ushort id, bool? value)
    {
        if (value is null)
        {
            return;
        }

        WriteShort(stream, id);
        WriteSize<bool>(stream);
        WriteBool(stream, value.Value);
    }

    protected static void SetPosition(Stream stream, long position) => stream.Position = position;

    protected static void WriteAtPosition(Stream stream, int value, long position)
    {
        long currentPosition = stream.Position;
        SetPosition(stream, position);
        WriteInt(stream, value);
        SetPosition(stream, currentPosition);
    }

    private static int GetSize<T>() => typeof(T) switch
    {
        Type type when type == typeof(int) => sizeof(int),
        Type type when type == typeof(long) => sizeof(long),
        Type type when type == typeof(short) => sizeof(short),
        Type type when type == typeof(bool) => sizeof(bool),
        _ => 0,
    };
}
