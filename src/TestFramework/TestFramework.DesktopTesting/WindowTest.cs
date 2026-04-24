// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Automation;

namespace Microsoft.MSTest.DesktopTesting;

/// <summary>
/// Base test class that provides access to the application's main window as an <see cref="AutomationElement"/>.
/// This is the primary class users should inherit from for desktop E2E tests,
/// analogous to <c>PageTest</c> in the Playwright MSTest integration.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and override <see cref="ApplicationTest.ApplicationPath"/> to
/// specify the application to test:
/// </para>
/// <code>
/// [TestClass]
/// public class MyAppTests : WindowTest
/// {
///     public override string ApplicationPath =&gt; @"C:\MyApp\MyApp.exe";
///
///     [TestMethod]
///     public void ClickButton_ShowsResult()
///     {
///         var button = MainWindow.FindFirst(TreeScope.Descendants,
///             new PropertyCondition(AutomationElement.NameProperty, "Calculate"));
///         ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
///
///         var result = MainWindow.FindFirst(TreeScope.Descendants,
///             new PropertyCondition(AutomationElement.AutomationIdProperty, "ResultText"));
///         Assert.IsNotNull(result);
///     }
/// }
/// </code>
/// <para>
/// For a richer element interaction API, add a library like FlaUI on top and use
/// <see cref="MainWindow"/> handle to bridge into it.
/// </para>
/// </remarks>
[TestClass]
public class WindowTest : ApplicationTest
{
    /// <summary>
    /// Gets the main window of the application under test as an <see cref="AutomationElement"/>.
    /// Available after <see cref="WindowSetup"/> has run.
    /// </summary>
    public AutomationElement MainWindow { get; private set; } = null!;

    /// <summary>
    /// Obtains the application's main window <see cref="AutomationElement"/> before each test method.
    /// </summary>
    [TestInitialize]
    public void WindowSetup()
    {
        MainWindow = AutomationElement.FromHandle(AppProcess.MainWindowHandle);
    }
}
