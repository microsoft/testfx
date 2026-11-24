// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using TestFramework.ForTestingMSTest;

using TestFx.Hashing;

namespace System.IO.Hashing.Tests;

public sealed class XxHashSharedStateMutationTests : TestContainer
{
#if DEBUG
    private const string BufferedCountMessage = "Expected state.BufferedCount to be less than or equals InternalBufferLengthBytes";
    private const string StripesProcessedMessage = "Expected NumStripesPerBlock to be greater than or equals state.StripesProcessedInCurrentBlock";
    private const string StripesToEndMessage = "Expected stripesToEnd to be smaller than or equals stripes";
#endif

    public void Append_OfTwoInternalBuffers_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(512), 0, 512);

        hash.Should().Equal(CreateExpectedHash(0x90C55E1047BF0012UL, 0x1D539050EF013BD8UL));
    }

    public void Append_OfExactlyOneBlock_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(1024), 0, 1024);

        hash.Should().Equal(CreateExpectedHash(0xBB8BC46463EAA7B0UL, 0x19162C2639BAF234UL));
    }

    public void Append_OfOneBlockAndOneByte_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(1025), 0, 1025);

        hash.Should().Equal(CreateExpectedHash(0xB522F3537B1144C3UL, 0xEB23735EEF7E157AUL));
    }

    public void Append_OfOneBlockAndOneByteWithAnExtremeSeed_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(1025), unchecked((long)0x9E3779B97F4A7C15UL), 1025);

        hash.Should().Equal(CreateExpectedHash(0x014184A6499BF187UL, 0x68F379C5A4B13EBDUL));
    }

    public void Append_OfASmallChunkFollowedByALargeChunk_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(1000), 0, 100, 900);

        hash.Should().Equal(CreateExpectedHash(0xDC9C659A646D3CCAUL, 0x5A26F396E966CB95UL));
    }

#if DEBUG
    public void Append_WithMoreBufferedBytesThanTheInternalBuffer_AssertsOnTheBufferedCount()
    {
        XxHashShared.State state = default;
        XxHashShared.Initialize(ref state, 0);
        state.BufferedCount = 257;

        List<string?> assertions = Observe(() => XxHashShared.Append(ref state, []), 1);

        assertions.Should().Equal(BufferedCountMessage);
    }

    public void Append_WithMoreProcessedStripesThanABlock_AssertsOnTheProcessedStripesAndTheStripesToTheEndOfTheBlock()
    {
        XxHashShared.State state = default;
        XxHashShared.Initialize(ref state, 0);
        state.StripesProcessedInCurrentBlock = 17;
        byte[] source = CreateData(1025);

        List<string?> assertions = Observe(() => XxHashShared.Append(ref state, source), 2);

        assertions.Should().Equal(StripesProcessedMessage, StripesToEndMessage);
    }
#endif

    private static byte[] AppendWithoutViolatingAssertions(byte[] data, long seed, params int[] chunkSizes)
    {
#if DEBUG
        byte[]? hash = null;
        List<string?> assertions = Observe(() => hash = AppendAll(data, seed, chunkSizes), 1);

        assertions.Should().BeEmpty();
        return hash!;
#else
        return AppendAll(data, seed, chunkSizes);
#endif
    }

    private static byte[] AppendAll(byte[] data, long seed, int[] chunkSizes)
    {
        var hasher = new XxHash128(seed);
        int offset = 0;
        foreach (int size in chunkSizes)
        {
            byte[] chunk = new byte[size];
            Buffer.BlockCopy(data, offset, chunk, 0, size);
            hasher.Append(chunk);
            offset += size;
        }

        return hasher.GetCurrentHash();
    }

    private static byte[] CreateData(int length)
    {
        byte[] data = new byte[length];
        uint value = 0x1234567;
        for (int i = 0; i < data.Length; i++)
        {
            value = unchecked((value * 1103515245) + 12345);
            data[i] = (byte)(value >> 16);
        }

        return data;
    }

    private static byte[] CreateExpectedHash(ulong high, ulong low)
    {
        byte[] hash = new byte[16];
        for (int i = 0; i < 8; i++)
        {
            hash[i] = (byte)(high >> (56 - (8 * i)));
            hash[8 + i] = (byte)(low >> (56 - (8 * i)));
        }

        return hash;
    }

#if DEBUG
    // Stops the production code at the given assertion failure, so that a mutant violating an invariant cannot run away.
    private static List<string?> Observe(Action action, int stopAtAssertionNumber)
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(message =>
        {
            assertions.Add(message);
            if (assertions.Count == stopAtAssertionNumber)
            {
                throw new AssertionObservedException(message ?? string.Empty);
            }
        });

        try
        {
            action();
        }
        catch (AssertionObservedException)
        {
            // Expected: the observer interrupted the production code.
        }

        return assertions;
    }

    private sealed class AssertionObservedException(string message) : Exception(message);
#endif
}
