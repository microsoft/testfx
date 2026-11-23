// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET && !WINDOWS_UWP

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AssemblyResolverSearchPass2MutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;

    public AssemblyResolverSearchPass2MutationTests()
        => PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void AnExecutableIsFoundWhenNoLibraryHasTheRequestedName()
    {
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchPass2MutationTests).Assembly;
        string name = NewUniqueName();
        CopyTestAssemblyTo(directory.Root, name + ".exe");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearch(resolver, [directory.Root], name).Should().BeSameAs(testAssembly);

        GetCache(resolver, "_resolvedAssemblies").Should().ContainKey(name).WhoseValue.Should().BeSameAs(testAssembly);
    }

    public void AFileWithoutAnExtensionIsNotACandidateForTheRequestedName()
    {
        using TempDirectory directory = new();
        string name = NewUniqueName();
        CopyTestAssemblyTo(directory.Root, name);
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearch(resolver, [directory.Root], name).Should().BeNull();

        GetCache(resolver, "_resolvedAssemblies").Should().NotContainKey(name);
    }

    private static AssemblyResolver CreateDetachedResolver(params string[] directories)
    {
        // Without its subscription, the resolver only sees what the test hands to it.
        var resolver = new AssemblyResolver(directories);
        AppDomain.CurrentDomain.AssemblyResolve -= resolver.OnResolve;
        return resolver;
    }

    private static string NewUniqueName()
        => "P14.Search.Pass2." + Guid.NewGuid().ToString("N");

    private static void CopyTestAssemblyTo(string directory, string fileName)
        => File.Copy(typeof(AssemblyResolverSearchPass2MutationTests).Assembly.Location, Path.Combine(directory, fileName));

    private static Dictionary<string, Assembly?> GetCache(AssemblyResolver resolver, string fieldName)
        => (Dictionary<string, Assembly?>)typeof(AssemblyResolver).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resolver)!;

    private static Assembly? InvokeSearch(AssemblyResolver resolver, List<string> directories, string name)
    {
        MethodInfo search = typeof(AssemblyResolver).GetMethod("SearchAssembly", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!;
        return (Assembly?)search.Invoke(resolver, [directories, name, false]);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-p14-resolver-pass2-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
