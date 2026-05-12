// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Automation;

namespace ProjectUsingWindowsAppTesting;

/// <summary>
/// Sample end-to-end tests for the Windows Calculator application.
/// Demonstrates the WindowTest base class which manages app launch, main window
/// discovery, and teardown — analogous to how PageTest works for Playwright.
/// </summary>
/// <remarks>
/// <para>
/// On modern Windows 10/11, <c>calc.exe</c> launches the Store/UWP Calculator, whose
/// UI Automation tree uses different AutomationIds than the classic Win32 Calculator.
/// The <c>Calculator_NumberButtons_AreVisible</c> test below is illustrative — you will
/// need to replace the AutomationId values with the actual IDs for your target application.
/// </para>
/// </remarks>
[STATestClass]
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
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(title),
            "Expected the main window to have a non-empty title.");
    }

    [TestMethod]
    public void Calculator_NumberButtons_AreVisible()
    {
        // NOTE: Modern Windows Calculator (UWP) uses different AutomationIds than the
        // classic Win32 Calculator. Replace "num1Button" / "num2Button" with the actual
        // AutomationIds for your target application version.
        AutomationElement? button1 = MainWindow.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "num1Button"));

        AutomationElement? button2 = MainWindow.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "num2Button"));

        Assert.IsNotNull(button1, "Could not find the button with AutomationId 'num1Button'.");
        Assert.IsNotNull(button2, "Could not find the button with AutomationId 'num2Button'.");
    }
}
