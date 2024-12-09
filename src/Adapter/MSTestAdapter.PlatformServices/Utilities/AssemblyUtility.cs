// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using System.Diagnostics.CodeAnalysis;
#if NETFRAMEWORK
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

/// <summary>
/// Utility for assembly specific functionality.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class AssemblyUtility
#if NETFRAMEWORK
    : IAssemblyUtility
#endif
{
    private readonly string[] _assemblyExtensions = [".dll", ".exe"];

#if NETFRAMEWORK
    /// <summary>
    /// Gets all supported culture names in Keys.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    private static HashSet<string> Cultures
    {
        get
        {
            if (field == null)
            {
                field = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (CultureInfo? info in CultureInfo.GetCultures(CultureTypes.AllCultures))
                {
                    field.Add(info.Name);
                }
            }

            return field;
        }
    }

    /// <summary>
    /// Loads an assembly into the reflection-only context, given its path.
    /// </summary>
    /// <param name="assemblyPath">The path of the file that contains the manifest of the assembly.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly ReflectionOnlyLoadFrom(string assemblyPath) => Assembly.ReflectionOnlyLoadFrom(assemblyPath);

    /// <summary>
    /// Loads an assembly into the reflection-only context, given its display name.
    /// </summary>
    /// <param name="assemblyString">The display name of the assembly, as returned by the System.Reflection.AssemblyName.FullName property.</param>
    /// <returns>The loaded assembly.</returns>
    public Assembly ReflectionOnlyLoad(string assemblyString) => Assembly.ReflectionOnlyLoad(assemblyString);
#endif

    /// <summary>
    /// Whether file extension is an assembly file extension.
    /// Returns true for .exe and .dll, otherwise false.
    /// </summary>
    /// <param name="extensionWithLeadingDot"> Extension containing leading dot, e.g. ".exe". </param>
    /// <remarks> Path.GetExtension() returns extension with leading dot. </remarks>
    /// <returns> True if this is an assembly extension. </returns>
    internal bool IsAssemblyExtension(string extensionWithLeadingDot)
        => _assemblyExtensions.Contains(extensionWithLeadingDot, StringComparer.OrdinalIgnoreCase);

#if NETFRAMEWORK
    /// <summary>
    /// Determines whether given file is managed assembly. Does not load the assembly. Does not check file extension.
    /// Performance: takes ~0.1 seconds on 2x CPU P4.
    /// </summary>
    /// <param name="path"> The path to the assembly. </param>
    /// <returns> True if managed assembly. </returns>
    internal static bool IsAssembly(string path)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(path), "path");
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
        if (!IsAssemblyExtension(Path.GetExtension(assemblyPath)) || !IsAssembly(assemblyPath))
        {
            EqtTrace.ErrorIf(
                    EqtTrace.IsErrorEnabled,
                    "AssemblyUtilities.GetSatelliteAssemblies: the specified file '{0}' is not managed assembly.",
                    assemblyPath);
            Debug.Fail("AssemblyUtilities.GetSatelliteAssemblies: the file '" + assemblyPath + "' is not an assembly.");

            // If e.g. this is unmanaged dll, we don't care about the satellites.
            return [];
        }

        assemblyPath = Path.GetFullPath(assemblyPath);
        string assemblyFileName = Path.GetFileName(assemblyPath);
        string assemblyDir = Path.GetDirectoryName(assemblyPath);
        var satellites = new List<string>();

        // Directory.Exists for 266 dirs takes 9ms while Path.GetDirectories can take up to 80ms on 10k dirs.
        foreach (string dir in Cultures)
        {
            string dirPath = Path.Combine(assemblyDir, dir);
            if (!Directory.Exists(dirPath))
            {
                continue;
            }

            // Check if the satellite exists in this dir.
            // We check filenames like: MyAssembly.dll -> MyAssembly.resources.dll.
            // Surprisingly, but both DLL and EXE are found by resource manager.
            foreach (string extension in _assemblyExtensions)
            {
                // extension contains leading dot.
                string satellite = Path.ChangeExtension(assemblyFileName, "resources" + extension);
                string satellitePath = Path.Combine(assemblyDir, Path.Combine(dir, satellite));

                // We don't use Assembly.LoadFrom/Assembly.GetSatelliteAssemblies because this is rather slow
                // (1620ms for 266 cultures when directories do not exist).
                if (File.Exists(satellitePath))
                {
                    // If the satellite found is not a managed assembly we do not report it as a reference.
                    if (!IsAssembly(satellitePath))
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
    /// <returns> The <see cref="string"/>[]. </returns>
    internal virtual /* for mocking purposes - should be refactored */ IReadOnlyList<string> GetFullPathToDependentAssemblies(string assemblyPath, string? configFile, out IList<string> warnings)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(assemblyPath), "assemblyPath");

        EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: start.");

        AppDomainSetup setupInfo = new();
        string dllDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath));
        setupInfo.ApplicationBase = dllDirectory;

        DebugEx.Assert(StringEx.IsNullOrEmpty(configFile) || File.Exists(configFile), $"Config file is specified but does not exist: {configFile}");

        AppDomainUtilities.SetConfigurationFile(setupInfo, configFile);

        EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: Using config file: '{0}'.", setupInfo.ConfigurationFile);

        setupInfo.LoaderOptimization = LoaderOptimization.MultiDomainHost;

        AppDomain? appDomain = null;
        try
        {
            // Force loading the resource assembly into the current domain so that we can get the resource strings even
            // when the custom assembly resolver kicks in.
            // This should not be required based on the algorithm followed during the satellite assembly resolution
            // https://learn.microsoft.com/dotnet/core/extensions/package-and-deploy-resources#net-framework-resource-fallback-process
            // BUT for some unknown reason the point 10 is not working as explained.
            // Satellite resolution should fallback to the NeutralResourcesLanguageAttribute that we set to en but don't
            // resulting in a FileNotFoundException.
            // See https://github.com/microsoft/testfx/issues/1598 for the error and https://github.com/microsoft/vstest/pull/4150
            // for the idea of the fix.
            _ = string.Format(CultureInfo.InvariantCulture, Resource.CannotFindFile, string.Empty);

            appDomain = AppDomain.CreateDomain("Dependency finder domain", null, setupInfo);
            if (EqtTrace.IsInfoEnabled)
            {
                EqtTrace.Info("AssemblyDependencyFinder.GetDependentAssemblies: Created AppDomain.");
            }

            Type assemblyResolverType = typeof(AssemblyResolver);

            EqtTrace.SetupRemoteEqtTraceListeners(appDomain);

            // This has to be LoadFrom, otherwise we will have to use AssemblyResolver to find self.
            using var resolver =
                    (AssemblyResolver)AppDomainUtilities.CreateInstance(
                                                appDomain,
                                                assemblyResolverType,
                                                [GetResolutionPaths()]);

            // This has to be Load, otherwise Serialization of argument types will not work correctly.
            var worker =
                (AssemblyLoadWorker)AppDomainUtilities.CreateInstance(appDomain, typeof(AssemblyLoadWorker), null);

            EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, "AssemblyDependencyFinder.GetDependentAssemblies: loaded the worker.");

            IReadOnlyCollection<string> allDependencies = worker.GetFullPathToDependentAssemblies(assemblyPath, out warnings);
            var dependenciesFromDllDirectory = new List<string>();
            string dllDirectoryUppercase = dllDirectory.ToUpperInvariant();
            foreach (string dependency in allDependencies)
            {
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                if (dependency.ToUpperInvariant().Contains(dllDirectoryUppercase))
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                {
                    dependenciesFromDllDirectory.Add(dependency);
                }
            }

            return dependenciesFromDllDirectory;
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
    internal static IList<string> GetResolutionPaths()
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        // Use the path of the currently executing assembly (use Uri(CodeBase).LocalPath as Location can be on shadow dir).
        string executingAssembly = Path.GetDirectoryName(Path.GetFullPath(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath))!;

        // Add the application base for this domain.
        return string.Equals(executingAssembly, baseDirectory, StringComparison.OrdinalIgnoreCase) ?
            [executingAssembly] :
            [executingAssembly, baseDirectory];
    }
#endif
}

#endif
