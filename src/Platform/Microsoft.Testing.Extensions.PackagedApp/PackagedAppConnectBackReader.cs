// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// The activated-host side of the connect-back hand-off. When the current process is a packaged (MSIX)
/// test host that was activated by Application User Model ID — and therefore did not inherit the
/// controller-to-host connect-back environment variables — this reads them back from the package's
/// LocalState (where <see cref="PackagedAppTestHostLauncher"/> wrote them) and applies them to the
/// process environment, so the platform's normal environment-variable-based connect-back then works
/// unchanged.
/// </summary>
internal static class PackagedAppConnectBackReader
{
    /// <summary>
    /// Applies the handed-off connect-back environment variables when the current process is an
    /// activated packaged test host. It is a no-op for the controller, for non-packaged layouts, and
    /// when there is no handshake file to consume. Must run before the platform reads the connect-back
    /// environment (that is, before the test application is built).
    /// </summary>
    /// <param name="commandLineArguments">The current process command line.</param>
    public static void TryApplyConnectBackEnvironment(IReadOnlyList<string> commandLineArguments)
    {
        // Only the spawned test host leg carries the test host controller PID option; the controller
        // process does not, so it never consumes a handshake.
        string? testHostControllerPid = PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(commandLineArguments);
        if (testHostControllerPid is null)
        {
            return;
        }

        // We only act when running from a packaged (MSIX) layout, detected by an AppxManifest.xml at or
        // above the app's install directory (the same manifest the launcher registered).
        string? manifestPath = AppxManifestInfo.FindManifestPath(AppContext.BaseDirectory);
        if (manifestPath is null)
        {
            return;
        }

        string packageFamilyName;
        try
        {
            packageFamilyName = AppxManifestInfo.ReadFromManifest(manifestPath).PackageFamilyName;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // A malformed or unreadable manifest is not fatal here: without the package family name we
            // cannot locate the handshake, so leave the platform to surface the missing connect-back.
            return;
        }

        // Resolve the handshake path from the package's own LocalState. In a packaged process the
        // package-aware ApplicationData.Current.LocalFolder is the correct, unredirected
        // %LOCALAPPDATA%\Packages\{PFN}\LocalState the (unpackaged) launcher wrote to;
        // Environment.SpecialFolder.LocalApplicationData is redirected here and would not find the file.
        string fileName = PackagedAppConnectBackHandshake.GetHandshakeFileName(testHostControllerPid);
        string? localStateDirectory = TryGetPackageLocalStateDirectory();
        string handshakePath = localStateDirectory is not null
            ? Path.Combine(localStateDirectory, fileName)
            : PackagedAppConnectBackHandshake.GetHandshakeFilePath(packageFamilyName, testHostControllerPid);

        IReadOnlyDictionary<string, string?>? environment = PackagedAppConnectBackHandshake.ReadAndDelete(handshakePath);
        if (environment is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string?> entry in environment)
        {
            // Apply authoritatively the controller-to-host connect-back variables the launcher handed
            // off (a null value deletes the variable). These are the variables an AUMID-activated host
            // needs to reach its controller and would not otherwise inherit across the activation
            // boundary.
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
    }

    // Returns the current package's LocalState directory (the package-aware, unredirected path), or null
    // when the process has no package identity. Only the Windows build can resolve this through the WinRT
    // ApplicationData projection; other builds fall back to the family-name-derived path.
#if PACKAGEDAPP_WINRT
    private static string? TryGetPackageLocalStateDirectory()
    {
        try
        {
            return Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // No package identity (not expected for an activated packaged host).
            return null;
        }
    }
#else
    private static string? TryGetPackageLocalStateDirectory() => null;
#endif
}
