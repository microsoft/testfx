﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
#if NET
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

using Microsoft.Testing.Platform;

namespace System.IO.Hashing;

/// <summary>Shared implementation of the XXH3 hash algorithm for 64-bit in XxHash3 and <see cref="XxHash128"/> version.</summary>
#if NET
[SkipLocalsInit]
#endif
internal static unsafe class XxHashShared
{
    public const int StripeLengthBytes = 64;
    public const int SecretLengthBytes = 192;
    public const int SecretSizeMin = 136;
    public const int SecretLastAccStartBytes = 7;
    public const int SecretConsumeRateBytes = 8;
    public const int SecretMergeAccsStartBytes = 11;
    public const int NumStripesPerBlock = (SecretLengthBytes - StripeLengthBytes) / SecretConsumeRateBytes;
    public const int AccumulatorCount = StripeLengthBytes / sizeof(ulong);
    public const int MidSizeMaxBytes = 240;
    public const int InternalBufferStripes = InternalBufferLengthBytes / StripeLengthBytes;
    public const int InternalBufferLengthBytes = 256;

    // Cast of DefaultSecret byte[] => ulong[] (See above for the correspondence)
#pragma warning disable SA1310 // Field names should not contain underscore
    public const ulong DefaultSecretUInt64_0 = 0xBE4BA423396CFEB8;
    public const ulong DefaultSecretUInt64_1 = 0x1CAD21F72C81017C;
    public const ulong DefaultSecretUInt64_2 = 0xDB979083E96DD4DE;
    public const ulong DefaultSecretUInt64_3 = 0x1F67B3B7A4A44072;
    public const ulong DefaultSecretUInt64_4 = 0x78E5C0CC4EE679CB;
    public const ulong DefaultSecretUInt64_5 = 0x2172FFCC7DD05A82;
    public const ulong DefaultSecretUInt64_6 = 0x8E2443F7744608B8;
    public const ulong DefaultSecretUInt64_7 = 0x4C263A81E69035E0;
    public const ulong DefaultSecretUInt64_8 = 0xCB00C391BB52283C;
    public const ulong DefaultSecretUInt64_9 = 0xA32E531B8B65D088;
    public const ulong DefaultSecretUInt64_10 = 0x4EF90DA297486471;
    public const ulong DefaultSecretUInt64_11 = 0xD8ACDEA946EF1938;
    public const ulong DefaultSecretUInt64_12 = 0x3F349CE33F76FAA8;
    public const ulong DefaultSecretUInt64_13 = 0x1D4F0BC7C7BBDCF9;
    public const ulong DefaultSecretUInt64_14 = 0x3159B4CD4BE0518A;
    public const ulong DefaultSecretUInt64_15 = 0x647378D9C97E9FC8;

    // Cast of DefaultSecret offset by 3 bytes, byte[] => ulong[]
    public const ulong DefaultSecret3UInt64_0 = 0x81017CBE4BA42339;
    public const ulong DefaultSecret3UInt64_1 = 0x6DD4DE1CAD21F72C;
    public const ulong DefaultSecret3UInt64_2 = 0xA44072DB979083E9;
    public const ulong DefaultSecret3UInt64_3 = 0xE679CB1F67B3B7A4;
    public const ulong DefaultSecret3UInt64_4 = 0xD05A8278E5C0CC4E;
    public const ulong DefaultSecret3UInt64_5 = 0x4608B82172FFCC7D;
    public const ulong DefaultSecret3UInt64_6 = 0x9035E08E2443F774;
    public const ulong DefaultSecret3UInt64_7 = 0x52283C4C263A81E6;
    public const ulong DefaultSecret3UInt64_8 = 0x65D088CB00C391BB;
    public const ulong DefaultSecret3UInt64_9 = 0x486471A32E531B8B;
    public const ulong DefaultSecret3UInt64_10 = 0xEF19384EF90DA297;
    public const ulong DefaultSecret3UInt64_11 = 0x76FAA8D8ACDEA946;
    public const ulong DefaultSecret3UInt64_12 = 0xBBDCF93F349CE33F;
    public const ulong DefaultSecret3UInt64_13 = 0xE0518A1D4F0BC7C7;

    public const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    public const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    public const ulong Prime64_3 = 0x165667B19E3779F9UL;
    public const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    public const ulong Prime64_5 = 0x27D4EB2F165667C5UL;

