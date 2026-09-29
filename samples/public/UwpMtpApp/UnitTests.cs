// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.ApplicationModel;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace UwpMtpApp;

[TestClass]
public sealed class UnitTests
{
    [TestMethod]
    public void PackageIdentityMatchesManifest()
    {
        Assert.AreEqual("MSTestUwpMtpSample", Package.Current.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(Package.Current.Id.FamilyName));
    }

    [UITestMethod]
    public void UITestMethodRunsOnTheCoreWindowDispatcher()
    {
        CoreWindow coreWindow = CoreWindow.GetForCurrentThread();
        Assert.IsNotNull(coreWindow);
        Assert.IsTrue(coreWindow.Dispatcher.HasThreadAccess);

        var grid = new Grid();
        Assert.IsNotNull(grid.Dispatcher);
        Assert.IsTrue(grid.Dispatcher.HasThreadAccess);
    }
}
