// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.SelfRealExamples.UnitTests;

[TestClass]
public class DynamicDataMethodArgumentsTests
{
    [TestMethod]
    [DynamicData(nameof(GetData), 4)]
    public void Test3(int a)
        => Assert.IsInRange(4, 6, a);

    public static IEnumerable<int> GetData(int i)
    {
        yield return i++;
        yield return i++;
        yield return i++;
    }
}
