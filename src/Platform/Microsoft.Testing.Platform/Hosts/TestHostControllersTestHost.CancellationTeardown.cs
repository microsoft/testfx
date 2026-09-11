// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
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
            await logger.LogDebugAsync($"Waiting for the test host to exit during cancellation teardown failed; continuing cleanup. {ex}").ConfigureAwait(false);
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

        await logger.LogDebugAsync($"Test host did not exit within '{cooperativeShutdownTimeout}' after cooperative cancellation; terminating it.").ConfigureAwait(false);
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
            await logger.LogDebugAsync($"Test host termination during cancellation failed; continuing cleanup. {ex}").ConfigureAwait(false);
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
            await logger.LogDebugAsync("Test host execution was canceled; requesting cooperative test host cancellation.").ConfigureAwait(false);
            await HandleCanceledTestHostAsync(
                testHostProcess,
                requestCancellation,
                logger,
                cooperativeShutdownTimeout,
                terminationTimeout).ConfigureAwait(false);
        }
    }
}
