// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The programmatic test-filter types (ITestFilter, TestFilterProviderAttribute) are [Experimental]
// public API. This file is part of the adapter implementation of that feature, so consuming them
// here is intentional.
#pragma warning disable MSTESTEXP

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    // Single filter instance cached per test assembly source path. The Lazy ensures discovery
    // (including any failure) is evaluated once per source per run, even under contention.
    // Stored as a TestFilterBox so the dictionary can cache the "no filter" answer alongside
    // real filter instances.
    private readonly ConcurrentDictionary<string, Lazy<TestFilterBox>> _testFilterBySource =
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
            .GetOrAdd(assemblySource, CreateTestFilterBox)
            .Value
            .Filter;

    private static Lazy<TestFilterBox> CreateTestFilterBox(string assemblySource)
        => new(() => new TestFilterBox(LoadTestFilterForSource(assemblySource)), isThreadSafe: true);

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
        if (!HasTestFilterProviderMarker(testAssembly, out bool hasGenericMarker))
        {
            return null;
        }

        object[] nonGenericMarkers = GetTestFilterProviderAttributes(testAssembly, typeof(TestFilterProviderAttribute));
        object[] genericMarkers = hasGenericMarker
            ? GetGenericTestFilterProviderAttributes(testAssembly)
            : [];

        int markerCount = nonGenericMarkers.Length + genericMarkers.Length;
        if (markerCount == 0)
        {
            return null;
        }

        if (markerCount > 1)
        {
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestFilterProviderMultipleDeclared,
                SafeGetAssemblyName(testAssembly) ?? "<unknown>");
            throw new TypeInspectionException(message);
        }

        return nonGenericMarkers.Length == 1
            ? nonGenericMarkers[0] is TestFilterProviderAttribute { FilterType: { } filterType }
                ? InstantiateTestFilter(filterType)
                : null
            : InstantiateTestFilterFromGenericProvider(genericMarkers[0]);
    }

    private static object[] GetTestFilterProviderAttributes(Assembly testAssembly, Type attributeType)
    {
        try
        {
            return PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(testAssembly, attributeType);
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
    }

    /// <summary>
    /// Resolves the generic <c>TestFilterProviderAttribute&lt;TFilter&gt;</c> markers through the internal
    /// <see cref="ITestFilterProviderAttribute"/> contract.
    /// </summary>
    /// <remarks>
    /// Kept out of <see cref="DiscoverTestFilterFromProvider"/>, and explicitly not inlined, so that the
    /// reference to <see cref="ITestFilterProviderAttribute"/> is only resolved once a generic marker has
    /// actually been seen in metadata. A newer adapter running against an older MSTest.TestFramework — where
    /// neither this contract nor the generic attribute exists — must never have to load the type, otherwise
    /// discovery of the shipped non-generic attribute would fail with UTA073.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object[] GetGenericTestFilterProviderAttributes(Assembly testAssembly)
    {
        // The lookup matches by assignability, so it also returns the non-generic attribute; keep only the
        // generic shape here, since the non-generic one is resolved through its own concrete type.
        object[] markers = GetTestFilterProviderAttributes(testAssembly, typeof(ITestFilterProviderAttribute));

        return [.. markers.Where(marker => IsTestFilterProviderMarkerType(marker.GetType(), out bool isGeneric) && isGeneric)];
    }

    /// <inheritdoc cref="GetGenericTestFilterProviderAttributes"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ITestFilter? InstantiateTestFilterFromGenericProvider(object marker)
        => marker is ITestFilterProviderAttribute { FilterType: { } filterType }
            ? InstantiateTestFilter(filterType)
            : null;

    internal static ITestFilter InstantiateTestFilter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type filterType)
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

    private static bool HasTestFilterProviderMarker(Assembly assembly, out bool hasGenericMarker)
    {
        bool hasMarker = false;
        hasGenericMarker = false;

        foreach (CustomAttributeData data in assembly.GetCustomAttributesData())
        {
            if (IsTestFilterProviderMarkerType(data.AttributeType, out bool isGeneric))
            {
                hasMarker = true;
                hasGenericMarker |= isGeneric;
            }
        }

        return hasMarker;
    }

    /// <summary>
    /// Whether <paramref name="attributeType"/> is one of the two attribute shapes that register an
    /// <see cref="ITestFilter"/>: <see cref="TestFilterProviderAttribute"/> or
    /// <c>TestFilterProviderAttribute&lt;TFilter&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Comparing type names keeps this a metadata-only probe: it never constructs the attribute, so it
    /// cannot trip on a <c>typeof(...)</c> argument whose target assembly fails to resolve — which is the
    /// whole point of probing metadata before the real lookup.
    /// <para>
    /// Matching exact names is only safe because both attributes are <see langword="sealed"/> and the
    /// contract they share (<c>ITestFilterProviderAttribute</c>) is internal, so this list cannot grow
    /// behind our back. Were the attributes user-derivable, the guarded assignability-based
    /// <c>GetCustomAttributes</c> lookup would return subclasses this probe rejects, silently dropping the
    /// user's filter.
    /// </para>
    /// </remarks>
    internal static bool IsTestFilterProviderMarkerType(Type? attributeType)
        => IsTestFilterProviderMarkerType(attributeType, out _);

    private static bool IsTestFilterProviderMarkerType(Type? attributeType, out bool isGeneric)
    {
        isGeneric = false;

        if (attributeType is null)
        {
            return false;
        }

        // A constructed generic type's FullName embeds the type argument, so compare the generic type
        // definition to recognize TestFilterProviderAttribute<TFilter>.
        Type attributeDefinition = attributeType.IsGenericType
            ? attributeType.GetGenericTypeDefinition()
            : attributeType;

        string markerFullName = typeof(TestFilterProviderAttribute).FullName!;

        if (string.Equals(attributeDefinition.FullName, markerFullName, StringComparison.Ordinal))
        {
            return true;
        }

        // The generic marker additionally has to come from the same assembly as the non-generic one.
        // Name matching alone would let an unrelated assembly that happens to declare this namespace and
        // name flip the flag, and the only thing the flag gates is the ITestFilterProviderAttribute
        // lookup -- which is precisely the type an older MSTest.TestFramework does not have. That would
        // turn a squatted name into UTA073 on a framework that is otherwise fine.
        isGeneric = string.Equals(attributeDefinition.FullName, markerFullName + "`1", StringComparison.Ordinal)
            && attributeDefinition.Assembly == typeof(TestFilterProviderAttribute).Assembly;

        return isGeneric;
    }

    // Tiny holder so the cache can distinguish "not computed yet" (missing key) from
    // "computed and result is no filter" (present key with Filter = null).
    private sealed class TestFilterBox
    {
        public TestFilterBox(ITestFilter? filter) => Filter = filter;

        public ITestFilter? Filter { get; }
    }
}
