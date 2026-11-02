// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Adapted from https://github.com/dotnet/runtime/blob/cd6580ff499dd9ae37656edcf2239bbab2362251/src/libraries/System.IO.Hashing/src/System/IO/Hashing/XxHashShared.cs
// Scalar hashing and digest/mixing operations, split from the upstream implementation.
using System.Numerics;
#if NET
using System.Runtime.Intrinsics;
#endif

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

    public static void HashInternalLoop(ulong* accumulators, byte* source, uint length, byte* secret)
    {
        Debug.Assert(length > 240, "Length was expected to be greater than 240.");

        const int StripesPerBlock = (SecretLengthBytes - StripeLengthBytes) / SecretConsumeRateBytes;
        const int BlockLen = StripeLengthBytes * StripesPerBlock;
        int blocksNum = (int)((length - 1) / BlockLen);

        Accumulate(accumulators, source, secret, StripesPerBlock, true, blocksNum);
        int offset = BlockLen * blocksNum;

        int stripesNumber = (int)((length - 1 - offset) / StripeLengthBytes);
        Accumulate(accumulators, source + offset, secret, stripesNumber);
        Accumulate512(accumulators, source + length - StripeLengthBytes, secret + (SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes));
    }

    public static void ConsumeStripes(ulong* accumulators, ref ulong stripesSoFar, ulong stripesPerBlock, byte* source, ulong stripes, byte* secret)
    {
        Debug.Assert(stripes <= stripesPerBlock, "stripes was expected to less than or equals stripesPerBlock"); // can handle max 1 scramble per invocation
        Debug.Assert(stripesSoFar < stripesPerBlock, "stripesSoFar was expected to be less than stripesPerBlock");

        ulong stripesToEndOfBlock = stripesPerBlock - stripesSoFar;
        if (stripesToEndOfBlock <= stripes)
        {
            // need a scrambling operation
            ulong stripesAfterBlock = stripes - stripesToEndOfBlock;
            Accumulate(accumulators, source, secret + ((int)stripesSoFar * SecretConsumeRateBytes), (int)stripesToEndOfBlock);
            ScrambleAccumulators(accumulators, secret + (SecretLengthBytes - StripeLengthBytes));
            Accumulate(accumulators, source + ((int)stripesToEndOfBlock * StripeLengthBytes), secret, (int)stripesAfterBlock);
            stripesSoFar = stripesAfterBlock;
        }
        else
        {
            Accumulate(accumulators, source, secret + ((int)stripesSoFar * SecretConsumeRateBytes), (int)stripes);
            stripesSoFar += stripes;
        }
    }

    public static void DigestLong(ref State state, ulong* accumulators, byte* secret)
    {
        Debug.Assert(state.BufferedCount > 0, "BufferedCount was expected to be greater than zero.");

        fixed (byte* buffer = &state.Buffer[0])
        {
            byte* accumulateData;
            if (state.BufferedCount >= StripeLengthBytes)
            {
                uint stripes = (state.BufferedCount - 1) / StripeLengthBytes;
                ulong stripesSoFar = state.StripesProcessedInCurrentBlock;

                ConsumeStripes(accumulators, ref stripesSoFar, NumStripesPerBlock, buffer, stripes, secret);

                accumulateData = buffer + state.BufferedCount - StripeLengthBytes;
            }
            else
            {
                byte* lastStripe = stackalloc byte[StripeLengthBytes];
                int catchupSize = StripeLengthBytes - (int)state.BufferedCount;

#if NET
                new ReadOnlySpan<byte>(buffer + InternalBufferLengthBytes - catchupSize, catchupSize).CopyTo(new Span<byte>(lastStripe, StripeLengthBytes));
                new ReadOnlySpan<byte>(buffer, (int)state.BufferedCount).CopyTo(new Span<byte>(lastStripe + catchupSize, (int)state.BufferedCount));
#else
                Buffer.MemoryCopy(buffer + InternalBufferLengthBytes - catchupSize, lastStripe, StripeLengthBytes, catchupSize);
                Buffer.MemoryCopy(buffer, lastStripe + catchupSize, (int)state.BufferedCount, (int)state.BufferedCount);
#endif
                accumulateData = lastStripe;
            }

            Accumulate512(accumulators, accumulateData, secret + (SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitializeAccumulators(ulong* accumulators)
    {
#if NET
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256.Store(Vector256.Create(Prime32_3, Prime64_1, Prime64_2, Prime64_3), accumulators);
            Vector256.Store(Vector256.Create(Prime64_4, Prime32_2, Prime64_5, Prime32_1), accumulators + 4);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128.Store(Vector128.Create(Prime32_3, Prime64_1), accumulators);
            Vector128.Store(Vector128.Create(Prime64_2, Prime64_3), accumulators + 2);
            Vector128.Store(Vector128.Create(Prime64_4, Prime32_2), accumulators + 4);
            Vector128.Store(Vector128.Create(Prime64_5, Prime32_1), accumulators + 6);
        }
        else
#endif
        {
            accumulators[0] = Prime32_3;
            accumulators[1] = Prime64_1;
            accumulators[2] = Prime64_2;
            accumulators[3] = Prime64_3;
            accumulators[4] = Prime64_4;
            accumulators[5] = Prime32_2;
            accumulators[6] = Prime64_5;
            accumulators[7] = Prime32_1;
        }
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

    public static void DeriveSecretFromSeed(byte* destinationSecret, ulong seed)
    {
#if NET
        fixed (byte* defaultSecret = &MemoryMarshal.GetReference(DefaultSecret))
#else
        fixed (byte* defaultSecret = DefaultSecret)
#endif
        {
#if NET
            if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
            {
                var seedVec = Vector256.Create(seed, 0u - seed, seed, 0u - seed);
                for (int i = 0; i < SecretLengthBytes; i += Vector256<byte>.Count)
                {
                    Vector256.Store(Vector256.Load((ulong*)(defaultSecret + i)) + seedVec, (ulong*)(destinationSecret + i));
                }
            }
            else if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian)
            {
                var seedVec = Vector128.Create(seed, 0u - seed);
                for (int i = 0; i < SecretLengthBytes; i += Vector128<byte>.Count)
                {
                    Vector128.Store(Vector128.Load((ulong*)(defaultSecret + i)) + seedVec, (ulong*)(destinationSecret + i));
                }
            }
            else
#endif
            {
                for (int i = 0; i < SecretLengthBytes; i += sizeof(ulong) * 2)
                {
                    WriteUInt64LE(destinationSecret + i, ReadUInt64LE(defaultSecret + i) + seed);
                    WriteUInt64LE(destinationSecret + i + 8, ReadUInt64LE(defaultSecret + i + 8) - seed);
                }
            }
        }
    }
}
