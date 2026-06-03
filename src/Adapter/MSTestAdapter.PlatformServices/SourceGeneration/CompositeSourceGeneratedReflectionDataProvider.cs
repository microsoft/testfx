// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Merges multiple per-assembly <see cref="SourceGeneratedReflectionDataProvider"/> instances into
/// a single provider so that more than one test assembly can be registered with
/// <see cref="ReflectionMetadataHook"/> in a single process.
/// </summary>
/// <remarks>
/// Thread-safety: every mutation rebuilds an immutable <see cref="CompositeState"/> from scratch
/// and publishes it via a single atomic field write (<c>Volatile.Write</c>). Readers go through
/// <see cref="GetSnapshot"/> / the per-provider lookup overrides which read the field with
/// <c>Volatile.Read</c>, so they always see a fully-constructed point-in-time view (no torn
/// dictionaries) even though they do not take a lock. Callers of
/// <see cref="Add(SourceGeneratedReflectionDataProvider)"/> are expected to serialize themselves
/// (today via <see cref="ReflectionMetadataHook"/>'s lock).
/// </remarks>
internal sealed class CompositeSourceGeneratedReflectionDataProvider : SourceGeneratedReflectionDataProvider
{
    private CompositeState _state = CompositeState.Empty;

    public void Add(SourceGeneratedReflectionDataProvider provider)
    {
        CompositeState previous = Volatile.Read(ref _state);
        CompositeState next = previous.With(provider);
        Volatile.Write(ref _state, next);
    }

    internal override SourceGeneratedReflectionDataProvider GetSnapshot()
        => Volatile.Read(ref _state).MergedSnapshot;

    internal override bool TryGetAssembly(string assemblyPath, [NotNullWhen(true)] out Assembly? assembly)
    {
        string name = Path.GetFileNameWithoutExtension(assemblyPath);
        CompositeState state = Volatile.Read(ref _state);
        if (state.ProvidersByAssemblyName.TryGetValue(name, out SourceGeneratedReflectionDataProvider? provider)
            && provider.Assembly is { } resolved)
        {
            assembly = resolved;
            return true;
        }

        assembly = null;
        return false;
    }

    internal override void GetNavigationData(string className, string methodName, out int minLineNumber, out string? fileName)
    {
        CompositeState state = Volatile.Read(ref _state);
        foreach (SourceGeneratedReflectionDataProvider provider in state.Providers)
        {
            provider.GetNavigationData(className, methodName, out minLineNumber, out fileName);
            if (fileName is not null)
            {
                return;
            }
        }

        minLineNumber = 0;
        fileName = null;
    }

    internal override object[] GetAssemblyAttributes(Assembly assembly)
    {
        CompositeState state = Volatile.Read(ref _state);
        return state.ProvidersByAssembly.TryGetValue(assembly, out SourceGeneratedReflectionDataProvider? provider)
            ? provider.AssemblyAttributes
            : [];
    }

    /// <summary>
    /// Immutable snapshot of the merged state. A new instance is produced for every
    /// <see cref="CompositeSourceGeneratedReflectionDataProvider.Add"/> and published as a single
    /// atomic field write, so any reader that observes a non-default state observes a fully
    /// rebuilt snapshot.
    /// </summary>
    private sealed class CompositeState
    {
#pragma warning disable IDE0028 // Dictionary needs an explicit comparer
        public static readonly CompositeState Empty = new(
            providers: [],
            providersByAssemblyName: new Dictionary<string, SourceGeneratedReflectionDataProvider>(StringComparer.OrdinalIgnoreCase),
            providersByAssembly: [],
            mergedSnapshot: new SourceGeneratedReflectionDataProvider());
#pragma warning restore IDE0028

        private CompositeState(
            IReadOnlyList<SourceGeneratedReflectionDataProvider> providers,
            Dictionary<string, SourceGeneratedReflectionDataProvider> providersByAssemblyName,
            Dictionary<Assembly, SourceGeneratedReflectionDataProvider> providersByAssembly,
            SourceGeneratedReflectionDataProvider mergedSnapshot)
        {
            Providers = providers;
            ProvidersByAssemblyName = providersByAssemblyName;
            ProvidersByAssembly = providersByAssembly;
            MergedSnapshot = mergedSnapshot;
        }

