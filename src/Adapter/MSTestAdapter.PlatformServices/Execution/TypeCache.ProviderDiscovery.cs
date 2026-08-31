// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;
#if NET && !WINDOWS_UWP
using System.Runtime.Loader;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TypeCache
{
    private static void DiscoverFixturesFromProviders(Assembly currentAssembly, TestAssemblyInfo assemblyInfo, TypeCache @this)
    {
#if NET && !WINDOWS_UWP
        // [AssemblyFixtureProvider] discovery walks the runtime assembly reference graph
        // (Assembly.GetReferencedAssemblies + assembly loading by name), which is not supported when
        // the runtime cannot generate dynamic code (Native AOT, Mono iOS AOT, Blazor WASM AOT).
        // Skipping the feature there keeps behavior predictable and lets the trimmer statically remove
        // the reflection path (so no IL2026/IL3050 is produced).
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            // The compile-time MSTEST0072 analyzer covers the referenced-provider case at build time
            // (it can read referenced assemblies' metadata). At run time under AOT we cannot walk the
            // reference graph (Assembly.GetReferencedAssemblies + load-by-name is the very dynamic-code
            // path this guard avoids), so a referenced provider that has not been loaded yet is not
            // detectable here. What we can reliably and AOT-safely surface is a marker on an
            // already-loaded assembly — in particular the test assembly itself when it self-applies the
            // attribute (a documented usage). Emit a best-effort warning for every loaded assembly that
            // carries the marker so those cases are not silent.
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (loaded.IsDynamic)
                    {
                        continue;
                    }

                    bool hasMarker;
                    try
                    {
                        // Metadata-only probe. Isolate per-assembly failures (unresolvable custom-attribute
                        // metadata on an unrelated assembly must not abort discovery), matching the normal
                        // discovery path's handling.
                        hasMarker = HasAssemblyFixtureProviderMarker(loaded);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (hasMarker)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                            "TypeCache: [AssemblyFixtureProvider] is not supported when the runtime cannot generate dynamic code (Native AOT, Mono iOS AOT, Blazor WebAssembly AOT). The AssemblyInitialize/AssemblyCleanup methods it exposes from assembly {0} will not run.",
                            SafeGetAssemblyName(loaded));
                    }
                }
            }

            return;
        }
