// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// These tests observe Debug.Assert failures, which only exist in DEBUG builds.
#if DEBUG
using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;

using TestFramework.ForTestingMSTest;

using TestFx.Hashing;

namespace System.IO.Hashing.Tests;

public sealed class XxHashSharedCoreMutationTests : TestContainer
{
    private const string LengthMessage = "Length was expected to be greater than 240.";
    private const string StripesMessage = "stripes was expected to less than or equals stripesPerBlock";
    private const string StripesSoFarMessage = "stripesSoFar was expected to be less than stripesPerBlock";
    private const string BufferedCountMessage = "BufferedCount was expected to be greater than zero.";

    public void HashInternalLoop_AssertsThatTheLengthIsGreaterThan240()
    {
        InvokeHashInternalLoop(240).Should().Equal(LengthMessage);
        InvokeHashInternalLoop(241).Should().BeEmpty();
    }

    public void ConsumeStripes_AssertsThatTheStripesDoNotExceedOneBlock()
    {
        InvokeConsumeStripes(0, 16, 17, out ulong stripesSoFar).Should().Equal(StripesMessage);
        stripesSoFar.Should().Be(1UL);

        InvokeConsumeStripes(0, 16, 16, out stripesSoFar).Should().BeEmpty();
        stripesSoFar.Should().Be(0UL);
    }

    public void ConsumeStripes_AssertsThatTheCurrentBlockIsNotAlreadyComplete()
    {
        InvokeConsumeStripes(16, 16, 4, out ulong stripesSoFar).Should().Equal(StripesSoFarMessage);
        stripesSoFar.Should().Be(4UL);

        InvokeConsumeStripes(15, 16, 4, out stripesSoFar).Should().BeEmpty();
        stripesSoFar.Should().Be(3UL);
    }

    public void DigestLong_AssertsThatSomeBytesAreBuffered()
    {
        var hasher = new XxHash128();
        hasher.Append(CreateData(300));

        Observe(() => hasher.GetCurrentHash()).Should().BeEmpty();

        FieldInfo stateField = typeof(XxHash128).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var state = (XxHashShared.State)stateField.GetValue(hasher)!;
        state.BufferedCount = 0;
        stateField.SetValue(hasher, state);

        Observe(() => hasher.GetCurrentHash()).Should().Equal(BufferedCountMessage);
    }

    private static List<string?> InvokeHashInternalLoop(uint length)
    {
        MethodInfo method = typeof(XxHashShared).GetMethod("HashInternalLoop", BindingFlags.Public | BindingFlags.Static)!;
        ulong[] accumulators = new ulong[8];
        byte[] source = CreateData(2048);
        byte[] secret = CreateData(192);
        using var accumulatorsPin = new PinnedArray(accumulators);
        using var sourcePin = new PinnedArray(source);
        using var secretPin = new PinnedArray(secret);
        object?[] args = [accumulatorsPin.Address, sourcePin.Address, length, secretPin.Address];
        return Observe(() => method.Invoke(null, args));
    }

    private static List<string?> InvokeConsumeStripes(ulong stripesSoFar, ulong stripesPerBlock, ulong stripes, out ulong stripesSoFarAfter)
    {
        MethodInfo method = typeof(XxHashShared).GetMethod("ConsumeStripes", BindingFlags.Public | BindingFlags.Static)!;
        ulong[] accumulators = new ulong[8];
        byte[] source = CreateData(2048);
        byte[] secret = CreateData(192);
        using var accumulatorsPin = new PinnedArray(accumulators);
        using var sourcePin = new PinnedArray(source);
        using var secretPin = new PinnedArray(secret);
        object?[] args = [accumulatorsPin.Address, stripesSoFar, stripesPerBlock, sourcePin.Address, stripes, secretPin.Address];
        List<string?> assertions = Observe(() => method.Invoke(null, args));

        // The ref parameter is written back into the argument array by the reflection call.
        stripesSoFarAfter = (ulong)args[1]!;
        return assertions;
    }

    private static byte[] CreateData(int length)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((i * 131) + 17);
        }

        return data;
    }

    private static List<string?> Observe(Action action)
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        action();
        return assertions;
    }

    private sealed class PinnedArray(Array array) : IDisposable
    {
        private GCHandle _handle = GCHandle.Alloc(array, GCHandleType.Pinned);

        public nint Address => _handle.AddrOfPinnedObject();

        public void Dispose() => _handle.Free();
    }
}
#endif
