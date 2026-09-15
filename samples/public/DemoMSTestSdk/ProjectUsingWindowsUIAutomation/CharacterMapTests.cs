// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Automation;

namespace ProjectUsingWindowsUIAutomation;

/// <summary>
/// Sample end-to-end tests for the Windows Character Map application.
/// Demonstrates the WindowTest base class which manages app launch, main window
/// discovery, and teardown.
/// </summary>
[STATestClass]
public class CharacterMapTests : WindowTest
{
    /// <summary>
    /// Creates the launch configuration for the application under test.
    /// </summary>
    protected override ProcessStartInfo CreateProcessStartInfo()
        => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "charmap.exe"));

    [TestMethod]
    public void CharacterMap_MainWindow_IsVisible()
    {
        Assert.AreEqual(ControlType.Window, MainWindow.Current.ControlType,
            "Expected the main window element to be of control type Window.");
    }

    [TestMethod]
    public void CharacterMap_MainWindow_HasNonEmptyTitle()
    {
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(MainWindow.Current.Name),
            "Expected the main window to have a non-empty title.");
    }
}
