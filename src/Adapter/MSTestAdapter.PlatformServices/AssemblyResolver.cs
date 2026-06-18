// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)
#if NETFRAMEWORK
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Security.Permissions;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Helps resolve MSTestFramework assemblies for CLR loader.
/// The idea is that Unit Test Adapter creates App Domain for running tests and sets AppBase to tests dir.
/// Since we don't want to put our assemblies to GAC and they are not in tests dir, we use custom way to resolve them.
/// </summary>
#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests on .NET Framework.
internal
#if !NETFRAMEWORK
    sealed
#endif
    partial class AssemblyResolver :
#if NETFRAMEWORK
        MarshalByRefObject,
#endif
    IDisposable
{
    /// <summary>
    /// The assembly name of the dll containing logger APIs(PlatformServiceProvider.Instance.AdapterTraceLogger) from the TestPlatform.
    /// </summary>
    /// <remarks>
    /// The reason we have this is because the AssemblyResolver itself logs information during resolution.
    /// If the resolver is called for the assembly containing the logger APIs, we do not log so as to prevent a stack overflow.
    /// </remarks>
    private const string LoggerAssemblyNameLegacy = "Microsoft.VisualStudio.TestPlatform.ObjectModel";

    /// <summary>
    /// The assembly name of the dll containing logger APIs(PlatformServiceProvider.Instance.AdapterTraceLogger) from the TestPlatform.
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
#if NET9_0_OR_GREATER
    private readonly Lock _syncLock = new();
#else
    private readonly object _syncLock = new();
#endif

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
        if (directories is null)
        {
            throw new ArgumentNullException(nameof(directories));
        }

        // Caller always ensures non-empty.
        if (directories.Count == 0)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        _searchDirectories = [.. directories];
        _directoryList = new Queue<RecursiveDirectoryPath>();

        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(OnResolve);
#if NETFRAMEWORK
        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(ReflectionOnlyOnResolve);

        // This is required for winmd resolution for arm built sources discovery on desktop.
        WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += new EventHandler<NamespaceResolveEventArgs>(WindowsRuntimeMetadataReflectionOnlyNamespaceResolve);
#endif
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
}
#endif
