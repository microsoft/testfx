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
        await _logger.LogDebugAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{currentPid} '{testHostProcessStartupTime}'").ConfigureAwait(false);
        await _logger.LogDebugAsync($"Starting test host process '{processStartInfo.FileName}' with args '{processStartInfo.Arguments}'").ConfigureAwait(false);

        ITestHostLauncher? testHostLauncher = _testHostsInformation.TestHostLauncher;
        using IProcess testHostProcess = testHostLauncher is null
            ? process.Start(processStartInfo)
            : await LaunchUsingCustomLauncherAsync(testHostLauncher, processStartInfo, partialCommandLine, applicationCancellationToken).ConfigureAwait(false);

        int? testHostProcessId = null;
        try
        {
            testHostProcessId = testHostProcess.Id;
        }
        catch (InvalidOperationException ex) when (testHostProcess.HasExited || testHostLauncher is not null)
        {
            // Access PID can throw InvalidOperationException if the process has already exited:
            // System.InvalidOperationException: No process is associated with this object.
            // A custom launcher may also legitimately not expose a local PID (e.g. container/remote).
            await _logger.LogDebugAsync($"Unable to obtain test host PID; process had already exited or does not expose a PID (HasExited: {testHostProcess.HasExited}). {ex.GetType().FullName}: {ex.Message}").ConfigureAwait(false);
        }

        testHostProcess.Exited += (_, _) =>
            _logger.LogDebug($"Test host process exited, PID: '{testHostProcessId}'");

        await _logger.LogDebugAsync($"Started test host process '{testHostProcessId}' HasExited: {testHostProcess.HasExited}").ConfigureAwait(false);
        var applicationCancellationTokenSource =
            (CTRLPlusCCancellationTokenSource)ServiceProvider.GetTestApplicationCancellationTokenSource();
        applicationCancellationTokenSource.SetForceExitAction(testHostProcess.Kill);

        try
        {
            // Note: we intentionally gate on HasExited only and not on 'testHostProcessId is null'.
            // A custom ITestHostLauncher may legitimately not expose a local PID (e.g. container,
            // remote, or AUMID-activated apps); the real test host PID still arrives via the IPC
            // handshake (_testHostPID). For the default Process.Start path, a null PID always
            // coincides with HasExited == true, so behavior is unchanged there.
            if (testHostProcess.HasExited)
            {
                await _logger.LogDebugAsync("Test host process exited prematurely").ConfigureAwait(false);
            }
            else
            {
                await RunWithCancellationTeardownAsync(
                    async () =>
                    {
                        string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds];
                        double timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : double.Parse(seconds, CultureInfo.InvariantCulture);
                        await _logger.LogDebugAsync($"Setting PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds '{timeoutSeconds}'").ConfigureAwait(false);

                        // Wait for the test host controller to connect.
                        using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds)))
                        using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, applicationCancellationToken))
                        {
                            await _logger.LogDebugAsync("Wait connection from the test host process").ConfigureAwait(false);
                            await testHostControllerIpc.WaitConnectionAsync(linkedToken.Token).ConfigureAwait(false);
                        }

                        // Wait for the test host controller to send the PID of the test host process.
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(applicationCancellationToken))
                        {
                            timeout.CancelAfter(TimeoutHelper.DefaultHangTimeSpanTimeout);
                            _waitForPid.Wait(timeout.Token);
                        }

                        await _logger.LogDebugAsync("Fire OnTestHostProcessStartedAsync").ConfigureAwait(false);

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

                        await _logger.LogDebugAsync("Wait for test host process exit").ConfigureAwait(false);
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
        }
        finally
        {
            applicationCancellationTokenSource.SetForceExitAction(null);
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
                await _logger.LogDebugAsync($"Fire OnTestHostProcessExitedAsync: ExitCode: {testHostProcessExitCode}").ConfigureAwait(false);
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

        await _logger.LogInformationAsync($"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcessExitCode}') in '{consoleRunStarted.Elapsed}'").ConfigureAwait(false);

        return (exitCode, testHostProcessInformation, extensionInformation);
    }

    private CancellationTokenSource EnsureControllerFinalizationCancellationTokenSource()
    {
        var candidate = new CancellationTokenSource();
        CancellationTokenSource? existing =
            Interlocked.CompareExchange(ref _controllerFinalizationCancellationTokenSource, candidate, null);
        if (existing is null)
        {
            return candidate;
        }

        candidate.Dispose();
        return existing;
    }

    private void ArmControllerFinalizationTimeout()
    {
        if (Interlocked.Exchange(ref _controllerFinalizationTimeoutArmed, 1) == 0)
        {
            EnsureControllerFinalizationCancellationTokenSource().CancelAfter(_controllerExtensionFinalizationTimeout);
        }
    }

    private void RegisterControllerFinalizationTransition(CancellationToken applicationCancellationToken)
        => _controllerFinalizationTransitionRegistration = applicationCancellationToken.Register(
            static state => ((TestHostControllersTestHost)state!).ArmControllerFinalizationTimeout(),
            this);

    private static void MarkOutputDeviceStillRunning(List<object> servicesStillRunning, ProxyOutputDevice outputDevice)
    {
        if (!servicesStillRunning.Contains(outputDevice))
        {
            servicesStillRunning.Add(outputDevice);
        }

        if (!servicesStillRunning.Contains(outputDevice.OriginalOutputDevice))
        {
            servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
        }
    }

    private static async Task<bool> WaitForExitAfterTerminationAsync(
        IProcess testHostProcess,
        TimeSpan timeout,
        ILogger logger)
    {
        try
        {
            await testHostProcess.WaitForExitAsync(CancellationToken.None)
                .TimeoutAfterAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await logger.LogDebugAsync($"Ignoring failure while waiting for the test host to exit during cancellation teardown: {ex}").ConfigureAwait(false);
            return false;
        }
    }

    internal static async Task HandleCanceledTestHostAsync(
        IProcess testHostProcess,
        Action requestCancellation,
        ILogger logger,
        TimeSpan cooperativeShutdownTimeout,
        TimeSpan terminationTimeout)
    {
        requestCancellation();
        if (await WaitForExitAfterTerminationAsync(testHostProcess, cooperativeShutdownTimeout, logger).ConfigureAwait(false))
        {
            return;
        }

        await logger.LogDebugAsync($"Test host did not exit within {cooperativeShutdownTimeout} after cooperative cancellation; terminating it").ConfigureAwait(false);
        try
        {
            testHostProcess.Kill();
        }
        catch (Exception ex)
        {
            // Termination is best-effort. The host may have exited between the cancellation
            // and this Kill call (InvalidOperationException), or Kill may delegate to a custom
            // ITestHostLauncher's Terminate() which can throw anything (e.g. NotSupportedException,
            // Win32Exception). Either way the host is on its way out, so swallow and log rather
            // than letting it mask the cancellation teardown flow.
            await logger.LogDebugAsync($"Ignoring failure while terminating the test host during cancellation: {ex}").ConfigureAwait(false);
        }

        if (await WaitForExitAfterTerminationAsync(testHostProcess, terminationTimeout, logger).ConfigureAwait(false))
        {
            return;
        }

        if (testHostProcess is TestHostHandleToProcessAdapter adapter)
        {
            adapter.DeferDisposalUntilExit();
        }

        await logger.LogWarningAsync(
            $"Test host did not exit within {terminationTimeout} after termination was requested; continuing controller finalization.").ConfigureAwait(false);
    }

    internal static async Task RunWithCancellationTeardownAsync(
        Func<Task> runAsync,
        CancellationToken applicationCancellationToken,
        IProcess testHostProcess,
        Action requestCancellation,
        ILogger logger,
        TimeSpan cooperativeShutdownTimeout,
        TimeSpan terminationTimeout)
    {
        try
        {
            await runAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (applicationCancellationToken.IsCancellationRequested)
        {
            await logger.LogDebugAsync("Test host execution was canceled; requesting cooperative test host cancellation").ConfigureAwait(false);
            await HandleCanceledTestHostAsync(
                testHostProcess,
                requestCancellation,
                logger,
                cooperativeShutdownTimeout,
                terminationTimeout).ConfigureAwait(false);
        }
    }

    private void ScheduleFinalizationTimeoutWarning()
    {
        if (Interlocked.Exchange(ref _finalizationTimeoutWarningScheduled, 1) != 0)
        {
            return;
        }

        // The cleanup deadline has expired, so logging cannot be awaited without making the deadline unbounded
        // again. Schedule it independently and observe a later provider fault.
        ObserveBackgroundTask(Task.Run(
            () => _logger.LogWarning(
                $"Test host controller extension finalization exceeded the {_controllerExtensionFinalizationTimeout} cleanup timeout."),
            CancellationToken.None));
    }

    private static void ObserveBackgroundTask(Task task)
        => _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static async Task<bool> TryRunControllerExtensionAsync(
        Func<CancellationToken, Task> finalization,
        CancellationToken cancellationToken)
    {
        try
        {
            // Invoke on the thread pool before applying the external cancellation wrapper. An extension can
            // block synchronously before returning its Task; running the delegate inline would prevent us from
            // ever reaching WithCancellationAsync and make the supposedly bounded cleanup wait unbounded.
            await Task.Run(() => finalization(cancellationToken), CancellationToken.None)
                .WithCancellationAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    [UnsupportedOSPlatform("browser")]
    private async Task<IProcess> LaunchUsingCustomLauncherAsync(
        ITestHostLauncher testHostLauncher,
        ProcessStartInfo processStartInfo,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified — populated from a runtime loop.
        Dictionary<string, string?> environmentVariables = new(StringComparer.Ordinal);
#pragma warning restore IDE0028
        foreach (string key in processStartInfo.EnvironmentVariables.Keys)
        {
            environmentVariables[key] = processStartInfo.EnvironmentVariables[key];
        }

        string? workingDirectory = RoslynString.IsNullOrEmpty(processStartInfo.WorkingDirectory) ? null : processStartInfo.WorkingDirectory;
        TestHostLaunchContext context = new(processStartInfo.FileName, arguments, environmentVariables, workingDirectory);

        await _logger.LogDebugAsync($"Delegating test host launch to '{testHostLauncher.DisplayName}' (UID: {testHostLauncher.Uid})").ConfigureAwait(false);
        ITestHostHandle handle = await testHostLauncher.LaunchTestHostAsync(context, cancellationToken).ConfigureAwait(false);
        await _logger.LogDebugAsync($"Test host launched by '{testHostLauncher.Uid}' (Identifier: '{handle.Identifier ?? "<none>"}')").ConfigureAwait(false);
        return new TestHostHandleToProcessAdapter(handle);
    }
}
