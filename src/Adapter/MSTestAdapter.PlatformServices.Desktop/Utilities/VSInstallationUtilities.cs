// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using Microsoft.Win32;

    using static System.String;

    public static class VSInstallationUtilities
    {
        /// <summary>
        /// Gets the visual studio installation path on the local machine.
        /// </summary>
        /// <remarks>It is a good idea to get the VS Install path from registry, as it would work well with the Dev10, Dev11 compat.</remarks>
        /// <returns>VS install path</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to ignore failures to read the registry settings")]
        public static string VSInstallPath
        {
            get
            {
                // Try custom Vs install path if available. This is done for rascal pro. 
                var vsInstallPathFromCustomRoot = GetVsInstallPathFromCustomRoot();
                if (!IsNullOrEmpty(vsInstallPathFromCustomRoot))
                {
                    return vsInstallPathFromCustomRoot;
                }

                using (var hklmKey = Registry.LocalMachine)
                {
                    try
                    {
                        var subKey = Constants.VisualStudioRootRegKey32ForDev14;
                        if (Is64BitProcess())
                        {
                            subKey = Constants.VisualStudioRootRegKey64ForDev14;
                        }
                        using (var visualstudioSubKey = hklmKey.OpenSubKey(subKey))
                        {

                            var registryValue = visualstudioSubKey.GetValue("InstallDir").ToString();
                            if (Directory.Exists(registryValue))
                            {
                                return registryValue;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //ignore the exception.
                    }

                    // If VS is not installed, check for team build.
                    try
                    {
                        using (RegistryKey vsKey = hklmKey.OpenSubKey(SideBySideKeyOnTeamBuildMachine, false))
                        {
                            var visualStudioInstallDir = (String)vsKey?.GetValue(Constants.VisualStudioVersion);
                            if (!string.IsNullOrEmpty(visualStudioInstallDir))
                            {
                                visualStudioInstallDir = Path.Combine(visualStudioInstallDir, @"Common7\IDE");
                                return visualStudioInstallDir;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //ignore the exception.
                    }
                }


                return null;
            }
        }


        /// <summary>
        /// Get path to public assemblies.
        /// 
        /// Returns null if VS is not installed on this machine.
        /// </summary>
        public static string PathToPublicAssemblies
        {
            get
            {
                return GetFullPath(PublicAssembliesDirectoryName);
            }
        }

        /// <summary>
        /// Get path to private assemblies.
        /// 
        /// Returns null if VS is not installed on this machine.
        /// </summary>
        public static string PathToPrivateAssemblies
        {
            get
            {
                return GetFullPath(PrivateAssembliesFolderName);
            }
        }

        public static bool CheckIfTestProcessIsRunningInXcopyableMode()
        {
            return CheckIfTestProcessIsRunningInXcopyableMode(Process.GetCurrentProcess().MainModule.FileName);
        }

        public static bool CheckIfTestProcessIsRunningInXcopyableMode(string exeName)
        {
            // Get the directory of the exe 
            var exeDir = Path.GetDirectoryName(exeName);
            if (!string.IsNullOrEmpty(exeDir))
            {
                return File.Exists(Path.Combine(exeDir, PortableVsTestManifestFilename));
            }

            return false;
        }

        /// <summary>
        ///     Returns true if the current process is run as 64 bit process.
        /// </summary>
        /// <returns></returns>
        private static bool Is64BitProcess()
        {
            return IntPtr.Size == 8;
        }

        private static string GetFullPath(string folderName)
        {
            var vsInstallDir = VSInstallPath;
            return IsNullOrWhiteSpace(vsInstallDir?.Trim()) ? null : Path.Combine(vsInstallDir, folderName);
        }

        /// <summary>
        /// Get Vs install path from custom root
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to ignore failures to read the registry settings")]
        private static string GetVsInstallPathFromCustomRoot()
        {
            try
            {
                var registryKeyWhichContainsVsInstallPath = GetEnvironmentVariable(RegistryRootEnvironmentVariableName);
                if (IsNullOrEmpty(registryKeyWhichContainsVsInstallPath))
                {
                    return null;
                }

                // For rascal, hive is always current user
                using (var hiveKey = RegistryKey.OpenRemoteBaseKey(RegistryHive.CurrentUser, Empty))
                {
                    var visualstudioSubKey = hiveKey.OpenSubKey(registryKeyWhichContainsVsInstallPath);
                    var registryValue = visualstudioSubKey.GetValue("InstallDir").ToString();
                    if (Directory.Exists(registryValue))
                    {
                        return registryValue;
                    }
                }
            }
            catch (Exception)
            {
                //ignore the exception.
            }

            return null;
        }

        /// <summary>
        /// Returns the value of specified environment name, or null, if not found.
        /// </summary>
        private static string GetEnvironmentVariable(string keyName)
        {
            var value = Environment.GetEnvironmentVariable(keyName);
            if (!IsNullOrEmpty(value))
            {
                return value;
            }

            using (var key = Registry.CurrentUser.OpenSubKey("Environment", false))
            {
                return key?.GetValue(keyName) as string;
            }
        }

        /// <summary>
        /// VS root registry key on 64 bit machine.
        /// </summary>
        public const string VSRegistryRootOn64BitMachine = @"SOFTWARE\Wow6432Node\Microsoft\VisualStudio\" + Constants.VisualStudioVersion;

        /// <summary>
        /// Key on the team build machine
        /// </summary>
        public const string SideBySideKeyOnTeamBuildMachine = @"SOFTWARE\Microsoft\VisualStudio\SxS\VS7";

        /// <summary>
        /// Environment variable key which specifies the registry root
        /// 
        /// (This key will be primarily used in rascalPro)
        /// </summary>
        private const string RegistryRootEnvironmentVariableName = @"VisualStudio_RootRegistryKey";

        /// <summary>
        /// Public assemblies directory name
        /// </summary>
        private const string PublicAssembliesDirectoryName = "PublicAssemblies";

        /// <summary>
        /// Folder name of private assemblies
        /// </summary>
        private const string PrivateAssembliesFolderName = "PrivateAssemblies";

        /// <summary>
        /// The manifest file name to determine if it is running in portable mode
        /// </summary>
        private const string PortableVsTestManifestFilename = "Portable.VsTest.Manifest";

        /// <summary>
        /// Name of the directory in which the datacollectors resider under Common7\Ide\PrivateAssemblies
        /// </summary>
        public const string DataCollectorsDirectory = "DataCollectors";
    }
}
