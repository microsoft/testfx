// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)
#if NETFRAMEWORK
using System.Reflection.Emit;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Remoting;
#endif

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AssemblyResolverContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly List<(string Name, string Directory)> _searches = [];

    public AssemblyResolverContinuationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
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

    public void FilesystemSearchReturnsDepthFirstDirectoriesAndIgnoresMissingRoots()
    {
        using ResolverDirectory fixture = new();
        string first = Directory.CreateDirectory(Path.Combine(fixture.Root, "first")).FullName;
        string firstChild = Directory.CreateDirectory(Path.Combine(first, "child")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(fixture.Root, "second")).FullName;
        string secondChild = Directory.CreateDirectory(Path.Combine(second, "child")).FullName;
        using AssemblyResolver resolver = new([fixture.Root]);
        List<string> directories = [];
#if NETFRAMEWORK
        resolver.AddSubdirectories(fixture.Root, directories);
#else
        AssemblyResolver.AddSubdirectories(fixture.Root, directories);
#endif
        directories.Should().BeEquivalentTo([first, firstChild, second, secondChild]);
        directories.IndexOf(firstChild).Should().Be(directories.IndexOf(first) + 1);
        directories.IndexOf(secondChild).Should().Be(directories.IndexOf(second) + 1);
        List<string> unchanged = ["sentinel"];
#if NETFRAMEWORK
        resolver.AddSubdirectories(Path.Combine(fixture.Root, "missing"), unchanged);
#else
        AssemblyResolver.AddSubdirectories(Path.Combine(fixture.Root, "missing"), unchanged);
#endif
        unchanged.Should().Equal("sentinel");
    }

    public void EmptyNamesAndNullRunSettingsDoNotResolveOrLog()
    {
#if DEBUG
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif
        using AssemblyResolver resolver = new([string.Empty]);
        resolver.AddSearchDirectoriesFromRunSetting(null!);
        resolver.OnResolve(null, new ResolveEventArgs(string.Empty)).Should().BeNull();
        resolver.OnResolve(null, new ResolveEventArgs(null!)).Should().BeNull();
        _provider.MockTraceLogger.Invocations.Should().BeEmpty();
#if DEBUG
        assertions.Should().Equal(
            "MSTest.AssemblyResolver.OnResolve: args.Name is null or empty.",
            "MSTest.AssemblyResolver.OnResolve: args.Name is null or empty.");
#endif
    }

    public void MissingAssemblyIsNegativelyCachedAndSystemFallbackIsCached()
    {
        using ResolverDirectory fixture = new();
        using AssemblyResolver resolver = new([fixture.Root]);
        string missing = "TestFx.Ad03.Missing." + Guid.NewGuid().ToString("N");
        string identity = missing + ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        resolver.OnResolve(null, new ResolveEventArgs(identity)).Should().BeNull();
        _searches.Where(search => search.Name == missing).Should().Equal((missing, fixture.Root));
        resolver.OnResolve(null, new ResolveEventArgs(identity)).Should().BeNull();
        _searches.Where(search => search.Name == missing).Should().HaveCount(1, "failed loads are cached, not repeatedly probed");

        Assembly core = typeof(string).Assembly;
        resolver.OnResolve(null, new ResolveEventArgs(core.FullName!)).Should().BeSameAs(core);
        int searches = _searches.Count;
        resolver.OnResolve(null, new ResolveEventArgs(core.FullName!)).Should().BeSameAs(core);
        _searches.Should().HaveCount(searches);
    }

    public void IncrementalSearchSkipsMissingPathsAndUsesRecursiveDirectories()
    {
        using ResolverDirectory fixture = new();
        string missing = Path.Combine(fixture.Root, "absent");
        string root = Directory.CreateDirectory(Path.Combine(fixture.Root, "search")).FullName;
        string child = Directory.CreateDirectory(Path.Combine(root, "first", "deep")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "second"));
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
        File.Copy(expected.Location, Path.Combine(child, Path.GetFileName(expected.Location)));
#if NETFRAMEWORK
        using TestableAssemblyResolver resolver = new([string.Empty]) { LoadAssemblyFromSetter = _ => expected };
#else
        using AssemblyResolver resolver = new([string.Empty]);
