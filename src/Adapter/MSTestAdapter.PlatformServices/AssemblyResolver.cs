// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || NET
using System.Diagnostics;
using System.Reflection;
#if NETFRAMEWORK
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Security.Permissions;
#endif

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Helps resolve MSTestFramework assemblies for CLR loader.
/// The idea is that Unit Test Adapter creates App Domain for running tests and sets AppBase to tests dir.
/// Since we don't want to put our assemblies to GAC and they are not in tests dir, we use custom way to resolve them.
/// </summary>
#if NETFRAMEWORK
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public
#else
internal sealed
#endif
class AssemblyResolver :
#if NETFRAMEWORK
        MarshalByRefObject,
#endif
    IDisposable
{
    /// <summary>
    /// The assembly name of the dll containing logger APIs(EqtTrace) from the TestPlatform.
    /// </summary>
    /// <remarks>
    /// The reason we have this is because the AssemblyResolver itself logs information during resolution.
    /// If the resolver is called for the assembly containing the logger APIs, we do not log so as to prevent a stack overflow.
    /// </remarks>
    private const string LoggerAssemblyNameLegacy = "Microsoft.VisualStudio.TestPlatform.ObjectModel";

    /// <summary>
    /// The assembly name of the dll containing logger APIs(EqtTrace) from the TestPlatform.
    /// </summary>
    /// <remarks>
    /// The reason we have this is because the AssemblyResolver itself logs information during resolution.
    /// If the resolver is called for the assembly containing the logger APIs, we do not log so as to prevent a stack overflow.
    /// </remarks>
    private const string LoggerAssemblyName = "Microsoft.TestPlatform.CoreUtilities";

    /// <summary>
    /// The name of the current assembly resources file.
    /// </summary>
    /// <remarks>
    /// When resolving the resources for the current assembly, we need to make sure that we do not log. Otherwise, we will end
    /// up either failing or at least printing warning messages to the user about how we could not load the resources dll even
    /// when it's not an error. For example, set a culture outside of supported cultures (e.g. en-gb) and you will have an error
    /// saying we could not find en-gb resource dll which is normal. For more information,
    /// <see href="https://github.com/microsoft/testfx/issues/1598" />.
    /// </remarks>
    private const string PlatformServicesResourcesName = "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.resources";

    /// <summary>
    /// This will have the list of all directories read from runsettings.
    /// </summary>
    private readonly Queue<RecursiveDirectoryPath> _directoryList;

    /// <summary>
    /// The directories to look for assemblies to resolve.
    /// </summary>
    private readonly List<string> _searchDirectories;

    /// <summary>
    /// Dictionary of Assemblies discovered to date.
    /// </summary>
    private readonly Dictionary<string, Assembly?> _resolvedAssemblies = [];

    /// <summary>
    /// Dictionary of Reflection-Only Assemblies discovered to date.
    /// </summary>
    private readonly Dictionary<string, Assembly?> _reflectionOnlyResolvedAssemblies = [];

    /// <summary>
    /// lock for the loaded assemblies cache.
    /// </summary>
    private readonly Lock _syncLock = new();

    private static List<string>? s_currentlyLoading;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyResolver"/> class.
    /// </summary>
    /// <param name="directories">
    /// A list of directories for resolution path.
    /// </param>
    /// <remarks>
    /// If there are additional paths where a recursive search is required
    /// call AddSearchDirectoryFromRunSetting method with that list.
    /// </remarks>
    public AssemblyResolver(IList<string> directories)
    {
        Guard.NotNullOrEmpty(directories);

        _searchDirectories = [.. directories];
        _directoryList = new Queue<RecursiveDirectoryPath>();

        // In source gen mode don't register any custom resolver. We can still resolve in the same folder,
        // but nothing more.
        if (!SourceGeneratorToggle.UseSourceGenerator)
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(OnResolve);
#if NETFRAMEWORK
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(ReflectionOnlyOnResolve);

            // This is required for winmd resolution for arm built sources discovery on desktop.
            WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += new EventHandler<NamespaceResolveEventArgs>(WindowsRuntimeMetadataReflectionOnlyNamespaceResolve);
#endif
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="AssemblyResolver"/> class.
    /// </summary>
    ~AssemblyResolver()
    {
        Dispose(false);
    }

    /// <summary>
    /// The dispose.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

#if NETFRAMEWORK
    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <remarks>
    /// Note that LinkDemand is needed by FxCop.
    /// </remarks>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
    public override object? InitializeLifetimeService() => null;
#endif

    /// <summary>
    /// It will add a list of search directories path with property recursive/non-recursive in assembly resolver .
    /// </summary>
    /// <param name="recursiveDirectoryPath">
    /// The recursive Directory Path.
    /// </param>
#if NETFRAMEWORK
    public
#else
    internal
#endif
    void AddSearchDirectoriesFromRunSetting(List<RecursiveDirectoryPath> recursiveDirectoryPath)
    {
        // Enqueue elements from the list in Queue
        if (recursiveDirectoryPath == null)
        {
            return;
        }

        foreach (RecursiveDirectoryPath recPath in recursiveDirectoryPath)
        {
            _directoryList.Enqueue(recPath);
        }
    }

#if NETFRAMEWORK
    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    internal Assembly? ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
        => OnResolveInternal(sender, args, true);
#endif

    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <returns> The <see cref="Assembly"/>.  </returns>
    internal Assembly? OnResolve(object? sender, ResolveEventArgs args)
        => OnResolveInternal(sender, args, false);

    /// <summary>
    /// Adds the subdirectories of the provided path to the collection.
    /// </summary>
    /// <param name="path"> Path go get subdirectories for. </param>
    /// <param name="searchDirectories"> The search Directories. </param>
    internal
#if NET
    static
#endif
    void AddSubdirectories(string path, List<string> searchDirectories)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(path), "'path' cannot be null or empty.");
        DebugEx.Assert(searchDirectories != null, "'searchDirectories' cannot be null.");

        // If the directory exists, get it's subdirectories
        if (DoesDirectoryExist(path))
        {
            // Get the directories in the path provided.
            string[] directories = GetDirectories(path);

            // Add each directory and its subdirectories to the collection.
            foreach (string directory in directories)
            {
                searchDirectories.Add(directory);

                AddSubdirectories(directory, searchDirectories);
            }
        }
    }

    /// <summary>
    /// The dispose.
    /// </summary>
    /// <param name="disposing">
    /// The disposing.
    /// </param>
