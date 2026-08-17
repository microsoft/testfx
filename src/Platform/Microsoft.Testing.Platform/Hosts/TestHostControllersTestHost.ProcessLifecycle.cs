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
        CancellationToken cancellationToken)
    {
        // Apply the ITestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync
        if (_testHostsInformation.LifetimeHandlers.Length > 0)
        {
            foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
            {
                await lifetimeHandler.BeforeTestHostProcessStartAsync(cancellationToken).ConfigureAwait(false);
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
            : await LaunchUsingCustomLauncherAsync(testHostLauncher, processStartInfo, partialCommandLine, cancellationToken).ConfigureAwait(false);

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
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken))
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
                    await lifetimeHandler.OnTestHostProcessStartedAsync(partialTestHostProcessInformation, cancellationToken).ConfigureAwait(false);
                }
            }

            await _logger.LogDebugAsync("Wait for test host process exit").ConfigureAwait(false);
            try
            {
                await testHostProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The run was canceled while waiting for the test host to exit. Tear the host down
                // and wait (without cancellation) for it to fully exit, so the exit-code
                // reconciliation below still observes a real OS exit code.
                await _logger.LogDebugAsync("Wait for test host process exit was canceled; terminating the test host").ConfigureAwait(false);
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

                await testHostProcess.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        if (_testHostPID is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        TestHostProcessInformation testHostProcessInformation = new(_testHostPID.Value, testHostProcess.ExitCode, _testHostCompletedReceived);

        if (_testHostsInformation.LifetimeHandlers.Length > 0)
        {
            await _logger.LogDebugAsync($"Fire OnTestHostProcessExitedAsync: ExitCode: {testHostProcess.ExitCode}").ConfigureAwait(false);
            var messageBusProxy = (MessageBusProxy)ServiceProvider.GetMessageBus();

            foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
            {
                await lifetimeHandler.OnTestHostProcessExitedAsync(testHostProcessInformation, cancellationToken).ConfigureAwait(false);

                // OnTestHostProcess could produce information that needs to be handled by others.
                await messageBusProxy.DrainDataAsync().ConfigureAwait(false);
            }

            // We disable after the drain because it's possible that the drain will produce more messages
            await messageBusProxy.DrainDataAsync().ConfigureAwait(false);
            await messageBusProxy.DisableAsync().ConfigureAwait(false);
        }

        await outputDevice.DisplayAfterSessionEndRunAsync(cancellationToken).ConfigureAwait(false);

        string? extensionInformation = null;
        // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
        if (telemetryInformation.IsEnabled)
        {
            extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider).ConfigureAwait(false);
        }

        // If we have a process in the middle between the test host controller and the test host process we need to keep it into account.
        int exitCode = _testHostUnfilteredExitCodeReceived ?? testHostProcess.ExitCode;
        if (exitCode == (int)ExitCode.Success && cancellationToken.IsCancellationRequested)
        {
            // In case of cancellation, only alter exit code if it was success.
            // If there is another exit code indicating another failure, we prefer it over the cancellation.
            exitCode = (int)ExitCode.TestSessionAborted;
        }
        else if (!testHostProcessInformation.HasExitedGracefully ||
            _testHostExitCodeReceived != testHostProcess.ExitCode)
        {
            await _logger.LogWarningAsync(
                $"""
                 Test host did not exit gracefully.
                   OS exit code: '{testHostProcess.ExitCode}'
                   IPC-reported exit code: '{(_testHostExitCodeReceived.HasValue ? _testHostExitCodeReceived.Value.ToString(CultureInfo.InvariantCulture) : "<not received>")}'
                   TestHostCompletedRequest received: '{_testHostCompletedReceived}'
                   PID: '{_testHostPID.Value.ToString(CultureInfo.InvariantCulture)}'
                   CancellationRequested: '{cancellationToken.IsCancellationRequested}'
                 """)
                .ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestProcessDidNotExitGracefullyErrorMessage, testHostProcess.ExitCode)), cancellationToken).ConfigureAwait(false);
            exitCode = (int)ExitCode.TestHostProcessExitedNonGracefully;
        }

        // Apply controller-only coverage thresholds to the child's pre-ignore verdict, then apply the
        // ignore policy exactly once so ignoring a higher-priority child verdict cannot expose coverage 14.
        exitCode = CoverageThresholdExitCodePolicy.Apply(exitCode, ServiceProvider);
        exitCode = ExitCodeIgnorePolicy.Apply(exitCode, ServiceProvider.GetCommandLineOptions(), ServiceProvider.GetEnvironment());

        await _logger.LogInformationAsync($"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcess.ExitCode}') in '{consoleRunStarted.Elapsed}'").ConfigureAwait(false);

        return (exitCode, testHostProcessInformation, extensionInformation);
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
