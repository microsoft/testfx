// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Runtime.Intrinsics;
#endif

#pragma warning disable RS0030 // Do not use banned APIs - Debug is okay here. RoslynDebug isn't yet available in PlatformServices which links this file.

namespace System.IO.Hashing;

internal static unsafe partial class XxHashShared
{
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

    public static void Append(ref State state, byte[] source)
    {
        Debug.Assert(state.BufferedCount <= InternalBufferLengthBytes, "Expected state.BufferedCount to be less than or equals InternalBufferLengthBytes");

        state.TotalLength += (uint)source.Length;

        fixed (byte* buffer = state.Buffer)
        {
            // Small input: just copy the data to the buffer.
            if (source.Length <= InternalBufferLengthBytes - state.BufferedCount)
            {
#if NET
                source.CopyTo(new Span<byte>(buffer + state.BufferedCount, source.Length));
#else
                fixed (byte* sourcePtr = source)
                {
                    Buffer.MemoryCopy(sourcePtr, buffer + state.BufferedCount, source.Length, source.Length);
                }
#endif
                state.BufferedCount += (uint)source.Length;
                return;
            }

            fixed (byte* secret = state.Secret)
#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement
            fixed (ulong* accumulators = state.Accumulators)
#if NET
            fixed (byte* sourcePtr = &MemoryMarshal.GetReference(source))
#else
            fixed (byte* sourcePtr = source)
#endif
            {
                // Internal buffer is partially filled (always, except at beginning). Complete it, then consume it.
                int sourceIndex = 0;
                if (state.BufferedCount != 0)
                {
                    int loadSize = InternalBufferLengthBytes - (int)state.BufferedCount;

#if NET
                    source.AsSpan().Slice(0, loadSize).CopyTo(new Span<byte>(buffer + state.BufferedCount, loadSize));
#else
                    Buffer.MemoryCopy(sourcePtr, buffer + state.BufferedCount, loadSize, loadSize);
#endif
                    sourceIndex = loadSize;

                    ConsumeStripes(accumulators, ref state.StripesProcessedInCurrentBlock, NumStripesPerBlock, buffer, InternalBufferStripes, secret);
                    state.BufferedCount = 0;
                }

                Debug.Assert(sourceIndex < source.Length, "Expected sourceIndex to be less than source.Length");

                // Large input to consume: ingest per full block.
                if (source.Length - sourceIndex > NumStripesPerBlock * StripeLengthBytes)
                {
                    ulong stripes = (ulong)(source.Length - sourceIndex - 1) / StripeLengthBytes;
                    Debug.Assert(state.StripesProcessedInCurrentBlock <= NumStripesPerBlock, "Expected NumStripesPerBlock to be greater than or equals state.StripesProcessedInCurrentBlock");

                    // Join to current block's end.
                    ulong stripesToEnd = NumStripesPerBlock - state.StripesProcessedInCurrentBlock;
                    Debug.Assert(stripesToEnd <= stripes, "Expected stripesToEnd to be smaller than or equals stripes");
                    Accumulate(accumulators, sourcePtr + sourceIndex, secret + ((int)state.StripesProcessedInCurrentBlock * SecretConsumeRateBytes), (int)stripesToEnd);
                    ScrambleAccumulators(accumulators, secret + (SecretLengthBytes - StripeLengthBytes));
                    state.StripesProcessedInCurrentBlock = 0;
                    sourceIndex += (int)stripesToEnd * StripeLengthBytes;
                    stripes -= stripesToEnd;

                    // Consume entire blocks.
                    while (stripes >= NumStripesPerBlock)
                    {
                        Accumulate(accumulators, sourcePtr + sourceIndex, secret, NumStripesPerBlock);
                        ScrambleAccumulators(accumulators, secret + (SecretLengthBytes - StripeLengthBytes));
                        sourceIndex += NumStripesPerBlock * StripeLengthBytes;
                        stripes -= NumStripesPerBlock;
                    }

                    // Consume complete stripes in the last partial block.
                    Accumulate(accumulators, sourcePtr + sourceIndex, secret, (int)stripes);
                    sourceIndex += (int)stripes * StripeLengthBytes;
                    Debug.Assert(sourceIndex < source.Length, "Expected sourceIndex to be smaller than source.Length");  // at least some bytes left
                    state.StripesProcessedInCurrentBlock = stripes;

                    // Copy the last stripe into the end of the buffer so it is available to GetCurrentHashCore when processing the "stripe from the end".
#if NET
                    source.AsSpan().Slice(sourceIndex - StripeLengthBytes, StripeLengthBytes).CopyTo(new Span<byte>(buffer + InternalBufferLengthBytes - StripeLengthBytes, StripeLengthBytes));
#else
                    Buffer.MemoryCopy(sourcePtr + sourceIndex - StripeLengthBytes, buffer + InternalBufferLengthBytes - StripeLengthBytes, StripeLengthBytes, StripeLengthBytes);
#endif
                }
                else if (source.Length - sourceIndex > InternalBufferLengthBytes)
                {
                    // Content to consume <= block size. Consume source by a multiple of internal buffer size.
                    do
                    {
                        ConsumeStripes(accumulators, ref state.StripesProcessedInCurrentBlock, NumStripesPerBlock, sourcePtr + sourceIndex, InternalBufferStripes, secret);
                        sourceIndex += InternalBufferLengthBytes;
                    }
                    while (source.Length - sourceIndex > InternalBufferLengthBytes);

                    // Copy the last stripe into the end of the buffer so it is available to GetCurrentHashCore when processing the "stripe from the end".
#if NET
                    source.AsSpan().Slice(sourceIndex - StripeLengthBytes, StripeLengthBytes).CopyTo(new Span<byte>(buffer + InternalBufferLengthBytes - StripeLengthBytes, StripeLengthBytes));
#else
                    Buffer.MemoryCopy(sourcePtr + sourceIndex - StripeLengthBytes, buffer + InternalBufferLengthBytes - StripeLengthBytes, StripeLengthBytes, StripeLengthBytes);
#endif
                }

                // Buffer the remaining input.
#if NET
                var remaining = new Span<byte>(buffer, source.Length - sourceIndex);
                Debug.Assert(sourceIndex < source.Length, "Expected sourceIndex to be less than source.Length");
                Debug.Assert(remaining.Length <= InternalBufferLengthBytes, "Expected remaining.Length to be less than or equals InternalBufferLengthBytes");
                Debug.Assert(state.BufferedCount == 0, "Expected BufferedCount to be zero.");
                source.AsSpan().Slice(sourceIndex).CopyTo(remaining);
                state.BufferedCount = (uint)remaining.Length;
#else
                Debug.Assert(sourceIndex < source.Length, "Expected sourceIndex to be less than source.Length");
                Debug.Assert(source.Length - sourceIndex <= InternalBufferLengthBytes, "Expected remaining.Length to be less than or equals InternalBufferLengthBytes");
                Debug.Assert(state.BufferedCount == 0, "Expected BufferedCount to be zero.");
                Buffer.MemoryCopy(sourcePtr + sourceIndex, buffer, source.Length - sourceIndex, source.Length - sourceIndex);
                state.BufferedCount = (uint)(source.Length - sourceIndex);
#endif
            }
#pragma warning restore SA1519 // Braces should not be omitted from multi-line child statement
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
        Debug.Assert(state.BufferedCount > 0, "BufferedCount was expected to be greater than zero.");

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
                Buffer.MemoryCopy(buffer, lastStripe + catchupSize, (int)state.BufferedCount, (int)state.BufferedCount);
#endif
                accumulateData = lastStripe;
            }

            Accumulate512(accumulators, accumulateData, secret + (SecretLengthBytes - StripeLengthBytes - SecretLastAccStartBytes));
        }
    }
}
