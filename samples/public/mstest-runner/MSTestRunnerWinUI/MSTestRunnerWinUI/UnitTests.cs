// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace MSTestRunnerWinUI;

[TestClass]
public partial class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
#pragma warning disable MSTEST0032 // Assertion condition is always true
        => Assert.AreEqual(0, 0);
#pragma warning restore MSTEST0032 // Assertion condition is always true

    // Use the UITestMethod attribute for tests that need to run on the UI thread.
    [UITestMethod]
    public void TestMethod2()
    {
        var grid = new Grid();
        Assert.AreEqual(0, grid.MinWidth);
    }
}
