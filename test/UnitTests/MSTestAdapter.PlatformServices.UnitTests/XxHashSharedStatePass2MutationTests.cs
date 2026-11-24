// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using TestFramework.ForTestingMSTest;

using TestFx.Hashing;

namespace System.IO.Hashing.Tests;

public sealed class XxHashSharedStatePass2MutationTests : TestContainer
{
    // A remainder shorter than one stripe means that GetCurrentHash consumes no buffered stripe, so it can never complete
    // a block that Append left unscrambled: the scramble ending the second block has to be applied by Append itself.
    public void Append_OfTwoBlocksAndOneByte_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(2049), 0, 2049);

        hash.Should().Equal(CreateExpectedHash(0x14808FE643A33DDDUL, 0x3C48F2806F4A59ACUL));
    }

    public void Append_OfTwoBlocksAndSixtyThreeBytes_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(2111), 0, 2111);

        hash.Should().Equal(CreateExpectedHash(0x51DEA3729D7E076FUL, 0x846CB58C7D532562UL));
    }

    // Completing the internal buffer with the second chunk leaves exactly one buffer's worth of input: that remainder
    // has to stay buffered for GetCurrentHash instead of being consumed by Append, which would leave nothing behind.
    public void Append_OfOneByteThenExactlyOneBufferAfterCompletingTheBuffer_MatchesTheKnownAnswer()
    {
        byte[] hash = AppendWithoutViolatingAssertions(CreateData(512), 0, 1, 511);

        hash.Should().Equal(CreateExpectedHash(0x90C55E1047BF0012UL, 0x1D539050EF013BD8UL));
    }

#if DEBUG
    public void Append_WithAFullBlockAwaitingItsScramble_DoesNotAssertOnTheProcessedStripes()
    {
        XxHashShared.State state = default;
        XxHashShared.Initialize(ref state, 0);
        state.StripesProcessedInCurrentBlock = 16;
        byte[] source = CreateData(1025);

        List<string?> assertions = Observe(() => XxHashShared.Append(ref state, source), 1);

        assertions.Should().BeEmpty();
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
