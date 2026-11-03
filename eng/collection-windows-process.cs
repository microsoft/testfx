// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

global using System.Text;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Win32.SafeHandles;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace TestFx.Collection;

public sealed class WindowsCollectionProcess : IDisposable
{
    private readonly SafeFileHandle _process;
    private readonly SafeFileHandle _job;

    private WindowsCollectionProcess(int id, SafeFileHandle process, SafeFileHandle job, StreamReader output, StreamReader error)
    {
        Id = id;
        _process = process;
        _job = job;
        StandardOutput = output;
        StandardError = error;
    }

    public int Id { get; }

    public StreamReader StandardOutput { get; }

    public StreamReader StandardError { get; }

    public bool HasExited => WaitForExit(0);

    public int ExitCode
    {
        get
        {
            if (!HasExited)
            {
                throw new InvalidOperationException("The collection process has not exited.");
            }

            Check(GetExitCodeProcess(_process, out uint code), nameof(GetExitCodeProcess));

            return unchecked((int)code);
        }
    }

    public static WindowsCollectionProcess Start(ProcessStartInfo startInfo)
    {
        if (startInfo.UseShellExecute || !startInfo.RedirectStandardOutput || !startInfo.RedirectStandardError)
        {
            throw new ArgumentException("Collection requires direct process creation with redirected output.", nameof(startInfo));
        }

        bool x64 = IntPtr.Size == 8;
        if (Marshal.SizeOf<StartupInfoEx>() != (x64 ? 112 : 72) ||
            Marshal.SizeOf<JobLimits>() != (x64 ? 144 : 112) ||
            Marshal.SizeOf<JobAccounting>() != 48)
        {
            throw new InvalidOperationException("The Windows collection interop layout is unsupported.");
        }

        SafeFileHandle job = CreateJobObjectW(IntPtr.Zero, null);
        if (job.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObjectW failed.");
        }

        SafeFileHandle? outputRead = null;
        SafeFileHandle? outputWrite = null;
        SafeFileHandle? errorRead = null;
        SafeFileHandle? errorWrite = null;
        SafeFileHandle? process = null;
        StreamReader? output = null;
        StreamReader? error = null;
        IntPtr attributes = IntPtr.Zero;
        IntPtr handles = IntPtr.Zero;
        IntPtr jobValue = IntPtr.Zero;
        IntPtr environment = IntPtr.Zero;
        bool attributesInitialized = false;
        bool transferred = false;
        try
        {
            JobLimits limits = new() { Basic = new() { LimitFlags = 0x2000 } };
            Check(SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf<JobLimits>()), nameof(SetInformationJobObject));

            SecurityAttributes security = new() { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = 1 };
            using SafeFileHandle input = CreateFileW("NUL", 0x80000000, 3, ref security, 3, 0x80, IntPtr.Zero);
            if (input.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Opening collection input failed.");
            }

            Check(CreatePipe(out outputRead, out outputWrite, ref security, 0), nameof(CreatePipe));
            Check(CreatePipe(out errorRead, out errorWrite, ref security, 0), nameof(CreatePipe));
            Check(SetHandleInformation(outputRead, 1, 0), nameof(SetHandleInformation));
            Check(SetHandleInformation(errorRead, 1, 0), nameof(SetHandleInformation));

            UIntPtr size = UIntPtr.Zero;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
            if (Marshal.GetLastWin32Error() != 122)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Sizing collection process attributes failed.");
            }

            attributes = Marshal.AllocHGlobal(checked((int)size.ToUInt64()));
            Check(InitializeProcThreadAttributeList(attributes, 2, 0, ref size), nameof(InitializeProcThreadAttributeList));
            attributesInitialized = true;
            handles = Marshal.AllocHGlobal(3 * IntPtr.Size);
            Marshal.WriteIntPtr(handles, 0, input.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, IntPtr.Size, outputWrite.DangerousGetHandle());
            Marshal.WriteIntPtr(handles, 2 * IntPtr.Size, errorWrite.DangerousGetHandle());
            Check(UpdateProcThreadAttribute(attributes, 0, new UIntPtr(0x20002), handles, new UIntPtr((uint)(3 * IntPtr.Size)), IntPtr.Zero, IntPtr.Zero), nameof(UpdateProcThreadAttribute));
            jobValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(jobValue, job.DangerousGetHandle());
            // Job membership is established by CreateProcess, before any child can be spawned.
            Check(UpdateProcThreadAttribute(attributes, 0, new UIntPtr(0x2000D), jobValue, new UIntPtr((uint)IntPtr.Size), IntPtr.Zero, IntPtr.Zero), nameof(UpdateProcThreadAttribute));

