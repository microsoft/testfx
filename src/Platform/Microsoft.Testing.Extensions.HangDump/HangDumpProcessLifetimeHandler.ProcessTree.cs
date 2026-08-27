// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Helpers;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

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

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    [UnsupportedOSPlatform("wasi")]
    private async Task TakeDumpOfTreeAsync(CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_testHostProcessInformation is not null);

        await _logger.LogInformationAsync($"Hang dump timeout({_activityTimerValue}) expired.").ConfigureAwait(false);
        await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTimeoutExpired, _activityTimerValue)), cancellationToken).ConfigureAwait(false);

        using IProcess process = _processHandler.GetProcessById(_testHostProcessInformation.PID);
        var processTree = (await process.GetProcessTreeAsync(_logger, _outputDisplay, cancellationToken).ConfigureAwait(false)).Where(p => p.Process?.Name is not null and not "conhost" and not "WerFault").ToList();
        IEnumerable<IProcess> bottomUpTree = processTree.OrderByDescending(t => t.Level).Select(t => t.Process).OfType<IProcess>();

        try
        {
            if (processTree.Count > 1)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(ExtensionResources.DumpingProcessTree), cancellationToken).ConfigureAwait(false);

                foreach (ProcessTreeNode? p in processTree.OrderBy(t => t.Level))
                {
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"{(p.Level != 0 ? " + " : " > ")}{new string('-', p.Level)} {p.Process!.Id} - {p.Process.Name}"), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.DumpingProcess, process.Id, process.Name)), cancellationToken).ConfigureAwait(false);
            }

            await _logger.LogInformationAsync($"Hang dump timeout({_activityTimerValue}) expired.").ConfigureAwait(false);

            // Do not suspend processes with NetClient dumper it stops the diagnostic thread running in
            // them and hang dump request will get stuck forever, because the process is not co-operating.
            // Instead we start one task per dump asynchronously, and hope that the parent process will start dumping
            // before the child process is done dumping. This way if the parent is waiting for the children to exit,
            // we will be dumping it before it observes the child exiting and we get a more accurate results. If we did not
            // do this, then parent that is awaiting child might exit before we get to dumping it.
            foreach (IProcess p in bottomUpTree)
            {
                try
                {
                    await TakeDumpAsync(p, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // exceptions.Add(new InvalidOperationException($"Error while taking dump of process {p.Name} {p.Id}", e));
                    await _logger.LogErrorAsync($"Error while taking dump of process {p.Id} - {p.Name}", e).ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorWhileDumpingProcess, p.Id, p.Name, e)), cancellationToken).ConfigureAwait(false);
                }
            }
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
                    await _logger.LogErrorAsync($"Problem killing {p.Id} - {p.Name}", e).ConfigureAwait(false);
                    await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorKillingProcess, p.Id, p.Name, e)), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static void NotifyCrashDumpServiceIfEnabled()
        => AppDomain.CurrentDomain.SetData("ProcessKilledByHangDump", "true");
}
