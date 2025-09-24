// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics.Helpers;

/// <summary>
/// Helper functions for IProcess.
/// </summary>
internal static class IProcessExtensions
{
    private const int InvalidProcessId = -1;

    public static List<ProcessTreeNode> GetProcessTree(this IProcess process, ILogger logger, OutputDeviceWriter outputDisplay)
    {
        var childProcesses = Process.GetProcesses()
            .Where(p => IsChildCandidate(p, process))
            .ToList();

        var acc = new List<ProcessTreeNode>();
        foreach (Process c in childProcesses)
        {
            try
            {
                int parentId = GetParentPid(c, logger, outputDisplay);

                // c.ParentId = parentId;
                acc.Add(new ProcessTreeNode { ParentId = parentId, Process = new SystemProcess(c) });
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to get parent for process {c.Id} - {c.ProcessName}", e);
                outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"Failed to get parent for process {c.Id} - {c.ProcessName}, {e}.")).Wait();
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
    /// <returns>The pid of the parent process.</returns>
    internal static int GetParentPid(Process process, ILogger logger, OutputDeviceWriter outputDisplay)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetParentPidWindows(process)
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? GetParentPidLinux(process)
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    GetParentPidMacOs(process, logger, outputDisplay)
                    : throw new PlatformNotSupportedException();

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
    internal static int GetParentPidLinux(Process process)
    {
        int pid = process.Id;

        // read /proc/<pid>/stat
        // 4th column will contain the ppid, 92 in the example below
        // ex: 93 (bash) S 92 93 2 4294967295 ...
        string path = $"/proc/{pid}/stat";
        string stat = File.ReadAllText(path);
        string[] parts = stat.Split(' ');

        return parts.Length < 5 ? InvalidProcessId : int.Parse(parts[3], CultureInfo.CurrentCulture);
    }

    internal static int GetParentPidMacOs(Process process, ILogger logger, OutputDeviceWriter outputDisplay)
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
        ps.WaitForExit(5_000);

        string o = output.ToString();
        string e = err.ToString();
        outputDisplay.DisplayAsync(new WarningMessageOutputDeviceData($"parent of {process.Id} - {process.ProcessName}  ps output: {o}")).Wait();
        outputDisplay.DisplayAsync(new WarningMessageOutputDeviceData($"ps err: {e}")).Wait();
        int parent = int.TryParse(o.Trim(), out int ppid) ? ppid : InvalidProcessId;

        if (err.ToString() is string error && !RoslynString.IsNullOrWhiteSpace(error))
        {
            logger.LogError($"Error getting parent of process {process.Id} - {process.ProcessName}, {error}.");
            outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData($"Error getting parent of process {process.Id} - {process.ProcessName}, {error}.")).Wait();
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
