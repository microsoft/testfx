// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics.Helpers;

/// <summary>
/// Helper functions for process info.
/// </summary>
internal static class ProcessCodeMethods
{
    private const int InvalidProcessId = -1;

    public static List<ProcessTreeNode> GetProcessTree(this IProcess process)
    {
        var childProcesses = Process.GetProcesses()
            .Where(p => IsChildCandidate(p, process))
            .ToList();

        var acc = new List<ProcessTreeNode>();
        foreach (Process c in childProcesses)
        {
            try
            {
                int parentId = GetParentPid(c);

                // c.ParentId = parentId;
                acc.Add(new ProcessTreeNode { ParentId = parentId, Process = new SystemProcess(c) });
            }
            catch
            {
                // many things can go wrong with this
                // just ignore errors
            }
        }

        int level = 1;
        int limit = 10;
        ResolveChildren(process, acc, level, limit);

        return [new() { Process = process, Level = 0 }, .. acc.Where(a => a.Level > 0)];
    }

    /// <summary>
    /// Returns the parent id of a process or -1 if it fails.
    /// </summary>
    /// <param name="process">The process to find parent of.</param>
    /// <returns>The pid of the parent process.</returns>
    internal static int GetParentPid(Process process)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetParentPidWindows(process)
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? GetParentPidLinux(process)
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                    GetParentPidMacOs(process)
                    : throw new PlatformNotSupportedException();

    internal static int GetParentPidWindows(Process process)
    {
        try
        {
            IntPtr handle = process.Handle;
            int res = NtQueryInformationProcess(handle, 0, out var pbi, Marshal.SizeOf<ProcessBasicInformation>(), out int size);

            int p = res != 0 ? InvalidProcessId : pbi.InheritedFromUniqueProcessId.ToInt32();

            return p;
        }
        catch (Exception ex)
        {
            // EqtTrace.Verbose($"ProcessCodeMethods.GetParentPidLinux: Error getting parent of process {process.Id} - {process.ProcessName}, {ex}.");
            return InvalidProcessId;
        }
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
        try
        {
            string stat = File.ReadAllText(path);
            string[] parts = stat.Split(' ');

            return parts.Length < 5 ? InvalidProcessId : int.Parse(parts[3], CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            // EqtTrace.Verbose($"ProcessCodeMethods.GetParentPidLinux: Error getting parent of process {process.Id} - {process.ProcessName}, {ex}.");
            return InvalidProcessId;
        }
    }

    internal static int GetParentPidMacOs(Process process)
    {
        try
        {
            var output = new StringBuilder();
            var err = new StringBuilder();
            Process ps = new();
            ps.StartInfo.FileName = "ps";
            ps.StartInfo.Arguments = $"-o ppid= {process.Id}";
            ps.StartInfo.UseShellExecute = false;
            ps.StartInfo.RedirectStandardOutput = true;
            ps.OutputDataReceived += (_, e) => output.Append(e.Data);
            ps.ErrorDataReceived += (_, e) => err.Append(e.Data);
            ps.Start();
            ps.BeginOutputReadLine();
            ps.WaitForExit(5_000);

            string o = output.ToString();
            int parent = int.TryParse(o.Trim(), out int ppid) ? ppid : InvalidProcessId;

            if (err.ToString() is string error && !RoslynString.IsNullOrWhiteSpace(error))
            {
                // EqtTrace.Verbose($"ProcessCodeMethods.GetParentPidMacOs: Error getting parent of process {process.Id} - {process.ProcessName}, {error}.");
            }

            return parent;
        }
        catch (Exception ex)
        {
            // EqtTrace.Verbose($"ProcessCodeMethods.GetParentPidMacOs: Error getting parent of process {process.Id} - {process.ProcessName}, {ex}.");
            return InvalidProcessId;
        }
    }

    private static void ResolveChildren(IProcess parent, List<ProcessTreeNode> acc, int level, int limit)
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
            ResolveChildren(child.Process!, acc, level + 1, limit);
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

internal class ProcessTreeNode
{
    public IProcess? Process { get; set; }

    public int Level { get; set; }

    public int ParentId { get; set; }

    public Process? ParentProcess { get; set; }
}
