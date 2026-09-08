// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.ApplicationModel;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace UwpVSTestApp;

[TestClass]
public sealed class UnitTests
{
    [TestMethod]
    public void TestMethodRunsInUwpPackage()
    {
        Assert.AreEqual("3a1115e9-2ece-44ab-944f-5b6240a08ea6", Package.Current.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName));
    }

    // Use the UITestMethod attribute for tests that need to run on the UI thread.
    [UITestMethod]
    public void UITestMethodRunsOnCoreWindowDispatcher()
    {
        CoreWindow coreWindow = CoreWindow.GetForCurrentThread();
        Grid grid = new();

        Assert.IsNotNull(coreWindow);
        Assert.IsTrue(coreWindow.Dispatcher.HasThreadAccess);
        Assert.IsNotNull(grid.Dispatcher);
        Assert.IsTrue(grid.Dispatcher.HasThreadAccess);
    }
}
