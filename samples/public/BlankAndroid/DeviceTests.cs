// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlankAndroid.Tests;

[TestClass]
public class DeviceTests
{
    [TestMethod]
    public void SimpleTest_ShouldPass()
    {
        // Arrange
        int a = 2;
        int b = 3;

        // Act
        int result = a + b;

        // Assert
        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void AndroidPlatformTest()
    {
        // Verify we're running on Android
        Assert.IsTrue(OperatingSystem.IsAndroid(), "Should be running on Android");
    }
}
