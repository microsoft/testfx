// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.UI.Xaml.Controls;

namespace App1;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(0, 0);
    }

    // Use the UITestMethod attribute for tests that need to run on the UI thread.
    [UITestMethod]
    public void TestMethod2()
    {
        Grid grid = new();

        Assert.AreEqual(0, grid.MinWidth);
    }
}