    public const uint Prime32_1 = 0x9E3779B1U;
    public const uint Prime32_2 = 0x85EBCA77U;
    public const uint Prime32_3 = 0xC2B2AE3DU;
    public const uint Prime32_4 = 0x27D4EB2FU;
    public const uint Prime32_5 = 0x165667B1U;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>Gets the default secret for when no seed is provided.</summary>
    /// <remarks>This is the same as a custom secret derived from a seed of 0.</remarks>
    public static
#if NET
        ReadOnlySpan<byte> DefaultSecret =>
#else
        readonly byte[] DefaultSecret =
#endif
    [
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, // DefaultSecretUInt64_0
        0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c, // DefaultSecretUInt64_1
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, // DefaultSecretUInt64_2
        0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f, // DefaultSecretUInt64_3
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, // DefaultSecretUInt64_4
        0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21, // DefaultSecretUInt64_5
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, // DefaultSecretUInt64_6
        0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c, // DefaultSecretUInt64_7
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, // DefaultSecretUInt64_8
        0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3, // DefaultSecretUInt64_9
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, // DefaultSecretUInt64_10
        0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8, // DefaultSecretUInt64_11
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, // DefaultSecretUInt64_12
        0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d, // DefaultSecretUInt64_13
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, // DefaultSecretUInt64_14
        0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64, // DefaultSecretUInt64_15
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, // DefaultSecretUInt64_16
        0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb, // DefaultSecretUInt64_17
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, // DefaultSecretUInt64_18
        0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e, // DefaultSecretUInt64_19
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, // DefaultSecretUInt64_20
        0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce, // DefaultSecretUInt64_21
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, // DefaultSecretUInt64_22
        0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e, // DefaultSecretUInt64_23
    ];

#if DEBUG && NET // TODO
    static XxHashShared()
    {
        // Make sure DefaultSecret is the custom secret derived from a seed of 0.
        byte* secret = stackalloc byte[SecretLengthBytes];
        DeriveSecretFromSeed(secret, 0);

        RoslynDebug.Assert(DefaultSecret.Length == SecretLengthBytes);
        for (int i = 0; i < DefaultSecret.Length; i++)
        {
            RoslynDebug.Assert(DefaultSecret[i] == secret[i]);
        }

        // Validate some relationships.
        RoslynDebug.Assert(InternalBufferLengthBytes % StripeLengthBytes == 0);

        ReadOnlySpan<ulong> defaultSecretUInt64 = MemoryMarshal.Cast<byte, ulong>(DefaultSecret);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[0]) == DefaultSecretUInt64_0);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[1]) == DefaultSecretUInt64_1);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[2]) == DefaultSecretUInt64_2);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[3]) == DefaultSecretUInt64_3);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[4]) == DefaultSecretUInt64_4);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[5]) == DefaultSecretUInt64_5);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[6]) == DefaultSecretUInt64_6);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[7]) == DefaultSecretUInt64_7);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[8]) == DefaultSecretUInt64_8);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[9]) == DefaultSecretUInt64_9);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[10]) == DefaultSecretUInt64_10);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[11]) == DefaultSecretUInt64_11);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[12]) == DefaultSecretUInt64_12);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[13]) == DefaultSecretUInt64_13);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[14]) == DefaultSecretUInt64_14);
        RoslynDebug.Assert(ReadLE64(defaultSecretUInt64[15]) == DefaultSecretUInt64_15);

        ReadOnlySpan<ulong> defaultSecret3UInt64 = MemoryMarshal.Cast<byte, ulong>(DefaultSecret.Slice(3));
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[0]) == DefaultSecret3UInt64_0);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[1]) == DefaultSecret3UInt64_1);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[2]) == DefaultSecret3UInt64_2);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[3]) == DefaultSecret3UInt64_3);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[4]) == DefaultSecret3UInt64_4);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[5]) == DefaultSecret3UInt64_5);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[6]) == DefaultSecret3UInt64_6);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[7]) == DefaultSecret3UInt64_7);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[8]) == DefaultSecret3UInt64_8);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[9]) == DefaultSecret3UInt64_9);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[10]) == DefaultSecret3UInt64_10);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[11]) == DefaultSecret3UInt64_11);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[12]) == DefaultSecret3UInt64_12);
        RoslynDebug.Assert(ReadLE64(defaultSecret3UInt64[13]) == DefaultSecret3UInt64_13);

        static ulong ReadLE64(ulong data) => BitConverter.IsLittleEndian ? data : BinaryPrimitives.ReverseEndianness(data);
    }
