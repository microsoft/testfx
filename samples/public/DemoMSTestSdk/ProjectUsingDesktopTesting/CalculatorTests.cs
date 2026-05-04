// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Automation;

namespace ProjectUsingDesktopTesting;

/// <summary>
/// Sample end-to-end tests for the Windows Calculator application.
/// Demonstrates the WindowTest base class which manages app launch, main window
/// discovery, and teardown — analogous to how PageTest works for Playwright.
/// </summary>
[TestClass]
public class CalculatorTests : WindowTest
{
    /// <summary>
    /// Path to the application under test.
    /// Override this to point to your own application executable.
    /// </summary>
    public override string ApplicationPath => "calc.exe";

    [TestMethod]
    public void Calculator_MainWindow_HasExpectedTitle()
    {
        // The MainWindow property is automatically populated by WindowTest
        // after launching the application specified by ApplicationPath.
        Assert.IsNotNull(MainWindow);

        string title = MainWindow.Current.Name;
        Assert.IsTrue(
            title.Contains("Calculator", StringComparison.OrdinalIgnoreCase),
            $"Expected window title to contain 'Calculator', but was '{title}'.");
    }

    [TestMethod]
    public void Calculator_NumberButtons_AreVisible()
    {
        // Use System.Windows.Automation to locate UI elements
        AutomationElement? button1 = MainWindow.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "One"));

        AutomationElement? button2 = MainWindow.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Two"));

        Assert.IsNotNull(button1, "Could not find the 'One' button.");
        Assert.IsNotNull(button2, "Could not find the 'Two' button.");
    }
}
