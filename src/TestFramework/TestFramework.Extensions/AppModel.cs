// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
namespace MSTest.PlatformServices.AppContainer;

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
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        return result != Interop.APPMODEL_ERROR_NO_PACKAGE && packageFullNameLength != 0;
    }

    /// <summary>
    /// Gets the package path for the calling process.
    /// </summary>
    /// <returns>The package path when packaged, <c>null</c> if not.</returns>
    /// <see href="https://docs.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagepath"/>
    public static string? GetCurrentPackagePath()
    {
        int result;
        int pathLength = 0;

        try
        {
            result = Interop.GetCurrentPackagePath2(Interop.PackagePathType.PackagePathType_Effective, ref pathLength, null);
            if (result != Interop.ERROR_INSUFFICIENT_BUFFER)
            {
                return null;
            }

            var path = new StringBuilder(pathLength);
            result = Interop.GetCurrentPackagePath2(Interop.PackagePathType.PackagePathType_Effective, ref pathLength, path);

            if (result == Interop.ERROR_SUCCESS)
            {
                return path.ToString();
            }
        }
        catch (EntryPointNotFoundException)
        {
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
        catch (EntryPointNotFoundException)
        {
        }

        return null;
    }

    [SuppressMessage("Performance", "CA1838:Avoid 'StringBuilder' parameters for P/Invokes", Justification = "Copied from WinUI source base")]
    private static class Interop
    {
#pragma warning disable SA1310 // Field names must not contain underscore
        public const int APPMODEL_ERROR_NO_PACKAGE = 0x00003D54;
        public const int ERROR_INSUFFICIENT_BUFFER = 0x0000007A;
        public const int ERROR_INVALID_PARAMETER = 0x00000057;
        public const int ERROR_SUCCESS = 0x00000000;

        [SuppressMessage("Naming", "CA1712:Do not prefix enum values with type name", Justification = "Copied from WinUI source base")]
        public enum PackagePathType
        {
            PackagePathType_Install = 0,
            PackagePathType_Mutable = 1,
            PackagePathType_Effective = 2,
            PackagePathType_MachineExternal = 3,
            PackagePathType_UserExternal = 4,
            PackagePathType_EffectiveExternal = 5,
        }

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Optional] StringBuilder? packageFullName);

        [DllImport("kernelbase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackagePath2(PackagePathType packagePathType, ref int pathLength, [Optional, MarshalAs(UnmanagedType.LPWStr)] StringBuilder? path);

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        public static extern int GetCurrentPackagePath(ref int pathLength, StringBuilder path);
    }
}
#endif
