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
    private static bool TryReadProviderMetadata(string path, out bool hasMarker, out AssemblyName[] references)
    {
        hasMarker = false;
        references = [];
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(stream, PEStreamOptions.PrefetchMetadata);
            MetadataReader reader = peReader.GetMetadataReader();
            AssemblyDefinition definition = reader.GetAssemblyDefinition();
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
            return true;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or SecurityException)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "TypeCache: Could not read assembly metadata from {0}; using runtime resolution for AssemblyFixtureProvider discovery. {1}",
                    path,
                    ex);
            }

            return false;
        }
    }

    private sealed class ProviderDiscoveryMetadataResolver
    {
#if NET && !WINDOWS_UWP
        private Dictionary<string, string?>? _trustedAssemblyPaths;
#endif
#if NETFRAMEWORK
        private bool? _hasCodeBaseOverrides;
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
                return _trustedAssemblyPaths.TryGetValue(referenceName.Name!, out string? path)
                    && path is not null
                    && HasMatchingProviderDiscoveryIdentity(referenceName, path) ? path : null;
#elif NETFRAMEWORK
                _ = referrer;
                AppDomain domain = AppDomain.CurrentDomain;
                AppDomainSetup setup = domain.SetupInformation;
                // Fusion can select a loaded image or the GAC before probing the application base.
                // Keep those bindings, culture probing, redirects and host overrides authoritative.
                if (domain.GetAssemblies().Any(assembly =>
                    string.Equals(assembly.GetName().Name, referenceName.Name, StringComparison.OrdinalIgnoreCase))
                    || referenceName.GetPublicKeyToken() is { Length: > 0 }
                    || !string.IsNullOrEmpty(referenceName.CultureName)
                    || setup.DisallowApplicationBaseProbing
                    || setup.PrivateBinPathProbe is not null
                    || setup.GetConfigurationBytes() is { Length: > 0 }
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DEVPATH"))
                    || !string.Equals(domain.ApplyPolicy(referenceName.FullName), referenceName.FullName, StringComparison.Ordinal)
                    || (_hasCodeBaseOverrides ??= HasCodeBaseOverrides()))
                {
                    return null;
                }

                foreach (string extension in new[] { ".dll", ".exe" })
                {
                    string path = Path.Combine(domain.BaseDirectory, referenceName.Name + extension);
                    if (File.Exists(path))
                    {
                        return HasMatchingProviderDiscoveryIdentity(referenceName, path) ? path : null;
                    }
                }
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

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)
        private static bool HasMatchingProviderDiscoveryIdentity(AssemblyName referenceName, string path)
            => string.Equals(referenceName.FullName, AssemblyName.GetAssemblyName(path).FullName, StringComparison.Ordinal);
#endif

#if NETFRAMEWORK
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