        public IReadOnlyList<SourceGeneratedReflectionDataProvider> Providers { get; }

        public Dictionary<string, SourceGeneratedReflectionDataProvider> ProvidersByAssemblyName { get; }

        public Dictionary<Assembly, SourceGeneratedReflectionDataProvider> ProvidersByAssembly { get; }

        public SourceGeneratedReflectionDataProvider MergedSnapshot { get; }

        public CompositeState With(SourceGeneratedReflectionDataProvider added)
        {
            var providers = new List<SourceGeneratedReflectionDataProvider>(Providers.Count + 1);
            providers.AddRange(Providers);
            providers.Add(added);

#pragma warning disable IDE0028 // Dictionary needs an explicit comparer
            var byName = new Dictionary<string, SourceGeneratedReflectionDataProvider>(ProvidersByAssemblyName, StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028
            byName[added.AssemblyName] = added;

            var byAssembly = new Dictionary<Assembly, SourceGeneratedReflectionDataProvider>(ProvidersByAssembly);
            if (added.Assembly is { } addedAssembly)
            {
                byAssembly[addedAssembly] = added;
            }

            return new CompositeState(
                providers,
                byName,
                byAssembly,
                BuildMergedSnapshot(providers));
        }

        private static SourceGeneratedReflectionDataProvider BuildMergedSnapshot(IReadOnlyList<SourceGeneratedReflectionDataProvider> providers)
        {
            // AssemblyName / Assembly do not make sense for a composite; leave at defaults.
            var types = new List<Type>();
            var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
            var typeAttributes = new Dictionary<Type, Attribute[]>();
            var assemblyAttributes = new List<object>();
            var typeProperties = new Dictionary<Type, PropertyInfo[]>();
            var typeMethods = new Dictionary<Type, MethodInfo[]>();
            var typeMethodLocations = new Dictionary<string, TypeLocation>(StringComparer.Ordinal);
            var typeMethodAttributes = new Dictionary<MethodInfo, Attribute[]>();
            var typeConstructors = new Dictionary<Type, ConstructorInfo[]>();
            var typePropertiesByName = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            var typeConstructorsInvoker = new Dictionary<Type, ConstructorInvoker[]>();

            foreach (SourceGeneratedReflectionDataProvider provider in providers)
            {
                types.AddRange(provider.Types);
                MergeInto(typesByName, provider.TypesByName);
                MergeInto(typeAttributes, provider.TypeAttributes);
                assemblyAttributes.AddRange(provider.AssemblyAttributes);
                MergeInto(typeProperties, provider.TypeProperties);
                MergeInto(typeMethods, provider.TypeMethods);
                MergeInto(typeMethodLocations, provider.TypeMethodLocations);
                MergeInto(typeMethodAttributes, provider.TypeMethodAttributes);
                MergeInto(typeConstructors, provider.TypeConstructors);
                MergeInto(typePropertiesByName, provider.TypePropertiesByName);
                MergeInto(typeConstructorsInvoker, provider.TypeConstructorsInvoker);
            }

            return new SourceGeneratedReflectionDataProvider
            {
                Types = [.. types],
                TypesByName = typesByName,
                TypeAttributes = typeAttributes,
                AssemblyAttributes = [.. assemblyAttributes],
                TypeProperties = typeProperties,
                TypeMethods = typeMethods,
                TypeMethodLocations = typeMethodLocations,
                TypeMethodAttributes = typeMethodAttributes,
                TypeConstructors = typeConstructors,
                TypePropertiesByName = typePropertiesByName,
                TypeConstructorsInvoker = typeConstructorsInvoker,
            };
        }

        private static void MergeInto<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
            where TKey : notnull
        {
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                target[kvp.Key] = kvp.Value;
            }
        }
    }
}
