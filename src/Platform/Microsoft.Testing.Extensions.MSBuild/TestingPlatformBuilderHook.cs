// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

using Microsoft.Testing.Extensions.MSBuild;
using Microsoft.Testing.Platform.Builder;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add MSBuild support.
/// </summary>
public static class TestingPlatformBuilderHook
{
    private const string TestHostControllerPidOption = "--internal-testhostcontroller-pid";

    /// <summary>
    /// Adds MSBuild support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
    {
        // The MSBuild extension provides the `dotnet test` / MSBuild-node integration, which relies on
        // named-pipe IPC that is unavailable on browser-wasm. This hook is auto-registered for every
        // MSTest runner app (via the generated SelfRegisteredExtensions, because MSTest.TestAdapter
        // references Microsoft.Testing.Platform.MSBuild), so skip it on browser instead of throwing —
        // otherwise every browser-wasm test app crashes at startup. See
        // https://github.com/microsoft/testfx/issues/2196.
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        // Only sandboxed children must avoid the outer build pipe. Ordinary controller children
        // still own the MSBuild lifecycle callbacks and must send module information and results.
        if (_.Contains(TestHostControllerPidOption, StringComparer.Ordinal) && IsAppContainer())
        {
            testApplicationBuilder.CommandLine.AddProvider(() => new MSBuildCommandLineProvider());
            return;
        }

        testApplicationBuilder.AddMSBuild();
    }

    private static bool IsAppContainer()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Environment.OSVersion.Version < new Version(6, 2))
        {
            return false;
        }

        const int TokenIsAppContainer = 29;
        // GetCurrentProcessToken's borrowed pseudo-handle has TOKEN_QUERY access and needs no cleanup.
        IntPtr currentProcessToken = new(-4);
        return GetTokenInformation(currentProcessToken, TokenIsAppContainer, out int isAppContainer, sizeof(int), out _)
            ? isAppContainer != 0
            : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("advapi32.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        out int tokenInformation,
        int tokenInformationLength,
        out int returnLength);
}
