// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public void LoadAssembly_SharesTheContractByNameEvenWhenAHigherVersionIsRequested()
    {
        // The extension may have been compiled against a newer Microsoft.Testing.Platform than the host.
        // Resolving through LoadFromAssemblyName would apply version binding, fail to bind, and silently give
        // the extension a private copy of the platform whose ITestApplicationBuilder is a different type --
        // which then surfaces as a bogus "the hook does not exist". Matching by simple name avoids that.
        Assembly platform = typeof(ITestApplicationBuilder).Assembly;

        Assembly? resolved = InvokeFindLoadedContractAssembly(platform.GetName().Name);

        Assert.AreSame(platform, resolved);
    }

    [TestMethod]
    public void LoadAssembly_DoesNotShareAnAssemblyTheHostHasNotLoaded()
    {
        Assembly? resolved = InvokeFindLoadedContractAssembly("Contoso.NotLoaded.Abstractions");

        // Falls through to the extension's own copy rather than failing, which is what lets an extension
        // depending on an abstractions package the test application never referenced still load.
        Assert.IsNull(resolved);
    }

    private static Assembly? InvokeFindLoadedContractAssembly(string? simpleName)
    {
        Type contextType = typeof(DynamicExtensionAssemblyLoader)
            .GetNestedType("DynamicExtensionLoadContext", BindingFlags.NonPublic)!;
        Assert.IsNotNull(contextType);
        MethodInfo method = contextType.GetMethod("FindLoadedContractAssembly", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.IsNotNull(method);
        return (Assembly?)method.Invoke(null, [simpleName]);
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
