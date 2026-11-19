// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostControllersTestHost
{
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
}