#endif

    public static void Initialize(ref State state, ulong seed)
    {
        state.Seed = seed;

        fixed (byte* secret = state.Secret)
        {
            if (seed == 0)
            {
#if NET
                DefaultSecret.CopyTo(new Span<byte>(secret, SecretLengthBytes));
#else
                for (int i = 0; i < SecretLengthBytes; i++)
                {
                    secret[i] = DefaultSecret[i];
                }
#endif
            }
            else
            {
                DeriveSecretFromSeed(secret, seed);
            }
        }

        Reset(ref state);
    }

    public static void Reset(ref State state)
    {
        state.BufferedCount = 0;
        state.StripesProcessedInCurrentBlock = 0;
        state.TotalLength = 0;

        fixed (ulong* accumulators = state.Accumulators)
        {
            InitializeAccumulators(accumulators);
        }
    }

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
        RoslynDebug.Assert(length > 240);

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
        RoslynDebug.Assert(stripes <= stripesPerBlock); // can handle max 1 scramble per invocation
        RoslynDebug.Assert(stripesSoFar < stripesPerBlock);

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

    public static void CopyAccumulators(ref State state, ulong* accumulators)
    {
        fixed (ulong* stateAccumulators = state.Accumulators)
        {
#if NET
            if (Vector256.IsHardwareAccelerated)
            {
                Vector256.Store(Vector256.Load(stateAccumulators), accumulators);
                Vector256.Store(Vector256.Load(stateAccumulators + 4), accumulators + 4);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                Vector128.Store(Vector128.Load(stateAccumulators), accumulators);
                Vector128.Store(Vector128.Load(stateAccumulators + 2), accumulators + 2);
                Vector128.Store(Vector128.Load(stateAccumulators + 4), accumulators + 4);
                Vector128.Store(Vector128.Load(stateAccumulators + 6), accumulators + 6);
            }
            else
#endif
            {
                for (int i = 0; i < 8; i++)
                {
                    accumulators[i] = stateAccumulators[i];
                }
            }
        }
    }

    public static void DigestLong(ref State state, ulong* accumulators, byte* secret)
    {
        RoslynDebug.Assert(state.BufferedCount > 0);

        fixed (byte* buffer = state.Buffer)
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

    /// <summary>Optimized version of looping over <see cref="Accumulate512"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Accumulate(ulong* accumulators, byte* source, byte* secret, int stripesToProcess, bool scramble = false, int blockCount = 1)
    {
        byte* secretForAccumulate = secret;
        byte* secretForScramble = secret + (SecretLengthBytes - StripeLengthBytes);

#if NET
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            var acc1 = Vector256.Load(accumulators);
            var acc2 = Vector256.Load(accumulators + Vector256<ulong>.Count);

            for (int j = 0; j < blockCount; j++)
            {
                secret = secretForAccumulate;
                for (int i = 0; i < stripesToProcess; i++)
                {
                    var secretVal = Vector256.Load((uint*)secret);
                    acc1 = Accumulate256(acc1, source, secretVal);
                    source += Vector256<byte>.Count;

                    secretVal = Vector256.Load((uint*)secret + Vector256<uint>.Count);
                    acc2 = Accumulate256(acc2, source, secretVal);
                    source += Vector256<byte>.Count;

                    secret += SecretConsumeRateBytes;
                }

                if (scramble)
                {
                    acc1 = ScrambleAccumulator256(acc1, Vector256.Load((ulong*)secretForScramble));
                    acc2 = ScrambleAccumulator256(acc2, Vector256.Load((ulong*)secretForScramble + Vector256<ulong>.Count));
                }
            }

            Vector256.Store(acc1, accumulators);
            Vector256.Store(acc2, accumulators + Vector256<ulong>.Count);
        }
        else if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            var acc1 = Vector128.Load(accumulators);
            var acc2 = Vector128.Load(accumulators + Vector128<ulong>.Count);
            var acc3 = Vector128.Load(accumulators + (Vector128<ulong>.Count * 2));
            var acc4 = Vector128.Load(accumulators + (Vector128<ulong>.Count * 3));

            for (int j = 0; j < blockCount; j++)
            {
                secret = secretForAccumulate;
                for (int i = 0; i < stripesToProcess; i++)
                {
                    var secretVal = Vector128.Load((uint*)secret);
                    acc1 = Accumulate128(acc1, source, secretVal);
                    source += Vector128<byte>.Count;

                    secretVal = Vector128.Load((uint*)secret + Vector128<uint>.Count);
                    acc2 = Accumulate128(acc2, source, secretVal);
                    source += Vector128<byte>.Count;

                    secretVal = Vector128.Load((uint*)secret + (Vector128<uint>.Count * 2));
                    acc3 = Accumulate128(acc3, source, secretVal);
                    source += Vector128<byte>.Count;

                    secretVal = Vector128.Load((uint*)secret + (Vector128<uint>.Count * 3));
                    acc4 = Accumulate128(acc4, source, secretVal);
                    source += Vector128<byte>.Count;

                    secret += SecretConsumeRateBytes;
                }

                if (scramble)
                {
                    acc1 = ScrambleAccumulator128(acc1, Vector128.Load((ulong*)secretForScramble));
                    acc2 = ScrambleAccumulator128(acc2, Vector128.Load((ulong*)secretForScramble + Vector128<ulong>.Count));
                    acc3 = ScrambleAccumulator128(acc3, Vector128.Load((ulong*)secretForScramble + (Vector128<ulong>.Count * 2)));
                    acc4 = ScrambleAccumulator128(acc4, Vector128.Load((ulong*)secretForScramble + (Vector128<ulong>.Count * 3)));
                }
            }

            Vector128.Store(acc1, accumulators);
            Vector128.Store(acc2, accumulators + Vector128<ulong>.Count);
            Vector128.Store(acc3, accumulators + (Vector128<ulong>.Count * 2));
            Vector128.Store(acc4, accumulators + (Vector128<ulong>.Count * 3));
        }
        else
