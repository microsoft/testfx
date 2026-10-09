// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
    internal static void TryCancelTestHostExitCancellationTokenSource(CancellationTokenSource cancellationTokenSource, ILogger logger)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException ex)
        {
            // The handler can race with the connection-wait cleanup: if the process exit signal is
            // queued before the handler is detached but executes after the CTS has been disposed,
            // cancellation throws. Keep the late notification observable without failing the run.
            logger.LogDebug($"CancellationTokenSource already disposed when test host process exited: {ex.Message}");
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
