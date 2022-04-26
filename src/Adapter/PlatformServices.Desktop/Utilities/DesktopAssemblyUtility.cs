// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    /// <summary>
    /// Utility for assembly specific functionality.
    /// </summary>
    internal class AssemblyUtility : IAssemblyUtility
    {
        private static Dictionary<string, object> cultures;
        private readonly string[] assemblyExtensions = new string[] { ".dll", ".exe" };

        /// <summary>
        /// Gets all supported culture names in Keys. The Values are always null.
        /// </summary>
        private static Dictionary<string, object> Cultures
        {
            get
            {
                if (cultures == null)
                {
                    cultures = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var info in CultureInfo.GetCultures(CultureTypes.AllCultures))
                    {
                        cultures.Add(info.Name, null);
                    }
                }

                return cultures;
            }
        }

        /// <summary>
        /// Loads an assembly into the reflection-only context, given its path.
        /// </summary>
        /// <param name="assemblyPath">The path of the file that contains the manifest of the assembly.</param>
        /// <returns>The loaded assembly.</returns>
        public Assembly ReflectionOnlyLoadFrom(string assemblyPath)
        {
            return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
        }

        /// <summary>
        /// Loads an assembly into the reflection-only context, given its display name.
        /// </summary>
        /// <param name="assemblyString">The display name of the assembly, as returned by the System.Reflection.AssemblyName.FullName property.</param>
        /// <returns>The loaded assembly.</returns>
        public Assembly ReflectionOnlyLoad(string assemblyString)
        {
            return Assembly.ReflectionOnlyLoad(assemblyString);
        }

        /// <summary>
        /// Whether file extension is an assembly file extension.
        /// Returns true for .exe and .dll, otherwise false.
        /// </summary>
        /// <param name="extensionWithLeadingDot"> Extension containing leading dot, e.g. ".exe". </param>
        /// <remarks> Path.GetExtension() returns extension with leading dot. </remarks>
        /// <returns> True if this is an assembly extension. </returns>
        internal bool IsAssemblyExtension(string extensionWithLeadingDot)
        {
            foreach (var realExtension in this.assemblyExtensions)
            {
                if (string.Equals(extensionWithLeadingDot, realExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether given file is managed assembly. Does not load the assembly. Does not check file extension.
        /// Performance: takes ~0.1 seconds on 2x CPU P4.
        /// </summary>
        /// <param name="path"> The path to the assembly. </param>
        /// <returns> True if managed assembly. </returns>
        internal bool IsAssembly(string path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path), "path");
            try
            {
                // AssemblyName.GetAssemblyName: causes the file to be opened and closed, but the assembly is not added to this domain.
                // Also if there are dependencies, they are never loaded.
                AssemblyName.GetAssemblyName(path);
                return true;
            }
            catch (FileLoadException)
            {
                // This is an executable image but not an assembly.
            }
            catch (BadImageFormatException)
            {
                // Happens when file is not a DLL/EXE, etc.
            }

            // If file cannot be found we will throw.
            // If there's anything else like SecurityException - we just pass exception through.
            return false;
        }

        /// <summary>
        /// Returns satellite assemblies. Returns full canonicalized paths.
        /// If the file is not an assembly returns empty list.
        /// </summary>
        /// <param name="assemblyPath"> The assembly to get satellites for. </param>
        /// <returns> List of satellite assemblies. </returns>
        internal virtual List<string> GetSatelliteAssemblies(string assemblyPath)
        {
            if (!this.IsAssemblyExtension(Path.GetExtension(assemblyPath)) || !this.IsAssembly(assemblyPath))
            {
                EqtTrace.ErrorIf(
                        EqtTrace.IsErrorEnabled,
                        "AssemblyUtilities.GetSatelliteAssemblies: the specified file '{0}' is not managed assembly.",
                        assemblyPath);
                Debug.Fail("AssemblyUtilities.GetSatelliteAssemblies: the file '" + assemblyPath + "' is not an assembly.");

                // If e.g. this is unmanaged dll, we don't care about the satellites.
                return new List<string>();
            }

            assemblyPath = Path.GetFullPath(assemblyPath);
            var assemblyDir = Path.GetDirectoryName(assemblyPath);
            var satellites = new List<string>();

            // Directory.Exists for 266 dirs takes 9ms while Path.GetDirectories can take up to 80ms on 10k dirs.
            foreach (string dir in Cultures.Keys)
            {
                var dirPath = Path.Combine(assemblyDir, dir);
                if (!Directory.Exists(dirPath))
                {
                    continue;
                }

                // Check if the satellite exists in this dir.
                // We check filenames like: MyAssembly.dll -> MyAssembly.resources.dll.
                // Surprisingly, but both DLL and EXE are found by resource manager.
                foreach (var extension in this.assemblyExtensions)
                {
                    // extension contains leading dot.
                    string satellite = Path.ChangeExtension(Path.GetFileName(assemblyPath), "resources" + extension);
                    string satellitePath = Path.Combine(assemblyDir, Path.Combine(dir, satellite));

                    // We don't use Assembly.LoadFrom/Assembly.GetSatelliteAssebmlies because this is rather slow
                    // (1620ms for 266 cultures when directories do not exist).
                    if (File.Exists(satellitePath))
                    {
                        // If the satellite found is not a managed assembly we do not report it as a reference.
                        if (!this.IsAssembly(satellitePath))
                        {
                            EqtTrace.ErrorIf(
                                EqtTrace.IsErrorEnabled,
                                "AssemblyUtilities.GetSatelliteAssemblies: found assembly '{0}' installed as satellite but it's not managed assembly.",
                                satellitePath);
                            continue;
                        }

                        // If both .exe and .dll exist we return both silently.
                        satellites.Add(satellitePath);
                    }
                }
            }

            return satellites;
        }

        /// <summary>
        /// Returns the dependent assemblies of the parameter assembly.
        /// </summary>
        /// <param name="assemblyPath"> Path to assembly to get dependencies for. </param>
        /// <param name="configFile"> Config file to use while trying to resolve dependencies. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> The <see cref="T:string[]"/>. </returns>
        internal virtual string[] GetFullPathToDependentAssemblies(string assemblyPath, string configFile, out IList<string> warnings)
        {
            Debug.Assert(!string.IsNullOrEmpty(assemblyPath), "assemblyPath");

            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: start.");

            AppDomainSetup setupInfo = new AppDomainSetup();
            var dllDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
            setupInfo.ApplicationBase = dllDirectory;

            Debug.Assert(string.IsNullOrEmpty(configFile) || File.Exists(configFile), "Config file is specified but does not exist: {0}", configFile);

            AppDomainUtilities.SetConfigurationFile(setupInfo, configFile);

            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: Using config file: '{0}'.", setupInfo.ConfigurationFile);

            setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;

            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain("Dependency finder domain", null, setupInfo);
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("AssemblyDependencyFinder.GetDependentAssemblies: Created AppDomain.");
                }

                var assemblyResolverType = typeof(AssemblyResolver);

                EqtTrace.SetupRemoteEqtTraceListeners(appDomain);

                // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
                using (
                    AssemblyResolver resolver =
                        (AssemblyResolver)AppDomainUtilities.CreateInstance(
                                                    appDomain,
                                                    assemblyResolverType,
                                                    new object[] { this.GetResolutionPaths() }))
                {
                    // This has to be Load, otherwise Serialization of argument types will not work correctly.
                    AssemblyLoadWorker worker =
                        (AssemblyLoadWorker)AppDomainUtilities.CreateInstance(appDomain, typeof(AssemblyLoadWorker), null);

                    EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: loaded the worker.");

                    var allDependencies = worker.GetFullPathToDependentAssemblies(assemblyPath, out warnings);
                    var dependenciesFromDllDirectory = new List<string>();
                    var dllDirectoryUppercase = dllDirectory.ToUpperInvariant();
                    foreach (var dependency in allDependencies)
                    {
                        if (dependency.ToUpperInvariant().Contains(dllDirectoryUppercase))
                        {
                            dependenciesFromDllDirectory.Add(dependency);
                        }
                    }

                    return dependenciesFromDllDirectory.ToArray();
                }
            }
            finally
            {
                if (appDomain != null)
                {
                    EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: unloading AppDomain...");
                    AppDomain.Unload(appDomain);
                    EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: unloading AppDomain succeeded.");
                }
            }
        }

        /// <summary>
        /// Gets the resolution paths for app domain creation.
        /// </summary>
        /// <returns> The <see cref="IList{T}"/> of resolution paths. </returns>
        internal IList<string> GetResolutionPaths()
        {
            // Use dictionary to ensure we get a list of unique paths, but keep a list as the
            // dictionary does not guarantee order.
            Dictionary<string, object> resolutionPathsDictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            List<string> resolutionPaths = new List<string>();

            // Add the path of the currently executing assembly (use Uri(CodeBase).LocalPath as Location can be on shadow dir).
            string currentlyExecutingAssembly = Path.GetDirectoryName(Path.GetFullPath(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath));
            resolutionPaths.Add(currentlyExecutingAssembly);
            resolutionPathsDictionary[currentlyExecutingAssembly] = null;

            // Add the application base for this domain.
            if (!resolutionPathsDictionary.ContainsKey(AppDomain.CurrentDomain.BaseDirectory))
            {
                resolutionPaths.Add(AppDomain.CurrentDomain.BaseDirectory);
                resolutionPathsDictionary[AppDomain.CurrentDomain.BaseDirectory] = null;
            }

            return resolutionPaths;
        }
    }
}
