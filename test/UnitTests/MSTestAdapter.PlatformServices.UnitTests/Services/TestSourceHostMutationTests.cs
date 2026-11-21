// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !NETFRAMEWORK
using System.Runtime.Loader;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

[DoNotParallelize]
public sealed class TestSourceHostMutationTests : TestContainer
{
    private const string DisableAppDomain = "<RunSettings><RunConfiguration><DisableAppDomain>true</DisableAppDomain></RunConfiguration></RunSettings>";
    private const string ResolveRequestFormat = "MSTest.AssemblyResolver.OnResolve: Resolving assembly '{0}'";
    private const string MissingDirectoryFormat = "MSTest.AssemblyResolver.OnResolve: the directory '{0}', does not exist";
    private static readonly ConcurrentQueue<string> FinalizedHosts = new();
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly ConcurrentQueue<string> _resolveRequests = new();
    private readonly string _previousDirectory = Environment.CurrentDirectory;

    public TestSourceHostMutationTests()
    {
        MSTestSettingsProvider.Reset();
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        _provider.MockTraceLogger.Setup(logger => logger.Info(ResolveRequestFormat, It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, arguments) => _resolveRequests.Enqueue((string)arguments[0]!));
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            Environment.CurrentDirectory = _previousDirectory;
        }
        finally
        {
            PlatformServiceProvider.Instance = _previousProvider;
            MSTestSettingsProvider.Reset();
            base.Dispose(disposing);
        }
    }

    public void SetupHostWithoutSearchDirectoriesDoesNotKeepTheAssemblyResolverSubscribed()
    {
        using HostDirectory fixture = new();
        int baseline = CountResolveRequestsForAMissingAssembly();
        Mock<TestSourceHost> host = CreateHost(fixture);
        using (host.Object)
        {
            host.Object.SetupHost();

            CountResolveRequestsForAMissingAssembly().Should().Be(baseline);
        }
    }

    public void SetupHostKeepsTheAssemblyResolverSubscribedForTheConfiguredSearchDirectories()
    {
        using HostDirectory fixture = new();
        string search = Directory.CreateDirectory(Path.Combine(fixture.Root, "search")).FullName;
        Configure(search);
        Mock<TestSourceHost> host = CreateHost(fixture);
        using (host.Object)
        {
            host.Object.SetupHost();
            Directory.Delete(search);

            LoadMissingAssembly();

            _provider.MockTraceLogger.Verify(logger => logger.Warning(MissingDirectoryFormat, search), Times.Once);
        }
    }

    public void DisposingTheHostUnsubscribesTheAssemblyResolver()
    {
        using HostDirectory fixture = new();
        string search = Directory.CreateDirectory(Path.Combine(fixture.Root, "search")).FullName;
        Configure(search);
        TestSourceHost host = CreateHost(fixture).Object;
        try
        {
            host.SetupHost();

            host.Dispose();
            Directory.Delete(search);
            LoadMissingAssembly();

            _provider.MockTraceLogger.Verify(logger => logger.Warning(MissingDirectoryFormat, search), Times.Never);
        }
        finally
        {
            host.Dispose();
        }
    }

    public void SearchDirectoryFailuresAreLoggedAndDoNotKeepTheAssemblyResolverSubscribed()
    {
        using HostDirectory fixture = new();
        Configure(Path.Combine(fixture.Root, "absent"));
        _provider.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>()))
            .Throws(new InvalidOperationException("warning collaborator failed"));
        int baseline = CountResolveRequestsForAMissingAssembly();
        Mock<TestSourceHost> host = CreateHost(fixture);
        using (host.Object)
        {
            host.Object.SetupHost();

            _provider.MockTraceLogger.Verify(
                logger => logger.Error(
                    "DesktopTestSourceHost.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(): Exception hit while trying to set assembly resolver for domain. Exception : {0} \n Message : {1}",
                    It.Is<InvalidOperationException>(exception => exception.Message == "warning collaborator failed"),
                    "warning collaborator failed"),
                Times.Once);
            CountResolveRequestsForAMissingAssembly().Should().Be(baseline);
        }
    }

    public void ResolutionPathsReportThatTheHostIsNotRunningInPortableModeOnlyWhenItIsNot()
    {
        using HostDirectory fixture = new();
        using TestSourceHost host = new(null!, DisableAppDomain);
        string source = Path.Combine(fixture.Root, "tests.dll");

        host.GetResolutionPaths(source, isPortableMode: true)[0].Should().Be(fixture.Root);

        _provider.MockTraceLogger.Verify(logger => logger.Info("DesktopTestSourceHost.GetResolutionPaths(): Not running in portable mode"), Times.Never);

        host.GetResolutionPaths(source, isPortableMode: false)[0].Should().Be(fixture.Root);

        _provider.MockTraceLogger.Verify(logger => logger.Info("DesktopTestSourceHost.GetResolutionPaths(): Not running in portable mode"), Times.Once);
    }

    public void LoadedAssemblyLookupFallsBackToLoadingTheAssemblyByName()
    {
        string[] candidates =
        [
            "System.Net.Mail",
            "System.Formats.Tar",
            "System.Net.WebSockets.Client",
            "System.Net.HttpListener",
            "System.Runtime.Serialization.Formatters",
            "System.IO.Compression.Brotli",
            "System.Net.Requests",
        ];
        string? notYetLoaded = candidates.FirstOrDefault(candidate => !IsLoaded(candidate));
        notYetLoaded.Should().NotBeNull("one of the framework assemblies has to be unloaded for the fallback to be exercised");
        MethodInfo getLoadedAssembly = typeof(TestSourceHost).GetMethod("GetLoadedAssembly", BindingFlags.Static | BindingFlags.NonPublic)!;

        var loaded = (Assembly)getLoadedAssembly.Invoke(null, [notYetLoaded])!;

        loaded.GetName().Name.Should().Be(notYetLoaded);
    }

    public void DeniedWorkingDirectoryReportsUnauthorizedAccessWithoutChangingContext()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using HostDirectory fixture = new();
        string directory = Directory.CreateDirectory(Path.Combine(fixture.Root, "denied")).FullName;
        string previous = Environment.CurrentDirectory;
        using (new DirectoryReadDenial(directory))
        {
            using TestSourceHost host = new(Path.Combine(directory, "tests.dll"), DisableAppDomain);

            Environment.CurrentDirectory.Should().Be(previous);
            _provider.MockTraceLogger.Verify(
                logger => logger.Error(
                    "MSTestExecutor.SetWorkingDirectory: Failed to set the working directory to '{0}'. {1}",
                    directory,
                    It.IsAny<UnauthorizedAccessException>()),
                Times.Once);
        }

        Directory.GetFiles(directory).Should().BeEmpty("the owned directory's access rules must have been restored");
        Environment.CurrentDirectory.Should().Be(previous);
    }

    public void DeniedOriginalDirectoryIsReportedAndRestoredOnceAccessReturns()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using HostDirectory fixture = new();
        string original = Directory.CreateDirectory(Path.Combine(fixture.Root, "original")).FullName;
        string target = Directory.CreateDirectory(Path.Combine(fixture.Root, "target")).FullName;
        string previous = Environment.CurrentDirectory;
        TestSourceHost? host = null;
        try
        {
            Environment.CurrentDirectory = original;
            host = new TestSourceHost(Path.Combine(target, "tests.dll"), DisableAppDomain);
            using (new DirectoryReadDenial(original))
            {
                host.Dispose();

                Environment.CurrentDirectory.Should().Be(target);
                _provider.MockTraceLogger.Verify(
                    logger => logger.Error(
                        "MSTestExecutor.ResetWorkingDirectory: Failed to restore the working directory to '{0}'. {1}",
                        original,
                        It.IsAny<UnauthorizedAccessException>()),
                    Times.Once);
            }

            host.Dispose();

            Environment.CurrentDirectory.Should().Be(original);
        }
        finally
        {
            host?.Dispose();
            Environment.CurrentDirectory = previous;
        }
    }

    public void DisposingTheHostSuppressesItsFinalization()
    {
        string disposed = "disposed-" + Guid.NewGuid().ToString("N");
        string abandoned = "abandoned-" + Guid.NewGuid().ToString("N");
        CreateFinalizableHost(disposed, dispose: true);
        CreateFinalizableHost(abandoned, dispose: false);

        // The abandoned host proves that finalizers have run, which makes the absence of the disposed one meaningful.
        for (int attempt = 0; attempt < 20 && !FinalizedHosts.Contains(abandoned); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        FinalizedHosts.Should().Contain(abandoned);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        FinalizedHosts.Should().NotContain(disposed);
    }

    public void ResolutionPathsListTheAdapterAndTheTestPlatformDirectoriesIndependently()
    {
        string adapterDirectory = Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location)!;
        string testPlatformDirectory = Path.GetDirectoryName(typeof(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase).Assembly.Location)!;
        string root = Path.Combine(Path.GetTempPath(), "mutation-adapter-" + Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(root, "source");
        string source = Path.Combine(sourceDirectory, "tests.dll");
        Directory.CreateDirectory(root);
        try
        {
            using TestSourceHost host = new(null!, null);

            host.GetResolutionPaths(source, isPortableMode: true).Should().Contain(adapterDirectory).And.Contain(testPlatformDirectory);

            // The adapter and the test platform share a directory in the test output, so each of them hides that the other one
            // is added. A copy of the adapter loaded from a different directory tells the two additions apart.
            List<string> isolatedPaths = GetResolutionPathsFromAdapterCopy(root, source, out string copiedAdapterDirectory);

            isolatedPaths.Should().Equal(sourceDirectory, copiedAdapterDirectory, testPlatformDirectory);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static void Configure(string search)
    {
        MSTestSettingsProvider.Reset();
        using StringReader text = new($"<MSTest><AssemblyResolution><Directory path='{SecurityElement.Escape(search)}' includeSubDirectories='true' /></AssemblyResolution></MSTest>");
        using var reader = XmlReader.Create(text);
        reader.MoveToContent();
        MSTestAdapterSettings.ToSettings(reader);
    }

    private static Mock<TestSourceHost> CreateHost(HostDirectory fixture)
    {
        string source = Path.Combine(fixture.Root, "tests.dll");
        Mock<TestSourceHost> host = new(source, DisableAppDomain) { CallBase = true };
        host.Setup(value => value.GetResolutionPaths(source, false)).Returns([fixture.Root]);
        return host;
    }

    private static string LoadMissingAssembly()
    {
        string name = "MutationProbe" + Guid.NewGuid().ToString("N");
        Action load = () => Assembly.Load(new AssemblyName(name));
        load.Should().Throw<FileNotFoundException>();
        return name;
    }

    private static bool IsLoaded(string simpleName)
        => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateFinalizableHost(string name, bool dispose)
    {
        FinalizableTestSourceHost host = new(name);
        if (dispose)
        {
            host.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<string> GetResolutionPathsFromAdapterCopy(string root, string source, out string adapterDirectory)
    {
        string location = typeof(TestSourceHost).Assembly.Location;
        string copyDirectory = Directory.CreateDirectory(Path.Combine(root, "adapter")).FullName;
        string copyLocation = Path.Combine(copyDirectory, Path.GetFileName(location));
        File.Copy(location, copyLocation);
        CollectibleLoadContext context = new();
        try
        {
            Assembly adapter = context.LoadFromAssemblyPath(copyLocation);
            adapterDirectory = Path.GetDirectoryName(adapter.Location)!;
            Type hostType = adapter.GetType(typeof(TestSourceHost).FullName!, throwOnError: true)!;
            object host = Activator.CreateInstance(hostType, new object?[] { null, null })!;
            try
            {
                MethodInfo getResolutionPaths = hostType.GetMethod(nameof(TestSourceHost.GetResolutionPaths), BindingFlags.Instance | BindingFlags.NonPublic)!;
                return (List<string>)getResolutionPaths.Invoke(host, [source, true])!;
            }
            finally
            {
                ((IDisposable)host).Dispose();
            }
        }
        finally
        {
            context.Unload();
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (int attempt = 0; attempt < 10 && Directory.Exists(path); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The copy of the adapter stays locked until its load context has been collected.
            }
        }
    }

    private int CountResolveRequestsForAMissingAssembly()
    {
        string name = LoadMissingAssembly();
        return _resolveRequests.Count(request => request.StartsWith(name, StringComparison.Ordinal));
    }

    [SupportedOSPlatform("windows")]
    private sealed class DirectoryReadDenial : IDisposable
    {
        private readonly string _path;
        private readonly byte[] _original;

        public DirectoryReadDenial(string path)
        {
            _path = path;
            DirectoryInfo directory = new(path);
            DirectorySecurity access = directory.GetAccessControl(AccessControlSections.Access);
            _original = access.GetSecurityDescriptorBinaryForm();
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.ReadAndExecute, AccessControlType.Deny));
            directory.SetAccessControl(access);
        }

        public void Dispose()
        {
            DirectorySecurity original = new();
            original.SetSecurityDescriptorBinaryForm(_original, AccessControlSections.Access);
            new DirectoryInfo(_path).SetAccessControl(original);
        }
    }

    private sealed class HostDirectory : IDisposable
    {
        public HostDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "mutation-host-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class FinalizableTestSourceHost : TestSourceHost
    {
        private readonly string _name;

        public FinalizableTestSourceHost(string name)
            : base(string.Empty, null)
        {
            _name = name;
        }

        ~FinalizableTestSourceHost()
        {
            FinalizedHosts.Enqueue(_name);
        }
    }

    private sealed class CollectibleLoadContext : AssemblyLoadContext
    {
        public CollectibleLoadContext()
            : base(isCollectible: true)
        {
        }
    }
}
#endif