#if NETFRAMEWORK
    protected virtual
#else
    private
#endif
    void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // cleanup Managed resources like calling dispose on other managed object created.
                AppDomain.CurrentDomain.AssemblyResolve -= OnResolve;

#if NETFRAMEWORK
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= ReflectionOnlyOnResolve;
                WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve -= WindowsRuntimeMetadataReflectionOnlyNamespaceResolve;
#endif
            }

            // cleanup native resources
            _disposed = true;
        }
    }

    /// <summary>
    /// Verifies if a directory exists.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    bool DoesDirectoryExist(string path) => Directory.Exists(path);

    /// <summary>
    /// Gets the directories from a path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>A list of directories in path.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    string[] GetDirectories(string path) => Directory.GetDirectories(path);

#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    bool DoesFileExist(string filePath) => File.Exists(filePath);

#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif

    // This whole class is not used in source generator mode.
#pragma warning disable IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming
    Assembly LoadAssemblyFrom(string path) => Assembly.LoadFrom(path);
#pragma warning restore IL2026 // Members attributed with RequiresUnreferencedCode may break when trimming

#if NETFRAMEWORK
    protected virtual Assembly ReflectionOnlyLoadAssemblyFrom(string path) => Assembly.ReflectionOnlyLoadFrom(path);
#endif

    /// <summary>
    /// It will search for a particular assembly in the given list of directory.
    /// </summary>
    /// <param name="searchDirectorypaths"> The search Directorypaths. </param>
    /// <param name="name"> The name. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
#if NETFRAMEWORK
    protected virtual
#else
    private
