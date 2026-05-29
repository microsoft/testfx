// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

#pragma warning disable RS0030 // Do not use banned APIs - Debug is okay here. RoslynDebug isn't yet available in PlatformServices which links this file.

namespace System.IO.Hashing;

internal static unsafe partial class XxHashShared
{
    /// <summary>This is a stronger avalanche, preferable when input has not been previously mixed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Rrmxmx(ulong hash, uint length)
    {
        hash ^= BitOperations.RotateLeft(hash, 49) ^ BitOperations.RotateLeft(hash, 24);
        hash *= 0x9FB21C651E98DF25;
        hash ^= (hash >> 35) + length;
        hash *= 0x9FB21C651E98DF25;
        return XorShift(hash, 28);
    }

    public static ulong MergeAccumulators(ulong* accumulators, byte* secret, ulong start)
    {
        ulong result64 = start;

        result64 += Multiply64To128ThenFold(accumulators[0] ^ ReadUInt64LE(secret), accumulators[1] ^ ReadUInt64LE(secret + 8));
        result64 += Multiply64To128ThenFold(accumulators[2] ^ ReadUInt64LE(secret + 16), accumulators[3] ^ ReadUInt64LE(secret + 24));
        result64 += Multiply64To128ThenFold(accumulators[4] ^ ReadUInt64LE(secret + 32), accumulators[5] ^ ReadUInt64LE(secret + 40));
        result64 += Multiply64To128ThenFold(accumulators[6] ^ ReadUInt64LE(secret + 48), accumulators[7] ^ ReadUInt64LE(secret + 56));

        return Avalanche(result64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Mix16Bytes(byte* source, ulong secretLow, ulong secretHigh, ulong seed) =>
        Multiply64To128ThenFold(
            ReadUInt64LE(source) ^ (secretLow + seed),
            ReadUInt64LE(source + sizeof(ulong)) ^ (secretHigh - seed));

    /// <summary>Calculates a 32-bit to 64-bit long multiply.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Multiply32To64(uint v1, uint v2) => (ulong)v1 * v2;

    /// <summary>This is a fast avalanche stage, suitable when input bits are already partially mixed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Avalanche(ulong hash)
    {
        hash = XorShift(hash, 37);
        hash *= 0x165667919E3779F9;
        hash = XorShift(hash, 32);
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Multiply64To128(ulong left, ulong right, out ulong lower)
    {
#if NET
        return Math.BigMul(left, right, out lower);
#else
        ulong lowerLow = Multiply32To64((uint)left, (uint)right);
        ulong higherLow = Multiply32To64((uint)(left >> 32), (uint)right);
        ulong lowerHigh = Multiply32To64((uint)left, (uint)(right >> 32));
        ulong higherHigh = Multiply32To64((uint)(left >> 32), (uint)(right >> 32));

        ulong cross = (lowerLow >> 32) + (higherLow & 0xFFFFFFFF) + lowerHigh;
        ulong upper = (higherLow >> 32) + (cross >> 32) + higherHigh;
        lower = (cross << 32) | (lowerLow & 0xFFFFFFFF);
        return upper;
#endif
    }

    /// <summary>Calculates a 64-bit to 128-bit multiply, then XOR folds it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Multiply64To128ThenFold(ulong left, ulong right)
    {
        ulong upper = Multiply64To128(left, right, out ulong lower);
        return lower ^ upper;
    }

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
}
