// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using TestFramework.ForTestingMSTest;

using TestFx.Hashing;

namespace System.IO.Hashing.Tests;

public sealed class XxHash128MutationTests : TestContainer
{
#if DEBUG
    private const string Length1To3Message = "Length was expected to be between 1 and 3.";
    private const string Length4To8Message = "Length was expected to be between 4 and 8.";
    private const string Length9To16Message = "Length was expected to be between 9 and 16.";
    private const string Length17To128Message = "Length was expected to be between 17 and 128.";
    private const string Length129To240Message = "Length was expected to be between 129 and 240.";
    private const string LengthOver240Message = "Length was expected to be greater than 240.";
#endif

    public void Hash_WithADestinationShorterThanTheHashSize_ThrowsForTheDestinationParameter()
    {
        Action action = () => XxHash128.Hash(new byte[1], new byte[15]);

        ArgumentException exception = action.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("destination");
        exception.Message.Should().StartWith("Destination is too short");
    }

#if DEBUG
    public void HashLength1To3_AssertsThatTheLengthIsBetween1And3()
    {
        InvokeHashLength("HashLength1To3", 4).Should().Equal(Length1To3Message);
        InvokeHashLength("HashLength1To3", 1).Should().BeEmpty();
        InvokeHashLength("HashLength1To3", 3).Should().BeEmpty();
    }

    public void HashLength4To8_AssertsThatTheLengthIsBetween4And8()
    {
        InvokeHashLength("HashLength4To8", 9).Should().Equal(Length4To8Message);
        InvokeHashLength("HashLength4To8", 4).Should().BeEmpty();
        InvokeHashLength("HashLength4To8", 8).Should().BeEmpty();
    }

    public void HashLength9To16_AssertsThatTheLengthIsBetween9And16()
    {
        InvokeHashLength("HashLength9To16", 17).Should().Equal(Length9To16Message);
        InvokeHashLength("HashLength9To16", 9).Should().BeEmpty();
        InvokeHashLength("HashLength9To16", 16).Should().BeEmpty();
    }

    public void HashLength17To128_AssertsThatTheLengthIsBetween17And128()
    {
        InvokeHashLength("HashLength17To128", 129).Should().Equal(Length17To128Message);
        InvokeHashLength("HashLength17To128", 17).Should().BeEmpty();
        InvokeHashLength("HashLength17To128", 128).Should().BeEmpty();
    }

    public void HashLength129To240_AssertsThatTheLengthIsBetween129And240()
    {
        InvokeHashLength("HashLength129To240", 241).Should().Equal(Length129To240Message);
        InvokeHashLength("HashLength129To240", 129).Should().BeEmpty();
        InvokeHashLength("HashLength129To240", 240).Should().BeEmpty();
    }

    public void HashLengthOver240_AssertsThatTheLengthIsGreaterThan240()
    {
        // The shared internal loop checks the very same precondition, hence the second record for 240.
        InvokeHashLength("HashLengthOver240", 240).Should().Equal(LengthOver240Message, LengthOver240Message);
        InvokeHashLength("HashLengthOver240", 241).Should().BeEmpty();
    }

    private static List<string?> InvokeHashLength(string methodName, uint length)
    {
        MethodInfo method = typeof(XxHash128).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        byte[] data = new byte[2048];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)((i * 131) + 17);
        }

        using var pinned = new PinnedArray(data);
        object?[] args = [pinned.Address + 64, length, 0UL];
        return Observe(() => method.Invoke(null, args));
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
#endif
}