            StringBuilder command = new();
            command.Append('"').Append(startInfo.FileName).Append('"');
            foreach (string argument in startInfo.ArgumentList)
            {
                PasteArguments.AppendArgument(command, argument);
            }

            environment = Marshal.StringToHGlobalUni(string.Join("\0", startInfo.Environment
                .Where(entry => entry.Value is not null)
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Key + "=" + entry.Value)) + "\0\0");
            StartupInfoEx startup = new()
            {
                Info = new()
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = 0x100,
                    Input = input.DangerousGetHandle(),
                    Output = outputWrite.DangerousGetHandle(),
                    Error = errorWrite.DangerousGetHandle(),
                },
                Attributes = attributes,
            };
            Check(CreateProcessW(startInfo.FileName, (command.ToString() + "\0").ToCharArray(),
                IntPtr.Zero, IntPtr.Zero, 1, 0x08080400, environment, startInfo.WorkingDirectory,
                ref startup, out ProcessInformation info), nameof(CreateProcessW));
            process = new SafeFileHandle(info.Process, true);
            using SafeFileHandle thread = new(info.Thread, true);
            output = new(new FileStream(outputRead, FileAccess.Read), startInfo.StandardOutputEncoding ?? Console.OutputEncoding);
            error = new(new FileStream(errorRead, FileAccess.Read), startInfo.StandardErrorEncoding ?? Console.OutputEncoding);
            WindowsCollectionProcess result = new(checked((int)info.Id), process, job, output, error);
            transferred = true;

            return result;
        }
        finally
        {
            if (attributesInitialized)
            {
                DeleteProcThreadAttributeList(attributes);
            }
            Marshal.FreeHGlobal(attributes);
            Marshal.FreeHGlobal(handles);
            Marshal.FreeHGlobal(jobValue);
            Marshal.FreeHGlobal(environment);
            outputWrite?.Dispose();
            errorWrite?.Dispose();
            if (!transferred)
            {
                job.Dispose();
                output?.Dispose();
                error?.Dispose();
                outputRead?.Dispose();
                errorRead?.Dispose();
                process?.Dispose();
            }
        }
    }

    public bool WaitForExit(int milliseconds)
    {
        if (milliseconds < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        uint result = WaitForSingleObject(_process, unchecked((uint)milliseconds));
        if (result != 0 && result != 258)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Waiting for the collection process failed.");
        }

        return result == 0;
    }

    public void WaitForExit() => WaitForExit(-1);

    public void StopTree(int milliseconds)
    {
        Stopwatch timer = Stopwatch.StartNew();
        Check(QueryInformationJobObject(_job, 1, out JobAccounting accounting, (uint)Marshal.SizeOf<JobAccounting>(), IntPtr.Zero), nameof(QueryInformationJobObject));
        uint observedProcesses = accounting.Processes;
        List<SafeFileHandle> members = CaptureMembers(accounting.ActiveProcesses);
        try
        {
            Check(TerminateJobObject(_job, uint.MaxValue), nameof(TerminateJobObject));
            foreach (SafeFileHandle member in members)
            {
                uint remaining = (uint)Math.Max(0, milliseconds - timer.ElapsedMilliseconds);
                uint result = WaitForSingleObject(member, remaining);
                if (result == 258)
                {
                    throw new TimeoutException("An owned collection process did not finish terminating.");
                }
                if (result != 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Waiting for an owned collection process failed.");
                }
            }

            Check(QueryInformationJobObject(_job, 1, out accounting, (uint)Marshal.SizeOf<JobAccounting>(), IntPtr.Zero), nameof(QueryInformationJobObject));
            if (accounting.Processes != observedProcesses || accounting.ActiveProcesses != 0 ||
                !WaitForExit((int)Math.Max(0, milliseconds - timer.ElapsedMilliseconds)))
            {
                throw new InvalidOperationException("Collection job termination could not confirm every owned process.");
            }
        }
        finally
        {
            foreach (SafeFileHandle member in members)
            {
                member.Dispose();
            }
        }
    }

    private List<SafeFileHandle> CaptureMembers(uint activeProcesses)
    {
        List<SafeFileHandle> members = [];
        int length = checked(8 + IntPtr.Size * (int)Math.Max(activeProcesses, 1));
        IntPtr buffer = IntPtr.Zero;
        try
        {
            while (true)
            {
                buffer = Marshal.AllocHGlobal(length);
                if (QueryJobProcessIds(_job, 3, buffer, (uint)length, IntPtr.Zero) != 0)
                {
                    break;
                }
                int error = Marshal.GetLastWin32Error();
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
                if (error != 234)
                {
                    throw new Win32Exception(error, "Reading collection job members failed.");
                }
                length = checked(length * 2);
            }

            int count = Marshal.ReadInt32(buffer, 4);
            for (int index = 0; index < count; index++)
            {
                uint id = checked((uint)Marshal.ReadIntPtr(buffer, 8 + index * IntPtr.Size).ToInt64());
                if (id == Id)
                {
                    continue;
                }
                SafeFileHandle? member = OpenProcess(0x00101000, 0, id);
                if (member.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    member.Dispose();
                    if (error == 87)
                    {
                        continue;
                    }
                    throw new Win32Exception(error, "Opening an owned collection process failed.");
                }
                try
                {
                    Check(IsProcessInJob(member, _job, out int belongs), nameof(IsProcessInJob));
                    if (belongs != 0)
                    {
                        members.Add(member);
                        member = null;
                    }
                }
                finally
                {
                    member?.Dispose();
                }
            }

            return members;
        }
        catch
        {
            foreach (SafeFileHandle member in members)
            {
                member.Dispose();
            }
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        _job.Dispose();
        try
        {
            StandardOutput.Dispose();
        }
        finally
        {
            try
            {
                StandardError.Dispose();
            }
            finally
            {
                _process.Dispose();
            }
        }
    }

    private static void Check(int result, string operation)
    {
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr Descriptor;
        public int InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XChars;
        public uint YChars;
        public uint Fill;
        public uint Flags;
        public ushort Show;
        public ushort ReservedSize;
        public IntPtr ReservedBytes;
        public IntPtr Input;
        public IntPtr Output;
        public IntPtr Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo Info;
        public IntPtr Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint Id;
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicJobLimits
    {
        public long ProcessTime;
        public long JobTime;
        public uint LimitFlags;
        public UIntPtr MinWorkingSet;
        public UIntPtr MaxWorkingSet;
        public uint ActiveLimit;
        public UIntPtr Affinity;
        public uint Priority;
        public uint Scheduling;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadCount;
        public ulong WriteCount;
        public ulong OtherCount;
        public ulong ReadBytes;
        public ulong WriteBytes;
        public ulong OtherBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobLimits
    {
        public BasicJobLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory;
        public UIntPtr JobMemory;
        public UIntPtr PeakProcessMemory;
        public UIntPtr PeakJobMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobAccounting
    {
        public long UserTime;
        public long KernelTime;
        public long PeriodUserTime;
        public long PeriodKernelTime;
        public uint Faults;
        public uint Processes;
        public uint ActiveProcesses;
        public uint TerminatedProcesses;
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateJobObjectW(IntPtr security, [MarshalAs(UnmanagedType.LPWStr)] string? name);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int SetInformationJobObject(SafeFileHandle job, int infoClass, ref JobLimits limits, uint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW([MarshalAs(UnmanagedType.LPWStr)] string name, uint access, uint share, ref SecurityAttributes security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes security, uint size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref UIntPtr size);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int UpdateProcThreadAttribute(IntPtr list, uint flags, UIntPtr attribute, IntPtr value, UIntPtr size, IntPtr previous, IntPtr returnedSize);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int CreateProcessW(string application, [In, Out] char[] command, IntPtr processSecurity, IntPtr threadSecurity, int inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation info);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetExitCodeProcess(SafeFileHandle process, out uint code);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int QueryInformationJobObject(SafeFileHandle job, int infoClass, out JobAccounting accounting, uint size, IntPtr returnedSize);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int TerminateJobObject(SafeFileHandle job, uint code);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, EntryPoint = "QueryInformationJobObject")]
    private static extern int QueryJobProcessIds(SafeFileHandle job, int infoClass, IntPtr buffer, uint size, IntPtr returnedSize);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle OpenProcess(uint access, int inherit, uint id);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int IsProcessInJob(SafeFileHandle process, SafeFileHandle job, out int belongs);
}
