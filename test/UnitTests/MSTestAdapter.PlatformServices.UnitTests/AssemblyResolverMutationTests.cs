// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET && !WINDOWS_UWP

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AssemblyResolverMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly List<(string Name, string Directory)> _searches = [];

    public AssemblyResolverMutationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsVerboseEnabled).Returns(true);
        _provider.MockTraceLogger.Setup(logger => logger.Verbose(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, arguments) => _searches.Add(((string)arguments[0]!, (string)arguments[1]!)));
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void ConstructorRejectsNullDirectories()
    {
        AssemblyResolver? created = null;
        try
        {
            Action create = () => created = new AssemblyResolver(null!);

            create.Should().ThrowExactly<ArgumentNullException>().WithParameterName("directories");
        }
        finally
        {
            created?.Dispose();
        }
    }

    public void ConstructorRejectsAnEmptyDirectoryList()
    {
        AssemblyResolver? created = null;
        try
        {
            Action create = () => created = new AssemblyResolver([]);

            Exception exception = create.Should().Throw<Exception>().Which;
            exception.GetType().FullName.Should().Be("System.Diagnostics.UnreachableException");
        }
        finally
        {
            created?.Dispose();
        }
    }

    public void ConstructorSubscribesTheResolverToAssemblyResolve()
    {
        string directory = NewUnusedDirectory();
        string missing = NewMissingName();
        using AssemblyResolver resolver = new([directory]);

        LoadMissingAssembly(missing);

        _searches.Where(search => search.Name == missing).Should().Equal((missing, directory));
    }

    public void DisposeUnsubscribesTheResolverFromAssemblyResolve()
    {
        string directory = NewUnusedDirectory();
        string missing = NewMissingName();
        var resolver = new AssemblyResolver([directory]);

        resolver.Dispose();
        LoadMissingAssembly(missing);

        _searches.Where(search => search.Name == missing).Should().BeEmpty("a disposed resolver must not take part in assembly resolution anymore");
        HasDisposedState(resolver).Should().BeTrue();
    }

    public void DisposingTwiceOnlyReleasesTheResolverOnce()
    {
        string directory = NewUnusedDirectory();
        string missing = NewMissingName();
        var resolver = new AssemblyResolver([directory]);
        ResolveEventHandler handler = resolver.OnResolve;
        resolver.Dispose();
        AppDomain.CurrentDomain.AssemblyResolve += handler;
        try
        {
            resolver.Dispose();
            LoadMissingAssembly(missing);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }

        _searches.Where(search => search.Name == missing).Should().Equal(
            [(missing, directory)],
            "the second Dispose is a no-op, so it must not remove the subscription added after the first one");
    }

    public void FinalizerOnlyMarksTheResolverDisposed()
    {
        string directory = NewUnusedDirectory();
        string missing = NewMissingName();
        var resolver = new AssemblyResolver([directory]);
        ResolveEventHandler handler = resolver.OnResolve;
        try
        {
            typeof(AssemblyResolver)
                .GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!
                .Invoke(resolver, null);

            HasDisposedState(resolver).Should().BeTrue();
            LoadMissingAssembly(missing);
            _searches.Where(search => search.Name == missing).Should().Equal(
                [(missing, directory)],
                "the finalizer only releases unmanaged state, so the managed AssemblyResolve subscription is left alone");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
            resolver.Dispose();
        }
    }

    public void DisposeSuppressesTheFinalizerOfTheResolver()
    {
        WeakReference disposed = CreateDisposedResolverTracker();
        WeakReference abandoned = CreateAbandonedResolverTracker();
        bool abandonedWasFinalized = false;
        bool disposedWasFinalized = false;

        for (int attempt = 0; attempt < 3 && !abandonedWasFinalized; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            abandonedWasFinalized = abandoned.Target is null || IsMarkedDisposed(abandoned);
            disposedWasFinalized |= IsMarkedDisposed(disposed);
        }

        abandonedWasFinalized.Should().BeTrue("an abandoned resolver gets finalized, which proves the collection used by this test is effective");
        disposedWasFinalized.Should().BeFalse("Dispose must suppress the finalizer");
    }

    // A long weak reference keeps tracking a finalized object until the next collection, so the finalizer's effect
    // (re-marking the resolver as disposed after the field was reset) stays observable right after the first collection.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedResolverTracker()
    {
        var resolver = new AssemblyResolver([NewUnusedDirectory()]);
        resolver.Dispose();
        typeof(AssemblyResolver).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(resolver, false);
        return new WeakReference(resolver, trackResurrection: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAbandonedResolverTracker()
    {
        var resolver = new AssemblyResolver([NewUnusedDirectory()]);
        AppDomain.CurrentDomain.AssemblyResolve -= resolver.OnResolve;
        return new WeakReference(resolver, trackResurrection: true);
    }

    private static bool IsMarkedDisposed(WeakReference tracker)
        => tracker.Target is AssemblyResolver resolver && HasDisposedState(resolver);

    private static bool HasDisposedState(AssemblyResolver resolver)
        => (bool)typeof(AssemblyResolver).GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resolver)!;

    private static string NewUnusedDirectory()
        => Path.Combine(Path.GetTempPath(), "testfx-p14-resolver-" + Guid.NewGuid().ToString("N"));

    private static string NewMissingName()
        => "TestFx.P14.Missing." + Guid.NewGuid().ToString("N");

    private static void LoadMissingAssembly(string name)
    {
        Action load = () => _ = Assembly.Load(name);

        load.Should().Throw<FileNotFoundException>();
    }
}
#endif