#endif
        resolver.AddSearchDirectoriesFromRunSetting(
        [
            new RecursiveDirectoryPath(missing, false),
            new RecursiveDirectoryPath(root, true),
        ]);

        Assembly resolved = resolver.OnResolve(null, new ResolveEventArgs(expected.FullName!))!;
        resolved.FullName.Should().Be(expected.FullName);
        resolved.ReflectionOnly.Should().BeFalse();
        _searches.Where(search => search.Name == expected.GetName().Name).Select(search => search.Directory)
            .Should().ContainInOrder(root, Path.GetDirectoryName(child)!, child);
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning("MSTest.AssemblyResolver.OnResolve: the directory '{0}', does not exist", missing),
            Times.Once);
        int count = _searches.Count;
        resolver.OnResolve(null, new ResolveEventArgs(expected.FullName!)).Should().BeSameAs(resolved);
        _searches.Should().HaveCount(count, "the discovered assembly must be returned from cache");
    }

    public void MalformedAssemblyIdentityAndBadImagesReturnNullWithoutPoisoningOtherLoads()
    {
        using ResolverDirectory fixture = new();
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
#if NETFRAMEWORK
        using TestableAssemblyResolver resolver = new([fixture.Root]) { LoadAssemblyFromSetter = _ => expected };
#else
        using AssemblyResolver resolver = new([fixture.Root]);
#endif
        const string malformed = "TestFx.Ad03.Invalid, Version=not-a-version";
#if NETFRAMEWORK
        Action applyInvalidPolicy = () => resolver.OnResolve(null, new ResolveEventArgs(malformed));
        applyInvalidPolicy.Should().ThrowExactly<FileLoadException>();
        using SearchProbeResolver search = new(fixture.Root);
        search.Find(malformed).Should().BeNull();
#else
        resolver.OnResolve(null, new ResolveEventArgs(malformed)).Should().BeNull();
#endif
        _provider.MockTraceLogger.Verify(
            logger => logger.Info(
                "MSTest.AssemblyResolver.OnResolve: Failed to create assemblyName '{0}'. Reason: {1} ",
                malformed, It.IsAny<Exception>()),
            Times.Once);

        string simpleName = expected.GetName().Name!;
        File.WriteAllText(Path.Combine(fixture.Root, simpleName + ".dll"), "not a managed image");
        File.Copy(expected.Location, Path.Combine(fixture.Root, simpleName + ".exe"));
        resolver.OnResolve(null, new ResolveEventArgs(expected.FullName!))!.FullName.Should().Be(expected.FullName);
        _provider.MockTraceLogger.Verify(
            logger => logger.Info(
                "MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ",
                expected.FullName, It.IsAny<BadImageFormatException>()),
            Times.Once);
    }

    public void LoggerAssemblyResolutionDoesNotReenterLogging()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled)
            .Throws(new InvalidOperationException("The logger must not be resolved recursively."));
        using AssemblyResolver resolver = new([string.Empty]);
        foreach (string prefix in new[]
        {
            "Microsoft.TestPlatform.CoreUtilities",
            "Microsoft.VisualStudio.TestPlatform.ObjectModel",
            "Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.resources",
        })
        {
            resolver.OnResolve(null, new ResolveEventArgs(prefix + ".Ad03Missing")).Should().BeNull();
        }

        _provider.MockTraceLogger.Invocations.Should().BeEmpty();
    }

