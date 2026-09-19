// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows.Automation;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation;

/// <summary>
/// Base test class that discovers an application window and exposes it as an <see cref="AutomationElement"/>.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and override <see cref="ApplicationTest.CreateProcessStartInfo"/> to
/// specify the application to test. Concrete test classes must repeat <see cref="STATestClassAttribute"/>
/// because MSTest test-class attributes are not inherited.
/// </para>
/// <code>
/// [STATestClass]
/// public class MyAppTests : WindowTest
/// {
///     protected override ProcessStartInfo CreateProcessStartInfo()
///         =&gt; new(@"C:\MyApp\MyApp.exe");
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
/// the <see cref="MainWindow"/> <see cref="AutomationElement"/> to bridge into it.
/// </para>
/// </remarks>
public abstract class WindowTest : ApplicationTest
{
    /// <summary>
    /// Gets the main window of the application under test as an <see cref="AutomationElement"/>.
    /// Available after <see cref="WindowSetup"/> has run.
    /// </summary>
    public AutomationElement MainWindow { get; private set; } = null!;

    /// <summary>
    /// Gets the time to wait for a window to become available.
    /// </summary>
    protected virtual TimeSpan WindowDiscoveryTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Finds the window to expose for the application.
    /// </summary>
    /// <param name="applicationProcess">The application process returned by <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    /// <returns>The window, or <see langword="null"/> when it is not available yet.</returns>
    /// <remarks>
    /// Override this method for applications that use a launcher process, show a splash screen,
    /// or require selection among multiple top-level windows.
    /// If the returned window belongs to a process other than <paramref name="applicationProcess"/>,
    /// also override <see cref="ApplicationTest.StopApplication"/> to track and stop that process.
    /// </remarks>
    protected virtual AutomationElement? FindWindow(Process applicationProcess)
    {
        if (applicationProcess.HasExited)
        {
            throw CreateProcessExitedException(applicationProcess);
        }

        try
        {
            applicationProcess.Refresh();
            IntPtr mainWindowHandle = applicationProcess.MainWindowHandle;
            return mainWindowHandle == IntPtr.Zero
                ? null
                : AutomationElement.FromHandle(mainWindowHandle);
        }
        catch (InvalidOperationException) when (applicationProcess.HasExited)
        {
            throw CreateProcessExitedException(applicationProcess);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Discovers the application's window before each test method.
    /// </summary>
    [TestInitialize]
    public void WindowSetup()
    {
        CancellationToken cancellationToken = TestContext.CancellationToken;
        TimeSpan configuredDiscoveryTimeout = WindowDiscoveryTimeout;
        TimeSpan discoveryTimeout = configuredDiscoveryTimeout != Timeout.InfiniteTimeSpan
            && configuredDiscoveryTimeout < TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(
                nameof(WindowDiscoveryTimeout),
                configuredDiscoveryTimeout,
                "The window discovery timeout must be non-negative or infinite.")
            : configuredDiscoveryTimeout;
        bool hasInfiniteDiscoveryTimeout = discoveryTimeout == Timeout.InfiniteTimeSpan;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AutomationElement? window = FindWindow(AppProcess);
            if (window is not null)
            {
                MainWindow = window;
                return;
            }

            TimeSpan remainingTime = hasInfiniteDiscoveryTimeout
                ? TimeSpan.FromMilliseconds(50)
                : discoveryTimeout - stopwatch.Elapsed;
            if (!hasInfiniteDiscoveryTimeout && remainingTime <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"Application '{AppProcess.StartInfo.FileName}' did not expose a window within {discoveryTimeout}.");
            }

            TimeSpan delay = remainingTime < TimeSpan.FromMilliseconds(50)
                ? remainingTime
                : TimeSpan.FromMilliseconds(50);
            _ = cancellationToken.WaitHandle.WaitOne(delay);
        }
    }

    private static InvalidOperationException CreateProcessExitedException(Process applicationProcess)
        => new(
            $"Application '{applicationProcess.StartInfo.FileName}' exited with code {applicationProcess.ExitCode} before a window was discovered.");
}
