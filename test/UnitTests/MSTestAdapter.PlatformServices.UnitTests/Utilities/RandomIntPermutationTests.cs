// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public class RandomIntPermutationTests : TestContainer
{
    public void Constructor_NegativeSize_RejectsInvalidRange()
    {
        foreach (int size in new[] { -1, int.MinValue })
        {
            Action action = () => _ = new RandomIntPermutation(size);

            action.Should().ThrowExactly<ArgumentException>().WithParameterName("numberOfObjects");
        }
    }

    public void GetEnumerator_EmptyRange_ReturnsNoValues()
        => new RandomIntPermutation(0).Should().BeEmpty();

    public void GetEnumerator_SingletonRange_ReturnsZero()
        => new RandomIntPermutation(1).Should().Equal(0);

    public void GetEnumerator_MultipleValues_VisitsEveryIndexExactlyOnce()
        => new RandomIntPermutation(17).Should().BeEquivalentTo(Enumerable.Range(0, 17));

    public void GetEnumerator_NonGenericEnumeration_VisitsEveryIndexExactlyOnce()
    {
        IEnumerable permutation = new RandomIntPermutation(2);

        permutation.Cast<int>().Should().BeEquivalentTo(new[] { 0, 1 });
    }

    public void GetEnumerator_RepeatedEnumeration_PreservesTheChosenOrder()
    {
        var permutation = new RandomIntPermutation(17);
        int[] firstPass = permutation.ToArray();

        firstPass.Should().BeEquivalentTo(Enumerable.Range(0, 17));
        permutation.Should().Equal(firstPass);
    }
}
#endif