#endif
        {
            for (int j = 0; j < blockCount; j++)
            {
                for (int i = 0; i < stripesToProcess; i++)
                {
                    Accumulate512Inlined(accumulators, source, secret + (i * SecretConsumeRateBytes));
                    source += StripeLengthBytes;
                }

                if (scramble)
                {
                    ScrambleAccumulators(accumulators, secretForScramble);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Accumulate512(ulong* accumulators, byte* source, byte* secret)
        => Accumulate512Inlined(accumulators, source, secret);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate512Inlined(ulong* accumulators, byte* source, byte* secret)
    {
#if NET
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector256<ulong>.Count; i++)
            {
                Vector256<ulong> accVec = Accumulate256(Vector256.Load(accumulators), source, Vector256.Load((uint*)secret));
                Vector256.Store(accVec, accumulators);

                accumulators += Vector256<ulong>.Count;
                secret += Vector256<byte>.Count;
                source += Vector256<byte>.Count;
            }
        }
        else if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector128<ulong>.Count; i++)
            {
                Vector128<ulong> accVec = Accumulate128(Vector128.Load(accumulators), source, Vector128.Load((uint*)secret));
                Vector128.Store(accVec, accumulators);

                accumulators += Vector128<ulong>.Count;
                secret += Vector128<byte>.Count;
                source += Vector128<byte>.Count;
            }
        }
        else
#endif
        {
            for (int i = 0; i < AccumulatorCount; i++)
            {
                ulong sourceVal = ReadUInt64LE(source + (8 * i));
                ulong sourceKey = sourceVal ^ ReadUInt64LE(secret + (i * 8));

                accumulators[i ^ 1] += sourceVal; // swap adjacent lanes
                accumulators[i] += Multiply32To64((uint)sourceKey, (uint)(sourceKey >> 32));
            }
        }
    }

