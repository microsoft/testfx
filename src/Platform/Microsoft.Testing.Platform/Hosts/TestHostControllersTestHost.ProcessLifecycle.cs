// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    private async Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)> RunTestHostProcessAsync(
        ProcessStartInfo processStartInfo,
        IReadOnlyList<string> partialCommandLine,
        int currentPid,
        IProcessHandler process,
        IConfiguration configuration,
        NamedPipeServer testHostControllerIpc,
        TestHostControllerCancellationServer testHostControllerCancellationServer,
        ProxyOutputDevice outputDevice,
        ITelemetryInformation telemetryInformation,
        Stopwatch consoleRunStarted,
        CancellationToken applicationCancellationToken)
    {
        // Launch the test host process
        string testHostProcessStartupTime = _clock.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        processStartInfo.EnvironmentVariables.Add($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{currentPid}", testHostProcessStartupTime);
        await _logger.LogDebugAsync(
            $"Test host process startup timestamp environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{currentPid}' is '{testHostProcessStartupTime}'.").ConfigureAwait(false);
        await _logger.LogDebugAsync(
            $"Starting test host process '{processStartInfo.FileName}' with arguments '{processStartInfo.Arguments}'.").ConfigureAwait(false);

        ITestHostLauncher? testHostLauncher = _testHostsInformation.TestHostLauncher;
        using IProcess testHostProcess = testHostLauncher is null
            ? process.Start(processStartInfo)
            : await LaunchUsingCustomLauncherAsync(testHostLauncher, processStartInfo, partialCommandLine, applicationCancellationToken).ConfigureAwait(false);
        var applicationCancellationTokenSource =
            (CTRLPlusCCancellationTokenSource)ServiceProvider.GetTestApplicationCancellationTokenSource();
        using IDisposable forceExitRegistration = applicationCancellationTokenSource.RegisterForceExitAction(testHostProcess.Kill);

        int? testHostProcessId = null;
        bool testHostControllerConnectionTimedOut = false;
        try
        {
            testHostProcessId = testHostProcess.Id;
        }
        catch (InvalidOperationException ex) when (testHostProcess.HasExited || testHostLauncher is not null)
        {
            // Access PID can throw InvalidOperationException if the process has already exited:
            // System.InvalidOperationException: No process is associated with this object.
            // A custom launcher may also legitimately not expose a local PID (e.g. container/remote).
            await _logger.LogDebugAsync(
                $"Unable to obtain the test host PID because the process had already exited or does not expose a PID. HasExited: '{testHostProcess.HasExited}'. {ex.GetType().FullName}: {ex.Message}").ConfigureAwait(false);
        }

        testHostProcess.Exited += (_, _) =>
            _logger.LogDebug($"Test host process exited. PID: '{testHostProcessId}'.");

        await _logger.LogDebugAsync($"Started test host process. PID: '{testHostProcessId}'. HasExited: '{testHostProcess.HasExited}'.").ConfigureAwait(false);
        // Note: we intentionally gate on HasExited only and not on 'testHostProcessId is null'.
        // A custom ITestHostLauncher may legitimately not expose a local PID (e.g. container,
        // remote, or AUMID-activated apps); the real test host PID still arrives via the IPC
        // handshake (_testHostPID). For the default Process.Start path, a null PID always
        // coincides with HasExited == true, so behavior is unchanged there.
        if (testHostProcess.HasExited)
        {
            await _logger.LogDebugAsync(
                $"Test host process exited before connecting to the test host controller. Exit code: '{testHostProcess.ExitCode}'.").ConfigureAwait(false);
            await outputDevice.DisplayAsync(
                this,
                new ErrorMessageOutputDeviceData(CreateTestHostControllerConnectionFailureMessage(
                    waitDuration: TimeSpan.Zero,
                    timeout: null,
                    testHostProcess)),
                CancellationToken.None).ConfigureAwait(false);
            int fallbackPid = testHostProcessId ?? 0;
            return (
                (int)ExitCode.GenericFailure,
                new TestHostProcessInformation(fallbackPid, (int)ExitCode.GenericFailure, testHostCompletedReceived: false),
                telemetryInformation.IsEnabled ? "[]" : null);
        }
        else
        {
            await RunWithCancellationTeardownAsync(
                async () =>
                {
                    string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds];
                    double timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : double.Parse(seconds, CultureInfo.InvariantCulture);
                    await _logger.LogDebugAsync(
                        $"Test host controller named-pipe connection timeout is '{timeoutSeconds}' seconds.").ConfigureAwait(false);

                    // Wait for the test host process to connect to the controller's named pipe.
                    await _logger.LogDebugAsync("Waiting for the test host process to connect to the controller's named pipe.").ConfigureAwait(false);
                    using var testHostExitCancellationTokenSource = new CancellationTokenSource();
                    EventHandler onTestHostExited = (_, _) =>
                        TryCancelTestHostExitCancellationTokenSource(testHostExitCancellationTokenSource, _logger);
                    testHostProcess.Exited += onTestHostExited;
                    if (testHostProcess.HasExited)
                    {
#if NET
                        await testHostExitCancellationTokenSource.CancelAsync().ConfigureAwait(false);
#else
                        testHostExitCancellationTokenSource.Cancel();
#endif
                    }

                    bool connected;
                    try
                    {
                        connected = await WaitForTestHostControllerConnectionOrProcessExitAsync(
                            testHostControllerIpc.WaitConnectionAsync,
                            timeoutSeconds,
                            applicationCancellationToken,
                            async () =>
                            {
                                testHostControllerConnectionTimedOut = true;
                                await outputDevice.DisplayAsync(
                                    this,
                                    new ErrorMessageOutputDeviceData(CreateTestHostControllerConnectionFailureMessage(
                                        TimeSpan.FromSeconds(timeoutSeconds),
                                        TimeSpan.FromSeconds(timeoutSeconds),
                                        testHostProcess)),
                                    CancellationToken.None).ConfigureAwait(false);
                            },
                            testHostExitCancellationTokenSource.Token,
                            () => outputDevice.DisplayAsync(
                                this,
                                new ErrorMessageOutputDeviceData(CreateTestHostControllerConnectionFailureMessage(
                                    consoleRunStarted.Elapsed,
                                    timeout: null,
                                    testHostProcess)),
                                CancellationToken.None)).ConfigureAwait(false);
                    }
                    finally
                    {
                        testHostProcess.Exited -= onTestHostExited;
                    }

                    if (!connected)
                    {
                        return;
                    }

                    // Wait for the test host controller to send the PID of the test host process.
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(applicationCancellationToken))
                    {
                        timeout.CancelAfter(TimeoutHelper.DefaultHangTimeSpanTimeout);
                        _waitForPid.Wait(timeout.Token);
                    }

                    await _logger.LogDebugAsync("Invoking test-host-process-started lifecycle handlers.").ConfigureAwait(false);

                    if (_testHostPID is null)
                    {
                        return;
                    }

                    if (testHostProcess is TestHostHandleToProcessAdapter handleAdapter
                        && handleAdapter.TrustedProcessId is int trustedProcessId
                        && _testHostPID.Value != trustedProcessId)
                    {
                        throw new InvalidOperationException(
                            $"The test host reported process ID {_testHostPID.Value}, but the launcher created process ID {trustedProcessId}.");
                    }

                    bool startHandlersCompleted = true;
                    if (_testHostsInformation.LifetimeHandlers.Length > 0)
                    {
                        // We don't block the host during the 'OnTestHostProcessStartedAsync' by-design, if 'ITestHostProcessLifetimeHandler' extensions needs
                        // to block the execution of the test host should add an in-process extension like an 'ITestHostApplicationLifetime' and
                        // wait for a connection/signal to return.
                        // This is partial information because we don't yet know the exit code, as we are just starting.
                        // The full info contains the exit code and happens after WaitForExit.
                        TestHostProcessInformation partialTestHostProcessInformation = new(_testHostPID.Value);
                        foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
                        {
                            startHandlersCompleted = await TryRunControllerExtensionAsync(
                                token => lifetimeHandler.OnTestHostProcessStartedAsync(partialTestHostProcessInformation, token),
                                applicationCancellationToken).ConfigureAwait(false);
                            if (!startHandlersCompleted)
                            {
                                _servicesStillRunning.Add(lifetimeHandler);
                                break;
                            }
                        }
                    }

                    await _logger.LogDebugAsync("Waiting for the test host process to exit.").ConfigureAwait(false);
                    if (!startHandlersCompleted)
                    {
                        throw new OperationCanceledException(applicationCancellationToken);
                    }

                    await testHostProcess.WaitForExitAsync(applicationCancellationToken).ConfigureAwait(false);
                },
                applicationCancellationToken,
                testHostProcess,
                testHostControllerCancellationServer.RequestCancellation,
                _logger,
                TestHostCooperativeShutdownTimeout,
                TestHostTerminationTimeout).ConfigureAwait(false);
        }

        if (testHostControllerConnectionTimedOut)
        {
            await TerminateTestHostAfterConnectionFailureAsync(testHostProcess, _logger, TestHostTerminationTimeout).ConfigureAwait(false);
            int fallbackPid = testHostProcessId ?? 0;
            return (
                (int)ExitCode.GenericFailure,
                new TestHostProcessInformation(fallbackPid, (int)ExitCode.GenericFailure, testHostCompletedReceived: false),
                telemetryInformation.IsEnabled ? "[]" : null);
        }

        if (_testHostPID is null && applicationCancellationToken.IsCancellationRequested)
        {
            int fallbackPid = testHostProcessId ?? 0;
            var canceledProcessInformation = new TestHostProcessInformation(
                fallbackPid,
                (int)ExitCode.TestSessionAborted,
                testHostCompletedReceived: false);
            return (
                (int)ExitCode.TestSessionAborted,
                canceledProcessInformation,
                telemetryInformation.IsEnabled ? "[]" : null);
        }

        if (_testHostPID is null)
        {
            int fallbackPid = testHostProcessId ?? 0;
            return (
                (int)ExitCode.GenericFailure,
                new TestHostProcessInformation(fallbackPid, (int)ExitCode.GenericFailure, testHostCompletedReceived: false),
                telemetryInformation.IsEnabled ? "[]" : null);
        }

        return await FinalizeTestHostProcessAsync(
            testHostProcess,
            outputDevice,
            telemetryInformation,
            consoleRunStarted,
            applicationCancellationToken).ConfigureAwait(false);
    }
}
