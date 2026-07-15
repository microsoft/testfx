// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if PACKAGEDAPP_WINRT

namespace Microsoft.Testing.Extensions.PackagedApp.Interop;

/// <summary>
/// Minimal, redistributable COM interop for the public
/// <c>IApplicationActivationManager::ActivateApplication</c> API (declared in <c>shobjidl_core.h</c>,
/// available since Windows 8). It activates a packaged app by Application User Model ID (AUMID) and
/// returns the created process id, which is exactly the primitive VSTest gets from a
/// Visual-Studio-internal deployment component — here obtained through a public OS API instead.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ApplicationActivationManager
{
    /// <summary>
    /// Activates the packaged app identified by <paramref name="appUserModelId"/> for the generic
    /// launch contract, delivering <paramref name="arguments"/> to it as command-line arguments (a
    /// packaged full-trust desktop app receives the activation arguments as <c>argv</c>).
    /// </summary>
    /// <param name="appUserModelId">The AUMID (<c>{PackageFamilyName}!{ApplicationId}</c>).</param>
    /// <param name="arguments">The command line to deliver to the activated app, or <see langword="null"/>.</param>
    /// <returns>The process id of the activated app instance.</returns>
    public static uint ActivateApplication(string appUserModelId, string? arguments)
    {
        var manager = (IApplicationActivationManager)new ApplicationActivationManagerClass();
        manager.ActivateApplication(appUserModelId, arguments, ActivateOptions.None, out uint processId);
        return processId;
    }

    [Flags]
    private enum ActivateOptions
    {
        None = 0x00000000,
    }

    // CLSID_ApplicationActivationManager.
    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManagerClass
    {
    }

    // IApplicationActivationManager. The methods must appear in vtable order (ActivateApplication,
    // ActivateForFile, ActivateForProtocol); the latter two are declared only to keep the layout
    // correct and take their IShellItemArray argument as an opaque pointer since they are never called.
    [ComImport]
    [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        void ActivateApplication(
            [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [In, MarshalAs(UnmanagedType.LPWStr)] string? arguments,
            [In] ActivateOptions options,
            [Out] out uint processId);

        void ActivateForFile(
            [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [In] IntPtr itemArray,
            [In, MarshalAs(UnmanagedType.LPWStr)] string verb,
            [Out] out uint processId);

        void ActivateForProtocol(
            [In, MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [In] IntPtr itemArray,
            [Out] out uint processId);
    }
}

#endif
