// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK

using System.Diagnostics;
using System.Runtime.InteropServices;

using static System.String;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public static class VSInstallationUtilities
{
    /// <summary>
    /// Public assemblies directory name.
    /// </summary>
    private const string PublicAssembliesDirectoryName = "PublicAssemblies";

    /// <summary>
    /// Folder name of private assemblies.
    /// </summary>
    private const string PrivateAssembliesFolderName = "PrivateAssemblies";

    /// <summary>
    /// The manifest file name to determine if it is running in portable mode.
    /// </summary>
    private const string PortableVsTestManifestFilename = "Portable.VsTest.Manifest";

    private static bool s_vsInstallPathEvaluated;

    /// <summary>
    /// Gets the visual studio installation path on the local machine.
    /// </summary>
    /// <returns>VS install path.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to ignore failures to read the registry settings")]
    public static string? VSInstallPath
    {
        get
        {
            if (!s_vsInstallPathEvaluated)
            {
                try
                {
                    field = null;

                    // Use the Setup API to find the installation folder for currently running VS instance.
                    if (new SetupConfiguration() is ISetupConfiguration setupConfiguration)
                    {
                        ISetupInstance currentConfiguration = setupConfiguration.GetInstanceForCurrentProcess();
                        string currentInstallationPath = currentConfiguration.GetInstallationPath();
                        field = Path.Combine(currentInstallationPath, @"Common7\IDE");
                    }
                }
                catch
                {
                    // SetupConfiguration won't work if VS is not installed or VS is pre-vs2017 version.
                    // So ignore all exception from it.
                }
                finally
                {
                    s_vsInstallPathEvaluated = true;
                }
            }

            return field;
        }
    }

    /// <summary>
    /// Gets path to public assemblies.
    ///
    /// Returns null if VS is not installed on this machine.
    /// </summary>
    public static string? PathToPublicAssemblies => GetFullPath(PublicAssembliesDirectoryName);

    /// <summary>
    /// Gets path to private assemblies.
    ///
    /// Returns null if VS is not installed on this machine.
    /// </summary>
    public static string? PathToPrivateAssemblies => GetFullPath(PrivateAssembliesFolderName);

    /// <summary>
    /// Is Current process running in Portable Mode.
    /// </summary>
    /// <returns>True, if portable mode; false, otherwise.</returns>
    public static bool IsCurrentProcessRunningInPortableMode()
        => IsProcessRunningInPortableMode(Process.GetCurrentProcess().MainModule.FileName);

    /// <summary>
    /// Is the EXE specified running in Portable Mode.
    /// </summary>
    /// <param name="exeName">EXE name.</param>
    /// <returns>True, if portable mode; false, otherwise.</returns>
    public static bool IsProcessRunningInPortableMode(string? exeName)
    {
        // Get the directory of the exe
        string exeDir = Path.GetDirectoryName(exeName);
        return !IsNullOrEmpty(exeDir) && File.Exists(Path.Combine(exeDir, PortableVsTestManifestFilename));
    }

    private static string? GetFullPath(string folderName)
    {
        string? vsInstallDir = VSInstallPath;
        return IsNullOrWhiteSpace(vsInstallDir?.Trim()) ? null : Path.Combine(vsInstallDir, folderName);
    }

    /// <summary>
    /// Information about an instance of a product.
    /// </summary>
    [Guid("B41463C3-8866-43B5-BC33-2B0676F7F42E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
#pragma warning disable SA1201 // Elements must appear in the correct order
    public interface ISetupInstance
#pragma warning restore SA1201 // Elements must appear in the correct order
    {
        /// <summary>
        /// Gets the instance identifier (should match the name of the parent instance directory).
        /// </summary>
        /// <returns>The instance identifier.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstanceId();

        /// <summary>
        /// Gets the local date and time when the installation was originally installed.
        /// </summary>
        /// <returns>The local date and time when the installation was originally installed.</returns>
        [return: MarshalAs(UnmanagedType.Struct)]
        System.Runtime.InteropServices.ComTypes.FILETIME GetInstallDate();

        /// <summary>
        /// Gets the unique name of the installation, often indicating the branch and other information used for telemetry.
        /// </summary>
        /// <returns>The unique name of the installation, often indicating the branch and other information used for telemetry.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationName();

        /// <summary>
        /// Gets the path to the installation root of the product.
        /// </summary>
        /// <returns>The path to the installation root of the product.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationPath();

        /// <summary>
        /// Gets the version of the product installed in this instance.
        /// </summary>
        /// <returns>The version of the product installed in this instance.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetInstallationVersion();

        /// <summary>
        /// Gets the display name (title) of the product installed in this instance.
        /// </summary>
        /// <param name="lcid">The LCID for the display name.</param>
        /// <returns>The display name (title) of the product installed in this instance.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDisplayName([In, MarshalAs(UnmanagedType.U4)] int lcid);

        /// <summary>
        /// Gets the description of the product installed in this instance.
        /// </summary>
        /// <param name="lcid">The LCID for the description.</param>
        /// <returns>The description of the product installed in this instance.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDescription([In, MarshalAs(UnmanagedType.U4)] int lcid);

        /// <summary>
        /// Resolves the optional relative path to the root path of the instance.
        /// </summary>
        /// <param name="relativePath">A relative path within the instance to resolve, or NULL to get the root path.</param>
        /// <returns>The full path to the optional relative path within the instance. If the relative path is NULL, the root path will always terminate in a backslash.</returns>
        [return: MarshalAs(UnmanagedType.BStr)]
        string ResolvePath([In, MarshalAs(UnmanagedType.LPWStr)] string relativePath);
    }

    /// <summary>
    /// A enumerator of installed <see cref="ISetupInstance"/> objects.
    /// </summary>
    [Guid("6380BCFF-41D3-4B2E-8B2E-BF8A6810C848")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumSetupInstances
    {
        /// <summary>
        /// Retrieves the next set of product instances in the enumeration sequence.
        /// </summary>
        /// <param name="celt">The number of product instances to retrieve.</param>
        /// <param name="rgelt">A pointer to an array of <see cref="ISetupInstance"/>.</param>
        /// <param name="pceltFetched">A pointer to the number of product instances retrieved. If celt is 1 this parameter may be NULL.</param>
#pragma warning disable CA1716 // Identifiers should not match keywords
        void Next(
            [In] int celt,
            [Out, MarshalAs(UnmanagedType.Interface)] out ISetupInstance rgelt,
            [Out] out int pceltFetched);
#pragma warning restore CA1716 // Identifiers should not match keywords

        /// <summary>
        /// Skips the next set of product instances in the enumeration sequence.
        /// </summary>
        /// <param name="celt">The number of product instances to skip.</param>
        void Skip([In, MarshalAs(UnmanagedType.U4)] int celt);

        /// <summary>
        /// Resets the enumeration sequence to the beginning.
        /// </summary>
        void Reset();

        /// <summary>
        /// Creates a new enumeration object in the same state as the current enumeration object: the new object points to the same place in the enumeration sequence.
        /// </summary>
        /// <returns>A pointer to a pointer to a new <see cref="IEnumSetupInstances"/> interface. If the method fails, this parameter is undefined.</returns>
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSetupInstances Clone();
    }

    /// <summary>
    /// Gets information about product instances set up on the machine.
    /// </summary>
    [Guid("42843719-DB4C-46C2-8E7C-64F1816EFD5B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISetupConfiguration
    {
        /// <summary>
        /// Enumerates all product instances installed.
        /// </summary>
        /// <returns>An enumeration of installed product instances.</returns>
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSetupInstances EnumInstances();

        /// <summary>
        /// Gets the instance for the current process path.
        /// </summary>
        /// <returns>The instance for the current process path.</returns>
        [return: MarshalAs(UnmanagedType.Interface)]
        ISetupInstance GetInstanceForCurrentProcess();

        /// <summary>
        /// Gets the instance for the given path.
        /// </summary>
        /// <param name="wzPath">Path used to determine instance.</param>
        /// <returns>The instance for the given path.</returns>
        [return: MarshalAs(UnmanagedType.Interface)]
        ISetupInstance GetInstanceForPath([In, MarshalAs(UnmanagedType.LPWStr)] string wzPath);
    }

    /// <summary>
    /// CoClass that implements <see cref="ISetupConfiguration"/>.
    /// </summary>
    [ComImport]
    [Guid("177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D")]
    public class SetupConfiguration;
}

#endif
