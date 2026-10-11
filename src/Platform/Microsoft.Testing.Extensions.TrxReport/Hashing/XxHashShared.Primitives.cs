// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Adapted from https://github.com/dotnet/runtime/blob/cd6580ff499dd9ae37656edcf2239bbab2362251/src/libraries/System.IO.Hashing/src/System/IO/Hashing/XxHashShared.cs
// Low-level read, write, and bit-mixing helpers, split from the upstream implementation.
using System.Buffers.Binary;

#pragma warning disable RS0030 // Do not use banned APIs - Debug is okay here. RoslynDebug isn't yet available in PlatformServices which links this file.

namespace System.IO.Hashing;

internal static unsafe partial class XxHashShared
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong XorShift(ulong value, int shift)
    {
        Debug.Assert(shift is >= 0 and < 64, "shift was expected to be between 0 and 63.");
        return value ^ (value >> shift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32LE(byte* data) =>
        BitConverter.IsLittleEndian ?
            ReadUnaligned<uint>(data) :
            BinaryPrimitives.ReverseEndianness(ReadUnaligned<uint>(data));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64LE(byte* data) =>
        BitConverter.IsLittleEndian ?
            ReadUnaligned<ulong>(data) :
            BinaryPrimitives.ReverseEndianness(ReadUnaligned<ulong>(data));

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
