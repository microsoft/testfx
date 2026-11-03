// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

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
        if (result != 0)
        {
            throw new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        const int fileTypeMask = 0xF000;
        const int regularFile = 0x8000;
        return (status.Mode & fileTypeMask) == regularFile;
    }

#if NETCOREAPP
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Stat(string path, out FileStatus status);
#else
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
