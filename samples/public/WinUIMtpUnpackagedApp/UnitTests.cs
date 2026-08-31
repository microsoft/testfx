// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace WinUIMtpUnpackagedApp;

[TestClass]
public partial class UnitTest1
{
    /// <summary>
    /// Asserts the property that defines this sample: the app runs with no MSIX package identity.
    /// <c>Package.Current</c> is only available to a packaged app and throws otherwise.
    /// </summary>
    [TestMethod]
    public void AppRunsWithoutPackageIdentity()
        => Assert.ThrowsExactly<InvalidOperationException>(() => _ = Windows.ApplicationModel.Package.Current);

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

        // A parseable version proves the bootstrapper resolved a real runtime, rather than the call
        // merely returning some non-empty string.
        Assert.IsNotNull(releaseInfo);
        Assert.IsTrue(Version.TryParse(releaseInfo, out _), $"Expected a Windows App Runtime version, got '{releaseInfo}'.");
    }

    /// <summary>
    /// Use [UITestMethod] for tests that need the UI thread. WinUI controls can only be created there,
    /// so constructing one would fail with RPC_E_WRONG_THREAD from a plain [TestMethod]. The control
    /// reports the dispatcher of the thread that created it, and that dispatcher confirms the test body
    /// is itself running on that thread.
    /// </summary>
    [UITestMethod]
    public void UITestMethodCreatesWinUIControlsOnTheUIThread()
    {
        var grid = new Grid();

        Assert.IsNotNull(grid.DispatcherQueue);
        Assert.IsTrue(grid.DispatcherQueue.HasThreadAccess, "The Grid was not created on the UI thread.");
    }
}
