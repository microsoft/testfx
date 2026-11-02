// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Runtime.Loader;
#endif
#if NET9_0_OR_GREATER
using System.Reflection.Emit;
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
#if NET9_0_OR_GREATER
    private const string CarriedContractName = "Contoso.CarriedContract.Abstractions";
#endif

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
        //
        // Which stage answered is not observable from here -- both return the same instance -- so a parallel
        // test loading a candidate in the window between the check and the call would make this pass without
        // covering the branch. Closing that would take a dedicated test process for one branch, which is not
        // worth it: mutating Default.LoadFromAssemblyName away fails this test under the full parallel suite,
        // and if every candidate ever becomes loaded the assertion below says exactly that.
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

#if NET9_0_OR_GREATER
    [TestMethod]
    public void SharedContract_CarriedOnlyByExtensions_IsPromotedSoEveryExtensionGetsTheSameCopy()
    {
        // The ordinary manifest-only deployment: the application does not carry the contract at all and every
        // extension brings its own copy. Loading each copy into its own context would give a capability
        // published by one dynamic extension a different identity from the one another extension consumes --
        // exactly the split the shared list exists to prevent. The first copy found is promoted into the
        // default context and must then be handed to every extension that follows.
        string root = Path.Combine(Path.GetTempPath(), $"mtp-carried-contract-{Guid.NewGuid():N}");
        string firstExtensionDirectory = Path.Combine(root, "first");
        string secondExtensionDirectory = Path.Combine(root, "second");
        Directory.CreateDirectory(firstExtensionDirectory);
        Directory.CreateDirectory(secondExtensionDirectory);

        try
        {
            // Deliberately different versions: if the second extension were handed its own copy back, the
            // assertion below would still see the right simple name, so only the version can tell the promoted
            // copy apart from a private one.
            WriteCarriedContractAssembly(Path.Combine(firstExtensionDirectory, CarriedContractName + ".dll"), new Version(1, 0, 0, 0));
            WriteCarriedContractAssembly(Path.Combine(secondExtensionDirectory, CarriedContractName + ".dll"), new Version(2, 0, 0, 0));

            Assembly? alreadyLoaded = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => string.Equals(a.GetName().Name, CarriedContractName, StringComparison.OrdinalIgnoreCase));
            Assert.IsNull(alreadyLoaded, "The contract must be absent from the application, otherwise this never reaches the promotion path.");

            Assembly? fromFirstExtension = InvokeResolveSharedContractAssembly(
                new AssemblyName(CarriedContractName) { Version = new Version(1, 0, 0, 0) },
                Path.Combine(firstExtensionDirectory, "Contoso.First.Extension.dll"));
            Assembly? fromSecondExtension = InvokeResolveSharedContractAssembly(
                new AssemblyName(CarriedContractName) { Version = new Version(2, 0, 0, 0) },
                Path.Combine(secondExtensionDirectory, "Contoso.Second.Extension.dll"));

            Assert.IsNotNull(fromFirstExtension);
            Assert.AreSame(fromFirstExtension, fromSecondExtension, "Both extensions must observe one contract identity.");
            Assert.AreEqual(new Version(1, 0, 0, 0), fromFirstExtension.GetName().Version, "The copy promoted first must stay canonical, whatever version a later extension ships.");
            Assert.Contains(fromFirstExtension, AssemblyLoadContext.Default.Assemblies, "The promoted copy must live in the default context so statically loaded code agrees with it too.");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Promotion is by definition into the non-collectible default context, so the file stays
                // mapped for the life of the process and cannot be deleted on Windows. Leaving it is the
                // price of covering this path at all.
            }
        }
    }

    private static void WriteCarriedContractAssembly(string path, Version version)
    {
        // A real assembly on disk rather than a copy of an existing one: the resolver matches on the simple
        // name recorded in the metadata, so a renamed file would take a different branch than the one under
        // test and quietly turn this into a tautology.
        PersistedAssemblyBuilder builder = new(new AssemblyName(CarriedContractName) { Version = version }, typeof(object).Assembly);
        builder.DefineDynamicModule(CarriedContractName);
        builder.Save(path);
    }
#endif

    private static Assembly? InvokeResolveSharedContractAssembly(AssemblyName assemblyName, string? extensionAssemblyPath = null)
    {
        Type contextType = typeof(DynamicExtensionAssemblyLoader)
            .GetNestedType("DynamicExtensionLoadContext", BindingFlags.NonPublic)!;
        Assert.IsNotNull(contextType);

        object context = Activator.CreateInstance(
            contextType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            args: [extensionAssemblyPath ?? ThisAssemblyPath, new SystemFileSystem()],
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
