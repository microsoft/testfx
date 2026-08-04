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

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming", Justification = "Loading an external assembly is the feature, and trimming cannot remove anything from that file since it is not part of the application. It can remove anything the *application* did not reference, including BCL members, so an extension calling one of those can load and then fail at run time -- a documented limitation of combining trimming with dynamic extensions (see docs/RFCs/023-Dynamic-Extension-Loading.md) rather than something this call site can fix. Runtimes that cannot load assemblies dynamically are rejected earlier by DynamicExtensionLoader.")]
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
            // and the same holds for the abstractions extensions exchange with each other. A listed contract is
            // therefore never loaded into this context, even when only the extension carries it.
            if (IsSharedContractAssembly(assemblyName.Name))
            {
                return ResolveSharedContractAssembly(assemblyName);
            }

            string? resolvedPath = _resolver?.ResolveAssemblyToPath(assemblyName);
            resolvedPath ??= ProbeDirectory(assemblyName);

            // Delegating to the default context is required, not merely convenient: the resolver returns no
            // path for framework assemblies, so an extension that could not fall back would fail on
            // System.Runtime. The cost is that a *missing* private dependency also falls through and can bind
            // to an application assembly of the same name. That is a deployment mistake rather than a trust
            // problem — both directories are fully trusted application folders — and the two cases are
            // indistinguishable here, since neither resolved. See the RFC's open questions.
            return resolvedPath is null ? null : LoadFromAssemblyPath(resolvedPath);
        }

        /// <summary>
        /// Resolves a shared contract assembly to the one copy every extension and the host must agree on,
        /// deliberately ignoring the version the extension was compiled against.
        /// </summary>
        /// <remarks>
        /// <para>
        /// An already-loaded copy is preferred first. Passing the extension's full <see cref="AssemblyName"/> to
        /// <see cref="AssemblyLoadContext.LoadFromAssemblyName(AssemblyName)"/> would apply version binding, so
        /// an extension compiled against a newer platform than the host would fail to bind, silently fall
        /// through to its own private copy, and end up with a different <c>ITestApplicationBuilder</c> identity
        /// — surfacing as a baffling "the type does not expose AddExtensions" error rather than the version
        /// mismatch it really is. If the versions are genuinely incompatible, the resulting
        /// <see cref="MissingMethodException"/> is the honest failure, and the same one a statically referenced
        /// extension would produce.
        /// </para>
        /// <para>
        /// Being loaded is not the same as being available. Dynamic hooks run before the statically registered
        /// extensions (RFC section 4), so a contract such as
        /// <c>Microsoft.Testing.Extensions.TrxReport.Abstractions</c> is frequently present in the application's
        /// dependency graph but not yet touched. Settling for the extension's private copy in that window would
        /// split the contract the moment the static extension loaded the real one, so when nothing is loaded yet
        /// the default context is asked to load it — by simple name only, which cannot fail on version.
        /// </para>
        /// <para>
        /// Finally, the application may not carry the contract at all, which is the ordinary manifest-only
        /// deployment: the extension brings its own copy. Loading that copy into *this* context would give every
        /// extension its own identity, so a capability implemented by one dynamic extension would be invisible to
        /// another — exactly the split the shared list exists to prevent. The first copy found is therefore
        /// promoted into the default context, making it canonical for every extension that follows.
        /// </para>
        /// </remarks>
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly.")]
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "See DynamicExtensionAssemblyLoader.LoadAssembly.")]
        private Assembly? ResolveSharedContractAssembly(AssemblyName assemblyName)
        {
            string? simpleName = assemblyName.Name;
            if (RoslynString.IsNullOrEmpty(simpleName))
            {
                return null;
            }

            foreach (Assembly loaded in Default.Assemblies)
            {
                if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                {
                    return loaded;
                }
            }

            try
            {
                return Default.LoadFromAssemblyName(new AssemblyName(simpleName!));
            }
            catch (FileNotFoundException)
            {
                // Genuinely absent from the host. Only this case falls through: a FileLoadException or
                // BadImageFormatException means the host does carry the contract but something is wrong with it,
                // and silently substituting a private copy would hide that behind a type-identity mismatch.
            }

            string? resolvedPath = _resolver?.ResolveAssemblyToPath(assemblyName) ?? ProbeDirectory(assemblyName);
            return resolvedPath is null ? null : Default.LoadFromAssemblyPath(resolvedPath);
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
