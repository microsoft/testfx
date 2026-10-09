// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
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
    private async Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)> FinalizeTestHostProcessAsync(
        IProcess testHostProcess,
        ProxyOutputDevice outputDevice,
        ITelemetryInformation telemetryInformation,
        Stopwatch consoleRunStarted,
        CancellationToken applicationCancellationToken)
    {
        bool testHostProcessExited = testHostProcess.HasExited;
        bool testHostProcessExitCodeIsAuthoritative = testHostProcess is not TestHostHandleToProcessAdapter handleAdapter
            || handleAdapter.IsExitCodeAuthoritative;
        int testHostProcessExitCode = testHostProcessExited
            ? testHostProcess.ExitCode
            : (int)ExitCode.TestSessionAborted;
        (bool testExecutionCanceled, int reportedTestHostExitCode) = ResolveTestHostExitState(
            applicationCancellationToken.IsCancellationRequested,
            _testHostUnfilteredExitCodeReceived,
            _testHostExitCodeReceived,
            testHostProcessExitCode,
            testHostProcessExitCodeIsAuthoritative);
#pragma warning disable CS8629
        TestHostProcessInformation testHostProcessInformation = new(_testHostPID.Value, reportedTestHostExitCode, _testHostCompletedReceived);
#pragma warning restore CS8629
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
        if (!_controllerFinalizationTimedOut && !testExecutionCanceled && !outputConsumerStillRunning)
        {
            TestCoverageResult coverageResult = ServiceProvider.GetRequiredService<TestCoverageResult>();
            foreach (string error in coverageResult.GetThresholdErrors())
            {
                bool displayed = await TryRunControllerExtensionAsync(
                    token => outputDevice.DisplayAsync(coverageResult, new ErrorMessageOutputDeviceData(error), token),
                    finalizationCancellationToken).ConfigureAwait(false);
                if (!displayed)
                {
                    MarkOutputDeviceStillRunning(_servicesStillRunning, outputDevice);
                    _controllerFinalizationTimedOut = true;
                    break;
                }
            }
        }

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
        int exitCode = _testHostUnfilteredExitCodeReceived
            ?? (!testHostProcessExitCodeIsAuthoritative && _testHostExitCodeReceived.HasValue
                ? _testHostExitCodeReceived.Value
                : testHostProcessExitCode);
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
        else if (!testHostProcessInformation.HasExitedGracefully
            || (testHostProcessExitCodeIsAuthoritative && _testHostExitCodeReceived != testHostProcessExitCode))
        {
            await _logger.LogWarningAsync(
                $"""
                 Test host did not exit gracefully.
                   OS exit code: '{testHostProcessExitCode}'
                   OS exit code authoritative: '{testHostProcessExitCodeIsAuthoritative}'
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

        if (!_controllerFinalizationTimedOut)
        {
            foreach (ITestHostControllerRunCompletionHandler handler in _testHostsInformation.RunCompletionHandlers)
            {
                if (!await TryRunControllerExtensionAsync(
                    token => handler.OnRunCompletedAsync(exitCode, _controllerSummaryArtifacts, token),
                    finalizationCancellationToken).ConfigureAwait(false))
                {
                    _servicesStillRunning.Add(handler);
                    _controllerFinalizationTimedOut = true;
                    ScheduleFinalizationTimeoutWarning();
                    if (exitCode == (int)ExitCode.Success)
                    {
                        exitCode = (int)ExitCode.TestSessionAborted;
                    }

                    break;
                }
            }
        }

        await _logger.LogInformationAsync(
            $"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcessExitCode}') in '{consoleRunStarted.Elapsed}'.").ConfigureAwait(false);

        return (exitCode, testHostProcessInformation, extensionInformation);
    }

    internal static (bool TestExecutionCanceled, int ReportedTestHostExitCode) ResolveTestHostExitState(
        bool applicationCancellationRequested,
        int? testHostUnfilteredExitCodeReceived,
        int? testHostExitCodeReceived,
        int testHostProcessExitCode,
        bool testHostProcessExitCodeIsAuthoritative)
    {
        bool testExecutionCanceled = applicationCancellationRequested
            || testHostUnfilteredExitCodeReceived is (int)ExitCode.TestSessionAborted
            || (testHostProcessExitCodeIsAuthoritative && testHostProcessExitCode == (int)ExitCode.TestSessionAborted);
        int reportedTestHostExitCode = testExecutionCanceled
            ? (int)ExitCode.TestSessionAborted
            : !testHostProcessExitCodeIsAuthoritative && testHostExitCodeReceived.HasValue
                ? testHostExitCodeReceived.Value
                : testHostProcessExitCode;

        return (testExecutionCanceled, reportedTestHostExitCode);
    }
}
