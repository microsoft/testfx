// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace CtrfPlayground.XunitMtp;

public class SampleTests
{
    [Fact]
    public void PassingTest()
        => Assert.Equal(2, 1 + 1);

    [Fact]
    public void FailingTest()
        => Assert.Fail("intentional failure to exercise CTRF status mapping");

    [Fact(Skip = "intentionally skipped to exercise CTRF status mapping")]
    public void SkippedTest()
    {
    }

    [Fact]
    public void ThrowingTest()
        => throw new InvalidOperationException("intentional exception to exercise CTRF error fields");

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(2, 3, 5)]
    [InlineData(2, 2, 5)] // intentional failure
    public void DataDrivenTest(int a, int b, int expected)
        => Assert.Equal(expected, a + b);
}
