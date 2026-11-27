// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;
#if NET && !WINDOWS_UWP
using System.Runtime.Loader;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Eagerly loads the non-framework assemblies referenced, directly or transitively, by a test assembly
/// so their load cost is paid once, before the first test starts, instead of by whichever test happens to
/// need them first. Opt-in through <c>PreloadReferencedAssemblies</c>.
/// </summary>
/// <remarks>
/// Assemblies are loaded by name in the load context of the assembly that references them, never by path,
/// so the loaded identities are exactly the ones the test code would have bound to later.
/// </remarks>
internal static class AssemblyPreloader
{
    // Resolution callbacks run synchronously on the loading thread. Concurrent ordinary binds must
    // keep their normal cache behavior, even when another test source is being preloaded.
    [field: ThreadStatic]
    internal static bool IsPreloading { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the current runtime can walk the assembly reference graph.
    /// Runtimes that cannot generate dynamic code (Native AOT, Mono iOS AOT, Blazor WASM AOT) and UWP
    /// (whose packaged app model does not load arbitrary assemblies by name) cannot.
    /// </summary>
    internal static bool IsSupported
#if WINDOWS_UWP
        => false;
#elif NET
        => RuntimeFeature.IsDynamicCodeSupported;
#else
        => true;
#endif

    /// <summary>
    /// Loads the non-framework assemblies reachable from <paramref name="testAssembly"/>.
    /// </summary>
    /// <param name="testAssembly">The root assembly whose references are walked.</param>
    /// <returns>The number of distinct assemblies loaded and the number of references that could not be loaded.</returns>
    internal static (int Loaded, int Failed) Preload(Assembly testAssembly)
    {
        bool wasPreloading = IsPreloading;
        try
        {
            IsPreloading = true;
            return Preload(testAssembly, GetReferencedAssemblies, TryLoadReferencedAssembly, IsRunCanceled);
        }
        finally
        {
            IsPreloading = wasPreloading;
        }
    }

    /// <summary>
    /// Breadth-first walk over the reference graph with injectable reference enumeration, loading and
    /// cancellation so the traversal can be tested without real assemblies.
    /// </summary>
    internal static (int Loaded, int Failed) Preload(
        Assembly testAssembly,
        Func<Assembly, AssemblyName[]> getReferences,
        Func<Assembly, AssemblyName, Assembly?> tryLoad,
        Func<bool> isCanceled)
    {
        // The same name can bind to different assemblies through different referrers. Deduplicate after
        // binding, so both graphs are visited but shared or redirected assemblies are walked only once.
        var visitedAssemblies = new HashSet<Assembly> { testAssembly };
        var pending = new Queue<Assembly>();
        pending.Enqueue(testAssembly);

        int loaded = 0;
        int failed = 0;

        while (pending.Count > 0 && !isCanceled())
        {
            Assembly referrer = pending.Dequeue();
            foreach (AssemblyName reference in getReferences(referrer))
            {
                if (isCanceled())
                {
                    return (loaded, failed);
                }

                if (reference.Name is not { } simpleName || TypeCache.IsFrameworkAssemblyName(simpleName))
                {
                    continue;
                }

                Assembly? assembly = tryLoad(referrer, reference);
                if (assembly is null)
                {
                    failed++;
                    continue;
                }

                if (assembly.IsDynamic || !visitedAssemblies.Add(assembly))
                {
                    continue;
                }

                loaded++;
                pending.Enqueue(assembly);
            }
        }

        return (loaded, failed);
    }

    private static bool IsRunCanceled()
        => PlatformServiceProvider.Instance.TestRunCancellationToken?.Canceled == true;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming", Justification = "Preloading is best-effort and only walks references retained in the published assembly. It does not require trimmed references to be preserved.")]
    private static AssemblyName[] GetReferencedAssemblies(Assembly assembly)
    {
        try
        {
            return assembly.GetReferencedAssemblies();
        }
        catch (Exception ex)
        {
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "AssemblyPreloader: Exception occurred while enumerating referenced assemblies of {0}. {1}",
                    assembly.FullName,
                    ex);
            }

            return [];
        }
    }

    private static Assembly? TryLoadReferencedAssembly(Assembly referrer, AssemblyName referenceName)
    {
        try
        {
#if NET && !WINDOWS_UWP
            // Resolve through the same AssemblyLoadContext as the referrer so plugin-style hosts
            // (which place the test assembly in a non-default ALC) don't end up with a second copy of
            // the library in the default ALC.
            AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(referrer) ?? AssemblyLoadContext.Default;
            return loadContext.LoadFromAssemblyName(referenceName);
#else
            _ = referrer;
            return Assembly.Load(referenceName);
#endif
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or SecurityException)
        {
            // Preloading is an optimization: a reference that cannot be loaded now is left for the test
            // code to hit (or not) exactly as it would without preloading.
            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                    "AssemblyPreloader: Could not load referenced assembly {0}. {1}",
                    referenceName.FullName,
                    ex);
            }

            return null;
        }
    }
}
