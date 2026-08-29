// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Automation;

namespace ProjectUsingWindowsAppTesting;

/// <summary>
/// Sample end-to-end tests for the Windows Character Map application.
/// Demonstrates the WindowTest base class which manages app launch, main window
/// discovery, and teardown.
/// </summary>
[STATestClass]
public class CharacterMapTests : WindowTest
{
    /// <summary>
    /// Path to the application under test.
    /// Override this to point to your own application executable.
    /// </summary>
    public override string ApplicationPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "charmap.exe");

    [TestMethod]
    public void CharacterMap_MainWindow_IsVisible()
    {
        Assert.AreEqual(ControlType.Window, MainWindow.Current.ControlType,
            "Expected the main window element to be of control type Window.");
    }

    [TestMethod]
    public void CharacterMap_MainWindow_HasTitle()
    {
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(MainWindow.Current.Name),
            "Expected the main window to have a non-empty title.");
    }
}
