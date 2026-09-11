// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
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
                    bool connected = await WaitForTestHostControllerConnectionAsync(
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
                        }).ConfigureAwait(false);
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
                        throw ApplicationStateGuard.Unreachable();
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
            throw ApplicationStateGuard.Unreachable();
        }

        bool testHostProcessExited = testHostProcess.HasExited;
        int testHostProcessExitCode = testHostProcessExited
            ? testHostProcess.ExitCode
            : (int)ExitCode.TestSessionAborted;
        bool testExecutionCanceled = applicationCancellationToken.IsCancellationRequested
            || _testHostUnfilteredExitCodeReceived is (int)ExitCode.TestSessionAborted
            || testHostProcessExitCode == (int)ExitCode.TestSessionAborted;
        int reportedTestHostExitCode = testExecutionCanceled
            ? (int)ExitCode.TestSessionAborted
            : testHostProcessExitCode;
        TestHostProcessInformation testHostProcessInformation = new(_testHostPID.Value, reportedTestHostExitCode, _testHostCompletedReceived);
        var messageBusProxy = (MessageBusProxy)ServiceProvider.GetMessageBus();
        CancellationTokenSource finalizationCancellationTokenSource = EnsureControllerFinalizationCancellationTokenSource();
        RegisterControllerFinalizationTransition(applicationCancellationToken);
        testExecutionCanceled |= applicationCancellationToken.IsCancellationRequested;
        if (testExecutionCanceled)
        {
            ArmControllerFinalizationTimeout();
        }

        CancellationToken finalizationCancellationToken = finalizationCancellationTokenSource.Token;
        bool abortCallbacksJoined = false;

        void TransitionToBoundedFinalizationIfCanceled()
        {
            if (!applicationCancellationToken.IsCancellationRequested)
            {
                return;
            }

            testExecutionCanceled = true;
            ArmControllerFinalizationTimeout();
        }

        async Task JoinAbortCallbacksIfCanceledAsync()
        {
            if (!testExecutionCanceled || abortCallbacksJoined)
            {
                return;
            }

            abortCallbacksJoined = true;
            IStopPoliciesService stopPoliciesService = ServiceProvider.GetRequiredService<IStopPoliciesService>();
            bool abortReported = await TryRunControllerExtensionAsync(
                _ => stopPoliciesService.ExecuteAbortCallbacksAsync(),
                finalizationCancellationToken).ConfigureAwait(false);
            if (!abortReported)
            {
                _servicesStillRunning.Add(stopPoliciesService);
                MarkOutputDeviceStillRunning(_servicesStillRunning, outputDevice);
                _controllerFinalizationTimedOut = true;
            }
        }

        try
        {
            if (testHostProcessExited && _testHostsInformation.LifetimeHandlers.Length > 0)
            {
                await _logger.LogDebugAsync($"Invoking test-host-process-exited lifecycle handlers. Exit code: '{testHostProcessExitCode}'.").ConfigureAwait(false);
                foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
                {
                    if (_servicesStillRunning.Contains(lifetimeHandler))
                    {
                        continue;
                    }

                    bool finalized = await TryRunControllerExtensionAsync(
                        token => lifetimeHandler.OnTestHostProcessExitedAsync(testHostProcessInformation, token),
                        finalizationCancellationToken).ConfigureAwait(false);
                    if (!finalized)
                    {
                        _servicesStillRunning.Add(lifetimeHandler);
                        TransitionToBoundedFinalizationIfCanceled();
                        _controllerFinalizationTimedOut = true;
                        break;
                    }

                    // OnTestHostProcess could produce information that needs to be handled by others.
                    await messageBusProxy.DrainDataAsync().WithCancellationAsync(finalizationCancellationToken).ConfigureAwait(false);
                }
            }

            if (!_controllerFinalizationTimedOut)
            {
                // We disable after the drain because it's possible that the drain will produce more messages.
                // This runs even without lifetime handlers because a data consumer alone can require a controller
                // process and must not escape the canceled-run cleanup budget.
                await messageBusProxy.DrainDataAsync().WithCancellationAsync(finalizationCancellationToken).ConfigureAwait(false);
                await messageBusProxy.DisableAsync().WithCancellationAsync(finalizationCancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (finalizationCancellationToken.IsCancellationRequested)
        {
            TransitionToBoundedFinalizationIfCanceled();
            _controllerFinalizationTimedOut = true;
        }

        TransitionToBoundedFinalizationIfCanceled();
        // Report the abort after controller extensions have finalized. ExecuteAbortCallbacksAsync is
        // one-shot and returns the same in-flight task, so this also joins application-token cancellation.
        await JoinAbortCallbacksIfCanceledAsync().ConfigureAwait(false);

        bool outputConsumerStillRunning = messageBusProxy.ConsumersStillRunning.Any(
            consumer => ReferenceEquals(consumer, outputDevice.OriginalOutputDevice));
        if (!_controllerFinalizationTimedOut && !outputConsumerStillRunning)
        {
            bool outputFinalized = await TryRunControllerExtensionAsync(
                token => outputDevice.DisplayAfterSessionEndRunAsync(token),
                finalizationCancellationToken).ConfigureAwait(false);
            if (!outputFinalized)
            {
                MarkOutputDeviceStillRunning(_servicesStillRunning, outputDevice);
                TransitionToBoundedFinalizationIfCanceled();
                _controllerFinalizationTimedOut = true;
            }
        }

        if (outputConsumerStillRunning)
        {
            MarkOutputDeviceStillRunning(_servicesStillRunning, outputDevice);
        }

        TransitionToBoundedFinalizationIfCanceled();
        await JoinAbortCallbacksIfCanceledAsync().ConfigureAwait(false);

        if (_controllerFinalizationTimedOut)
        {
            ScheduleFinalizationTimeoutWarning();
        }

        // Telemetry requires a valid JSON payload even when the cleanup deadline prevents extension
        // enumeration. An empty array records that collection was intentionally skipped without re-entering
        // abandoned extensions.
        string? extensionInformation = telemetryInformation.IsEnabled ? "[]" : null;
        // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
        if (telemetryInformation.IsEnabled && !testExecutionCanceled)
        {
            extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider).ConfigureAwait(false);
        }

        // If we have a process in the middle between the test host controller and the test host process we need to keep it into account.
        int exitCode = _testHostUnfilteredExitCodeReceived ?? testHostProcessExitCode;
        if (!testHostProcessExited)
        {
            exitCode = (int)ExitCode.TestSessionAborted;
        }
        else if (exitCode == (int)ExitCode.Success
            && (testExecutionCanceled || _controllerFinalizationTimedOut))
        {
            // In case of cancellation, only alter exit code if it was success.
            // If there is another exit code indicating another failure, we prefer it over the cancellation.
            exitCode = (int)ExitCode.TestSessionAborted;
        }
        else if (!testHostProcessInformation.HasExitedGracefully ||
            _testHostExitCodeReceived != testHostProcessExitCode)
        {
            await _logger.LogWarningAsync(
                $"""
                 Test host did not exit gracefully.
                   OS exit code: '{testHostProcessExitCode}'
                   IPC-reported exit code: '{(_testHostExitCodeReceived.HasValue ? _testHostExitCodeReceived.Value.ToString(CultureInfo.InvariantCulture) : "<not received>")}'
                   TestHostCompletedRequest received: '{_testHostCompletedReceived}'
                   PID: '{_testHostPID.Value.ToString(CultureInfo.InvariantCulture)}'
                   CancellationRequested: '{testExecutionCanceled}'
                 """)
                .ConfigureAwait(false);
            if (!_controllerFinalizationTimedOut)
            {
                bool diagnosticDisplayed = await TryRunControllerExtensionAsync(
                    token => outputDevice.DisplayAsync(
                        this,
                        new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestProcessDidNotExitGracefullyErrorMessage, testHostProcessExitCode)),
                        token),
                    finalizationCancellationToken).ConfigureAwait(false);
                if (!diagnosticDisplayed)
                {
                    MarkOutputDeviceStillRunning(_servicesStillRunning, outputDevice);
                    TransitionToBoundedFinalizationIfCanceled();
                    _controllerFinalizationTimedOut = true;
                }
            }

            exitCode = (int)ExitCode.TestHostProcessExitedNonGracefully;
        }

        if (_controllerFinalizationTimedOut)
        {
            ScheduleFinalizationTimeoutWarning();
        }

        // Apply controller-only coverage thresholds to the child's pre-ignore verdict, then apply the
        // ignore policy exactly once so ignoring a higher-priority child verdict cannot expose coverage 14.
        exitCode = CoverageThresholdExitCodePolicy.Apply(exitCode, ServiceProvider);
        exitCode = ExitCodeIgnorePolicy.Apply(exitCode, ServiceProvider.GetCommandLineOptions(), ServiceProvider.GetEnvironment());

        await _logger.LogInformationAsync(
            $"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcessExitCode}') in '{consoleRunStarted.Elapsed}'.").ConfigureAwait(false);

        return (exitCode, testHostProcessInformation, extensionInformation);
    }

    internal static async Task<bool> WaitForTestHostControllerConnectionAsync(
        Func<CancellationToken, Task> waitConnectionAsync,
        double timeoutSeconds,
        CancellationToken applicationCancellationToken,
        Func<Task> onTimeoutAsync)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, applicationCancellationToken);
        try
        {
            await waitConnectionAsync(linkedToken.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !applicationCancellationToken.IsCancellationRequested)
        {
            await onTimeoutAsync().ConfigureAwait(false);
            return false;
        }
    }

    internal static string CreateTestHostControllerConnectionFailureMessage(
        TimeSpan waitDuration,
        TimeSpan? timeout,
        IProcess testHostProcess)
    {
        bool hasExited = testHostProcess.HasExited;
        string processState = hasExited
            ? $"The test host process exited with code '{testHostProcess.ExitCode}'."
            : "The test host process is still running.";

        string timeoutDetails = timeout is null
            ? string.Empty
            : $" The configured connection timeout was '{timeout.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture)}' seconds.";
        return $"The test host process did not connect to the controller's named pipe after '{waitDuration.TotalSeconds.ToString(CultureInfo.InvariantCulture)}' seconds.{timeoutDetails} {processState}";
    }

    internal static async Task TerminateTestHostAfterConnectionFailureAsync(
        IProcess testHostProcess,
        ILogger logger,
        TimeSpan terminationTimeout)
    {
        if (testHostProcess.HasExited)
        {
            return;
        }

        try
        {
            testHostProcess.Kill();
        }
        catch (Exception ex)
        {
            await logger.LogDebugAsync($"Test host termination after a connection failure failed; continuing cleanup. {ex}").ConfigureAwait(false);
        }

        bool exited = await WaitForExitAfterTerminationAsync(testHostProcess, terminationTimeout, logger).ConfigureAwait(false);
        if (!exited && testHostProcess is TestHostHandleToProcessAdapter adapter)
        {
            adapter.DeferDisposalUntilExit();
        }
    }
}
