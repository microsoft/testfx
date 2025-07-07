// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
public class DynamicDataMethodArgumentsTests
{
    [TestMethod]
    [DynamicData(nameof(GetData1), 4)]
    public void TestMethod(int a)
        => Assert.IsInRange(4, 6, a);

    [TestMethod]
    [DynamicData(nameof(GetData2), [new int[] { 4, 5, 6 }])]
    public void TestMethod2(int x)
        => Assert.IsTrue(x is 5 or 7 or 9);

    public static IEnumerable<int> GetData1(int i)
    {
        yield return i++;
        yield return i++;
        yield return i++;
    }

    public static IEnumerable<object[]> GetData2(int[] input)
    {
        yield return [1 + input[0]];
        yield return [2 + input[1]];
        yield return [3 + input[2]];
    }
}
