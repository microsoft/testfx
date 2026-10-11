// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    private readonly record struct ResourceBaseline(
        TimeSpan TotalProcessorTime,
        long? IoReadBytes,
        long? IoWriteBytes);

    private readonly struct ResourceSnapshot
    {
        private ResourceSnapshot(
            int processId,
            string? processName,
            TimeSpan totalProcessorTime,
            long workingSetBytes,
            long privateMemoryBytes,
            long managedHeapBytes,
            long? memoryLoadBytes,
            long? totalAvailableMemoryBytes,
            long? ioReadBytes,
            long? ioWriteBytes)
        {
            ProcessId = processId;
            ProcessName = processName;
            TotalProcessorTime = totalProcessorTime;
            WorkingSetBytes = workingSetBytes;
            PrivateMemoryBytes = privateMemoryBytes;
            ManagedHeapBytes = managedHeapBytes;
            GcMemoryLoadBytes = memoryLoadBytes;
            GcTotalAvailableMemoryBytes = totalAvailableMemoryBytes;
            IoReadBytes = ioReadBytes;
            IoWriteBytes = ioWriteBytes;
        }

        public int ProcessId { get; }

        public string? ProcessName { get; }

        public TimeSpan TotalProcessorTime { get; }

        public long WorkingSetBytes { get; }

        public long PrivateMemoryBytes { get; }

        public long ManagedHeapBytes { get; }

        public long? GcMemoryLoadBytes { get; }

        public long? GcTotalAvailableMemoryBytes { get; }

        public long? IoReadBytes { get; }

        public long? IoWriteBytes { get; }

        public static ResourceBaseline CaptureBaseline()
        {
            using var process = Process.GetCurrentProcess();
            (long? ioReadBytes, long? ioWriteBytes) = TryCaptureProcessIo(process);
            return new ResourceBaseline(process.TotalProcessorTime, ioReadBytes, ioWriteBytes);
        }

        public static ResourceSnapshot Capture()
        {
            using var process = Process.GetCurrentProcess();
            long? memoryLoadBytes = null;
            long? totalAvailableMemoryBytes = null;
#if NETCOREAPP
            GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();
            memoryLoadBytes = gcMemoryInfo.MemoryLoadBytes;
            totalAvailableMemoryBytes = gcMemoryInfo.TotalAvailableMemoryBytes;
#endif
            (long? ioReadBytes, long? ioWriteBytes) = TryCaptureProcessIo(process);

            return new ResourceSnapshot(
                process.Id,
                process.ProcessName,
                process.TotalProcessorTime,
                process.WorkingSet64,
                process.PrivateMemorySize64,
                GC.GetTotalMemory(forceFullCollection: false),
                memoryLoadBytes,
                totalAvailableMemoryBytes,
                ioReadBytes,
                ioWriteBytes);
        }

        private static (long? ReadBytes, long? WriteBytes) TryCaptureProcessIo(Process process)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    return NativeMethods.GetProcessIoCounters(process.Handle, out IoCounters counters)
                        ? (checked((long)counters.ReadTransferCount), checked((long)counters.WriteTransferCount))
                        : default;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException)
                {
                    return default;
                }
            }

            if (File.Exists("/proc/self/io"))
            {
                try
                {
                    long? readBytes = null;
                    long? writeBytes = null;
                    foreach (string line in File.ReadLines("/proc/self/io"))
                    {
                        if (line.StartsWith("rchar:", StringComparison.Ordinal))
                        {
                            readBytes = ParseCounter(line);
                        }
                        else if (line.StartsWith("wchar:", StringComparison.Ordinal))
                        {
                            writeBytes = ParseCounter(line);
                        }
                    }

                    return (readBytes, writeBytes);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return default;
                }
            }

            return default;
        }

        private static long? ParseCounter(string line)
        {
            int separatorIndex = line.IndexOf(':');
            return separatorIndex >= 0
                && long.TryParse(line.Substring(separatorIndex + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                    ? value
                    : null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessIoCounters(IntPtr processHandle, out IoCounters ioCounters);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(
            string directoryName,
            out ulong freeBytesAvailable,
            out ulong totalNumberOfBytes,
            out ulong totalNumberOfFreeBytes);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVolumePathName(string fileName, StringBuilder volumePathName, int bufferLength);
    }
}
#endif
