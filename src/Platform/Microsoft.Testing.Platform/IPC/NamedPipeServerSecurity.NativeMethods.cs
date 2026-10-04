// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace Microsoft.Testing.Platform.IPC;

internal static partial class NamedPipeServerSecurity
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    private sealed class SafeLocalAllocHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        private SafeLocalAllocHandle()
        {
        }

        public IntPtr Value => handle;

        protected override bool ReleaseHandle()
            => LocalFree(handle).Equals(IntPtr.Zero);
    }

    private sealed class SafeTokenHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        private SafeTokenHandle()
        {
        }

        public IntPtr Value => handle;

        protected override bool ReleaseHandle()
            => CloseHandle(handle);
    }

    private sealed class SafeHGlobalHandle : CriticalHandleZeroOrMinusOneIsInvalid
    {
        private SafeHGlobalHandle(int byteCount)
        {
            SetHandle(Marshal.AllocHGlobal(byteCount));
        }

        public IntPtr Value => handle;

        public static SafeHGlobalHandle Allocate(int byteCount)
            => new(byteCount);

        protected override bool ReleaseHandle()
            => LocalFree(handle).Equals(IntPtr.Zero);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSecurityDescriptorRevision,
        out SafeLocalAllocHandle securityDescriptor,
        IntPtr securityDescriptorSize);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSidToStringSid(IntPtr sid, out SafeLocalAllocHandle stringSid);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSidToSidW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSidToSid(string stringSid, out SafeLocalAllocHandle sid);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetAppContainerNamedObjectPath(
        IntPtr token,
        IntPtr appContainerSid,
        uint objectPathLength,
        StringBuilder? objectPath,
        out uint returnLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out SafeTokenHandle tokenHandle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafePipeHandle CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint nOutBufferSize,
        uint nInBufferSize,
        uint nDefaultTimeOut,
        ref SecurityAttributes securityAttributes);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

#if !NETCOREAPP
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentProcessId")]
    private static extern uint GetCurrentProcessIdNative();
#endif

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
