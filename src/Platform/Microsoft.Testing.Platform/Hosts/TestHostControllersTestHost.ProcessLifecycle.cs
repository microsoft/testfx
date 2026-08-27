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
        ProxyOutputDevice outputDevice,
        ITelemetryInformation telemetryInformation,
        Stopwatch consoleRunStarted,
        CancellationToken applicationCancellationToken)
    {
        // Apply the ITestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync
        if (_testHostsInformation.LifetimeHandlers.Length > 0)
        {
            foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
            {
                await lifetimeHandler.BeforeTestHostProcessStartAsync(applicationCancellationToken).ConfigureAwait(false);
            }
        }

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
            string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds];
            double timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : double.Parse(seconds, CultureInfo.InvariantCulture);
            await _logger.LogDebugAsync($"Setting PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds '{timeoutSeconds}'").ConfigureAwait(false);

            // Wait for the test host controller to connect
            using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, applicationCancellationToken))
            {
                await _logger.LogDebugAsync("Wait connection from the test host process").ConfigureAwait(false);
                await testHostControllerIpc.WaitConnectionAsync(linkedToken.Token).ConfigureAwait(false);
            }

            // Wait for the test host controller to send the PID of the test host process
            using (CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
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
            try
            {
                if (!startHandlersCompleted)
                {
                    throw new OperationCanceledException(applicationCancellationToken);
                }

                await testHostProcess.WaitForExitAsync(applicationCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (applicationCancellationToken.IsCancellationRequested)
            {
                // The run was canceled while waiting for the test host to exit. Tear the host down
                // and wait (without cancellation) for it to fully exit, so the exit-code
                // reconciliation below still observes a real OS exit code.
                await _logger.LogDebugAsync("Test host execution was canceled; terminating the test host").ConfigureAwait(false);
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
                    await _logger.LogDebugAsync($"Ignoring failure while terminating the test host during cancellation: {ex}").ConfigureAwait(false);
                }

                try
                {
                    await testHostProcess.WaitForExitAsync(CancellationToken.None)
                        .TimeoutAfterAsync(TestHostTerminationTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await _logger.LogWarningAsync(
                        $"Test host did not exit within {TestHostTerminationTimeout} after termination was requested; continuing controller finalization.").ConfigureAwait(false);
                }
            }
        }

        if (_testHostPID is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        int testHostProcessExitCode = testHostProcess.HasExited
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
        using CancellationTokenSource? finalizationCancellationTokenSource = testExecutionCanceled
            ? new(ControllerExtensionFinalizationTimeout)
            : null;
        CancellationToken finalizationCancellationToken = finalizationCancellationTokenSource?.Token ?? applicationCancellationToken;

        try
        {
            if (_testHostsInformation.LifetimeHandlers.Length > 0)
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
        catch (OperationCanceledException) when (finalizationCancellationTokenSource?.IsCancellationRequested is true)
        {
            _controllerFinalizationTimedOut = true;
        }

        if (testExecutionCanceled && !applicationCancellationToken.IsCancellationRequested)
        {
            // The child owns timeout cancellation and reported an aborted exit. Report that state explicitly
            // after controller extensions have finalized, without canceling their independent cleanup token.
            IStopPoliciesService stopPoliciesService = ServiceProvider.GetRequiredService<IStopPoliciesService>();
            bool abortReported = await TryRunControllerExtensionAsync(
                _ => stopPoliciesService.ExecuteAbortCallbacksAsync(),
                finalizationCancellationToken).ConfigureAwait(false);
            if (!abortReported)
            {
                _servicesStillRunning.Add(stopPoliciesService);
                _servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
                _controllerFinalizationTimedOut = true;
            }
        }

        bool outputConsumerStillRunning = messageBusProxy.ConsumersStillRunning.Any(
            consumer => ReferenceEquals(consumer, outputDevice.OriginalOutputDevice));
        if (!_controllerFinalizationTimedOut && !outputConsumerStillRunning)
        {
            bool outputFinalized = await TryRunControllerExtensionAsync(
                token => outputDevice.DisplayAfterSessionEndRunAsync(token),
                finalizationCancellationToken).ConfigureAwait(false);
            if (!outputFinalized)
            {
                _servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
                _controllerFinalizationTimedOut = true;
            }
        }

        if (outputConsumerStillRunning && !_servicesStillRunning.Contains(outputDevice.OriginalOutputDevice))
        {
            _servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
        }

        string? extensionInformation = null;
        // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
        if (telemetryInformation.IsEnabled)
        {
            extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider).ConfigureAwait(false);
        }

        // If we have a process in the middle between the test host controller and the test host process we need to keep it into account.
        int exitCode = _testHostUnfilteredExitCodeReceived ?? testHostProcessExitCode;
        if (exitCode == (int)ExitCode.Success && testExecutionCanceled)
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
                    if (!_servicesStillRunning.Contains(outputDevice.OriginalOutputDevice))
                    {
                        _servicesStillRunning.Add(outputDevice.OriginalOutputDevice);
                    }

                    _controllerFinalizationTimedOut = true;
                }
            }

            exitCode = (int)ExitCode.TestHostProcessExitedNonGracefully;
        }

        if (_controllerFinalizationTimedOut)
        {
            // DisableCoreAsync observes cancellation arriving during its normal unbounded wait and
            // downgrades to the canceled-shutdown budget. Without this transition, disposal would re-await
            // the same unfinished graceful-disable task after our finalization token had already expired.
            ServiceProvider.GetTestApplicationCancellationTokenSource().Cancel();
            await _logger.LogWarningAsync(
                $"Test host controller extension finalization exceeded the {ControllerExtensionFinalizationTimeout} cleanup timeout.").ConfigureAwait(false);
        }

        // Apply controller-only coverage thresholds to the child's pre-ignore verdict, then apply the
        // ignore policy exactly once so ignoring a higher-priority child verdict cannot expose coverage 14.
        exitCode = CoverageThresholdExitCodePolicy.Apply(exitCode, ServiceProvider);
        exitCode = ExitCodeIgnorePolicy.Apply(exitCode, ServiceProvider.GetCommandLineOptions(), ServiceProvider.GetEnvironment());

        await _logger.LogInformationAsync($"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcessExitCode}') in '{consoleRunStarted.Elapsed}'").ConfigureAwait(false);

        return (exitCode, testHostProcessInformation, extensionInformation);
    }

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
