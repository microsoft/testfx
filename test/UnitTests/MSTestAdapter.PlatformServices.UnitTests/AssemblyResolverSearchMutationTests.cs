// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET && !WINDOWS_UWP

using System.Reflection.Emit;
using System.Runtime.ExceptionServices;

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AssemblyResolverSearchMutationTests : TestContainer
{
    private const string SearchingFormat = "MSTest.AssemblyResolver.OnResolve: Searching assembly '{0}' in the directory '{1}'";
    private const string RecursionFormat = "MSTest.AssemblyResolver.OnResolve: Assembly '{0}' is searching for itself recursively '{1}', returning as not found.";
    private const string ResolvedAssemblyFormat = "MSTest.AssemblyResolver.OnResolve: Resolved assembly '{0}'";
    private const string FailedToLoadFormat = "MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ";

    private static readonly FieldInfo CurrentlyLoadingField = typeof(AssemblyResolver).GetField("s_currentlyLoading", BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly ConcurrentQueue<string> _entries = new();
    private Action<string>? _onInfo;

    public AssemblyResolverSearchMutationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsVerboseEnabled).Returns(true);
        _provider.MockTraceLogger.Setup(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) =>
            {
                _entries.Enqueue(Render("Info", format, arguments));
                _onInfo?.Invoke(format);
            });
        _provider.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _entries.Enqueue(Render("Warning", format, arguments)));
        _provider.MockTraceLogger.Setup(logger => logger.Verbose(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _entries.Enqueue(Render("Verbose", format, arguments)));
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void AddSubdirectoriesReportsAMissingPath()
    {
        using AssertionRecorder assertions = new();

        AssemblyResolver.AddSubdirectories(string.Empty, []);
        AssemblyResolver.AddSubdirectories(null!, []);

#if DEBUG
        assertions.Messages.Should().Equal("'path' cannot be null or empty.", "'path' cannot be null or empty.");
#endif
    }

    public void AddSubdirectoriesReportsAMissingCollection()
    {
        using AssertionRecorder assertions = new();

        AssemblyResolver.AddSubdirectories(NewUnusedDirectory(), null!);

#if DEBUG
        assertions.Messages.Should().Equal("'searchDirectories' cannot be null.");
#endif
    }

    public void SearchTracesEachNonEmptyDirectoryItLooksIn()
    {
        using TempDirectory first = new();
        using TempDirectory second = new();
        string name = NewMissingName();
        using AssemblyResolver resolver = CreateDetachedResolver(first.Root);

        InvokeSearch(resolver, [first.Root, string.Empty, second.Root], name).Should().BeNull();

        EntriesMentioning(name).Should().Equal(
            Entry("Verbose", SearchingFormat, name, first.Root),
            Entry("Verbose", SearchingFormat, name, second.Root));
    }

    public void RequestedAssemblyNamesMatchFoundOnesOnlyOnPublicKeyTokenAndVersion()
    {
        const string Token = "PublicKeyToken=b03f5f7f11d50a3a";
        (string Description, string Requested, string Found, bool Expected)[] cases =
        [
            ("a name without token and version matches any strong named assembly", "Foo", $"Foo, Version=2.0.0.0, {Token}", true),
            ("a name without token and version matches an assembly without token", "Foo", "Foo", true),
            ("the same token matches", $"Foo, {Token}", $"Foo, {Token}", true),
            ("a different first byte of the token does not match", $"Foo, {Token}", "Foo, PublicKeyToken=a03f5f7f11d50a3a", false),
            ("a different middle byte of the token does not match", $"Foo, {Token}", "Foo, PublicKeyToken=b03f5f7f01d50a3a", false),
            ("a different last byte of the token does not match", $"Foo, {Token}", "Foo, PublicKeyToken=b03f5f7f11d50a3b", false),
            ("a requested token is not met by an assembly without token", $"Foo, {Token}", "Foo", false),
            ("an explicitly empty token matches any token", "Foo, PublicKeyToken=null", $"Foo, {Token}", true),
            ("the same version matches", "Foo, Version=1.0.0.0", "Foo, Version=1.0.0.0", true),
            ("a different version does not match", "Foo, Version=1.0.0.0", "Foo, Version=2.0.0.0", false),
            ("a requested version is not met by an assembly without version", "Foo, Version=1.0.0.0", "Foo", false),
            ("the same token and version match", $"Foo, Version=1.0.0.0, {Token}", $"Foo, Version=1.0.0.0, {Token}", true),
            ("the same token does not compensate a different version", $"Foo, Version=1.0.0.0, {Token}", $"Foo, Version=2.0.0.0, {Token}", false),
        ];

        foreach ((string description, string requested, string found, bool expected) in cases)
        {
            InvokeNameMatch(new AssemblyName(requested), new AssemblyName(found)).Should().Be(expected, because: description);
        }
    }

    public void RequestedAssemblyNameMatchingReportsMissingNames()
    {
        using AssertionRecorder assertions = new();

        Action withoutRequestedName = () => InvokeNameMatch(null!, new AssemblyName("Foo"));
        withoutRequestedName.Should().ThrowExactly<NullReferenceException>();
        InvokeNameMatch(new AssemblyName("Foo"), null!).Should().BeTrue("a requested name without token and version does not look at the found one");

#if DEBUG
        assertions.Messages.Should().Equal("requested assembly name should not be null.", "found assembly name should not be null.");
#endif
    }

    public void AFileWithTheRequestedIdentityIsLoadedCachedAndReported()
    {
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchMutationTests).Assembly;
        string name = NewUniqueName();
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearchAndLoad(resolver, path, name, new AssemblyName(name)).Should().BeSameAs(testAssembly);

        GetCache(resolver, "_resolvedAssemblies").Should().ContainKey(name).WhoseValue.Should().BeSameAs(testAssembly);
        EntriesMentioning(name).Should().Equal(Entry("Info", ResolvedAssemblyFormat, name));
    }

    public void LoadingAFileDoesNotTraceWhenInformationLoggingIsDisabled()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchMutationTests).Assembly;
        string name = NewUniqueName();
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearchAndLoad(resolver, path, name, new AssemblyName(name)).Should().BeSameAs(testAssembly);

        _entries.Should().BeEmpty();
    }

    public void AFileWithAnotherVersionIsNeitherLoadedNorCached()
    {
        using TempDirectory directory = new();
        string name = NewUniqueName();
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearchAndLoad(resolver, path, name, new AssemblyName(name + ", Version=99.9.9.9")).Should().BeNull();

        GetCache(resolver, "_resolvedAssemblies").Should().NotContainKey(name);
        _entries.Should().BeEmpty();
    }

    public void AMissingFileIsSkippedWithoutAnyTrace()
    {
        using TempDirectory directory = new();
        string name = NewUniqueName();
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        InvokeSearchAndLoad(resolver, Path.Combine(directory.Root, name + ".dll"), name, new AssemblyName(name)).Should().BeNull();

        _entries.Should().BeEmpty();
    }

    public void AFileThatIsFoundButCannotBeLoadedIsReportedAndTheFailureIsRethrown()
    {
        using TempDirectory directory = new();

        // An assembly that has the simple name of an assembly that is already loaded cannot be loaded in its place.
        string name = typeof(AssemblyResolverSearchMutationTests).Assembly.GetName().Name!;
        string path = Path.Combine(directory.Root, name + ".dll");
        var builder = new PersistedAssemblyBuilder(new AssemblyName(name) { Version = new Version(99, 0, 0, 0) }, typeof(object).Assembly);
        builder.DefineDynamicModule(name).DefineType("Anchor", TypeAttributes.Public | TypeAttributes.Class).CreateType();
        builder.Save(path);
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        Action load = () => InvokeSearchAndLoad(resolver, path, name, new AssemblyName(name));

        load.Should().Throw<FileLoadException>();
        _entries.Should().Equal(Entry("Info", FailedToLoadFormat, name, nameof(FileLoadException)));
    }

    public void SearchingAResourceTracksItsPathWhileLoadingAndLeavesNothingBehind()
    {
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchMutationTests).Assembly;
        string name = NewUniqueName() + ".resources";
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        using CurrentlyLoadingScope scope = new(seed: null);
        List<string[]?> snapshots = [];
        _onInfo = format => snapshots.Add(GetCurrentlyLoading()?.ToArray());

        InvokeSearch(resolver, [directory.Root], name).Should().BeSameAs(testAssembly);

        snapshots.Should().ContainSingle().Which.Should().Equal(path);
        GetCurrentlyLoading().Should().NotBeNull().And.BeEmpty();
