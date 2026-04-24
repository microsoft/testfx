// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.MSTest.DesktopTesting;

/// <summary>
/// Base test class that manages the lifecycle of a desktop application process.
/// Analogous to <c>BrowserTest</c> in the Playwright MSTest integration.
/// </summary>
/// <remarks>
/// Override <see cref="ApplicationPath"/> and optionally <see cref="ApplicationArguments"/>
/// to configure which application to launch.
/// If <see cref="ApplicationPath"/> is not overridden, the test will attempt to read
/// the path from the <c>DESKTOP_TEST_APP_PATH</c> environment variable.
/// </remarks>
[STATestClass]
public class ApplicationTest : AutomationTest
{
    private const string AppPathEnvVar = "DESKTOP_TEST_APP_PATH";

    /// <summary>
    /// Gets the process of the application under test.
    /// Available after <see cref="ApplicationSetup"/> has run.
    /// </summary>
    public Process AppProcess { get; private set; } = null!;

    /// <summary>
    /// Gets the path to the application executable to launch.
    /// Override this property to specify the application under test.
    /// Defaults to the <c>DESKTOP_TEST_APP_PATH</c> environment variable.
    /// </summary>
    public virtual string ApplicationPath
        => Environment.GetEnvironmentVariable(AppPathEnvVar)
           ?? throw new InvalidOperationException(
               $"Override {nameof(ApplicationPath)} or set the '{AppPathEnvVar}' environment variable to the path of the application under test.");

    /// <summary>
    /// Gets the command-line arguments to pass when launching the application.
    /// Override to customize. Defaults to <see langword="null"/> (no arguments).
    /// </summary>
    public virtual string? ApplicationArguments => null;

    /// <summary>
    /// Gets the timeout to wait for the application's main window to become available.
    /// Override to customize. Defaults to 10 seconds.
    /// </summary>
    public virtual TimeSpan ApplicationStartTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Launches the application under test before each test method.
    /// </summary>
    [TestInitialize]
    public void ApplicationSetup()
    {
        ProcessStartInfo startInfo = new(ApplicationPath);
        if (ApplicationArguments is not null)
        {
            startInfo.Arguments = ApplicationArguments;
        }

        AppProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {ApplicationPath}");

        // Wait for the main window to be created
        Stopwatch sw = Stopwatch.StartNew();
        while (AppProcess.MainWindowHandle == IntPtr.Zero && sw.Elapsed < ApplicationStartTimeout)
        {
            AppProcess.Refresh();
            Thread.Yield();
        }

        if (AppProcess.MainWindowHandle == IntPtr.Zero)
        {
            throw new TimeoutException(
                $"Application '{ApplicationPath}' did not create a main window within {ApplicationStartTimeout}.");
        }
    }

    /// <summary>
    /// Closes and disposes the application after each test method.
    /// </summary>
    [TestCleanup]
    public void ApplicationTearDown()
    {
        if (AppProcess is not null && !AppProcess.HasExited)
        {
            AppProcess.CloseMainWindow();
            if (!AppProcess.WaitForExit(5000))
            {
                AppProcess.Kill();
            }
        }

        AppProcess?.Dispose();
        AppProcess = null!;
    }
}
