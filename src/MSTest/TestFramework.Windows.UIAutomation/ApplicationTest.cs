// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

using UIAutomationResources = Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation.Resources.WindowsUIAutomationResources;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Windows.UIAutomation;

/// <summary>
/// Base test class that manages the lifecycle of an application process.
/// </summary>
/// <remarks>
/// Concrete test classes must also be decorated with <see cref="STATestClassAttribute"/>
/// because MSTest test-class attributes are not inherited.
/// </remarks>
public abstract class ApplicationTest : IDisposable
{
    private Process? _appProcess;
    private int _shutdownTimeoutMilliseconds;

    /// <summary>
    /// Gets or sets the test context populated by MSTest before each test runs.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Gets the process of the application under test.
    /// </summary>
    protected Process AppProcess
        => _appProcess
        ?? throw new InvalidOperationException(UIAutomationResources.ApplicationProcessNotAvailable);

    /// <summary>
    /// Gets the time to wait for graceful shutdown and forced termination.
    /// </summary>
    protected virtual TimeSpan ApplicationShutdownTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates the process start information for the application under test.
    /// </summary>
    /// <returns>The process start information.</returns>
    protected abstract ProcessStartInfo CreateProcessStartInfo();

    /// <summary>
    /// Launches the application under test before each test method.
    /// </summary>
    [TestInitialize]
    public void ApplicationSetup()
    {
        TestContext.CancellationToken.ThrowIfCancellationRequested();
        _shutdownTimeoutMilliseconds = GetShutdownTimeoutMilliseconds();

        ProcessStartInfo startInfo = CreateProcessStartInfo();
        _appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, UIAutomationResources.ApplicationProcessFailedToStart, startInfo.FileName));
    }

    /// <summary>
    /// Stops and disposes the application process.
    /// </summary>
    /// <remarks>
    /// MSTest invokes this method even when a test cleanup declared by a derived class fails.
    /// </remarks>
    public void Dispose()
    {
        Process? applicationProcess = _appProcess;

        try
        {
            if (applicationProcess is not null)
            {
                StopApplication(applicationProcess);
            }
        }
        finally
        {
            applicationProcess?.Dispose();
            _appProcess = null;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Stops the application process.
    /// </summary>
    /// <param name="applicationProcess">The application process.</param>
    protected virtual void StopApplication(Process applicationProcess)
    {
        if (applicationProcess.HasExited)
        {
            return;
        }

        try
        {
            bool closeRequested = applicationProcess.CloseMainWindow();
            if (!closeRequested || !applicationProcess.WaitForExit(_shutdownTimeoutMilliseconds))
            {
                applicationProcess.Kill(entireProcessTree: true);
                if (!applicationProcess.WaitForExit(_shutdownTimeoutMilliseconds))
                {
                    throw new TimeoutException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            UIAutomationResources.ApplicationProcessTerminationTimedOut,
                            applicationProcess.Id,
                            ApplicationShutdownTimeout));
                }
            }
        }
        catch (Exception ex) when ((ex is InvalidOperationException or Win32Exception) && applicationProcess.HasExited)
        {
            // The process exited between the state check and the shutdown operation.
        }
    }

    private int GetShutdownTimeoutMilliseconds()
    {
        TimeSpan timeout = ApplicationShutdownTimeout;
        return timeout == Timeout.InfiniteTimeSpan
            ? Timeout.Infinite
            : timeout < TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue
            ? throw new ArgumentOutOfRangeException(
                nameof(ApplicationShutdownTimeout),
                timeout,
                UIAutomationResources.InvalidApplicationShutdownTimeout)
            : (int)Math.Ceiling(timeout.TotalMilliseconds);
    }
}