#if DEBUG
        assertions.Messages.Should().BeEmpty();
#endif
    }

    public void SearchingAResourceKeepsTheLoadsThatAreAlreadyPending()
    {
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchMutationTests).Assembly;
        string name = NewUniqueName() + ".resources";
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        List<string> pending = ["unrelated"];
        using CurrentlyLoadingScope scope = new(pending);
        List<string[]?> snapshots = [];
        _onInfo = format => snapshots.Add(GetCurrentlyLoading()?.ToArray());

        InvokeSearch(resolver, [directory.Root], name).Should().BeSameAs(testAssembly);

        snapshots.Should().ContainSingle().Which.Should().Equal("unrelated", path);
        GetCurrentlyLoading().Should().BeSameAs(pending);
        pending.Should().Equal("unrelated");
#if DEBUG
        assertions.Messages.Should().BeEmpty();
#endif
    }

    public void SearchingAnAssemblyThatIsNotAResourceDoesNotTrackPendingLoads()
    {
        using TempDirectory directory = new();
        Assembly testAssembly = typeof(AssemblyResolverSearchMutationTests).Assembly;
        string name = NewUniqueName();
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        List<string> pending = ["unrelated"];
        using CurrentlyLoadingScope scope = new(pending);
        List<string[]?> snapshots = [];
        _onInfo = format => snapshots.Add(GetCurrentlyLoading()?.ToArray());

        InvokeSearch(resolver, [directory.Root], name).Should().BeSameAs(testAssembly);

        snapshots.Should().ContainSingle().Which.Should().Equal("unrelated");
        GetCurrentlyLoading().Should().BeSameAs(pending);
        pending.Should().Equal("unrelated");
#if DEBUG
        assertions.Messages.Should().BeEmpty();
#endif
    }

    public void SearchingAResourceThatIsAlreadyBeingLoadedStopsTheRecursionAndRemembersTheFailure()
    {
        using TempDirectory directory = new();
        string name = NewUniqueName() + ".resources";
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        List<string> pending = ["unrelated", path, "other"];
        using CurrentlyLoadingScope scope = new(pending);

        InvokeSearch(resolver, [directory.Root], name).Should().BeNull();

        EntriesMentioning(name).Should().Equal(
            Entry("Verbose", SearchingFormat, name, directory.Root),
            Entry("Info", RecursionFormat, name, path));
        GetCache(resolver, "_resolvedAssemblies").Should().ContainKey(name).WhoseValue.Should().BeNull();
        pending.Should().Equal("unrelated", path, "other");
#if DEBUG
        assertions.Messages.Should().BeEmpty();
#endif
    }

    public void TheRecursionOfAResourceSearchIsNotTracedWhenInformationLoggingIsDisabled()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        using TempDirectory directory = new();
        string name = NewUniqueName() + ".resources";
        string path = CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        List<string> pending = [path];
        using CurrentlyLoadingScope scope = new(pending);

        InvokeSearch(resolver, [directory.Root], name).Should().BeNull();

        EntriesMentioning(name).Should().Equal(Entry("Verbose", SearchingFormat, name, directory.Root));
    }

    public void SearchFailsWhenThePendingLoadsDisappearWhileAResourceIsLoading()
    {
        using TempDirectory directory = new();
        string name = NewUniqueName() + ".resources";
        CopyTestAssemblyTo(directory.Root, name + ".dll");
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using AssertionRecorder assertions = new();
        using CurrentlyLoadingScope scope = new(seed: null);
        _onInfo = format => CurrentlyLoadingField.SetValue(null, null);

        Action search = () => InvokeSearch(resolver, [directory.Root], name);

        search.Should().ThrowExactly<NullReferenceException>();
#if DEBUG
        assertions.Messages.Should().Equal("_currentlyLoading should not be null");
#endif
    }

    private static AssemblyResolver CreateDetachedResolver(params string[] directories)
    {
        // Without its subscription, the resolver only sees what the test hands to it.
        var resolver = new AssemblyResolver(directories);
        AppDomain.CurrentDomain.AssemblyResolve -= resolver.OnResolve;
        return resolver;
    }

    private static string NewUnusedDirectory()
        => Path.Combine(Path.GetTempPath(), "testfx-p14-resolver-" + Guid.NewGuid().ToString("N"));

    private static string NewMissingName()
        => "TestFx.P14.Missing." + Guid.NewGuid().ToString("N");

    private static string NewUniqueName()
        => "P14.Search." + Guid.NewGuid().ToString("N");

    private static string CopyTestAssemblyTo(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        File.Copy(typeof(AssemblyResolverSearchMutationTests).Assembly.Location, path);
        return path;
    }

    private static List<string>? GetCurrentlyLoading()
        => (List<string>?)CurrentlyLoadingField.GetValue(null);

    private static Dictionary<string, Assembly?> GetCache(AssemblyResolver resolver, string fieldName)
        => (Dictionary<string, Assembly?>)typeof(AssemblyResolver).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resolver)!;

    private static Assembly? InvokeSearch(AssemblyResolver resolver, List<string> directories, string name)
        => Invoke<Assembly?>(resolver, "SearchAssembly", directories, name, false);

    private static Assembly? InvokeSearchAndLoad(AssemblyResolver resolver, string path, string name, AssemblyName requested)
        => Invoke<Assembly?>(resolver, "SearchAndLoadAssembly", path, name, requested, false);

    private static bool InvokeNameMatch(AssemblyName requested, AssemblyName found)
        => Invoke<bool>(null, "RequestedAssemblyNameMatchesFound", requested, found);

    private static T Invoke<T>(object? instance, string methodName, params object?[] arguments)
    {
        MethodInfo method = typeof(AssemblyResolver).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            return (T)method.Invoke(instance, arguments)!;
        }
        catch (TargetInvocationException exception)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
    }

    private static string Entry(string level, string format, params string[] arguments)
        => Render(level, format, arguments);

    private static string Render(string level, string format, IEnumerable<object?> arguments)
        => $"{level}: {format} [{string.Join(", ", arguments.Select(argument => argument is Exception exception ? exception.GetType().Name : argument?.ToString()))}]";

    private string[] EntriesMentioning(string text)
        => [.. _entries.Where(entry => entry.Contains(text, StringComparison.Ordinal))];

    private sealed class AssertionRecorder : IDisposable
    {
#if DEBUG
        private readonly DiagnosticAssertionObserver _observer;
#endif

        public AssertionRecorder()
        {
#if DEBUG
            _observer = new DiagnosticAssertionObserver(Messages.Add);
#endif
        }

        public List<string?> Messages { get; } = [];

        public void Dispose()
        {
#if DEBUG
            _observer.Dispose();
#endif
        }
    }

    private sealed class CurrentlyLoadingScope : IDisposable
    {
        private readonly List<string>? _original = GetCurrentlyLoading();

        public CurrentlyLoadingScope(List<string>? seed) => CurrentlyLoadingField.SetValue(null, seed);

        public void Dispose() => CurrentlyLoadingField.SetValue(null, _original);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-p14-resolver-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
