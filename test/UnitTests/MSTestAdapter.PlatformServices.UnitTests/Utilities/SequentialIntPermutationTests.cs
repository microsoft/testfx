// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class SequentialIntPermutationTests : TestContainer
{
    public void NegativeSizeIsRejected()
    {
        foreach (int count in new[] { -1, int.MinValue })
        {
            Action create = () => _ = new SequentialIntPermutation(count);
            create.Should().ThrowExactly<ArgumentException>().WithParameterName("numberOfObjects")
                .Which.Message.Should().StartWith(Resource.WrongNumberOfObjects);
        }
    }

    public void EnumerationIsSequentialRepeatableAndSupportsNongenericConsumers()
    {
        new SequentialIntPermutation(0).Should().BeEmpty();
        var permutation = new SequentialIntPermutation(5);
        permutation.Should().Equal(0, 1, 2, 3, 4);
        permutation.Should().Equal(0, 1, 2, 3, 4);
        ((IEnumerable)permutation).Cast<int>().Should().Equal(0, 1, 2, 3, 4);
    }

    public void ExplicitNongenericEnumeratorReturnsBoxedOrderedValuesAndTerminates()
    {
        IEnumerable sequence = new SequentialIntPermutation(5);
        IEnumerator first = sequence.GetEnumerator();
        IEnumerator second = sequence.GetEnumerator();
        try
        {
            first.Should().NotBeSameAs(second);
            for (int expected = 0; expected < 5; expected++)
            {
                first.MoveNext().Should().BeTrue();
                first.Current.Should().BeOfType<int>().Which.Should().Be(expected);
            }

            first.MoveNext().Should().BeFalse();
            first.MoveNext().Should().BeFalse();
            second.MoveNext().Should().BeTrue();
            second.Current.Should().Be(0, "an independently acquired enumerator must not share iteration state");
        }
        finally
        {
            (first as IDisposable)?.Dispose();
            (second as IDisposable)?.Dispose();
        }

        IEnumerator empty = ((IEnumerable)new SequentialIntPermutation(0)).GetEnumerator();
        try
        {
            empty.MoveNext().Should().BeFalse();
        }
        finally
        {
            (empty as IDisposable)?.Dispose();
        }
    }
}
#endif