#if NET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> Accumulate256(Vector256<ulong> accVec, byte* source, Vector256<uint> secret)
    {
        var sourceVec = Vector256.Load((uint*)source);
        Vector256<uint> sourceKey = sourceVec ^ secret;

        // TODO: Figure out how to unwind this shuffle and just use Vector256.Multiply
        var sourceKeyLow = Vector256.Shuffle(sourceKey, Vector256.Create(1u, 0, 3, 0, 5, 0, 7, 0));
        var sourceSwap = Vector256.Shuffle(sourceVec, Vector256.Create(2u, 3, 0, 1, 6, 7, 4, 5));
        Vector256<ulong> sum = accVec + sourceSwap.AsUInt64();
        Vector256<ulong> product = Avx2.IsSupported ?
            Avx2.Multiply(sourceKey, sourceKeyLow) :
            (sourceKey & Vector256.Create(~0u, 0u, ~0u, 0u, ~0u, 0u, ~0u, 0u)).AsUInt64() * (sourceKeyLow & Vector256.Create(~0u, 0u, ~0u, 0u, ~0u, 0u, ~0u, 0u)).AsUInt64();

        accVec = product + sum;
        return accVec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> Accumulate128(Vector128<ulong> accVec, byte* source, Vector128<uint> secret)
    {
        var sourceVec = Vector128.Load((uint*)source);
        Vector128<uint> sourceKey = sourceVec ^ secret;

        // TODO: Figure out how to unwind this shuffle and just use Vector128.Multiply
        var sourceSwap = Vector128.Shuffle(sourceVec, Vector128.Create(2u, 3, 0, 1));
        Vector128<ulong> sum = accVec + sourceSwap.AsUInt64();

        Vector128<ulong> product = MultiplyWideningLower(sourceKey);
        accVec = product + sum;
        return accVec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> MultiplyWideningLower(Vector128<uint> source)
    {
        if (AdvSimd.IsSupported)
        {
            Vector64<uint> sourceLow = Vector128.Shuffle(source, Vector128.Create(0u, 2, 0, 0)).GetLower();
            Vector64<uint> sourceHigh = Vector128.Shuffle(source, Vector128.Create(1u, 3, 0, 0)).GetLower();
            return AdvSimd.MultiplyWideningLower(sourceLow, sourceHigh);
        }
        else
        {
            var sourceLow = Vector128.Shuffle(source, Vector128.Create(1u, 0, 3, 0));
            return Sse2.IsSupported ?
                Sse2.Multiply(source, sourceLow) :
                (source & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64() * (sourceLow & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64();
        }
    }
#endif

    private static void ScrambleAccumulators(ulong* accumulators, byte* secret)
    {
#if NET
        if (Vector256.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector256<ulong>.Count; i++)
            {
                Vector256<ulong> accVec = ScrambleAccumulator256(Vector256.Load(accumulators), Vector256.Load((ulong*)secret));
                Vector256.Store(accVec, accumulators);

                accumulators += Vector256<ulong>.Count;
                secret += Vector256<byte>.Count;
            }
        }
        else if (Vector128.IsHardwareAccelerated && BitConverter.IsLittleEndian)
        {
            for (int i = 0; i < AccumulatorCount / Vector128<ulong>.Count; i++)
            {
                Vector128<ulong> accVec = ScrambleAccumulator128(Vector128.Load(accumulators), Vector128.Load((ulong*)secret));
                Vector128.Store(accVec, accumulators);

                accumulators += Vector128<ulong>.Count;
                secret += Vector128<byte>.Count;
            }
        }
        else
#endif
        {
            for (int i = 0; i < AccumulatorCount; i++)
            {
                ulong xorShift = XorShift(*accumulators, 47);
                ulong xorWithKey = xorShift ^ ReadUInt64LE(secret);
                *accumulators = xorWithKey * Prime32_1;

                accumulators++;
                secret += sizeof(ulong);
            }
        }
    }

#if NET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ulong> ScrambleAccumulator256(Vector256<ulong> accVec, Vector256<ulong> secret)
    {
        Vector256<ulong> xorShift = accVec ^ Vector256.ShiftRightLogical(accVec, 47);
        Vector256<ulong> xorWithKey = xorShift ^ secret;
        accVec = xorWithKey * Vector256.Create((ulong)Prime32_1);
        return accVec;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> ScrambleAccumulator128(Vector128<ulong> accVec, Vector128<ulong> secret)
    {
        Vector128<ulong> xorShift = accVec ^ Vector128.ShiftRightLogical(accVec, 47);
        Vector128<ulong> xorWithKey = xorShift ^ secret;
        accVec = xorWithKey * Vector128.Create((ulong)Prime32_1);
        return accVec;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong XorShift(ulong value, int shift)
    {
        RoslynDebug.Assert(shift is >= 0 and < 64);
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
#if NET
        => Unsafe.ReadUnaligned<T>(source);
#else
        // ldarg.0
        // unaligned. 0x1
        // ldobj !!T
        // ret
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        => *(T*)source;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUnaligned<T>(void* destination, T value)
#if NET
        => Unsafe.WriteUnaligned<T>(destination, value);
#else
    // ldarg .0
    // ldarg .1
    // unaligned. 0x01
    // stobj !!T
    // ret
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    => *(T*)destination = value;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
#endif

    [StructLayout(LayoutKind.Auto)]
    public struct State
    {
        /// <summary>The accumulators. Length is <see cref="AccumulatorCount"/>.</summary>
        internal fixed ulong Accumulators[AccumulatorCount];

        /// <summary>Used to store a custom secret generated from a seed. Length is <see cref="SecretLengthBytes"/>.</summary>
        internal fixed byte Secret[SecretLengthBytes];

        /// <summary>The internal buffer. Length is <see cref="InternalBufferLengthBytes"/>.</summary>
        internal fixed byte Buffer[InternalBufferLengthBytes];

        /// <summary>The amount of memory in <see cref="Buffer"/>.</summary>
        internal uint BufferedCount;

        /// <summary>Number of stripes processed in the current block.</summary>
        internal ulong StripesProcessedInCurrentBlock;

        /// <summary>Total length hashed.</summary>
        internal ulong TotalLength;

        /// <summary>The seed employed (possibly 0).</summary>
        internal ulong Seed;
    }
}
