// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Background slow-test detection and periodic reporting.
/// </summary>
internal abstract partial class SimplifiedConsoleOutputDeviceBase
{
    /// <summary>
    /// Reports tests that have crossed their next slow-test threshold.
    /// </summary>
    /// <remarks>
    /// Browser WebAssembly runs this cooperatively. Delay continuations can run while a test is asynchronously
    /// suspended, but no managed diagnostic can execute while a test synchronously blocks the sole WebAssembly thread.
    /// </remarks>
    internal async Task ReportSlowTestsOnceAsync(CancellationToken cancellationToken)
    {
        SlowTestDiagnostic[] diagnostics = _activeTestTracker.GetDueDiagnostics();
        if (diagnostics.Length == 0)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (SlowTestDiagnostic diagnostic in diagnostics)
            {
                if (!_activeTestTracker.IsActive(diagnostic))
                {
                    continue;
                }

                string duration = HumanReadableDurationFormatter.Render(diagnostic.Elapsed, wrapInParentheses: false);
                ConsoleLog(string.Format(
                    CultureInfo.CurrentCulture,
                    TerminalResources.TerminalProgressSlowTest,
                    duration,
                    diagnostic.DisplayName));
            }
        }
    }

    private async Task ReportSlowTestsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_slowTestPollInterval, cancellationToken).ConfigureAwait(false);
                await ReportSlowTestsOnceAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private sealed class SlowTestReporterState(CancellationTokenSource cancellationTokenSource, Task task)
    {
        internal CancellationTokenSource CancellationTokenSource { get; } = cancellationTokenSource;

        internal Task Task { get; } = task;
    }
}
