// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class HangDumpProcessLifetimeHandler
{
    private static string GetDiskInfo()
    {
        var builder = new StringBuilder();
        DriveInfo[] allDrives = DriveInfo.GetDrives();

        foreach (DriveInfo d in allDrives)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Drive {d.Name}");
            if (d.IsReady)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Available free space: {d.AvailableFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total free space: {d.TotalFreeSpace} bytes");
                builder.AppendLine(CultureInfo.InvariantCulture, $"  Total size: {d.TotalSize} bytes");
            }
        }

        return builder.ToString();
    }

    internal static async Task RunBestEffortDiagnosticAsync(Func<Task> diagnosticAsync, TimeSpan timeout)
    {
        try
        {
            await diagnosticAsync().TimeoutAfterAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Diagnostics must never prevent the dump or the process-tree kill.
        }
    }

    internal static async Task<List<ProcessTreeNode>> GetProcessTreeWithTimeoutAsync(
        Func<CancellationToken, Task<List<ProcessTreeNode>>> getProcessTreeAsync,
        TimeSpan timeout,
        Func<Exception, Task> logFailureAsync,
        IProcess rootProcess,
        CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(timeout);

        try
        {
            Task<List<ProcessTreeNode>> processTreeTask = getProcessTreeAsync(timeoutCancellationTokenSource.Token);
            await processTreeTask.TimeoutAfterAsync(timeout, cancellationToken).ConfigureAwait(false);
            return await processTreeTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunBestEffortDiagnosticAsync(
                () => logFailureAsync(ex),
                BestEffortDiagnosticsTimeout).ConfigureAwait(false);
            return [new ProcessTreeNode { Process = rootProcess, Level = 0 }];
        }
    }

    /// <summary>
    /// Asks <paramref name="queryInProgressTestsAsync"/> once and then dumps every process in
    /// <paramref name="bottomUpTree"/>, annotating each dump with that single answer.
    /// </summary>
    /// <remarks>
    /// The in-progress-test list describes the test host, so it is the same for every process in the tree,
    /// while the query is bounded by <see cref="InProgressTestsQueryTimeout"/>. Asking per process would
    /// multiply that bound by the size of the tree, and with a wedged consumer pipe a six-process tree would
    /// spend the entire default 30s dump margin waiting before a single dump is written.
    /// This is a separate method so that guarantee can be exercised with a fake tree and a stalled query:
    /// <see cref="TakeDumpOfTreeAsync"/> itself dumps and then kills every process it walks, so a test
    /// cannot drive it against a real process tree.
    /// </remarks>
    internal static async Task QueryOnceAndDumpTreeAsync(
        IEnumerable<IProcess> bottomUpTree,
        ITask task,
        Func<CancellationToken, Task<(string, int)[]>> queryInProgressTestsAsync,
        Func<IProcess, (string, int)[], CancellationToken, Task> dumpProcessAsync,
        CancellationToken cancellationToken)
    {
        (string, int)[] inProgressTests = await queryInProgressTestsAsync(cancellationToken).ConfigureAwait(false);

        // Do not suspend processes with NetClient dumper it stops the diagnostic thread running in
        // them and hang dump request will get stuck forever, because the process is not co-operating.
        // Instead we start one task per dump asynchronously, and hope that the parent process will start dumping
        // before the child process is done dumping. This way if the parent is waiting for the children to exit,
        // we will be dumping it before it observes the child exiting and we get a more accurate results. If we did not
        // do this, then parent that is awaiting child might exit before we get to dumping it.
        List<Task> dumpTasks = [];
        foreach (IProcess p in bottomUpTree)
        {
            dumpTasks.Add(task.Run(() => dumpProcessAsync(p, inProgressTests, cancellationToken), CancellationToken.None));
        }

        await task.WhenAll([.. dumpTasks]).ConfigureAwait(false);
    }

    internal static IProcess? TryGetProcessById(IProcessHandler processHandler, int processId)
    {
        try
        {
            return processHandler.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Asks the test host which tests are still running, so the dump can be annotated with them.
    /// </summary>
    /// <remarks>
    /// Called once per dump operation, not once per process: the answer describes the test host and the query
    /// is bounded by <see cref="InProgressTestsQueryTimeout"/>, so repeating it for every process in the tree
    /// would multiply that wait by the tree size and eat the dump margin.
    /// The consumer pipe is only usable once the test host connected back over it. A non-null client is not
    /// enough: it is created when the host sends its pipe name but only connected later, so a deadline dump
    /// firing in that window (or a host that wedged during startup) would hit an unconnected pipe. The list is
    /// therefore best-effort -- a connected-but-wedged host never replies and the app token is not cancelled
    /// mid-run, so any failure is logged and swallowed and an empty list is returned, and it can never block
    /// taking the dump and killing the tree.
    /// </remarks>
    private Task<(string, int)[]> GetInProgressTestsAsync(CancellationToken cancellationToken)
    {
        NamedPipeClient? namedPipeClient = _namedPipeClient;
        return namedPipeClient is null
            ? Task.FromResult<(string, int)[]>([])
            : QueryInProgressTestsWithTimeoutAsync(
                async queryCancellationToken =>
                {
                    GetInProgressTestsResponse tests = await namedPipeClient.RequestReplyAsync<GetInProgressTestsRequest, GetInProgressTestsResponse>(new GetInProgressTestsRequest(), queryCancellationToken).ConfigureAwait(false);
                    return tests.Tests;
                },
                InProgressTestsQueryTimeout,
                ex => _logger.LogDebugAsync($"Could not collect the in-progress tests before dumping (the consumer pipe may not be connected, or the host did not reply within {InProgressTestsQueryTimeout}). Continuing with the dump. {ex}"),
                cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="requestInProgressTestsAsync"/> under a bound of <paramref name="timeout"/>, and
    /// returns an empty list if it does not answer in time or fails -- including when reporting that failure
    /// itself fails.
    /// </summary>
    /// <remarks>
    /// The bound lives here rather than in the caller's delegate so it is the product, not the caller, that
    /// gives up on a connected-but-wedged host: the application token is not cancelled while the run is still
    /// "in progress", which is exactly when the deadline dump fires, so an unbounded request/reply would block
    /// the dump and the kill indefinitely and consume the whole dump margin.
    /// This is a separate method so that bound can be exercised with a reply that never arrives: the real
    /// request/reply goes over a named pipe to another process, which a unit test cannot stand up.
    /// </remarks>
    internal static async Task<(string, int)[]> QueryInProgressTestsWithTimeoutAsync(
        Func<CancellationToken, Task<(string, int)[]>> requestInProgressTestsAsync,
        TimeSpan timeout,
        Func<Exception, Task> logFailureAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            using var queryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            queryCts.CancelAfter(timeout);
            Task<(string, int)[]> queryTask = requestInProgressTestsAsync(queryCts.Token);
            await queryTask.TimeoutAfterAsync(timeout, cancellationToken).ConfigureAwait(false);
            return await queryTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The empty-list fallback is the whole point of this method, so it must survive a failing
            // diagnostic too. logFailureAsync is a logger call and logger providers can fail; letting that
            // throw would escape the caller, which is explicitly best-effort, and skip the dump entirely.
            await RunBestEffortDiagnosticAsync(() => logFailureAsync(ex), BestEffortDiagnosticsTimeout).ConfigureAwait(false);

            return [];
        }
    }
}
