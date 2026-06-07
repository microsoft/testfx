// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    // Filter instances cached per test assembly source path. Computed lazily on the first request
    // for that source so the cost is paid at most once per run, even when many tests target the
    // same assembly.
    private readonly ConcurrentDictionary<string, IReadOnlyList<ITestFilter>> _testFiltersBySource =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the cached <see cref="ITestFilter"/> instances registered via
    /// <see cref="TestFilterProviderAttribute"/> for the given test assembly source path.
    /// </summary>
    /// <param name="assemblySource">The test assembly source path (typically <c>TestMethod.AssemblyName</c>).</param>
    /// <remarks>
    /// Discovery is metadata-only for the probe step and never forces the test types of the
    /// assembly to load. Filter <em>types</em> are loaded the first time the filter for a given
    /// source is requested.
    /// </remarks>
    internal IReadOnlyList<ITestFilter> GetOrLoadTestFilters(string assemblySource)
        => _testFiltersBySource.TryGetValue(assemblySource, out IReadOnlyList<ITestFilter>? cached)
            ? cached
            : _testFiltersBySource.GetOrAdd(assemblySource, LoadTestFiltersForSource);

    private static IReadOnlyList<ITestFilter> LoadTestFiltersForSource(string assemblySource)
    {
        Assembly assembly;
        try
        {
            assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblySource);
        }
        catch (Exception ex)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "TypeCache: Could not load test assembly {0} for TestFilterProvider discovery. {1}",
                    assemblySource,
                    ex);
            }

            return [];
        }

        return DiscoverTestFiltersFromProviders(assembly);
    }

    private static IReadOnlyList<ITestFilter> DiscoverTestFiltersFromProviders(Assembly currentAssembly)
    {
        List<ITestFilter>? filters = null;
        var visitedFilterTypes = new HashSet<Type>();

        foreach (Assembly candidate in EnumerateCandidateAssemblies(currentAssembly))
        {
            bool hasMarker;
            try
            {
                hasMarker = HasTestFilterProviderMarker(candidate);
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Exception occurred while probing TestFilterProviderAttribute metadata from assembly {0}. {1}",
                        SafeGetAssemblyName(candidate),
                        ex);
                }

                continue;
            }

            if (!hasMarker)
            {
                continue;
            }

            object[] markers;
            try
            {
                markers = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(candidate, typeof(TestFilterProviderAttribute));
            }
            catch (Exception ex)
            {
                // Marker is present (CustomAttributeData saw it) but the attribute cannot be
                // instantiated. This typically means the type referenced by typeof(...) cannot be
                // loaded. [TestFilterProvider] is explicit opt-in: silently dropping the marker
                // would let the user's filter logic disappear at runtime, which is a more
                // dangerous failure mode than a clear diagnostic.
                string message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_TestFilterProviderLoadFailed,
                    SafeGetAssemblyName(candidate) ?? "<unknown>",
                    ex.Message);
                throw new TypeInspectionException(message, ex);
            }

            if (markers is null || markers.Length == 0)
            {
                continue;
            }

            foreach (object marker in markers)
            {
                if (marker is not TestFilterProviderAttribute providerAttribute)
                {
                    continue;
                }

                Type? filterType = providerAttribute.FilterType;
                if (filterType is null || !visitedFilterTypes.Add(filterType))
                {
                    // De-dup so a filter type referenced from both the consumer assembly and a
                    // shared infrastructure library isn't applied twice.
                    continue;
                }

                ITestFilter filter = InstantiateTestFilter(filterType);
                (filters ??= []).Add(filter);
            }
        }

        return filters is null ? [] : filters.ToArray();
    }

    private static ITestFilter InstantiateTestFilter(Type filterType)
    {
        if (filterType.IsGenericType)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestFilterProviderTypeIsGeneric, filterType.FullName);
            throw new TypeInspectionException(message);
        }

        if (filterType.IsAbstract || filterType.IsInterface)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestFilterProviderTypeIsNotInstantiable, filterType.FullName);
            throw new TypeInspectionException(message);
        }

        if (!typeof(ITestFilter).IsAssignableFrom(filterType))
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestFilterProviderTypeDoesNotImplementInterface, filterType.FullName, typeof(ITestFilter).FullName);
            throw new TypeInspectionException(message);
        }

        try
        {
            return (ITestFilter)Activator.CreateInstance(filterType)!;
        }
        catch (Exception ex)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestFilterProviderInstantiationFailed, filterType.FullName, ex.Message);
            throw new TypeInspectionException(message, ex);
        }
    }

    private static bool HasTestFilterProviderMarker(Assembly assembly)
    {
        // Compare on the attribute type's FullName so we don't trigger attribute construction,
        // mirroring the AssemblyFixtureProvider probe.
        string markerFullName = typeof(TestFilterProviderAttribute).FullName!;
        foreach (CustomAttributeData data in assembly.GetCustomAttributesData())
        {
            if (string.Equals(data.AttributeType.FullName, markerFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
