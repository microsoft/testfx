// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1121 // Keep IntPtr for older target frameworks sharing this file.

using System.ComponentModel;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed partial class SystemFileSystem
{
    private static bool IsRegularUnixFile(string path)
    {
        // FileAttributes and File.Exists also classify Unix FIFOs/devices as files.
        // Use the runtime's normalized stat ABI instead of architecture-specific libc stat layouts.
#if NETCOREAPP
        int result = Stat(path, out FileStatus status);
#else
        int result = Stat(Encoding.UTF8.GetBytes(path + '\0'), out FileStatus status);
#endif
        return result == 0
            ? IsRegularFileMode(status.Mode)
            : throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    private static IFileStream? OpenReadRegularUnixFile(string path)
    {
        int flags = GetNonBlockingReadFlags();
#if NETCOREAPP
        int descriptor = Open(path, flags);
#else
        int descriptor = Open(Encoding.UTF8.GetBytes(path + '\0'), flags);
#endif
        if (descriptor < 0)
        {
            throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        bool transferred = false;
        try
        {
            if (FStat(handle, out FileStatus status) != 0)
            {
                throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }

            if (!IsRegularFileMode(status.Mode))
            {
                return null;
            }

            var result = new SystemFileStream(new FileStream(handle, FileAccess.Read), path);
            transferred = true;
            return result;
        }
        finally
        {
            if (!transferred)
            {
                handle.Dispose();
            }
        }
    }

    private static bool IsRegularFileMode(int mode)
    {
        const int fileTypeMask = 0xF000;
        const int regularFile = 0x8000;
        return (mode & fileTypeMask) == regularFile;
    }

    // O_RDONLY is zero; acquire O_NONBLOCK, O_CLOEXEC and O_NOCTTY atomically.
    private static int GetNonBlockingReadFlags()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? 0x800 | 0x80000 | 0x100
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? 0x4 | 0x1000000 | 0x20000
                : RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))
                    ? 0x4 | 0x100000 | 0x8000
                    : throw new PlatformNotSupportedException();

#if NETCOREAPP
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Open(string path, int flags);

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_FStat", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int FStat(SafeFileHandle handle, out FileStatus status);

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Stat(string path, out FileStatus status);
#else
    [DllImport("libc", EntryPoint = "open", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Open([In] byte[] path, int flags);

    [DllImport("libSystem.Native", EntryPoint = "SystemNative_FStat", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int FStat(SafeFileHandle handle, out FileStatus status);

    [DllImport("libSystem.Native", EntryPoint = "SystemNative_Stat", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int Stat([In] byte[] path, out FileStatus status);
#endif

    // Matches FileStatus in dotnet/runtime's System.Native/pal_io.h (fixed-width fields).
    [StructLayout(LayoutKind.Sequential)]
    private struct FileStatus
    {
        internal int Flags;
        internal int Mode;
        internal uint Uid;
        internal uint Gid;
        internal long Size;
        internal long ATime;
        internal long ATimeNsec;
        internal long MTime;
        internal long MTimeNsec;
        internal long CTime;
        internal long CTimeNsec;
        internal long BirthTime;
        internal long BirthTimeNsec;
        internal long Dev;
        internal long RDev;
        internal long Ino;
        internal uint UserFlags;
    }
}
