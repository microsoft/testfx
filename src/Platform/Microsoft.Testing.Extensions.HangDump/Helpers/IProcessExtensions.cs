// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Extensions.Diagnostics.Helpers;

/// <summary>
/// Helper functions for IProcess.
/// </summary>
internal static class IProcessExtensions
{
    private const int InvalidProcessId = -1;

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public static async Task<List<ProcessTreeNode>> GetProcessTreeAsync(this IProcess process, ILogger logger, OutputDeviceWriter outputDisplay, CancellationToken cancellationToken)
    {
        var childProcesses = Process.GetProcesses()
            .Where(p => IsChildCandidate(p, process))
            .ToList();

        var acc = new List<ProcessTreeNode>();
        foreach (Process c in childProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                int parentId = await GetParentPidAsync(c, logger, outputDisplay, cancellationToken).ConfigureAwait(false);

                acc.Add(new ProcessTreeNode { ParentId = parentId, Process = new SystemProcess(c) });
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to get parent for process {c.Id} - {c.ProcessName}", e);
                await outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorGettingParentOfProcess, c.Id, c.ProcessName, e)), cancellationToken).ConfigureAwait(false);
            }
        }

        int level = 1;
        int limit = 10;
        ResolveChildren(process, logger, acc, level, limit);

        return [new() { Process = process, Level = 0 }, .. acc.Where(a => a.Level > 0)];
    }

    /// <summary>
    /// Returns the parent id of a process or -1 if it fails.
    /// </summary>
    /// <param name="process">The process to find parent of.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="outputDisplay">The output display.git.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pid of the parent process.</returns>
    internal static async Task<int> GetParentPidAsync(Process process, ILogger logger, OutputDeviceWriter outputDisplay, CancellationToken cancellationToken)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetParentPidWindows(process)
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? GetParentPidLinux(process)
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    await GetParentPidMacOsAsync(process, logger, outputDisplay, cancellationToken).ConfigureAwait(false)
                    : throw new PlatformNotSupportedException();

    [UnsupportedOSPlatform("browser")]
    internal static int GetParentPidWindows(Process process)
    {
        IntPtr handle = process.Handle;
        int res = NtQueryInformationProcess(handle, 0, out ProcessBasicInformation pbi, Marshal.SizeOf<ProcessBasicInformation>(), out int _);

        int p = res != 0 ? InvalidProcessId : pbi.InheritedFromUniqueProcessId.ToInt32();

        return p;
    }

    /// <summary>Read the /proc file system for information about the parent.</summary>
    /// <param name="process">The process to get the parent process from.</param>
    /// <returns>The process id.</returns>
    [UnsupportedOSPlatform("browser")]
    internal static int GetParentPidLinux(Process process)
        => ParseParentPidFromProcStat(File.ReadAllText($"/proc/{process.Id}/stat"));

    /// <summary>
    /// Parses the parent PID out of a line read from <c>/proc/&lt;pid&gt;/stat</c>.
    /// </summary>
    /// <remarks>
    /// Per <c>proc(5)</c>, the line has the format <c>pid (comm) state ppid pgrp ...</c>.
    /// The <c>comm</c> field is allowed to contain spaces and parentheses (the kernel only
    /// truncates it to 16 chars and does not escape its content), so the line cannot be split
    /// on <c>' '</c>. The kernel does guarantee that <c>)</c> terminates <c>comm</c>, so the
    /// last <c>)</c> in the line marks its end. After that, fields are space-separated:
    /// <c>[0] = state</c>, <c>[1] = ppid</c>, <c>[2] = pgrp</c>, ...
    /// </remarks>
    /// <param name="stat">The full contents of <c>/proc/&lt;pid&gt;/stat</c>.</param>
    /// <returns>The parent process id, or <see cref="InvalidProcessId"/> if the input is malformed.</returns>
    internal static int ParseParentPidFromProcStat(string stat)
    {
        int commEnd = stat.LastIndexOf(')');
        if (commEnd < 0 || commEnd + 2 >= stat.Length)
        {
            return InvalidProcessId;
        }

        string[] afterComm = stat.Substring(commEnd + 2).Split(' ');
        return afterComm.Length >= 2
            && int.TryParse(afterComm[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ppid)
            ? ppid
            : InvalidProcessId;
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    internal static async Task<int> GetParentPidMacOsAsync(Process process, ILogger logger, OutputDeviceWriter outputDisplay, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var err = new StringBuilder();
        Process ps = new();
        ps.StartInfo.FileName = "ps";
        ps.StartInfo.Arguments = $"-o ppid= {process.Id}";
        ps.StartInfo.UseShellExecute = false;
        ps.StartInfo.RedirectStandardOutput = true;
        ps.StartInfo.RedirectStandardError = true;
        ps.OutputDataReceived += (_, e) => output.Append(e.Data);
        ps.ErrorDataReceived += (_, e) => err.Append(e.Data);
        ps.Start();
        ps.BeginOutputReadLine();
        ps.BeginErrorReadLine();

        int timeout = 5_000;
        // This will read the output streams till the end.
        using var cts = new CancellationTokenSource(timeout);
        await ps.WaitForExitAsync(cts.Token).ConfigureAwait(false);

        string o = output.ToString();
        string e = err.ToString();

        int parent = int.TryParse(o.Trim(), out int ppid) ? ppid : InvalidProcessId;

        if (!RoslynString.IsNullOrWhiteSpace(e))
        {
            logger.LogError($"Error getting parent of process {process.Id} - {process.ProcessName}, {e}.");
            await outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.ErrorGettingParentOfProcess, process.Id, process.ProcessName, e)), cancellationToken).ConfigureAwait(false);
        }

        return parent;
    }

    private static void ResolveChildren(IProcess parent, ILogger logger, List<ProcessTreeNode> acc, int level, int limit)
    {
        if (limit < 0)
        {
            // hit recursion limit, just returning
            return;
        }

        // only take children that are newer than the parent, because process ids (PIDs) get recycled
        var children = acc.Where(p => p.ParentId == parent.Id && p.Process?.StartTime > parent.StartTime).ToList();

        foreach (ProcessTreeNode child in children)
        {
            child.Level = level;
            ResolveChildren(child.Process!, logger, acc, level + 1, limit);
        }
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    private static bool IsChildCandidate(Process child, IProcess parent)
    {
        // this is extremely slow under debugger, but fast without it
        try
        {
            return child.StartTime > parent.StartTime && child.Id != parent.Id;
        }
        catch
        {
            /* access denied or process has exits most likely */
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public readonly IntPtr ExitStatus;
        public readonly IntPtr PebBaseAddress;
        public readonly IntPtr AffinityMask;
        public readonly IntPtr BasePriority;
        public readonly IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);
}
