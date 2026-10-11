// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace Microsoft.Testing.Platform.Browser;

internal abstract partial class WorkerProcessContainment : IDisposable
{
    public static WorkerProcessContainment Create()
        => OperatingSystem.IsWindows()
            ? new WindowsJobProcessContainment()
            : new UnixProcessGroupContainment();

    public static void InitializeWorkerProcessGroup()
    {
        if (!OperatingSystem.IsWindows()
            && Interop.SetProcessGroup(processId: 0, processGroupId: 0) != 0)
        {
            throw new BrowserLauncherException(
                $"Unable to create the browser worker process group (errno {Marshal.GetLastWin32Error()}).");
        }
    }

    public static void KillCurrentProcessGroup()
    {
        if (!OperatingSystem.IsWindows())
        {
            _ = Interop.Kill(-Environment.ProcessId, Interop.SigKill);
        }
    }

    public abstract void Attach(Process worker);

    public abstract Task TerminateAsync(Process worker, TimeSpan timeout);

    public abstract void Dispose();

    private sealed class WindowsJobProcessContainment : WorkerProcessContainment
    {
        private readonly SafeJobHandle _job;

        public WindowsJobProcessContainment()
        {
            _job = new SafeJobHandle(Interop.CreateJobObject(IntPtr.Zero, null));
            if (_job.IsInvalid)
            {
                throw new BrowserLauncherException(
                    $"Unable to create the browser worker job object (error {Marshal.GetLastWin32Error()}).");
            }

            var information = new Interop.JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new Interop.JobObjectBasicLimitInformation
                {
                    LimitFlags = Interop.JobObjectLimitKillOnJobClose,
                },
            };
            int size = Marshal.SizeOf<Interop.JobObjectExtendedLimitInformation>();
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
                if (!Interop.SetInformationJobObject(
                    _job,
                    Interop.JobObjectExtendedLimitInformationClass,
                    buffer,
                    (uint)size))
                {
                    throw new BrowserLauncherException(
                        $"Unable to configure the browser worker job object (error {Marshal.GetLastWin32Error()}).");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public override void Attach(Process worker)
        {
            if (!Interop.AssignProcessToJobObject(_job, worker.SafeHandle))
            {
                throw new BrowserLauncherException(
                    $"Unable to assign the browser worker to its job object (error {Marshal.GetLastWin32Error()}).");
            }
        }

        public override async Task TerminateAsync(Process worker, TimeSpan timeout)
        {
            if (!Interop.TerminateJobObject(_job, 1))
            {
                throw new BrowserLauncherException(
                    $"Unable to terminate the browser worker job object (error {Marshal.GetLastWin32Error()}).");
            }

            await worker.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
        }

        public override void Dispose()
            => _job.Dispose();
    }

    private sealed class UnixProcessGroupContainment : WorkerProcessContainment
    {
        private int _processGroupId;

        public override void Attach(Process worker)
        {
            int processGroupId = Interop.GetProcessGroup(worker.Id);
            if (processGroupId != worker.Id)
            {
                throw new BrowserLauncherException(
                    $"The browser worker did not establish its owned process group (expected {worker.Id}, received {processGroupId}, errno {Marshal.GetLastWin32Error()}).");
            }

            _processGroupId = processGroupId;
        }

        public override async Task TerminateAsync(Process worker, TimeSpan timeout)
        {
            Signal(Interop.SigTerm);
            try
            {
                await worker.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Signal(Interop.SigKill);
                await worker.WaitForExitAsync().WaitAsync(timeout).ConfigureAwait(false);
            }
        }

        public override void Dispose()
        {
            try
            {
                Signal(Interop.SigKill);
            }
            catch (BrowserLauncherException)
            {
            }
        }

        private void Signal(int signal)
        {
            if (_processGroupId != 0
                && Interop.Kill(-_processGroupId, signal) != 0
                && Marshal.GetLastWin32Error() != Interop.NoSuchProcess)
            {
                throw new BrowserLauncherException(
                    $"Unable to signal browser worker process group {_processGroupId} (signal {signal}, errno {Marshal.GetLastWin32Error()}).");
            }
        }
    }

    private static partial class Interop
    {
        public const uint JobObjectLimitKillOnJobClose = 0x00002000;
        public const int JobObjectExtendedLimitInformationClass = 9;
        public const int SigTerm = 15;
        public const int SigKill = 9;
        public const int NoSuchProcess = 3;

        [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetInformationJobObject(
            SafeJobHandle job,
            int informationClass,
            IntPtr information,
            uint informationLength);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AssignProcessToJobObject(
            SafeJobHandle job,
            SafeProcessHandle process);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool TerminateJobObject(SafeJobHandle job, uint exitCode);

        [LibraryImport("libc", EntryPoint = "setpgid", SetLastError = true)]
        public static partial int SetProcessGroup(int processId, int processGroupId);

        [LibraryImport("libc", EntryPoint = "getpgid", SetLastError = true)]
        public static partial int GetProcessGroup(int processId);

        [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
        public static partial int Kill(int processId, int signal);

        [StructLayout(LayoutKind.Sequential)]
        public struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }

    private sealed partial class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobHandle(IntPtr handle)
            : base(ownsHandle: true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
            => CloseHandle(handle);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(IntPtr handle);
    }
}
