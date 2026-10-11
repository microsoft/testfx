// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Windows.ApplicationModel;

namespace WinUIMtpPackagedApp;

[TestClass]
public partial class UnitTest1
{
    [TestMethod]
    public void PackageIdentityAndAumidMatchManifest()
    {
        Package package = Package.Current;
        string installLocation = Path.TrimEndingDirectorySeparator(package.InstalledLocation.Path);
        string baseDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

        Assert.AreEqual("27a818e1-af01-4177-9e34-ad49120c15ed", package.Id.Name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(package.Id.FamilyName));
        Assert.AreEqual($"{package.Id.FamilyName}!App", AppInfo.Current.AppUserModelId);
        Assert.AreEqual(installLocation, baseDirectory, ignoreCase: true);
        Assert.IsNotNull(Environment.ProcessPath);
        Assert.StartsWith(installLocation, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
    }

    [UITestMethod]
    public void UITestMethodRunsOnTheWinUIDispatcher()
    {
        var grid = new Grid();

        Assert.IsNotNull(grid.DispatcherQueue);
        Assert.IsTrue(grid.DispatcherQueue.HasThreadAccess);
    }

    [TestMethod]
    public void RetryFailsFirstAttempt()
    {
        Assert.AreNotEqual(
            "1",
            Environment.GetEnvironmentVariable("TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"),
            "This probe deliberately fails the first retry attempt and passes the next one.");
    }
}
