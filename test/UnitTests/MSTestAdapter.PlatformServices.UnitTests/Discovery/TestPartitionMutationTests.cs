// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class TestPartitionMutationTests : TestContainer
{
    public void TryParseAcceptsEveryDecimalDigitIncludingNine()
    {
        foreach ((string value, int index, int count) in new[]
        {
            ("9/9", 9, 9),
            ("9/99", 9, 99),
            ("90/99", 90, 99),
            ("99/900", 99, 900),
            ("1/9", 1, 9),
        })
        {
            TestPartition.TryParse(value, out TestPartition? partition).Should().BeTrue(because: value);
            partition!.Index.Should().Be(index, because: value);
            partition.Count.Should().Be(count, because: value);
        }
    }

    public void SelectTestsFramesFieldLengthsAbove64KiBWithTheSecondLengthByte()
    {
        // UTF-8 length 66051 (0x00010203). Independent SHA-256 framing vector (4 big-endian length bytes + payload,
        // for "mtp.partition.v1", "Acme.Tests" and the class name); first 8 hash bytes modulo int.MaxValue, plus 1.
        UnitTestElement element = Element("Acme." + new string('A', 66_046), "Run");

        new TestPartition(1_024_632_688, int.MaxValue).SelectTests([element], null).Should().Equal(element);
        new TestPartition(1_024_632_687, int.MaxValue).SelectTests([element], null).Should().BeEmpty();
        new TestPartition(1_024_632_689, int.MaxValue).SelectTests([element], null).Should().BeEmpty();
    }

    public void SelectTestsFramesFieldLengthsAbove16MiBWithTheFirstLengthByte()
    {
        // UTF-8 length 16777218 (0x01000002): 5592406 characters of three bytes each. Independent SHA-256 framing vector.
        UnitTestElement element = Element(new string('\u20AC', 5_592_406), "Run");

        new TestPartition(1_262_223_100, int.MaxValue).SelectTests([element], null).Should().Equal(element);
        new TestPartition(1_262_223_099, int.MaxValue).SelectTests([element], null).Should().BeEmpty();
        new TestPartition(1_262_223_101, int.MaxValue).SelectTests([element], null).Should().BeEmpty();
    }

    private static UnitTestElement Element(string className, string methodName)
        => new(new TestMethod(methodName, className, "Acme.Tests.dll", displayName: null));
}
