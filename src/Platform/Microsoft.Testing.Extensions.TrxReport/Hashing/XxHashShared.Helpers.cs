// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace System.IO.Hashing;

internal static unsafe partial class XxHashShared
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64LE(byte* data, ulong value)
    {
        if (!BitConverter.IsLittleEndian)
        {
            value = BinaryPrimitives.ReverseEndianness(value);
        }

        WriteUnaligned(data, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ReadUnaligned<T>(void* source)
        where T : unmanaged
#if NET
        => Unsafe.ReadUnaligned<T>(source);
#else
    {
        T t;
        Buffer.MemoryCopy(source, &t, sizeof(T), sizeof(T));
        return t;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUnaligned<T>(void* destination, T value)
        where T : unmanaged
#if NET
        => Unsafe.WriteUnaligned<T>(destination, value);
#else
        => Buffer.MemoryCopy(&value, destination, sizeof(T), sizeof(T));
#endif
}
