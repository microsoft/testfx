// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    internal static async Task<bool> WaitForTestHostControllerConnectionAsync(
        Func<CancellationToken, Task> waitConnectionAsync,
        double timeoutSeconds,
        CancellationToken applicationCancellationToken,
        Func<Task> onTimeoutAsync)
        => await WaitForTestHostControllerConnectionOrProcessExitAsync(
            waitConnectionAsync,
            timeoutSeconds,
            applicationCancellationToken,
            onTimeoutAsync,
            CancellationToken.None,
            onTestHostExitAsync: null).ConfigureAwait(false);

    internal static async Task<bool> WaitForTestHostControllerConnectionOrProcessExitAsync(
        Func<CancellationToken, Task> waitConnectionAsync,
        double timeoutSeconds,
        CancellationToken applicationCancellationToken,
        Func<Task> onTimeoutAsync,
        CancellationToken testHostExitCancellationToken,
        Func<Task>? onTestHostExitAsync)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            applicationCancellationToken,
            testHostExitCancellationToken);
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
        catch (OperationCanceledException) when (testHostExitCancellationToken.IsCancellationRequested
            && !timeout.IsCancellationRequested
            && !applicationCancellationToken.IsCancellationRequested)
        {
            if (onTestHostExitAsync is not null)
            {
                await onTestHostExitAsync().ConfigureAwait(false);
            }

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
