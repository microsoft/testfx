// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace MSTestRunnerWinUIUnpackaged;

[TestClass]
public partial class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
#pragma warning disable MSTEST0032 // Assertion condition is always true
        => Assert.AreEqual(0, 0);
#pragma warning restore MSTEST0032 // Assertion condition is always true

    /// <summary>
    /// A plain [TestMethod] that touches a Windows App SDK WinRT type. In an unpackaged app this only
    /// resolves once the Windows App SDK bootstrapper has run, which the Windows App SDK arranges
    /// through a module initializer it injects into this assembly. It is covered here because it is the
    /// case that does NOT go through [UITestMethod].
    /// </summary>
    [TestMethod]
    public void WindowsAppSdkWinRTIsUsableFromAPlainTestMethod()
    {
        var releaseInfo = Microsoft.Windows.ApplicationModel.WindowsAppRuntime.RuntimeInfo.AsString;

        Assert.IsNotNull(releaseInfo);
        Assert.IsGreaterThan(0, releaseInfo.Length);
    }

    // Use the UITestMethod attribute for tests that need to run on the UI thread.
    [UITestMethod]
    public void TestMethod2()
    {
        var grid = new Grid();

        Assert.AreEqual(0, grid.MinWidth);
    }
}
