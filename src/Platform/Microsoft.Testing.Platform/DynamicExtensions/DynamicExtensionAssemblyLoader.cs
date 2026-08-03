// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Runtime.Loader;
#endif

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Default <see cref="IDynamicExtensionAssemblyLoader"/>.
/// </summary>
/// <remarks>
/// <para>
/// On .NET, each distinct extension assembly is loaded into its own <c>AssemblyLoadContext</c> whose
/// dependencies are resolved from the extension's own <c>.deps.json</c>, so an extension cannot conflict with
/// the test application's dependency graph or with another extension's.
/// </para>
/// <para>
/// On .NET Framework (the netstandard2.0 asset) <c>AssemblyLoadContext</c> does not exist, so the assembly is
/// loaded with <c>Assembly.LoadFrom</c> and no isolation is available. <see cref="IsIsolated"/> reports this so
/// the caller can record it in the diagnostic log.
/// </para>
/// </remarks>
internal sealed class DynamicExtensionAssemblyLoader : IDynamicExtensionAssemblyLoader
{
    private readonly Dictionary<string, Assembly> _loadedAssemblies = [with(DynamicExtensionConstants.PathComparer)];
#if NETCOREAPP
    private readonly IFileSystem _fileSystem;
#endif

    public DynamicExtensionAssemblyLoader(IFileSystem fileSystem)
    {
#if NETCOREAPP
        _fileSystem = fileSystem;
#else
        // The netstandard2.0 asset cannot isolate, so it never probes for dependencies itself.
        _ = fileSystem;
#endif
    }

#if NETCOREAPP
    public bool IsIsolated => true;
#else
    public bool IsIsolated => false;
#endif

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming", Justification = "The extension assembly is an external file that is not part of the trimmed application, so trimming cannot remove anything it needs. Runtimes that cannot load assemblies dynamically are rejected earlier by DynamicExtensionLoader.")]
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Loading an assembly from a file is unsupported on some platforms (for example browser). The resulting PlatformNotSupportedException is wrapped by DynamicExtensionLoader into an actionable error naming the manifest and the extension.")]
    public Assembly LoadAssembly(string assemblyPath)
    {
        if (_loadedAssemblies.TryGetValue(assemblyPath, out Assembly? cached))
        {
            return cached;
        }

#if NETCOREAPP
        DynamicExtensionLoadContext context = new(assemblyPath, _fileSystem);
        Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
#else
        var assembly = Assembly.LoadFrom(assemblyPath);
#endif

        _loadedAssemblies.Add(assemblyPath, assembly);
        return assembly;
    }

#if NETCOREAPP
    private sealed class DynamicExtensionLoadContext : AssemblyLoadContext
    {
        private static readonly string[] AssemblyExtensions = [".dll", ".exe"];

        private readonly AssemblyDependencyResolver? _resolver;
        private readonly IFileSystem _fileSystem;
        private readonly string _directory;

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly. Dependency resolution failures degrade to probing the extension directory.")]
        public DynamicExtensionLoadContext(string assemblyPath, IFileSystem fileSystem)
            : base($"MicrosoftTestingPlatformDynamicExtension:{assemblyPath}", isCollectible: false)
        {
            _fileSystem = fileSystem;
            _directory = Path.GetDirectoryName(assemblyPath) ?? string.Empty;

            try
            {
                _resolver = new AssemblyDependencyResolver(assemblyPath);
            }
            catch (Exception)
            {
                // The extension was deployed without a usable .deps.json (for example xcopied). Fall back to
                // probing the extension's own directory, which still keeps its dependencies out of the host's
                // load context.
                _resolver = null;
            }
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly.")]
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly.")]
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // The platform contract must have a single identity across the host and every extension, otherwise
            // the ITestApplicationBuilder the extension sees is a different type from the one we pass to it —
            // and the same holds for the abstractions extensions exchange with each other. Matching by name is
            // stronger than relying on the resolver, because it does not depend on whether the assembly
            // happens to be deployed next to the extension.
            if (IsSharedContractAssembly(assemblyName.Name)
                && FindLoadedContractAssembly(assemblyName.Name) is { } shared)
            {
                return shared;
            }

            // Not a contract assembly, or the host does not carry it (an abstractions package the test
            // application never referenced). Fall through so the extension can still use its own copy: an
            // isolated copy is worse than a shared one but far better than failing to load at all.
            string? resolvedPath = _resolver?.ResolveAssemblyToPath(assemblyName);
            resolvedPath ??= ProbeDirectory(assemblyName);

            // Returning null delegates to the default context, which is what we want for framework assemblies.
            return resolvedPath is null ? null : LoadFromAssemblyPath(resolvedPath);
        }

        /// <summary>
        /// Finds an assembly already loaded in the default context by simple name, deliberately ignoring the
        /// requested version.
        /// </summary>
        /// <remarks>
        /// <see cref="AssemblyLoadContext.LoadFromAssemblyName(AssemblyName)"/> would apply version binding, so
        /// an extension compiled against a newer platform than the host would fail to bind, silently fall
        /// through to its own private copy, and end up with a different <c>ITestApplicationBuilder</c> identity
        /// — surfacing as a baffling "the type does not expose AddExtensions" error rather than the version
        /// mismatch it really is. Sharing the contract means sharing it by name; if the versions are genuinely
        /// incompatible, the resulting <see cref="MissingMethodException"/> is the honest failure, and the same
        /// one a statically referenced extension would produce.
        /// </remarks>
        private static Assembly? FindLoadedContractAssembly(string? simpleName)
        {
            foreach (Assembly loaded in Default.Assemblies)
            {
                if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                {
                    return loaded;
                }
            }

            return null;
        }

        private static bool IsSharedContractAssembly(string? simpleName)
        {
            foreach (string shared in DynamicExtensionConstants.SharedContractAssemblyNames)
            {
                if (string.Equals(simpleName, shared, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly.")]
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? resolvedPath = _resolver?.ResolveUnmanagedDllToPath(unmanagedDllName);
            return resolvedPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(resolvedPath);
        }

        private string? ProbeDirectory(AssemblyName assemblyName)
        {
            string? simpleName = assemblyName.Name;
            if (RoslynString.IsNullOrEmpty(simpleName) || _directory.Length == 0)
            {
                return null;
            }

            // Satellite assemblies live under a culture sub-directory by convention. Only reachable when the
            // extension was deployed without a usable .deps.json; otherwise the resolver handles them.
            string? culture = assemblyName.CultureName;
            string directory = RoslynString.IsNullOrEmpty(culture)
                ? _directory
                : Path.Combine(_directory, culture!);

            foreach (string extension in AssemblyExtensions)
            {
                string candidate = Path.Combine(directory, simpleName + extension);
                if (_fileSystem.ExistFile(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
#endif
}
