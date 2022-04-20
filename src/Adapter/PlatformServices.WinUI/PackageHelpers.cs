// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1310 // Field names must not contain underscore

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;

    // See https://github.com/microsoft/WindowsAppSDK/blob/f1cd5fc8ce6a6fed8442b3f64978b9e65b1d43fd/dev/Common/AppModel.Identity.h#L11

    /// <summary>
    /// Global helpers for queriying package information
    /// </summary>
    internal static class PackageHelpers
    {
        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        /// <summary>
        /// Checks whether the current application is packaged or not.
        /// </summary>
        /// <returns>Returns true if application is packaged.</returns>
        public static bool IsPackagedProcess()
        {
            // This API is supported in Windows 6.2+
            // See https://docs.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname#requirements
            var os = (Environment.OSVersion.Version.Major * 1000) + Environment.OSVersion.Version.Minor;
            if (os < 6002)
            {
                return false;
            }

            StringBuilder packageFullName = new StringBuilder(0);
            int packageFullNameLength = 0;
            var result = GetCurrentPackageFullName(ref packageFullNameLength, packageFullName);

            /* Second call will return the package name.
            packageFullName = new StringBuilder(packageFullNameLength);
            result = GetCurrentPackageFullName(ref packageFullNameLength, packageFullName);
            */

            return result != APPMODEL_ERROR_NO_PACKAGE && packageFullNameLength != 0;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder packageFullName);
    }
}
