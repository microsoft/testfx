// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security;
#if NET && !WINDOWS_UWP
using System.Runtime.Loader;
#endif

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    private static ProviderMetadataReadResult TryReadProviderMetadata(string path, AssemblyName expectedIdentity, out bool hasMarker, out AssemblyName[] references)
    {
        hasMarker = false;
        references = [];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
            MetadataReader reader = peReader.GetMetadataReader();
            AssemblyDefinition definition = reader.GetAssemblyDefinition();

            // The runtime binds the exact requested identity; any other image at the probed path
            // means the runtime would select something else (or raise AssemblyResolve).
            if (!string.Equals(expectedIdentity.FullName, definition.GetAssemblyName().FullName, StringComparison.Ordinal))
            {
                return ProviderMetadataReadResult.NoMatchingImage;
            }

            foreach (CustomAttributeHandle handle in definition.GetCustomAttributes())
            {
                EntityHandle constructor = reader.GetCustomAttribute(handle).Constructor;
                EntityHandle type = constructor.Kind switch
                {
                    HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
                    HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
                    _ => default,
                };
                if (type.Kind == HandleKind.TypeReference)
                {
                    TypeReference attribute = reader.GetTypeReference((TypeReferenceHandle)type);
                    hasMarker = reader.StringComparer.Equals(attribute.Name, nameof(AssemblyFixtureProviderAttribute))
                        && reader.StringComparer.Equals(attribute.Namespace, typeof(AssemblyFixtureProviderAttribute).Namespace!);
                }
                else if (type.Kind == HandleKind.TypeDefinition)
                {
                    TypeDefinition attribute = reader.GetTypeDefinition((TypeDefinitionHandle)type);
                    hasMarker = reader.StringComparer.Equals(attribute.Name, nameof(AssemblyFixtureProviderAttribute))
                        && reader.StringComparer.Equals(attribute.Namespace, typeof(AssemblyFixtureProviderAttribute).Namespace!);
                }

                if (hasMarker)
                {
                    break;
                }
            }

            references = reader.AssemblyReferences.Select(handle => reader.GetAssemblyReference(handle).GetAssemblyName()).ToArray();
            return ProviderMetadataReadResult.Read;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or SecurityException or InvalidOperationException or ArgumentException)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "TypeCache: Could not read assembly metadata from {0}; using runtime resolution for AssemblyFixtureProvider discovery. {1}",
                    path,
                    ex);
            }

            return ProviderMetadataReadResult.Unreadable;
        }
    }

    private enum ProviderMetadataReadResult
    {
        Read,
        NoMatchingImage,
        Unreadable,
    }

    private sealed class ProviderDiscoveryMetadataResolver
    {
#if NET && !WINDOWS_UWP
        private Dictionary<string, string?>? _trustedAssemblyPaths;
#endif
#if NETFRAMEWORK
        private readonly Dictionary<Assembly, string?> _loadedAssemblyNames = [];
        private bool? _hasDomainBindingOverrides;
#endif

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The resolver caches runtime-specific metadata paths on other target frameworks.")]
        public string? Resolve(Assembly referrer, AssemblyName referenceName)
        {
            try
            {
#if NET && !WINDOWS_UWP
                AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(referrer) ?? AssemblyLoadContext.Default;
                // A component's deps/adjacent files do not describe Default's binding policy.
                // A later-loaded consumer may instead bind through Default.Resolving.
                if (context != AssemblyLoadContext.Default || context.Assemblies.Any(assembly =>
                    string.Equals(assembly.GetName().Name, referenceName.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return null;
                }

                _trustedAssemblyPaths ??= ReadTrustedAssemblyPaths();
                return _trustedAssemblyPaths.TryGetValue(referenceName.Name!, out string? path) ? path : null;
#elif NETFRAMEWORK
                _ = referrer;
                AppDomain domain = AppDomain.CurrentDomain;
                // Strong-named binds depend on signature verification, GAC and publisher/machine
                // policy. Keep the runtime authoritative even when ApplyPolicy returns the same name.
                // The identity read with the metadata rejects any other appbase image.
                if (referenceName.GetPublicKeyToken() is { Length: > 0 }
                    || !string.IsNullOrEmpty(referenceName.CultureName)
                    || (_hasDomainBindingOverrides ??= HasDomainBindingOverrides(domain))
                    || IsLoaded(domain, referenceName.Name!)
                    || !string.Equals(domain.ApplyPolicy(referenceName.FullName), referenceName.FullName, StringComparison.Ordinal))
                {
                    return null;
                }

                // Fusion probes [appbase]/name.dll first; any other hit (name/name.dll, private paths,
                // then .exe) depends on probing order we don't replicate, so leave it to the runtime.
                string path = Path.Combine(domain.BaseDirectory, referenceName.Name + ".dll");
                return File.Exists(path) ? path : null;
#else
                _ = referrer;
                _ = referenceName;
#endif
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or SecurityException or XmlException)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Could not resolve a metadata path for {0}; using runtime resolution for AssemblyFixtureProvider discovery. {1}",
                        referenceName.FullName,
                        ex);
                }
            }

            return null;
        }

#if NET && !WINDOWS_UWP
        private static Dictionary<string, string?> ReadTrustedAssemblyPaths()
        {
            Dictionary<string, string?> paths = [with(StringComparer.OrdinalIgnoreCase)];
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedPaths)
            {
                return paths;
            }

            foreach (string path in trustedPaths.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                paths[name] = paths.TryGetValue(name, out string? previous)
                    && !string.Equals(previous, path, StringComparison.Ordinal) ? null : path;
            }

            return paths;
        }
#endif

#if NETFRAMEWORK
        private static bool HasDomainBindingOverrides(AppDomain domain)
        {
            AppDomainSetup setup = domain.SetupInformation;
            return setup.DisallowApplicationBaseProbing
                || setup.PrivateBinPathProbe is not null
                || setup.GetConfigurationBytes() is { Length: > 0 }
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEVPATH"))
                || HasCodeBaseOverrides();
        }

        private bool IsLoaded(AppDomain domain, string name)
        {
            // Query the live list for every reference: discovery itself and user code can load
            // assemblies between checks. Only the per-assembly name lookup is cached.
            foreach (Assembly assembly in domain.GetAssemblies())
            {
                if (!_loadedAssemblyNames.TryGetValue(assembly, out string? loadedName))
                {
                    loadedName = assembly.GetName().Name;
                    _loadedAssemblyNames[assembly] = loadedName;
                }

                if (string.Equals(loadedName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCodeBaseOverrides()
        {
            string configurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (!File.Exists(configurationFile))
            {
                return false;
            }

            using var reader = XmlReader.Create(configurationFile, new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit });
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "codeBase")
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
