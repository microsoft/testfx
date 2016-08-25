// ---------------------------------------------------------------------------
// <copyright file="AssemblyHelper.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      This contains an API to find the asseblies referenced by the .
// </summary>
// <owner>aseemb</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{
    /// <summary>
    /// Implementation of finding assembly referencies using "managed route", i.e. Assembly.Load.
    /// </summary>
    public static class AssemblyHelper
    {
        private static Version defaultVersion = new Version();
        private static Version version45 = new Version("4.5");

        /// <summary>
        /// Checks whether the source assembly directly references given assembly or not.
        /// Only assembly name and publickey token are match. Version is ignored for matching.
        /// Returns null if not able to check if source references assembly.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool? DoesReferencesAssembly(string source, AssemblyName referenceAssembly)
        {
            try
            {
                ValidateArg.NotNullOrEmpty(source, "source");
                ValidateArg.NotNull(referenceAssembly, "referenceAssembly");

                Debug.Assert(!string.IsNullOrEmpty(source));

                string referenceAssemblyName = referenceAssembly.Name;
                byte[] referenceAssemblyPublicKeyToken = referenceAssembly.GetPublicKeyToken();

                AppDomainSetup setupInfo = new AppDomainSetup();
                setupInfo.ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(source));

                // In Dev10 by devenv uses its own app domain host which has default optimization to share everything.
                // Set LoaderOptimization to MultiDomainHost which means:
                //   Indicates that the application will probably host unique code in multiple domains, 
                //   and the loader must share resources across application domains only for globally available (strong-named) 
                //   assemblies that have been added to the global assembly cache. 
                // See Dev10 bug 660791. 
                setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;

                AppDomain ad = null;
                try
                {
                    ad = AppDomain.CreateDomain("Dependency finder domain", null, setupInfo);

                    Type assemblyLoadWorker = typeof(AssemblyLoadWorker);
                    AssemblyLoadWorker worker = null;
                    if (assemblyLoadWorker.Assembly.GlobalAssemblyCache)
                    {
                        worker = (AssemblyLoadWorker)ad.CreateInstanceAndUnwrap(
                            assemblyLoadWorker.Assembly.FullName,
                            assemblyLoadWorker.FullName,
                            false, BindingFlags.Default, null,
                            null, null, null, null);
                    }
                    else
                    {
                        // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
                        worker = (AssemblyLoadWorker)ad.CreateInstanceFromAndUnwrap(
                            assemblyLoadWorker.Assembly.Location,
                            assemblyLoadWorker.FullName,
                            false, BindingFlags.Default, null,
                            null, null, null, null);
                    }

                    return worker.CheckAssemblyReference(source, referenceAssemblyName, referenceAssemblyPublicKeyToken);
                }
                finally
                {
                    if (ad != null)
                    {
                        AppDomain.Unload(ad);
                    }
                }
            }
            catch
            {
                return null; // Return null if something goes wrong.
            }
        }

        /// <summary>
        /// Finds platform and .Net framework version for a given test container.
        /// If there is an error while infering this information, defaults (AnyCPU, None) are returned
        /// for faulting container.
        /// </summary>
        /// <param name="testSources"></param>
        /// <returns></returns>
        public static KeyValuePair<Architecture, FrameworkVersion> GetFrameworkVersionAndArchitectureForSource(
            string testSource)
        {
            ValidateArg.NotNullOrEmpty(testSource, "testSource");

            string sourceDirectory = Path.GetDirectoryName(testSource);
            AppDomainSetup setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = sourceDirectory;
            setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;
            AppDomain ad = null;
            try
            {
                ad = AppDomain.CreateDomain("Multiargeting settings domain", null, setupInfo);

                Type assemblyLoadWorker = typeof(AssemblyLoadWorker);
                AssemblyLoadWorker worker = null;

                // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
                worker = (AssemblyLoadWorker)ad.CreateInstanceFromAndUnwrap(
                    assemblyLoadWorker.Assembly.Location,
                    assemblyLoadWorker.FullName,
                    false, BindingFlags.Default, null,
                    null, null, null, null);

                string procArchType;
                string frameworkVersion;
                worker.GetPlatformAndFrameworkSettings(testSource, out procArchType, out frameworkVersion);

                Architecture targetPlatform = (Architecture)Enum.Parse(typeof(Architecture), procArchType);
                FrameworkVersion targetFramework = FrameworkVersion.Framework45;
                switch (frameworkVersion.ToUpperInvariant())
                {
                    case "V4.5":
                        targetFramework = FrameworkVersion.Framework45;
                        break;

                    case "V4.0":
                        targetFramework = FrameworkVersion.Framework40;
                        break;

                    case "V3.5":
                    case "V2.0":
                        targetFramework = FrameworkVersion.Framework35;
                        break;

                    default:
                        targetFramework = FrameworkVersion.None;
                        break;
                }
                if (EqtTrace.IsVerboseEnabled)
                {
                    EqtTrace.Verbose("Inferred Multi-Targeting settings:{0} Platform:{1} FrameworkVersion:{2}", testSource, targetPlatform, targetFramework);
                }
                return new KeyValuePair<Architecture, FrameworkVersion>(targetPlatform, targetFramework);

            }
            finally
            {
                if (ad != null)
                {
                    AppDomain.Unload(ad);
                }
            }
        }


        /// <summary>
        /// Returns the full name (AssemblyName.FullName) of the referenced assemblies by the assembly on the specified path.
        /// 
        /// Returns null on failure and an empty array if there is no reference in the project. 
        /// </summary>
        /// <param name="source">Full path to the assembly to get dependencies for.</param>
        public static string[] GetReferencedAssemblies(string source)
        {
            Debug.Assert(!string.IsNullOrEmpty(source));

            AppDomainSetup setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(source));

            // In Dev10 by devenv uses its own app domain host which has default optimization to share everything.
            // Set LoaderOptimization to MultiDomainHost which means:
            //   Indicates that the application will probably host unique code in multiple domains, 
            //   and the loader must share resources across application domains only for globally available (strong-named) 
            //   assemblies that have been added to the global assembly cache. 
            // See Dev10 bug 660791. 
            setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;

            AppDomain ad = null;
            try
            {
                ad = AppDomain.CreateDomain("Dependency finder domain", null, setupInfo);

                Type assemblyLoadWorker = typeof(AssemblyLoadWorker);
                AssemblyLoadWorker worker = null;
                if (assemblyLoadWorker.Assembly.GlobalAssemblyCache)
                {
                    worker = (AssemblyLoadWorker)ad.CreateInstanceAndUnwrap(
                        assemblyLoadWorker.Assembly.FullName,
                        assemblyLoadWorker.FullName,
                        false, BindingFlags.Default, null,
                        null, null,null,null);
                }
                else
                {
                    // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
                    worker = (AssemblyLoadWorker)ad.CreateInstanceFromAndUnwrap(
                        assemblyLoadWorker.Assembly.Location,
                        assemblyLoadWorker.FullName,
                        false, BindingFlags.Default, null,
                        null, null,null,null);
                }

                return worker.GetReferencedAssemblies(source);
            }
            finally
            {
                if (ad != null)
                {
                    AppDomain.Unload(ad);
                }
            }
        }

        /// <summary>
        /// Set the target framework for app domain setup if target framework of dll is > 4.5
        /// </summary>
        /// <param name="setup">AppdomainSetup for app domain creation</param>
        /// <param name="testSource">path of test file</param>
        public static void SetAppDomainFrameworkVersionBasedOnTestSource(AppDomainSetup setup, string testSource)
        {
            string assemblyVersionString = GetTargetFrameworkVersionString(testSource);

            if (GetTargetFrameworkVersionFromVersionString(assemblyVersionString).CompareTo(version45) > 0)
            {
                PropertyInfo pInfo = typeof(AppDomainSetup).GetProperty(Constants.TargetFrameworkName);
                if (null != pInfo)
                {
                    pInfo.SetValue(setup, assemblyVersionString, null);
                }
            }
        }

        /// <summary>
        /// Get the target dot net framework string for the assembly
        /// </summary>
        /// <param name="path">Path of the assembly file</param>
        /// <returns>String representation of the the target dot net framework e.g. .NETFramework,Version=v4.0 </returns>
        internal static string GetTargetFrameworkVersionString(string path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));

            AppDomainSetup setupInfo = new AppDomainSetup();
            setupInfo.ApplicationBase = Path.GetDirectoryName(Path.GetFullPath(path));

            // In Dev10 by devenv uses its own app domain host which has default optimization to share everything.
            // Set LoaderOptimization to MultiDomainHost which means:
            //   Indicates that the application will probably host unique code in multiple domains, 
            //   and the loader must share resources across application domains only for globally available (strong-named) 
            //   assemblies that have been added to the global assembly cache. 
            // See Dev10 bug 660791. 
            setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;

            if (File.Exists(path))
            {
                AppDomain ad = null;
                try
                {
                    ad = AppDomain.CreateDomain("Framework Version String Domain", null, setupInfo);

                    Type assemblyLoadWorker = typeof(AssemblyLoadWorker);
                    AssemblyLoadWorker worker = null;
                    if (assemblyLoadWorker.Assembly.GlobalAssemblyCache)
                    {
                        worker = (AssemblyLoadWorker)ad.CreateInstanceAndUnwrap(
                            assemblyLoadWorker.Assembly.FullName,
                            assemblyLoadWorker.FullName,
                            false, BindingFlags.Default, null,
                            null, null, null, null);
                    }
                    else
                    {
                        // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
                        worker = (AssemblyLoadWorker)ad.CreateInstanceFromAndUnwrap(
                            assemblyLoadWorker.Assembly.Location,
                            assemblyLoadWorker.FullName,
                            false, BindingFlags.Default, null,
                            null, null, null, null);
                    }

                    return worker.GetTargetFrameworkVersionStringFromPath(path);
                }
                finally
                {
                    if (ad != null)
                    {
                        AppDomain.Unload(ad);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the Version for the target framework version string
        /// </summary>
        /// <param name="version">Target framework string</param>
        /// <returns>Framework Version</returns>
        internal static Version GetTargetFrameworkVersionFromVersionString(string version)
        {
            if (version.Length > Constants.DotNetFrameWorkStringPrefix.Length + 1)
            {
                string versionPart = version.Substring(Constants.DotNetFrameWorkStringPrefix.Length + 1);
                return new Version(versionPart);
            }

            return defaultVersion;
        }

        /// <summary>
        /// When test run is targeted for .Net4.0, set target framework of test appdomain to be v4.0.
        /// With this done tests would be executed in 4.0 compatiblity mode even when  .Net4.5 is installed.
        /// This needs to be done using reflection, as this dll is compiled for .Net3.5.
        /// bug: 883380	MS-TEST runner breaks BinaryCompatibility/TargetFrameworkAttribute quirks infrastructure for .NET 4.5
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Failure to set this property should be ignored.")]
        internal static void SetNETFrameworkCompatiblityMode(AppDomainSetup setup, IRunContext runContext)
        {
            try
            {
                RunConfiguration runConfiguration = XmlRunSettingsUtilities.GetRunConfigurationNode(runContext.RunSettings.SettingsXml);
                if (null != runConfiguration && Enum.Equals(runConfiguration.TargetFrameworkVersion, FrameworkVersion.Framework40))
                {
                    PropertyInfo pInfo = typeof(AppDomainSetup).GetProperty(Constants.TargetFrameworkName);
                    if (null != pInfo)
                    {
                        if (EqtTrace.IsVerboseEnabled)
                        {
                            EqtTrace.Verbose("AssemblyHelper.SetNETFrameworkCompatiblityMode: setting .NetFramework,Version=v4.0 compatiblity mode.");
                        }
                        pInfo.SetValue(setup, Constants.DotNetFramework40, null);
                    }
                    else
                    {
                        if (EqtTrace.IsWarningEnabled)
                        {
                            EqtTrace.Warning("AssemblyHelper:SetNETFrameworkCompatiblityMode: Binary compatiblity mode needed, but AppDomainSetup.TargetFrameworkName property not found. Ignoring compatiblity mode.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("AssemblyHelper:SetNETFrameworkCompatiblityMode:  Caught an exception:{0}", e);
                }
            }
        }        
    }

    /// <summary>
    /// Does the real work of finding references using Assembly.ReflectionOnlyLoadFrom. 
    /// The caller is supposed to create AppDomain and create instance of given class in there.
    /// </summary>
    internal class AssemblyLoadWorker : MarshalByRefObject
    {
        /// <summary>
        /// Get the target dot net framework string for the assembly
        /// </summary>
        /// <param name="path">Path of the assembly file</param>
        /// <returns> String representation of the the target dot net framework e.g. .NETFramework,Version=v4.0 </returns>
        public string GetTargetFrameworkVersionStringFromPath(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    Assembly a = Assembly.ReflectionOnlyLoadFrom(path);
                    return GetTargetFrameworkStringFromAssembly(a);
                }
                catch (BadImageFormatException)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("AssemblyHelper:GetTargetFrameworkVersionString() caught BadImageFormatException. Falling to native binary.");
                    }
                }
                catch (Exception ex)
                {
                    if (EqtTrace.IsErrorEnabled)
                    {
                        EqtTrace.Error("AssemblyHelper:GetTargetFrameworkVersionString() Returning default. Unhandled exception: {0}.", ex);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Get the target dot net framework string for the assembly
        /// </summary>
        /// <param name="assembly">Assembly</param>
        /// <returns>String representation of the the target dot net framework e.g. .NETFramework,Version=v4.0 </returns>
        internal static string GetTargetFrameworkStringFromAssembly(Assembly assembly)
        {
            string dotNetVersion = string.Empty;
            foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(assembly))
            {
                if (data.NamedArguments.Count > 0)
                {
                    string attributeName = data.NamedArguments[0].MemberInfo.DeclaringType.FullName;
                    if (string.Equals(attributeName, Constants.TargetFrameworkAttributeFullName, StringComparison.OrdinalIgnoreCase))
                    {
                        dotNetVersion = data.ConstructorArguments[0].Value.ToString();
                        break;
                    }
                }
            }

            return dotNetVersion;
        }

        /// <summary>
        /// Returns the full name of the referenced assemblies by the assembly on the specified path.
        /// 
        /// Returns null on failure and an empty array if there is no reference in the project. 
        /// </summary>
        /// <param name="path">Path to the assembly file to load from.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification="Being created in a separate app-domain"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public string[] GetReferencedAssemblies(string path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path));

            Assembly a = null;
            try
            {
                // ReflectionOnlyLoadFrom does not use the probing paths and loads from the 
                // specified path only and does not let code to be executed by the assembly 
                // in the loaded context. 
                //
                a = Assembly.ReflectionOnlyLoadFrom(path);
            }
            catch
            {
                return null; 
            }
            Debug.Assert(a != null);

            AssemblyName[] assemblies = a.GetReferencedAssemblies();
            if (assemblies == null || assemblies.Length == 0)
            {
                return new string[0];
            }

            return (from assembly in assemblies
                    select assembly.FullName).ToArray();
        }

        /// <summary>
        /// Returns true if given assembly matched name and public key token.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification="Being created in a separate app-domain"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public bool? CheckAssemblyReference(string path, string referenceAssemblyName, byte[] publicKeyToken)
        {
            Assembly a = null;
            try
            {
                // ReflectionOnlyLoadFrom does not use the probing paths and loads from the 
                // specified path only and does not let code to be executed by the assembly 
                // in the loaded context. 
                //
                a = Assembly.ReflectionOnlyLoadFrom(path);


                Debug.Assert(a != null);

                AssemblyName[] assemblies = a.GetReferencedAssemblies();

                foreach (AssemblyName referencedAssembly in assemblies)
                {
                    // Check without version. Only name and publikey token.
                    if (string.Compare(referencedAssembly.Name, referenceAssemblyName, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        byte[] publicKeyToken1 = referencedAssembly.GetPublicKeyToken();

                        bool isMatch = true;
                        if (publicKeyToken1.Length != publicKeyToken.Length)
                        {
                            continue;
                        }

                        for (int i = 0; i < publicKeyToken1.Length; ++i)
                        {
                            if (publicKeyToken1[i] != publicKeyToken[i])
                            {
                                isMatch = false;
                                break;
                            }
                        }

                        if (isMatch)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return null; // return null if not able to check.
            }

            return false;
        }

        /// <summary>
        /// Finds platform and .Net framework version for a given container.
        /// In case of errors defaults are returned.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="procArchType"></param>
        /// <param name="frameworkVersion"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Being created in a separate app-domain")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void GetPlatformAndFrameworkSettings(string path, out string procArchType, out string frameworkVersion)
        {
            procArchType =  Architecture.Default.ToString();
            frameworkVersion = String.Empty;


            try
            {
                // ReflectionOnlyLoadFrom does not use the probing paths and loads from the 
                // specified path only and does not let code to be executed by the assembly 
                // in the loaded context. 
                //


                Assembly a = Assembly.ReflectionOnlyLoadFrom(path);
                Debug.Assert(a != null);
                PortableExecutableKinds peKind;
                ImageFileMachine machine;
                a.ManifestModule.GetPEKind(out peKind, out machine);


                // conversion to string type is needed for below reason
                // -- PortableExecutableKinds.Preferred32Bit and ImageFileMachine.ARM is available only
                //    in .Net4.0 and above. Below code is compiled with .Net3.5 but runs in .Net4.0
                string peKindString = peKind.ToString();
                string machineTypeString = machine.ToString();
                if (string.Equals(machineTypeString, "I386", StringComparison.OrdinalIgnoreCase))
                {
                    if (peKindString.Contains("Preferred32Bit") || peKindString.Contains("Required32Bit"))
                    {
                        procArchType = "X86";
                    }
                    else if (string.Equals(peKindString, "ILOnly", StringComparison.OrdinalIgnoreCase))
                    {
                        procArchType = "AnyCPU";
                    }
                }
                else if (string.Equals(machineTypeString, "AMD64", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(machineTypeString, "IA64", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(peKindString, "ILOnly, PE32Plus", StringComparison.OrdinalIgnoreCase))
                    {
                        procArchType = "X64";
                    }
                }
                else if (string.Equals(machineTypeString, "ARM", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(peKindString, "ILOnly", StringComparison.OrdinalIgnoreCase))
                    {
                        procArchType = "ARM";
                    }
                }

                if (string.IsNullOrEmpty(procArchType))
                {
                    if (EqtTrace.IsVerboseEnabled)
                    {
                        EqtTrace.Verbose("Unable to find the platform type for image:{0} with PEKind:{1}, Machine:{2}. Returning Default:{3}", path, peKindString, machineTypeString, "AnyCPU");                        
                    }
                    procArchType = "AnyCPU";
                }


                frameworkVersion = a.ImageRuntimeVersion.Substring(0, 4).ToUpperInvariant();

                // ImageRuntimeVersion for v4.0 & v4.5 are same and it return v4.0
                // Since there is behaviourial differnece in both its important to differentiate them
                // Using TargetFrameworkAttribute for the purpose.
                if (string.Equals(frameworkVersion, "v4.0", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to determine the exact .NET framework by inspecting custom attributes on assembly.
                    string dotNetVersion = GetTargetFrameworkStringFromAssembly(a);
                    if (dotNetVersion.StartsWith(Constants.DotNetFramework40, StringComparison.OrdinalIgnoreCase))
                    {
                        frameworkVersion = "v4.0";
                    }
                    else if (dotNetVersion.StartsWith(Constants.DotNetFramework45, StringComparison.OrdinalIgnoreCase))
                    {
                        frameworkVersion = "v4.5";
                    }
                    else if (dotNetVersion.Length > Constants.DotNetFrameWorkStringPrefix.Length)
                    {
                        frameworkVersion = dotNetVersion.Substring(Constants.DotNetFrameWorkStringPrefix.Length);
                    }
                }

            }
            catch (BadImageFormatException)
            {
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("AssemblyLoadWorker:GetPlatformAndFrameworkSettings() caught BadImageFormatException. Falling to native binary.");
                }
                procArchType = GetArchitectureForSource(path);
            }
            catch(Exception ex)
            {
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("AssemblyLoadWorker:GetPlatformAndFrameworkSettings() Returning default. Unhandled exception: {0}.", ex);
                }
                return;
            }
           
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private static string GetArchitectureForSource(string imagePath)
        {
            // For details refer to below code available on MSDN.
            // http://code.msdn.microsoft.com/CSCheckExeType-aab06100/sourcecode?fileId=22010&pathId=1874010322

            string archType = "AnyCPU";
            ushort machine = 0;
   
            uint peHeader;
            const int IMAGE_FILE_MACHINE_AMD64 = 0x8664;
            const int IMAGE_FILE_MACHINE_IA64 = 0x200;
            const int IMAGE_FILE_MACHINE_I386 = 0x14c;
            const int IMAGE_FILE_MACHINE_ARM =   0x01c0;  // ARM Little-Endian
            const int IMAGE_FILE_MACHINE_THUMB =  0x01c2;  // ARM Thumb/Thumb-2 Little-Endian
            const int IMAGE_FILE_MACHINE_ARMNT = 0x01c4; // ARM Thumb-2 Little-Endian

   
            try
            {
                //get the input stream
                using (Stream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    bool validImage = true;

                    BinaryReader reader = new BinaryReader(fs);
                    //PE Header starts @ 0x3C (60). Its a 4 byte header.
                    fs.Position = 0x3C;
                    peHeader = reader.ReadUInt32();

                    // Check if the offset is invalid
                    if (peHeader > fs.Length - 5)
                    {
                        validImage = false;
                    }
                    if (validImage)
                    {
                        //Moving to PE Header start location...
                        fs.Position = peHeader;

                        UInt32 signature = reader.ReadUInt32(); //peHeaderSignature
                        // 0x00004550 is the letters "PE" followed by two terminating zeroes.
                        if (signature != 0x00004550)
                        {
                            validImage = false;
                        }

                        if (validImage)
                        {
                            //Read the image file header header.
                            machine = reader.ReadUInt16();
                            reader.ReadUInt16(); //NumberOfSections
                            reader.ReadUInt32(); //TimeDateStamp
                            reader.ReadUInt32(); //PointerToSymbolTable
                            reader.ReadUInt32(); //NumberOfSymbols
                            reader.ReadUInt16(); //SizeOfOptionalHeader
                            reader.ReadUInt16(); //Characteristics

                            // magic number.32bit or 64bit assembly.
                            UInt16 magic = reader.ReadUInt16();
                            if (magic != 0x010B && magic != 0x020B)
                            {
                                validImage = false;
                            }
                        }

                        if (validImage)
                        {
                            switch (machine)
                            {
                                case IMAGE_FILE_MACHINE_I386:
                                    archType = "X86";
                                    break;

                                case IMAGE_FILE_MACHINE_AMD64:
                                case IMAGE_FILE_MACHINE_IA64:
                                    archType = "X64";
                                    break;

                                case IMAGE_FILE_MACHINE_ARM:
                                case IMAGE_FILE_MACHINE_THUMB:
                                case IMAGE_FILE_MACHINE_ARMNT:
                                    archType = "ARM";
                                    break;
                            }
                        }
                        else
                        {
                            if (EqtTrace.IsVerboseEnabled)
                            {
                                EqtTrace.Verbose("Source path {0} is not a valid image path. Returning default proc arch type {1}.", imagePath, "AnyCPU");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Ignore all exception
                if (EqtTrace.IsErrorEnabled)
                {
                    EqtTrace.Error("AssemblyLoadWorker:GetArchitectureForSource() Returning default:{0}. Unhandled exception: {1}.",  "AnyCPU", ex.Message);
                }
            }        

            return archType;
        }
        
    }

}
