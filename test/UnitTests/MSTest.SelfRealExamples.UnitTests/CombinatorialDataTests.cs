// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
public sealed class CombinatorialDataTests
{
    [TestMethod]
    [CombinatorialData]
    public void CombinesInferredExplicitAndRangeValues(
        bool enabled,
        [CombinatorialValues(1, 3)] int factor,
        [CombinatorialRange(2, 6, 2)] int value)
    {
        int result = enabled ? factor * value : value;

        Assert.IsTrue(factor is 1 or 3);
        Assert.IsTrue(value is 2 or 4 or 6);
        Assert.IsTrue(result is 2 or 4 or 6 or 12 or 18);
    }

    [TestMethod]
    [CombinatorialData]
    public void GeneratesSeededRandomValues(
        [CombinatorialRandomData(Count = 3, Minimum = 10, Maximum = 20, Seed = 42)] int value)
        => Assert.IsTrue(value is >= 10 and <= 20);
}
