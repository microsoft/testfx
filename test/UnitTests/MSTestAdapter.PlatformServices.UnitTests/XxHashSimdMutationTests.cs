// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace System.IO.Hashing.Tests;

// With Vector256 and AVX2 available, XxHashShared never reaches its Vector128 helpers, so the known-answer hash tests cannot
// notice a faulty one. These tests call the private helpers directly through reflection (the test project cannot use pointers;
// a boxed nint binds to the byte* parameter) and compare them with independent scalar implementations of the XXH3 formulas.
public sealed class XxHashSimdMutationTests : TestContainer
{
    private const ulong Prime32One = 0x9E3779B1UL;
    private const ulong LowerHalfMask = 0xFFFFFFFFUL;

    public void ScrambleAccumulator128ReturnsHandComputedKnownAnswers()
    {
        // Both accumulators are below 2^47, so the xor-shift is a no-op and only the key xor remains: (1 ^ 3) == 2 and (2 ^ 4) == 6.
        Vector128<ulong> result = InvokeSimdHelper<Vector128<ulong>>("ScrambleAccumulator128", Vector128.Create(1UL, 2UL), Vector128.Create(3UL, 4UL));

        result.GetElement(0).Should().Be(2 * Prime32One, because: "lane 0 is ((1 ^ (1 >> 47)) ^ 3) * Prime32_1");
        result.GetElement(1).Should().Be(6 * Prime32One, because: "lane 1 is ((2 ^ (2 >> 47)) ^ 4) * Prime32_1");
    }

    public void ScrambleAccumulator128MatchesTheScalarFormulaOnFullWidthValues()
    {
        ulong[][] cases =
        [
            [0x0123456789ABCDEFUL, 0xFEDCBA9876543210UL, 0xA5A5A5A55A5A5A5AUL, 0x0F1E2D3C4B5A6978UL],
            [ulong.MaxValue, 0x8000000000000000UL, 0UL, ulong.MaxValue],
            [0x0000800000000000UL, 0x00007FFFFFFFFFFFUL, 0x9E3779B185EBCA87UL, 0x0000000100000001UL],
        ];

        for (int i = 0; i < cases.Length; i++)
        {
            ulong[] c = cases[i];
            Vector128<ulong> result = InvokeSimdHelper<Vector128<ulong>>("ScrambleAccumulator128", Vector128.Create(c[0], c[1]), Vector128.Create(c[2], c[3]));

            result.GetElement(0).Should().Be(ScrambleReference(c[0], c[2]), because: $"lane 0 of case {i} is xor-shifted, xored with its secret lane and multiplied by Prime32_1");
            result.GetElement(1).Should().Be(ScrambleReference(c[1], c[3]), because: $"lane 1 of case {i} is xor-shifted, xored with its secret lane and multiplied by Prime32_1");
        }
    }

    public void MultiplyWideningLowerMultipliesEachEvenLaneByTheFollowingOddLane()
    {
        Vector128<ulong> small = InvokeSimdHelper<Vector128<ulong>>("MultiplyWideningLower", Vector128.Create(2U, 3U, 5U, 7U));

        small.GetElement(0).Should().Be(6UL, because: "lane 0 is source[0] * source[1]");
        small.GetElement(1).Should().Be(35UL, because: "lane 1 is source[2] * source[3]");

        uint[][] cases =
        [
            [uint.MaxValue, uint.MaxValue, 0x80000001U, 0x00010003U],
            [0xDEADBEEFU, 0x01234567U, 0x89ABCDEFU, 0xFEDCBA98U],
            [0U, uint.MaxValue, uint.MaxValue, 0U],
        ];

        for (int i = 0; i < cases.Length; i++)
        {
            uint[] c = cases[i];
            Vector128<ulong> result = InvokeSimdHelper<Vector128<ulong>>("MultiplyWideningLower", Vector128.Create(c[0], c[1], c[2], c[3]));

            result.GetElement(0).Should().Be((ulong)c[0] * c[1], because: $"lane 0 of case {i} is the full 64-bit product of source[0] and source[1]");
            result.GetElement(1).Should().Be((ulong)c[2] * c[3], because: $"lane 1 of case {i} is the full 64-bit product of source[2] and source[3]");
        }
    }