#if NETFRAMEWORK
    public void EmptySearchDirectoriesReturnBeforeParsingOrLoading()
    {
        using SearchProbeResolver resolver = new(string.Empty);

        resolver.FindInEmptyDirectories("not-valid, Version=not-a-version").Should().BeNull();

        resolver.LoadPaths.Should().BeEmpty();
        _provider.MockTraceLogger.Invocations.Should().BeEmpty();
    }

    public void VersionAndPublicKeyMismatchesAreRejectedBeforeLoaderInvocation()
    {
        using ResolverDirectory fixture = new();
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
        string simpleName = expected.GetName().Name;
        File.Copy(expected.Location, Path.Combine(fixture.Root, simpleName + ".dll"));
        File.Copy(expected.Location, Path.Combine(fixture.Root, simpleName + ".exe"));
        using SearchProbeResolver resolver = new(fixture.Root);
        AssemblyName wrongVersion = new(expected.FullName) { Version = new Version(99, 0, 0, 0) };
        resolver.Find(wrongVersion.FullName).Should().BeNull();

        byte[] token = expected.GetName().GetPublicKeyToken();
        token.Should().HaveCount(8, "this project is strong named");
        foreach (int differingByte in new[] { 0, 7 })
        {
            byte[] differentToken = (byte[])token.Clone();
            differentToken[differingByte] ^= 0xff;
            AssemblyName wrongToken = new(expected.FullName);
            wrongToken.SetPublicKeyToken(differentToken);
            resolver.Find(wrongToken.FullName).Should().BeNull();
        }

        resolver.LoadPaths.Should().BeEmpty();
        resolver.Loader = _ => expected;
        Assembly resolved = resolver.Find(expected.FullName)!;
        resolved.FullName.Should().Be(expected.FullName);
        resolved.ReflectionOnly.Should().BeFalse();
        resolver.LoadPaths.Should().Equal(Path.Combine(fixture.Root, simpleName + ".dll"));
    }

    public void FileLoadExceptionsAreRethrownButOtherLoaderFailuresTryExecutable()
    {
        using ResolverDirectory fixture = new();
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
        string dll = Path.Combine(fixture.Root, expected.GetName().Name + ".dll");
        string exe = Path.Combine(fixture.Root, expected.GetName().Name + ".exe");
        File.Copy(expected.Location, dll);
        File.Copy(expected.Location, exe);
        FileLoadException failure = new("The existing image is locked.", dll);
        using (SearchProbeResolver resolver = new(fixture.Root) { Loader = _ => throw failure })
        {
            Action load = () => resolver.Find(expected.FullName);
            load.Should().ThrowExactly<FileLoadException>().Which.Should().BeSameAs(failure);
            resolver.LoadPaths.Should().Equal(dll);
        }

        using SearchProbeResolver retry = new(fixture.Root)
        {
            Loader = path => path == dll ? throw new InvalidOperationException("bad first candidate") : expected,
        };
        retry.Find(expected.FullName).Should().BeSameAs(expected);
        retry.LoadPaths.Should().Equal(dll, exe);
    }

    public void ResourceRecursionReturnsNullAndDoesNotBreakLaterLoads()
    {
        using ResolverDirectory fixture = new();
        string name = "TestFx.Ad03." + Guid.NewGuid().ToString("N") + ".resources";
        AssemblyBuilder image = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Save, fixture.Root);
        ModuleBuilder module = image.DefineDynamicModule(name, name + ".dll");
        module.DefineType("ResourceAnchor", TypeAttributes.Public).CreateType();
        image.Save(name + ".dll");
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
        using SearchProbeResolver resolver = new(fixture.Root);
        int loads = 0;
        resolver.Loader = _ =>
        {
            loads++;
            resolver.Find(name).Should().BeNull("the same resource candidate is already being loaded");
            return expected;
        };

        resolver.Find(name).Should().BeSameAs(expected);
        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeSameAs(expected);
        loads.Should().Be(1);
        _provider.MockTraceLogger.Verify(
            logger => logger.Info(
                "MSTest.AssemblyResolver.OnResolve: Assembly '{0}' is searching for itself recursively '{1}', returning as not found.",
                name, Path.Combine(fixture.Root, name + ".dll")),
            Times.Once);
        resolver.Loader = null;
        resolver.Find(typeof(string).Assembly.FullName).Should().BeNull("an unrelated absent candidate must not inherit the resource recursion state");
    }

    public void ReflectionOnlyFallbackAndCachedMissesStayIsolatedFromNormalLoads()
    {
        using SearchProbeResolver resolver = new(string.Empty);
        Assembly normal = typeof(XmlDocument).Assembly;
        Assembly reflected = resolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(normal.FullName))!;
        reflected.FullName.Should().Be(normal.FullName);
        reflected.ReflectionOnly.Should().BeTrue();
        resolver.OnResolve(null, new ResolveEventArgs(normal.FullName)).Should().BeSameAs(normal);
        resolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(normal.FullName)).Should().BeSameAs(reflected);
        resolver.OnResolve(null, new ResolveEventArgs(normal.FullName)).Should().BeSameAs(normal);

        string missing = "TestFx.Ad03.ReflectionMissing." + Guid.NewGuid().ToString("N");
        resolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(missing)).Should().BeNull();
        resolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(missing)).Should().BeNull();
        resolver.OnResolve(null, new ResolveEventArgs(missing)).Should().BeNull();
        resolver.OnResolve(null, new ResolveEventArgs(missing)).Should().BeNull();
        resolver.SearchModes.Should().Equal(true, false, true, false);
    }

    public void RecursiveSettingsRemainLiveAcrossDomainAndDriveDependencyResolution()
    {
        using ResolverDirectory fixture = new();
        string search = Directory.CreateDirectory(Path.Combine(fixture.Root, "search")).FullName;
        string deep = Directory.CreateDirectory(Path.Combine(search, "nested")).FullName;
        Assembly expected = typeof(AssemblyResolverContinuationTests).Assembly;
        File.Copy(expected.Location, Path.Combine(deep, Path.GetFileName(expected.Location)));
        RecursiveDirectoryPath settings = new(search, true);
        ResolutionWorker worker = fixture.CreateWorker();

        string[] observed = worker.ResolveFromSetting(settings, expected.FullName);

        observed.Should().Equal(expected.FullName, "True", "True", "True");
    }

    public void WinRtMetadataLoadsDependentNamespacesAndCalendarSignatures()
    {
        using ResolverDirectory fixture = new();
        ResolutionWorker worker = fixture.CreateWorker();
        string metadata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WinMetadata", "Windows.Globalization.winmd");

        WinRtObservation observed = worker.ReadWinRt(metadata);

        observed.TypeNames.Should().Contain("Windows.Globalization.Calendar");
        observed.MethodNames.Should().Contain("AddDays").And.Contain("GetDateTime");
        observed.NamespaceBindings.Keys.Should().Contain("Windows.Foundation").And.Contain("Windows.UI.Text");
        observed.NamespaceBindings["Windows.Foundation"].Should().Contain("Windows.Foundation");
        observed.NamespaceBindings["Windows.UI.Text"].Should().Contain("Windows.UI");
        observed.AllBindingsAreReflectionOnly.Should().BeTrue();
    }

    private sealed class SearchProbeResolver(string directory) : AssemblyResolver([directory])
    {
        public Func<string, Assembly>? Loader { get; set; }

        public List<string> LoadPaths { get; } = [];

        public List<bool> SearchModes { get; } = [];

        public Assembly? Find(string name) => base.SearchAssembly([directory], name, false);

        public Assembly? FindInEmptyDirectories(string name) => base.SearchAssembly([], name, false);

        protected override Assembly? SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
        {
            SearchModes.Add(isReflectionOnly);
            return base.SearchAssembly(searchDirectorypaths, name, isReflectionOnly);
        }

        protected override Assembly LoadAssemblyFrom(string path)
        {
            LoadPaths.Add(path);
            return Loader is null ? base.LoadAssemblyFrom(path) : Loader(path);
        }
    }

    [Serializable]
    public sealed class WinRtObservation
    {
        public string[] TypeNames { get; set; } = [];

        public string[] MethodNames { get; set; } = [];

        public Dictionary<string, string[]> NamespaceBindings { get; set; } = [];

        public bool AllBindingsAreReflectionOnly { get; set; } = true;
    }

    public sealed class ResolutionWorker : MarshalByRefObject
    {
        public string[] ResolveFromSetting(object value, string identity)
        {
            var settings = (RecursiveDirectoryPath)value;
            using AssemblyResolver resolver = new([string.Empty]);
            resolver.AddSearchDirectoriesFromRunSetting([settings]);
            Assembly resolved = resolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(identity))!;
            return
            [
                resolved.FullName,
                resolved.ReflectionOnly.ToString(),
                RemotingServices.IsTransparentProxy(settings).ToString(),
                (settings.GetLifetimeService() is null).ToString(),
            ];
        }

        public WinRtObservation ReadWinRt(string metadata)
        {
            WinRtObservation observed = new();
            void Record(object? sender, NamespaceResolveEventArgs args) => observed.NamespaceBindings[args.NamespaceName] = [];

            WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += Record;
            try
            {
                using AssemblyResolver resolver = new([Path.GetDirectoryName(metadata)]);
                var assembly = Assembly.ReflectionOnlyLoadFrom(metadata);
                observed.TypeNames = assembly.GetTypes().Select(type => type.FullName).ToArray();
                Type calendar = assembly.GetType("Windows.Globalization.Calendar", throwOnError: true)!;
                MethodInfo[] methods = calendar.GetMethods();
                foreach (MethodInfo method in methods)
                {
                    _ = method.ReturnType;
                    _ = method.GetParameters();
                }

                observed.MethodNames = methods.Select(method => method.Name).ToArray();
                Assembly[] bindings = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies();
                foreach (string requestedNamespace in observed.NamespaceBindings.Keys.ToArray())
                {
                    Assembly[] matches = bindings.Where(binding => requestedNamespace == binding.GetName().Name
                        || requestedNamespace.StartsWith(binding.GetName().Name + ".", StringComparison.Ordinal)).ToArray();
                    observed.NamespaceBindings[requestedNamespace] = matches.Select(binding => binding.GetName().Name).ToArray();
                    observed.AllBindingsAreReflectionOnly &= matches.Length > 0 && matches.All(binding => binding.ReflectionOnly);
                }

                return observed;
            }
            finally
            {
                WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve -= Record;
            }
        }
    }
#endif

    private sealed class ResolverDirectory : IDisposable
    {
#if NETFRAMEWORK
        private AppDomain? _domain;
#endif

        public ResolverDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-resolver-" + Guid.NewGuid().ToString("N"));

#if NETFRAMEWORK
        public ResolutionWorker CreateWorker()
        {
            _domain = AppDomain.CreateDomain("Adapter resolution " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
            {
                ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
            });
            return (ResolutionWorker)_domain.CreateInstanceFromAndUnwrap(typeof(ResolutionWorker).Assembly.Location, typeof(ResolutionWorker).FullName);
        }
#endif

        public void Dispose()
        {
#if NETFRAMEWORK
            if (_domain is not null)
            {
                AppDomain.Unload(_domain);
            }
#endif
            Directory.Delete(Root, recursive: true);
        }
    }
}
#endif