#endif
    Assembly? SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
    {
        if (searchDirectorypaths == null || searchDirectorypaths.Count == 0)
        {
            return null;
        }

        // args.Name is like: "Microsoft.VisualStudio.TestTools.Common, Version=[VersionMajor].0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a".
        AssemblyName? requestedName;

        try
        {
            // Can throw ArgumentException, FileLoadException if arg is empty/wrong format, etc. Should not return null.
            requestedName = new AssemblyName(name);
        }
        catch (Exception ex)
        {
            SafeLog(
                name,
                () =>
                {
                    if (EqtTrace.IsInfoEnabled)
                    {
                        EqtTrace.Info(
                            "MSTest.AssemblyResolver.OnResolve: Failed to create assemblyName '{0}'. Reason: {1} ",
                            name,
                            ex);
                    }
                });

            return null;
        }

        DebugEx.Assert(requestedName != null && !StringEx.IsNullOrEmpty(requestedName.Name), "MSTest.AssemblyResolver.OnResolve: requested is null or name is empty!");

        foreach (string dir in searchDirectorypaths)
        {
            if (StringEx.IsNullOrEmpty(dir))
            {
                continue;
            }

            SafeLog(
                name,
                () =>
                {
                    if (EqtTrace.IsVerboseEnabled)
                    {
                        EqtTrace.Verbose("MSTest.AssemblyResolver.OnResolve: Searching assembly '{0}' in the directory '{1}'", requestedName.Name, dir);
                    }
                });

            foreach (string extension in new string[] { ".dll", ".exe" })
            {
                string assemblyPath = Path.Combine(dir, requestedName.Name + extension);

                bool isPushed = false;
                bool isResource = requestedName.Name.EndsWith(".resources", StringComparison.InvariantCulture);
                if (isResource)
                {
                    // Are we recursively looking up the same resource?  Note - our backout code will set
                    // the ResourceHelper's currentlyLoading stack to null if an exception occurs.
                    if (s_currentlyLoading != null && s_currentlyLoading.Count > 0 && s_currentlyLoading.LastIndexOf(assemblyPath) != -1)
                    {
                        SafeLog(
                            name,
                            () =>
                            {
                                if (EqtTrace.IsInfoEnabled)
                                {
                                    EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Assembly '{0}' is searching for itself recursively '{1}', returning as not found.", name, assemblyPath);
                                }
                            });
                        _resolvedAssemblies[name] = null;
                        return null;
                    }

                    s_currentlyLoading ??= new List<string>();
                    s_currentlyLoading.Add(assemblyPath); // Push
                    isPushed = true;
                }

                Assembly? assembly = SearchAndLoadAssembly(assemblyPath, name, requestedName, isReflectionOnly);
                if (isResource && isPushed)
                {
                    DebugEx.Assert(s_currentlyLoading is not null, "_currentlyLoading should not be null");
                    s_currentlyLoading.RemoveAt(s_currentlyLoading.Count - 1); // Pop
                }

                if (assembly != null)
                {
                    return assembly;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Verifies that found assembly name matches requested to avoid security issues.
    /// Looks only at PublicKeyToken and Version, empty matches anything.
    /// </summary>
    /// <param name="requestedName"> The requested Name. </param>
    /// <param name="foundName"> The found Name. </param>
    /// <returns> The <see cref="bool"/>. </returns>
    private static bool RequestedAssemblyNameMatchesFound(AssemblyName requestedName, AssemblyName foundName)
    {
        DebugEx.Assert(requestedName != null, "requested assembly name should not be null.");
        DebugEx.Assert(foundName != null, "found assembly name should not be null.");

        byte[]? requestedPublicKey = requestedName.GetPublicKeyToken();
        if (requestedPublicKey != null)
        {
            byte[]? foundPublicKey = foundName.GetPublicKeyToken();
            if (foundPublicKey == null)
            {
                return false;
            }

            for (int i = 0; i < requestedPublicKey.Length; ++i)
            {
                if (requestedPublicKey[i] != foundPublicKey[i])
                {
                    return false;
                }
            }
        }

        return requestedName.Version == null || requestedName.Version.Equals(foundName.Version);
    }

#if NETFRAMEWORK
    /// <summary>
    /// Event handler for windows winmd resolution.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    private void WindowsRuntimeMetadataReflectionOnlyNamespaceResolve(object sender, NamespaceResolveEventArgs args)
    {
        // Note: This will throw on pre-Win8 OS versions
        IEnumerable<string> fileNames = WindowsRuntimeMetadata.ResolveNamespace(
            args.NamespaceName,
            null,   // Will use OS installed .winmd files, you can pass explicit Windows SDK path here for searching 1st party WinRT types
            _searchDirectories);  // You can pass package graph paths, they will be used for searching .winmd files with 3rd party WinRT types

        foreach (string fileName in fileNames)
        {
            args.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(fileName));
        }
    }
#endif

    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="senderAppDomain"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>.  </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "senderAppDomain", Justification = "This is an event handler.")]
#pragma warning disable IDE0060 // Remove unused parameter
    private Assembly? OnResolveInternal(object? senderAppDomain, ResolveEventArgs args, bool isReflectionOnly)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (StringEx.IsNullOrEmpty(args.Name))
        {
            Debug.Fail("MSTest.AssemblyResolver.OnResolve: args.Name is null or empty.");
            return null;
        }

        SafeLog(
            args.Name,
            () =>
            {
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Resolving assembly '{0}'", args.Name);
                }
            });

        string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

        SafeLog(
            assemblyNameToLoad,
            () =>
            {
                if (EqtTrace.IsInfoEnabled)
                {
                    EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Resolving assembly after applying policy '{0}'", assemblyNameToLoad);
                }
            });

        lock (_syncLock)
        {
            // Since both normal and reflection only cache are accessed in same block, putting only one lock should be sufficient.
            if (TryLoadFromCache(assemblyNameToLoad, isReflectionOnly, out Assembly? assembly))
            {
                return assembly;
            }

            assembly = SearchAssembly(_searchDirectories, assemblyNameToLoad, isReflectionOnly);
            if (assembly != null)
            {
                return assembly;
            }

            // required assembly is not present in searchDirectories??
            // see, if we can find it in user specified search directories.
            while (assembly == null && _directoryList?.Count > 0)
            {
                // instead of loading whole search directory in one time, we are adding directory on the basis of need
                RecursiveDirectoryPath currentNode = _directoryList.Dequeue();

                List<string> incrementalSearchDirectory = [];

                if (DoesDirectoryExist(currentNode.DirectoryPath))
                {
                    incrementalSearchDirectory.Add(currentNode.DirectoryPath);

                    if (currentNode.IncludeSubDirectories)
                    {
                        // Add all its sub-directory in depth first search order.
                        AddSubdirectories(currentNode.DirectoryPath, incrementalSearchDirectory);
                    }

                    // Add this directory list in this.searchDirectories so that when we will try to resolve some other
                    // assembly, then it will look in this whole directory first.
                    _searchDirectories.AddRange(incrementalSearchDirectory);

                    assembly = SearchAssembly(incrementalSearchDirectory, assemblyNameToLoad, isReflectionOnly);
                }
                else
                {
                    // generate warning that path does not exist.
                    SafeLog(
                        assemblyNameToLoad,
                        () =>
                        {
                            if (EqtTrace.IsWarningEnabled)
                            {
                                EqtTrace.Warning(
                                "MSTest.AssemblyResolver.OnResolve: the directory '{0}', does not exist",
                                currentNode.DirectoryPath);
                            }
                        });
                }
            }

            if (assembly != null)
            {
                return assembly;
            }

            // Try for default load for System dlls that can't be found in search paths. Needs to loaded just by name.
            try
            {
#if NETFRAMEWORK
                if (isReflectionOnly)
                {
                    // Put it in the resolved assembly cache so that if the Load call below
                    // triggers another assembly resolution, then we don't end up in stack overflow.
                    _reflectionOnlyResolvedAssemblies[assemblyNameToLoad] = null;

                    assembly = Assembly.ReflectionOnlyLoad(assemblyNameToLoad);

                    if (assembly != null)
                    {
                        _reflectionOnlyResolvedAssemblies[assemblyNameToLoad] = assembly;
                    }

                    return assembly;
                }
#endif

                // Put it in the resolved assembly cache so that if the Load call below
                // triggers another assembly resolution, then we don't end up in stack overflow.
                _resolvedAssemblies[assemblyNameToLoad] = null;

                assembly = Assembly.Load(assemblyNameToLoad);

                if (assembly != null)
                {
                    _resolvedAssemblies[assemblyNameToLoad] = assembly;
                }

                return assembly;
            }
            catch (Exception ex)
            {
                SafeLog(
                    args.Name,
                    () =>
                    {
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason: {1}", assemblyNameToLoad, ex);
                        }
                    });
            }

            return assembly;
        }
    }

    /// <summary>
    /// Load assembly from cache if available.
    /// </summary>
    /// <param name="assemblyName"> The assembly Name. </param>
    /// <param name="isReflectionOnly">Indicates if this is a reflection-only context.</param>
    /// <param name="assembly"> The assembly. </param>
    /// <returns> The <see cref="bool"/>. </returns>
    private bool TryLoadFromCache(string assemblyName, bool isReflectionOnly, out Assembly? assembly)
    {
        bool isFoundInCache = isReflectionOnly
            ? _reflectionOnlyResolvedAssemblies.TryGetValue(assemblyName, out assembly)
            : _resolvedAssemblies.TryGetValue(assemblyName, out assembly);
        if (isFoundInCache)
        {
            SafeLog(
                assemblyName,
                () =>
                {
                    if (EqtTrace.IsInfoEnabled)
                    {
                        EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Resolved '{0}'", assemblyName);
                    }
                });
            return true;
        }

        return false;
    }

    /// <summary>
    /// Call logger APIs safely. We do not want a stackoverflow when objectmodel assembly itself
    /// is being resolved and an EqtTrace message prompts the load of the same dll again.
    /// CLR does not trigger a load when the EqtTrace messages are in a lambda expression. Leaving it that way
    /// to preserve readability instead of creating wrapper functions.
    /// </summary>
    /// <param name="assemblyName">The assembly being resolved.</param>
    /// <param name="loggerAction">The logger function.</param>
    private static void SafeLog(string? assemblyName, Action loggerAction)
    {
        // Logger assembly was in `Microsoft.VisualStudio.TestPlatform.ObjectModel` assembly in legacy versions and we need to omit it as well.
        if (!StringEx.IsNullOrEmpty(assemblyName)
            && !assemblyName.StartsWith(LoggerAssemblyName, StringComparison.Ordinal)
            && !assemblyName.StartsWith(LoggerAssemblyNameLegacy, StringComparison.Ordinal)
            && !assemblyName.StartsWith(PlatformServicesResourcesName, StringComparison.Ordinal))
        {
            loggerAction.Invoke();
        }
    }

    /// <summary>
    /// Search for assembly and if exists then load.
    /// </summary>
    /// <param name="assemblyPath"> The assembly Path. </param>
    /// <param name="assemblyName"> The assembly Name. </param>
    /// <param name="requestedName"> The requested Name. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom", Justification = "The assembly location is figured out from the configuration that the user passes in.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private Assembly? SearchAndLoadAssembly(string assemblyPath, string assemblyName, AssemblyName requestedName, bool isReflectionOnly)
    {
        try
        {
            if (!DoesFileExist(assemblyPath))
            {
                return null;
            }

            var foundName = AssemblyName.GetAssemblyName(assemblyPath);

            if (!RequestedAssemblyNameMatchesFound(requestedName, foundName))
            {
                return null; // File exists but version/public key is wrong. Try next extension.
            }

            Assembly assembly;

#if NETFRAMEWORK
            if (isReflectionOnly)
            {
                assembly = ReflectionOnlyLoadAssemblyFrom(assemblyPath);
                _reflectionOnlyResolvedAssemblies[assemblyName] = assembly;
            }
            else
#endif
            {
                assembly = LoadAssemblyFrom(assemblyPath);
                _resolvedAssemblies[assemblyName] = assembly;
            }

            SafeLog(
                assemblyName,
                () =>
                    {
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Resolved assembly '{0}'", assemblyName);
                        }
                    });

            return assembly;
        }
        catch (FileLoadException ex)
        {
            SafeLog(
                assemblyName,
                () =>
                    {
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ", assemblyName, ex);
                        }
                    });

            // Re-throw FileLoadException, because this exception means that the assembly
            // was found, but could not be loaded. This will allow us to report a more
            // specific error message to the user for things like access denied.
            throw;
        }
        catch (Exception ex)
        {
            // For all other exceptions, try the next extension.
            SafeLog(
                assemblyName,
                () =>
                    {
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ", assemblyName, ex);
                        }
                    });
        }

        return null;
    }
}
#endif
