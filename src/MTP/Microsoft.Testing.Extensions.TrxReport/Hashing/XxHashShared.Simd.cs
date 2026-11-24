// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Adapted from https://github.com/dotnet/runtime/blob/cd6580ff499dd9ae37656edcf2239bbab2362251/src/libraries/System.IO.Hashing/src/System/IO/Hashing/XxHashShared.cs
// Vectorized accumulation and scrambling operations, split from the upstream implementation.
#if NET
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.Wasm;
using System.Runtime.Intrinsics.X86;
#endif

#pragma warning disable RS0030 // Do not use banned APIs - Debug is okay here. RoslynDebug isn't yet available in PlatformServices which links this file.

namespace System.IO.Hashing;

internal static unsafe partial class XxHashShared
{
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
        else if (Sse2.IsSupported)
        {
            var sourceLow = Vector128.Shuffle(source, Vector128.Create(1u, 0, 3, 0));
            return Sse2.Multiply(source, sourceLow);
        }
        else if (PackedSimd.IsSupported)
        {
            // PackedSimd.MultiplyWideningLower (i64x2.extmul_low_i32x4_u) does
            // result[i] = (ulong)a[i] * (ulong)b[i] for i in {0, 1}.
            // We need { source[0]*source[1], source[2]*source[3] } to match the Sse2/AdvSimd paths,
            // so first move the even lanes into one operand and the odd lanes into the other.
            var evens = Vector128.Shuffle(source, Vector128.Create(0u, 2, 0, 0));
            var odds = Vector128.Shuffle(source, Vector128.Create(1u, 3, 0, 0));
            return PackedSimd.MultiplyWideningLower(evens, odds);
        }
        else
        {
            var sourceLow = Vector128.Shuffle(source, Vector128.Create(1u, 0, 3, 0));
            return (source & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64() * (sourceLow & Vector128.Create(~0u, 0u, ~0u, 0u)).AsUInt64();
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

}