    public void Accumulate128AddsTheSwappedDataLaneAndTheKeyProductToEachAccumulator()
    {
        // data0 = 0x0000000300000002 and data1 = 0x0000000500000004, both read little-endian from the source bytes.
        byte[] source = [2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 0, 0, 5, 0, 0, 0];

        // With a zero secret the keys equal the data, so the widening products are 2 * 3 and 4 * 5.
        Vector128<ulong> result = InvokeAccumulate128(Vector128.Create(100UL, 200UL), source, Vector128<uint>.Zero);

        result.GetElement(0).Should().Be(100UL + 0x0000000500000004UL + (2UL * 3UL), because: "accumulator 0 receives the second data lane and the product of the first");
        result.GetElement(1).Should().Be(200UL + 0x0000000300000002UL + (4UL * 5UL), because: "accumulator 1 receives the first data lane and the product of the second");
    }

    public void Accumulate128MatchesTheScalarFormulaOnFullWidthValues()
    {
        AssertAccumulate128MatchesScalarFormula(
            1UL,
            2UL,
            [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16],
            [3U, 4U, 5U, 6U]);

        AssertAccumulate128MatchesScalarFormula(
            ulong.MaxValue,
            0x8000000000000001UL,
            [0xF3, 0x1B, 0x8E, 0xD5, 0x07, 0xA9, 0x62, 0xC4, 0x3D, 0xFE, 0x90, 0x2B, 0x75, 0xE8, 0x14, 0xB6],
            [0xDEADBEEFU, 0x01234567U, 0x89ABCDEFU, 0xFEDCBA98U]);

        AssertAccumulate128MatchesScalarFormula(
            0x0123456789ABCDEFUL,
            0xFEDCBA9876543210UL,
            [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80],
            [0U, 0xFFFFFFFFU, 0x80000000U, 1U]);
    }

    private static void AssertAccumulate128MatchesScalarFormula(ulong accumulator0, ulong accumulator1, byte[] source, uint[] secret)
    {
        Vector128<ulong> result = InvokeAccumulate128(Vector128.Create(accumulator0, accumulator1), source, Vector128.Create(secret[0], secret[1], secret[2], secret[3]));

        ulong data0 = BinaryPrimitives.ReadUInt64LittleEndian(source);
        ulong data1 = BinaryPrimitives.ReadUInt64LittleEndian(source.AsSpan(8));
        ulong secret0 = secret[0] | ((ulong)secret[1] << 32);
        ulong secret1 = secret[2] | ((ulong)secret[3] << 32);

        result.GetElement(0).Should().Be(AccumulateReference(accumulator0, data0, data1, secret0), because: $"accumulator 0 starts at {accumulator0} and receives data1 plus the key product of data0");
        result.GetElement(1).Should().Be(AccumulateReference(accumulator1, data1, data0, secret1), because: $"accumulator 1 starts at {accumulator1} and receives data0 plus the key product of data1");
    }

    private static ulong ScrambleReference(ulong accumulator, ulong secret)
        => unchecked(((accumulator ^ (accumulator >> 47)) ^ secret) * Prime32One);

    private static ulong AccumulateReference(ulong accumulator, ulong ownData, ulong swappedData, ulong secret)
    {
        ulong key = ownData ^ secret;
        return unchecked(accumulator + swappedData + ((key & LowerHalfMask) * (key >> 32)));
    }

    private static Vector128<ulong> InvokeAccumulate128(Vector128<ulong> accumulator, byte[] source, Vector128<uint> secret)
    {
        nint buffer = Marshal.AllocHGlobal(source.Length);
        try
        {
            Marshal.Copy(source, 0, buffer, source.Length);
            return InvokeSimdHelper<Vector128<ulong>>("Accumulate128", accumulator, buffer, secret);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static TResult InvokeSimdHelper<TResult>(string methodName, params object[] arguments)
    {
        MethodInfo? method = typeof(XxHashShared).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull(because: $"XxHashShared is expected to declare the private static helper '{methodName}'");

        try
        {
            return (TResult)method!.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
#endif
