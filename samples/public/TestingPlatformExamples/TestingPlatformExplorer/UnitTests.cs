// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestingPlatformExplorer.TestingFramework;

namespace TestingPlatformExplorer.UnitTests;

public class SomeTests
{
    [TestMethod]
    public static void TestMethod1() => Assert.AreEqual(1, 1);

    [TestMethod]
    public static void TestMethod2() => Assert.AreEqual(1, 2);

    [TestMethod]
    public static void TestMethod3(ITestOutputHelper testOutputHelper)
    {
        testOutputHelper.WriteLine("I'm running TestMethod3");

        try
        {
            int a = 1;
            int b = 0;
            int c = a / b;

            Assert.AreEqual(c, 2);
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteErrorLine(ex.ToString());
            throw;
        }
    }

    [Skip("Temporary disabled")]
    [TestMethod]
    public static void TestMethod4() => Assert.AreEqual(1, 1);
}
