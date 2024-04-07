// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestingPlatformExplorer.TestingFramework;

namespace TestingPlatformExplorer.UnitTests;

public class UnitTests
{
    [TestMethod]
    public static void TestMethod1()
    {
        Assert.Equals(1, 1);
    }

    [TestMethod]
    public static void TestMethod2()
    {
        Assert.Equals(1, 2);
    }

    [TestMethod(skip: true)]
    public static void TestMethod3()
    {
        Assert.Equals(1, 1);
    }
}