#endif

        // Snapshot which slots were filled by the in-assembly pass. Local declarations are
        // authoritative — never let a provider overwrite or even consider those slots, so the
        // provider pass stays silent when the test assembly already declared a fixture method.
        bool localProvidedInit = assemblyInfo.AssemblyInitializeMethod is not null;
        bool localProvidedCleanup = assemblyInfo.AssemblyCleanupMethod is not null;

        if (localProvidedInit && localProvidedCleanup)
        {
            return;
        }

        foreach (Assembly candidate in EnumerateCandidateAssemblies(currentAssembly))
        {
            // Cheap presence check via metadata: CustomAttributeData does not invoke the attribute
            // constructor and so cannot trip on a typeof(...) argument whose target assembly fails
            // to resolve. We only need to know whether the marker is on the assembly; if it is, an
            // instantiation failure below becomes a real diagnostic instead of a silent drop.
            bool hasMarker;
            try
            {
                hasMarker = HasAssemblyFixtureProviderMarker(candidate);
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Exception occurred while probing AssemblyFixtureProviderAttribute metadata from assembly {0}. {1}",
                        SafeGetAssemblyName(candidate),
                        ex);
                }

                continue;
            }

            if (!hasMarker)
            {
                continue;
            }

            object[] markers = LoadProviderMarkers(candidate);

            if (markers is null || markers.Length == 0)
            {
                continue;
            }

            foreach (object marker in markers)
            {
                if (marker is not AssemblyFixtureProviderAttribute providerAttribute)
                {
                    continue;
                }

                Type? fixtureType = providerAttribute.FixtureType;
                if (fixtureType is null)
                {
                    continue;
                }

                ProcessProviderFixtureType(fixtureType, assemblyInfo, @this, localProvidedInit, localProvidedCleanup);
            }
        }
    }

    internal static object[] LoadProviderMarkers(Assembly candidate)
    {
        try
        {
            return PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributes(candidate, typeof(AssemblyFixtureProviderAttribute));
        }
        catch (Exception ex)
        {
            // The marker is present (CustomAttributeData saw it) but the attribute cannot be
            // instantiated. This usually means the type referenced by typeof(...) cannot be
            // loaded. [AssemblyFixtureProvider] is explicit opt-in: silently dropping the
            // marker here would let assembly init/cleanup quietly disappear. Surface as a
            // standard MSTest diagnostic so the failure is visible to the user.
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_AssemblyFixtureProviderLoadFailed,
                SafeGetAssemblyName(candidate) ?? "<unknown>",
                ex.Message);
            throw new TypeInspectionException(message, ex);
        }
    }

    internal static void ProcessProviderFixtureType(
        Type fixtureType,
        TestAssemblyInfo assemblyInfo,
        TypeCache @this,
        bool localProvidedInit,
        bool localProvidedCleanup)
    {
        // Reject both open generics (e.g. typeof(MyFixture<>)) and closed generics
        // (e.g. typeof(MyFixture<int>)). The documented contract of
        // [AssemblyFixtureProvider] is that the fixture type must be non-generic;
        // ContainsGenericParameters alone would miss the closed case.
        if (fixtureType.IsGenericType)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_AssemblyFixtureProviderTypeIsGeneric, fixtureType.FullName);
            throw new TypeInspectionException(message);
        }

        CollectFixtureMethodsFromProviderType(fixtureType, assemblyInfo, @this, localProvidedInit, localProvidedCleanup);
    }

    internal static void CollectFixtureMethodsFromProviderType(
        Type fixtureType,
        TestAssemblyInfo assemblyInfo,
        TypeCache @this,
        bool localProvidedInit,
        bool localProvidedCleanup)
    {
        MethodInfo[] methods;
        try
        {
            methods = PlatformServiceProvider.Instance.ReflectionOperations.GetDeclaredMethods(fixtureType);
        }
        catch (Exception ex)
        {
            // [AssemblyFixtureProvider] is explicit opt-in: silently dropping the provider type
            // would let a missing dependency or metadata problem make assembly init/cleanup quietly
            // disappear. Surface as a standard MSTest diagnostic instead.
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_AssemblyFixtureProviderTypeReflectionFailed,
                fixtureType.FullName,
                ex.Message);
            throw new TypeInspectionException(message);
        }

        foreach (MethodInfo methodInfo in methods)
        {
            // Slots filled by the in-assembly pass (local declarations) are immutable — silently
            // skip provider methods that target them. Slots left empty are filled here; the
            // setters on TestAssemblyInfo throw UTA_ErrorMultiAssemblyInit / UTA_ErrorMultiAssemblyClean
            // if two providers contribute methods for the same slot, surfacing the conflict as a
            // standard MSTest diagnostic.
            if (!localProvidedInit && @this.IsAssemblyOrClassInitializeMethod<AssemblyInitializeAttribute>(methodInfo))
            {
                assemblyInfo.AssemblyInitializeMethod = methodInfo;
                assemblyInfo.AssemblyInitializeMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyInitialize);
            }
            else if (!localProvidedCleanup && @this.IsAssemblyOrClassCleanupMethod<AssemblyCleanupAttribute>(methodInfo))
            {
                assemblyInfo.AssemblyCleanupMethod = methodInfo;
                assemblyInfo.AssemblyCleanupMethodTimeoutMilliseconds = @this.TryGetTimeoutInfo(methodInfo, FixtureKind.AssemblyCleanup);
            }
        }
    }

    private static IEnumerable<Assembly> EnumerateCandidateAssemblies(Assembly currentAssembly)
    {
        // BFS over the consumer assembly's reference graph. This bounds the work to assemblies that
        // are recorded in the metadata reference table (the runtime reference graph), instead of
        // scanning the entire AppDomain. Note that a bare project / PackageReference is NOT
        // sufficient: the C# compiler omits references that the compiled IL never uses, so a
        // provider library only shows up here if its types are actually touched by the consumer
        // (or a `[assembly: TypeForwardedTo]` / similar pulls the reference in). The acceptance
        // asset for this feature accordingly calls into the provider library to materialize the
        // reference.
        //
        // Dedup uses AssemblyName.FullName (name + version + culture + public-key-token) so multi-version
        // / multi-token references with the same simple name are not collapsed.
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>();

        visited.Add(currentAssembly.FullName ?? string.Empty);

        // The consumer assembly itself is yielded first so users may also place the marker on the
        // test project (handy escape hatch when the library author cannot ship the attribute themselves).
        queue.Enqueue(currentAssembly);

        while (queue.Count > 0)
        {
            Assembly current = queue.Dequeue();
            yield return current;

            AssemblyName[] references;
            try
            {
                references = current.GetReferencedAssemblies();
            }
            catch (Exception ex)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                        "TypeCache: Exception occurred while enumerating referenced assemblies of {0} for AssemblyFixtureProvider discovery. {1}",
                        SafeGetAssemblyName(current),
                        ex);
                }

                continue;
            }

            foreach (AssemblyName referenceName in references)
            {
                string? name = referenceName.Name;
                if (name is null || IsFrameworkAssemblyName(name))
                {
                    continue;
                }

                if (!visited.Add(referenceName.FullName))
                {
                    continue;
                }

                Assembly? referenced = TryLoadReferencedAssembly(current, referenceName);
                if (referenced is null || referenced.IsDynamic)
                {
                    continue;
                }

                queue.Enqueue(referenced);
            }
        }
    }

    private static Assembly? TryLoadReferencedAssembly(Assembly referrer, AssemblyName referenceName)
    {
        try
        {
#if NET && !WINDOWS_UWP
            // Resolve through the same AssemblyLoadContext as the referrer so plugin-style hosts
            // (which place the test assembly in a non-default ALC) don't end up loading a second
            // copy of the provider library into the default ALC, which would cause assembly
            // fixtures to mutate static state on the wrong assembly instance.
            AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(referrer) ?? AssemblyLoadContext.Default;
            return loadContext.LoadFromAssemblyName(referenceName);
#else
            _ = referrer;
            return Assembly.Load(referenceName);
#endif
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "TypeCache: Could not load referenced assembly {0} for AssemblyFixtureProvider discovery. {1}",
                    referenceName.FullName,
                    ex);
            }

            return null;
        }
    }

    private static bool IsFrameworkAssemblyName(string name)
        => name.StartsWith("System.", StringComparison.Ordinal)
            || name.Equals("System", StringComparison.Ordinal)
            || name.Equals("mscorlib", StringComparison.Ordinal)
            || name.Equals("netstandard", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.VisualStudio.TestTools", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.TestPlatform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Testing.", StringComparison.Ordinal)
            || name.Equals("Microsoft.Testing.Platform", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.NET.", StringComparison.Ordinal)
            || name.Equals("Microsoft.CSharp", StringComparison.Ordinal)
            || name.StartsWith("MSTest.", StringComparison.Ordinal)
            || name.StartsWith("MSTestAdapter.", StringComparison.Ordinal);

    private static bool HasAssemblyFixtureProviderMarker(Assembly assembly)
    {
        // Compare on the attribute type's FullName so we don't trigger attribute construction
        // (and therefore don't depend on the typeof(...) argument being resolvable). This is
        // exactly the "is the marker present at all?" probe.
        string markerFullName = typeof(AssemblyFixtureProviderAttribute).FullName!;
        foreach (CustomAttributeData data in assembly.GetCustomAttributesData())
        {
            if (string.Equals(data.AttributeType.FullName, markerFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? SafeGetAssemblyName(Assembly assembly)
    {
        try
        {
            return assembly.GetName().Name;
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException or SecurityException or NotSupportedException)
        {
            return null;
        }
    }
}
