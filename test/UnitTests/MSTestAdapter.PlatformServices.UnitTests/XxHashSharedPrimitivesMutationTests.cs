// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using TestFramework.ForTestingMSTest;

namespace System.IO.Hashing.Tests;

public sealed class XxHashSharedPrimitivesMutationTests : TestContainer
{
#if DEBUG
    private const string ShiftMessage = "shift was expected to be between 0 and 63.";
#endif

    public void XorShift_XorsTheValueWithItsRightShiftedSelf()
    {
        XxHashShared.XorShift(0x8123456789ABCDEFUL, 1).Should().Be(0xC1B2E7D44D7E2B18UL);
        XxHashShared.XorShift(0x8123456789ABCDEFUL, 63).Should().Be(0x8123456789ABCDEEUL);
        XxHashShared.XorShift(0UL, 5).Should().Be(0UL);
    }

    public void WriteUInt64LE_WritesTheValueInLittleEndianOrderAtTheGivenAddress()
    {
        MethodInfo method = typeof(XxHashShared).GetMethod("WriteUInt64LE", BindingFlags.NonPublic | BindingFlags.Static)!;
        byte[] buffer = new byte[16];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = 0xAA;
        }

        using var pinned = new PinnedArray(buffer);
        object?[] args = [pinned.Address + 4, 0x0102030405060708UL];
        method.Invoke(null, args);

        BitConverter.ToString(buffer).Should().Be("AA-AA-AA-AA-08-07-06-05-04-03-02-01-AA-AA-AA-AA");
    }

#if DEBUG
    public void XorShift_AssertsThatTheShiftIsBetween0And63()
    {
        Observe(() => XxHashShared.XorShift(1UL, -1)).Should().Equal(ShiftMessage);
        Observe(() => XxHashShared.XorShift(1UL, 64)).Should().Equal(ShiftMessage);
        Observe(() => XxHashShared.XorShift(1UL, 0)).Should().BeEmpty();
        Observe(() => XxHashShared.XorShift(1UL, 1)).Should().BeEmpty();
        Observe(() => XxHashShared.XorShift(1UL, 63)).Should().BeEmpty();
    }

    private static List<string?> Observe(Action action)
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        action();
        return assertions;
    }
#endif

    private sealed class PinnedArray(Array array) : IDisposable
    {
        private GCHandle _handle = GCHandle.Alloc(array, GCHandleType.Pinned);

        public nint Address => _handle.AddrOfPinnedObject();

        public void Dispose() => _handle.Free();
    }
}
