// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Runtime.Loader;
#endif

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.DynamicExtensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Exercises the real loader rather than the fake used by <see cref="DynamicExtensionLoaderTests"/>, so that
/// the caching and load-context rules are covered rather than assumed.
/// </summary>
[TestClass]
public sealed class DynamicExtensionAssemblyLoaderTests
{
    private static string ThisAssemblyPath => typeof(DynamicExtensionAssemblyLoaderTests).Assembly.Location;

    [TestMethod]
    public void LoadAssembly_SamePathTwice_ReturnsTheSameAssemblyInstance()
    {
        DynamicExtensionAssemblyLoader loader = new(new SystemFileSystem());

        Assembly first = loader.LoadAssembly(ThisAssemblyPath);
        Assembly second = loader.LoadAssembly(ThisAssemblyPath);

        // Two manifests naming the same assembly must share one load context, otherwise their types would not
        // be interchangeable and the assembly would be loaded twice.
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void LoadAssembly_ResolvesThePlatformContractToTheHostsCopy()
    {
        DynamicExtensionAssemblyLoader loader = new(new SystemFileSystem());

        Assembly loaded = loader.LoadAssembly(ThisAssemblyPath);
        Type? hookType = loaded.GetType(typeof(IdentityProbe).FullName!, throwOnError: false);
        Assert.IsNotNull(hookType);

        MethodInfo? builderTypeAccessor = hookType.GetMethod(nameof(IdentityProbe.GetBuilderInterfaceType), BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(builderTypeAccessor);

        // The probe runs inside the (possibly isolated) context, and the ITestApplicationBuilder it reports
        // must be the very same runtime type the host uses — that identity is what lets a dynamically loaded
        // extension register into the host at all.
        object? typeFromLoadedCopy = builderTypeAccessor.Invoke(null, null);
        Assert.AreSame(typeof(ITestApplicationBuilder), typeFromLoadedCopy);
    }

#if NETCOREAPP
    [TestMethod]
    public void LoadAssembly_OnDotNet_LoadsIntoAnIsolatedContext()
    {
        DynamicExtensionAssemblyLoader loader = new(new SystemFileSystem());

        Assembly loaded = loader.LoadAssembly(ThisAssemblyPath);

        Assert.IsTrue(loader.IsIsolated);
        Assert.AreNotSame(typeof(DynamicExtensionAssemblyLoaderTests).Assembly, loaded, "The extension must not be resolved from the host's own context.");
    }
#else
    [TestMethod]
    public void LoadAssembly_OnDotNetFramework_ReportsThatIsolationIsUnavailable()
    {
        DynamicExtensionAssemblyLoader loader = new(new SystemFileSystem());

        // Not a limitation we can fix on this target, but one the loader must report so the diagnostic log can
        // say so rather than implying an isolation guarantee that does not exist.
        Assert.IsFalse(loader.IsIsolated);
    }
#endif

#if NETCOREAPP
    [TestMethod]
    public void SharedContract_IsResolvedByNameEvenWhenAHigherVersionIsRequested()
    {
        // The extension may have been compiled against a newer Microsoft.Testing.Platform than the host.
        // Resolving through the extension's full AssemblyName would apply version binding, fail to bind, and
        // silently give the extension a private copy of the platform whose ITestApplicationBuilder is a
        // different type -- which then surfaces as a bogus "the hook does not exist".
        Assembly platform = typeof(ITestApplicationBuilder).Assembly;
        AssemblyName requestedWithHigherVersion = new(platform.GetName().Name!)
        {
            Version = new Version(platform.GetName().Version!.Major + 1, 0, 0, 0),
        };

        Assembly? resolved = InvokeResolveSharedContractAssembly(requestedWithHigherVersion);

        Assert.AreSame(platform, resolved);
    }

    [TestMethod]
    public void SharedContract_ThatIsNotLoadedYetIsStillResolvedFromTheApplication()
    {
        // Dynamic hooks run before static ones, so a contract can be in the application's dependency graph
        // without having been loaded yet. Settling for the extension's private copy in that window would split
        // the contract as soon as a static extension loaded the real one.
        //
        // The assembly must genuinely not be loaded, otherwise the already-loaded scan short-circuits and this
        // never reaches Default.LoadFromAssemblyName. Naming a type (for example typeof(Moq.Mock)) would itself
        // load it, so pick from framework assemblies that resolve but that a test process has no reason to have
        // touched, and verify the precondition rather than assuming it.
        string[] candidates = ["System.Net.Ping", "System.Formats.Tar", "System.Net.WebSockets", "System.IO.Pipes"];
        string? notLoaded = candidates.FirstOrDefault(name =>
            !AssemblyLoadContext.Default.Assemblies.Any(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)));
        Assert.IsNotNull(notLoaded, "Every candidate was already loaded, so this test could not exercise the load-by-name path.");

        Assembly? resolved = InvokeResolveSharedContractAssembly(new AssemblyName(notLoaded));

        Assert.IsNotNull(resolved);
        Assert.AreEqual(notLoaded, resolved.GetName().Name);
    }

    [TestMethod]
    public void SharedContract_ThatNobodyCarriesResolvesToNothing()
    {
        Assembly? resolved = InvokeResolveSharedContractAssembly(new AssemblyName("Contoso.NotPresent.Abstractions"));

        Assert.IsNull(resolved);
    }

    private static Assembly? InvokeResolveSharedContractAssembly(AssemblyName assemblyName)
    {
        Type contextType = typeof(DynamicExtensionAssemblyLoader)
            .GetNestedType("DynamicExtensionLoadContext", BindingFlags.NonPublic)!;
        Assert.IsNotNull(contextType);

        object context = Activator.CreateInstance(
            contextType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [ThisAssemblyPath, new SystemFileSystem()],
            culture: null)!;
        Assert.IsNotNull(context);

        MethodInfo method = contextType.GetMethod("ResolveSharedContractAssembly", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.IsNotNull(method);

        try
        {
            return (Assembly?)method.Invoke(context, [assemblyName]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
#endif

    [TestMethod]
    public void LoadAssembly_WithMissingFile_Throws()
    {
        DynamicExtensionAssemblyLoader loader = new(new SystemFileSystem());
        string missing = Path.Combine(Path.GetDirectoryName(ThisAssemblyPath)!, "ThisAssemblyDoesNotExist.dll");

        Assert.ThrowsExactly<FileNotFoundException>(() => loader.LoadAssembly(missing));
    }

    /// <summary>
    /// Loaded reflectively from a second copy of this assembly to compare type identity across the boundary.
    /// </summary>
    public static class IdentityProbe
    {
        public static Type GetBuilderInterfaceType() => typeof(ITestApplicationBuilder);
    }
}
