// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    // Single filter instance cached per test assembly source path. Computed lazily on the first
    // request for that source so the cost is paid at most once per run, even when many tests
    // target the same assembly. Stored as a TestFilterBox so the dictionary can cache the
    // "no filter" answer alongside real filter instances.
    private readonly ConcurrentDictionary<string, TestFilterBox> _testFilterBySource =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the cached <see cref="ITestFilter"/> instance registered via
    /// <see cref="TestFilterProviderAttribute"/> on the given test assembly, or
    /// <see langword="null"/> if the assembly does not register one.
    /// </summary>
    /// <param name="assemblySource">The test assembly source path (typically <c>TestMethod.AssemblyName</c>).</param>
    /// <remarks>
    /// Discovery is metadata-only for the probe step and never forces the test types of the
    /// assembly to load. The filter <em>type</em> is loaded the first time the filter for a
    /// given source is requested. Only the test assembly itself is inspected — registering a
    /// <see cref="TestFilterProviderAttribute"/> in a referenced library has no effect.
    /// </remarks>
    internal ITestFilter? GetOrLoadTestFilter(string assemblySource)
        => _testFilterBySource
            .GetOrAdd(assemblySource, static src => new TestFilterBox(LoadTestFilterForSource(src)))
            .Filter;

    private static ITestFilter? LoadTestFilterForSource(string assemblySource)
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

            return null;
        }

        return DiscoverTestFilterFromProvider(assembly);
    }

    private static ITestFilter? DiscoverTestFilterFromProvider(Assembly testAssembly)
    {
        // Cheap metadata-only probe first: avoid loading the filter's Type unless the attribute is
        // actually present. Mirrors the AssemblyFixtureProvider probe pattern.
        if (!HasTestFilterProviderMarker(testAssembly))
        {
            return null;
        }

        object[] markers;
        try
        {
            markers = PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(testAssembly, typeof(TestFilterProviderAttribute));
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
                SafeGetAssemblyName(testAssembly) ?? "<unknown>",
                ex.Message);
            throw new TypeInspectionException(message, ex);
        }

        if (markers is null || markers.Length == 0)
        {
            return null;
        }

        if (markers.Length > 1)
        {
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestFilterProviderMultipleDeclared,
                SafeGetAssemblyName(testAssembly) ?? "<unknown>");
            throw new TypeInspectionException(message);
        }

        return markers[0] is TestFilterProviderAttribute { FilterType: { } filterType }
            ? InstantiateTestFilter(filterType)
            : null;
    }

    internal static ITestFilter InstantiateTestFilter(Type filterType)
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

    // Tiny holder so the cache can distinguish "not computed yet" (missing key) from
    // "computed and result is no filter" (present key with Filter = null).
    private sealed class TestFilterBox
    {
        public TestFilterBox(ITestFilter? filter) => Filter = filter;

        public ITestFilter? Filter { get; }
    }
}
