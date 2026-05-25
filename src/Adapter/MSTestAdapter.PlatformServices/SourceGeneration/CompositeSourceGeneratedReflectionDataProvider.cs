// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Merges multiple per-assembly <see cref="SourceGeneratedReflectionDataProvider"/> instances into
/// a single provider so that more than one test assembly can be registered with
/// <see cref="ReflectionMetadataHook.SetMetadata(SourceGeneratedReflectionDataProvider)"/> in a
/// single process. The merged dictionaries are recomputed from scratch on every
/// <see cref="Add(SourceGeneratedReflectionDataProvider)"/> so that lookups remain a simple
/// dictionary read on the hot path.
/// </summary>
internal sealed class CompositeSourceGeneratedReflectionDataProvider : SourceGeneratedReflectionDataProvider
{
    private readonly List<SourceGeneratedReflectionDataProvider> _providers = [];
#pragma warning disable IDE0028 // Collection initialization can be simplified — Dictionary needs explicit comparer
    private readonly Dictionary<string, SourceGeneratedReflectionDataProvider> _providersByAssemblyName = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore IDE0028
    private readonly Dictionary<Assembly, SourceGeneratedReflectionDataProvider> _providersByAssembly = [];

    public void Add(SourceGeneratedReflectionDataProvider provider)
    {
        _providers.Add(provider);
        _providersByAssemblyName[provider.AssemblyName] = provider;
        if (provider.Assembly is not null)
        {
            _providersByAssembly[provider.Assembly] = provider;
        }

        Rebuild();
    }

    internal override Assembly GetAssembly(string assemblyPath)
    {
        string name = Path.GetFileNameWithoutExtension(assemblyPath);
        return _providersByAssemblyName.TryGetValue(name, out SourceGeneratedReflectionDataProvider? provider)
            && provider.Assembly is { } assembly
            ? assembly
            : throw new ArgumentException($"Assembly '{assemblyPath}' is not registered with the MSTest source generator.");
    }

    internal override void GetNavigationData(string className, string methodName, out int minLineNumber, out string? fileName)
    {
        foreach (SourceGeneratedReflectionDataProvider provider in _providers)
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
        => _providersByAssembly.TryGetValue(assembly, out SourceGeneratedReflectionDataProvider? provider)
            ? provider.AssemblyAttributes
            : [];

    private void Rebuild()
    {
        // AssemblyName / Assembly do not make sense for a composite; leave at defaults.
        var types = new List<Type>();
        var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        var typeAttributes = new Dictionary<Type, Attribute[]>();
        var assemblyAttributes = new List<object>();
        var typeProperties = new Dictionary<Type, PropertyInfo[]>();
        var typeMethods = new Dictionary<Type, MethodInfo[]>();
        var typeMethodLocations = new Dictionary<string, TypeLocation>(StringComparer.Ordinal);
        var typeMethodAttributes = new Dictionary<Type, Dictionary<string, Attribute[]>>();
        var typeConstructors = new Dictionary<Type, ConstructorInfo[]>();
        var typePropertiesByName = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
        var typeConstructorsInvoker = new Dictionary<Type, ConstructorInvoker[]>();

        foreach (SourceGeneratedReflectionDataProvider provider in _providers)
        {
            types.AddRange(provider.Types);
            foreach (KeyValuePair<string, Type> kvp in provider.TypesByName)
            {
                typesByName[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, Attribute[]> kvp in provider.TypeAttributes)
            {
                typeAttributes[kvp.Key] = kvp.Value;
            }

            assemblyAttributes.AddRange(provider.AssemblyAttributes);

            foreach (KeyValuePair<Type, PropertyInfo[]> kvp in provider.TypeProperties)
            {
                typeProperties[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, MethodInfo[]> kvp in provider.TypeMethods)
            {
                typeMethods[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<string, TypeLocation> kvp in provider.TypeMethodLocations)
            {
                typeMethodLocations[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, Dictionary<string, Attribute[]>> kvp in provider.TypeMethodAttributes)
            {
                typeMethodAttributes[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, ConstructorInfo[]> kvp in provider.TypeConstructors)
            {
                typeConstructors[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, Dictionary<string, PropertyInfo>> kvp in provider.TypePropertiesByName)
            {
                typePropertiesByName[kvp.Key] = kvp.Value;
            }

            foreach (KeyValuePair<Type, ConstructorInvoker[]> kvp in provider.TypeConstructorsInvoker)
            {
                typeConstructorsInvoker[kvp.Key] = kvp.Value;
            }
        }

        Types = [.. types];
        TypesByName = typesByName;
        TypeAttributes = typeAttributes;
        AssemblyAttributes = [.. assemblyAttributes];
        TypeProperties = typeProperties;
        TypeMethods = typeMethods;
        TypeMethodLocations = typeMethodLocations;
        TypeMethodAttributes = typeMethodAttributes;
        TypeConstructors = typeConstructors;
        TypePropertiesByName = typePropertiesByName;
        TypeConstructorsInvoker = typeConstructorsInvoker;
    }
}
