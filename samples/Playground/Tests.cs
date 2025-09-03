// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 0)]

namespace Playground;

[TestClass]
public class TestClass
{
    [TestMethod]
    [DynamicData(nameof(Data))]
    public void Test3(int a, int b)
    {
        Thread.Sleep(5000);
        Assert.IsPositive(a);
    }

    public static IEnumerable<(int A, int B)> Data
    {
        get
        {
            yield return (1, 2);
            yield return (3, 4);
        }
    }
}
