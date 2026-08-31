// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class HangDumpProcessLifetimeHandler
{
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private void TriggerDumpOnce(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        // The inactivity timer and the deadline timer can both fire, and disposal can run
        // concurrently. Claim the gate and publish the running dump task under the same lock, so both
        // disposal paths (which take the lock, claim the gate, and capture _activityIndicatorTask)
        // always observe and await the winning dump instead of tearing down the pipes underneath it.
        CancellationTokenSource? handshakeCancellation;
        lock (_dumpLock)
        {
            if (_dumpTaken != 0)
            {
                return;
            }

            _dumpTaken = 1;
            _activityIndicatorTask = TakeDumpOfTreeAsync(cancellationToken, triggeredByDeadline);
            handshakeCancellation = _handshakeCancellationTokenSource;
        }

        // Interrupt the pipe handshake, if one is still in flight. We are about to dump and kill the test
        // host, and a host that wedged before connecting leaves that handshake waiting for a connection
        // that will never arrive; killing it does not complete our own wait, so nothing else would end it
        // before DefaultHangTimeSpanTimeout and the dump would never reach OnTestHostProcessExitedAsync
        // to be published. Cancelled outside _dumpLock on purpose: the waiters' continuations can run
        // inline here, and they continue into a handshake that takes _dumpLock again on its way out.
        try
        {
            handshakeCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The handshake finished and disposed the source between the read above and this call, so
            // there is nothing left to interrupt.
        }
    }

    private async Task TakeDumpOfTreeAsync(CancellationToken cancellationToken, bool triggeredByDeadline)
    {
        // This method is started synchronously inside the _dumpLock (see TriggerDumpOnce), which also
        // publishes the returned task into _activityIndicatorTask. Yield immediately so none of the
        // dump work runs while the lock is held: control returns to the caller, the task field is
        // observed, and the lock is released before the (potentially slow) dump proceeds. HangDump runs
        // out-of-process on a full runtime, so yielding to the thread pool here is safe.
        await Task.Yield();

        lock (_dumpLock)
        {
            if (_hostExited)
            {
                return;
            }
        }

        ITestHostProcessInformation testHostProcessInformation =
            _testHostProcessInformation ?? throw ApplicationStateGuard.Unreachable();

        string dumpReason = triggeredByDeadline
            ? $"CI deadline approaching (dump scheduled at {_deadlineDumpAt:o})"
            : $"Hang dump timeout({_activityTimerValue}) expired";

        // Announcing the dump is diagnostics only, and it runs before the try/finally that kills the
        // process tree. Loggers and output devices propagate exceptions, so letting one escape here would
        // fault the dump task and leave the wedged host alive with no dump at all -- the exact situation
        // this handler exists to resolve. Report the failure and take the dump anyway.
        await RunBestEffortDiagnosticAsync(
            () => _logger.LogInformationAsync($"{dumpReason}. Taking hang dump."),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
        await RunBestEffortDiagnosticAsync(
            () => _outputDisplay.DisplayAsync(
                new ErrorMessageOutputDeviceData(triggeredByDeadline
                    ? ExtensionResources.HangDumpDeadlineApproaching
                    : string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)),
                cancellationToken),
            BestEffortDiagnosticsTimeout).ConfigureAwait(false);

        using IProcess? process = TryGetProcessById(_processHandler, testHostProcessInformation.PID);
        if (process is null)
        {
            await RunBestEffortDiagnosticAsync(
                () => _logger.LogDebugAsync("The test host exited before the hang dump could start."),
                BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            return;
        }

        // Walking the tree writes diagnostics through the same logger and output device, so deadline-driven
        // enumeration gets a short bound. Fall back to the root test host process: dumping and killing at least
        // that one is what unblocks the run.
        TimeSpan processTreeTimeout = triggeredByDeadline
            ? BestEffortDiagnosticsTimeout
            : TimeoutHelper.DefaultHangTimeSpanTimeout;
        List<ProcessTreeNode> processTree = await GetProcessTreeWithTimeoutAsync(
            token => process.GetProcessTreeAsync(_logger, _outputDisplay, token),
            processTreeTimeout,
            ex => _logger.LogErrorAsync("Could not enumerate the test host process tree. Falling back to the root test host process.", ex),
            process,
            cancellationToken).ConfigureAwait(false);
        processTree = processTree.Where(p => p.Process?.Name is not null and not "conhost" and not "WerFault").ToList();

        IEnumerable<IProcess> bottomUpTree = processTree.OrderByDescending(t => t.Level).Select(t => t.Process).OfType<IProcess>();

        try
        {
            if (processTree.Count > 1)
            {
                string processTreeDisplay = string.Join(
                    Environment.NewLine,
                    processTree
                        .OrderBy(t => t.Level)
                        .Select(p => $"{(p.Level != 0 ? " + " : " > ")}{new string('-', p.Level)} {p.Process!.Id} - {p.Process.Name}"));
                await RunBestEffortDiagnosticAsync(
                    () => _outputDisplay.DisplayAsync(
                        new ErrorMessageOutputDeviceData($"{ExtensionResources.DumpingProcessTree}{Environment.NewLine}{processTreeDisplay}"),
                        cancellationToken),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }
            else
            {
                await RunBestEffortDiagnosticAsync(
                    () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.DumpingProcess, process.Id, process.Name)), cancellationToken),
                    BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            }

            await RunBestEffortDiagnosticAsync(
                () => _logger.LogInformationAsync($"{dumpReason}."),
                BestEffortDiagnosticsTimeout).ConfigureAwait(false);

            await QueryOnceAndDumpTreeAsync(
                bottomUpTree,
                _task,
                GetInProgressTestsAsync,
                async (p, inProgressTests, ct) =>
                {
                    try
                    {
                        await TakeDumpAsync(p, inProgressTests, ct).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        await RunBestEffortDiagnosticAsync(
                            () => _logger.LogErrorAsync($"Error while taking dump of process {p.Id} - {p.Name}", e),
                            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                        await RunBestEffortDiagnosticAsync(
                            () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, p.Id, p.Name, e)), ct),
                            BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            NotifyCrashDumpServiceIfEnabled();

            // Some of the processes might crashed, which breaks the process tree (on windows it is just an illusion),
            // so try extra hard to kill all the known processes in the tree, since we already spent a bunch of time getting
            // to know which processes are involved.
            foreach (ProcessTreeNode node in processTree)
            {
                IProcess? p = node.Process;
                if (p == null)
                {
                    continue;
                }

                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        await p.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    await RunBestEffortDiagnosticAsync(
                        () => _logger.LogErrorAsync($"Problem killing {p.Id} - {p.Name}", e),
                        BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                    await RunBestEffortDiagnosticAsync(
                        () => _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorKillingProcess, p.Id, p.Name, e)), cancellationToken),
                        BestEffortDiagnosticsTimeout).ConfigureAwait(false);
                }
            }
        }
    }

    internal static string EnsureProcessIdPlaceholder(string pattern)
    {
        if (pattern.Contains("%p", StringComparison.Ordinal) || pattern.Contains("{pid}", StringComparison.Ordinal))
        {
            return pattern;
        }

        string? directory = Path.GetDirectoryName(pattern);
        string fileName = Path.GetFileNameWithoutExtension(pattern);
        string extension = Path.GetExtension(pattern);
        string uniqueFileName = $"{fileName}_%p{extension}";
        return directory is null or ""
            ? uniqueFileName
            : Path.Combine(directory, uniqueFileName);
    }

    internal static string GetDumpFileNamePattern(string? configuredPattern, string processName, int processId, int rootProcessId)
        => configuredPattern is null
            ? $"{processName}_%p_hang.dmp"
            : processId == rootProcessId
                ? configuredPattern
                : EnsureProcessIdPlaceholder(configuredPattern);

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");
}
