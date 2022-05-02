// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Provides package information about the application.
    /// </summary>
    internal static class AppModel
    {
        /// <summary>
        /// Checks whether the current application is packaged or not.
        /// </summary>
        /// <returns>Returns true if application is packaged; false otherwise.</returns>
        /// <see href="https://github.com/microsoft/WindowsAppSDK/blob/f1cd5fc8ce6a6fed8442b3f64978b9e65b1d43fd/dev/Common/AppModel.Identity.h#L11"/>
        public static bool IsPackagedProcess()
        {
            int result, packageFullNameLength = 0;

            try
            {
                result = Interop.GetCurrentPackageFullName(ref packageFullNameLength, null);

                /* Second call will return the package name.
                var packageFullName = new StringBuilder(packageFullNameLength);
                result = GetCurrentPackageFullName(ref packageFullNameLength, packageFullName);
                */
            }
            catch
#if NET5_0_OR_GREATER
                (EntryPointNotFoundException)
#endif
            {
                return false;
            }

            return result != Interop.APPMODEL_ERROR_NO_PACKAGE && packageFullNameLength != 0;
        }

        /// <summary>
        /// Gets the package path for the calling process.
        /// </summary>
        /// <returns>The package path when packaged, <c>null</c> if not.</returns>
        /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagepath2"/>
        public static string? GetCurrentPackagePath(PackagePathType pathType)
        {
            int result;
            int pathLength = 0;

            try
            {
                result = Interop.GetCurrentPackagePath2(pathType, ref pathLength, null);
                if (result != Interop.ERROR_INSUFFICIENT_BUFFER)
                {
                    return null;
                }

                var path = new StringBuilder(pathLength);
                result = Interop.GetCurrentPackagePath2(pathType, ref pathLength, path);

                if (result == Interop.ERROR_SUCCESS)
                {
                    return path.ToString();
                }
            }
            catch
#if NET5_0_OR_GREATER
                (EntryPointNotFoundException)
#endif
            {
            }

            return null;
        }

        /// <summary>
        /// Gets the package install path for the calling process.
        /// </summary>
        /// <returns>The package install path when packaged, <c>null</c> if not.</returns>
        /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagepath"/>
        public static string? GetCurrentPackagePath()
        {
            int result;
            int pathLength = 0;

            if (GetCurrentPackagePath(PackagePathType.Install) is { } installPath) {
                return installPath;
            }

            try
            {
                var path = new StringBuilder(0);
                result = Interop.GetCurrentPackagePath(ref pathLength, path);
                if (result != Interop.ERROR_INSUFFICIENT_BUFFER)
                {
                    return null;
                }

                path = new StringBuilder(pathLength);
                result = Interop.GetCurrentPackagePath(ref pathLength, path);

                if (result == Interop.ERROR_SUCCESS)
                {
                    return path.ToString();
                }
            }
            catch
#if NET5_0_OR_GREATER
                (EntryPointNotFoundException)
#endif
            {
            }

            return null;
        }
 
        private static class Interop
        {
            public const int APPMODEL_ERROR_NO_PACKAGE = 0x00003D54;
            public const int ERROR_INSUFFICIENT_BUFFER = 0x0000007A;
            public const int ERROR_INVALID_PARAMETER = 0x00000057;
            public const int ERROR_SUCCESS = 0x00000000;

            [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
            public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Optional] StringBuilder? packageFullName);

            [DllImport("kernelbase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
            public static extern int GetCurrentPackagePath2(PackagePathType packagePathType, ref int pathLength, [Optional, MarshalAs(UnmanagedType.LPWStr)] StringBuilder? path);

            [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
            public static extern int GetCurrentPackagePath(ref int pathLength, StringBuilder path);
        }
    }

    /// <summary>
    /// Indicates the type of folder path to retrieve in a query for the path or other info about a package.
    /// </summary>
    /// <remarks>
    /// An application has a mutable install folder if it uses the
    /// <a href="https://docs.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-desktop6-package-extension">windows.mutablePackageDirectories</a>
    /// extension in its package manifest. 
    /// This extension specifies a folder under the <c>%ProgramFiles%\ModifiableWindowsApps</c>
    /// path where the contents of the application's install folder are projected so that users can modify
    /// the installation files. This feature is currently available only for certain types of
    /// desktop PC games that are published by Microsoft and our partners, and it enables these
    /// types of games to support mods.
    /// </remarks>
    /// <see cref="https://docs.microsoft.com/en-us/windows/win32/api/appmodel/ne-appmodel-packagepathtype"/>
    public enum PackagePathType
    {
        /// <summary>
        /// Retrieve the package path in the original install folder for the application.
        /// </summary>
        Install = 0,

        /// <summary>
        /// Retrieve the package path in the mutable install folder for the application,
        /// if the application is declared as mutable in the package manifest.
        /// </summary>
        Mutable = 1,

        /// <summary>
        /// Retrieve the package path in the mutable folder if the application is declared
        /// as mutable in the package manifest, or in the original install folder if the
        /// application is not mutable.
        /// </summary>
        Effective = 2,

        MachineExternal = 3,
        UserExternal = 4,
        EffectiveExternal = 5
    }
}
