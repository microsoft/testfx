// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
#if NETFRAMEWORK
using System.Reflection.Emit;
using System.Security;
using System.Security.AccessControl;
using System.Security.Policy;
using System.Security.Principal;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestSourceHostContinuationTests : TestContainer
{
    private const string DisableAppDomain = "<RunSettings><RunConfiguration><DisableAppDomain>true</DisableAppDomain></RunConfiguration></RunSettings>";
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public TestSourceHostContinuationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void MissingWorkingDirectoryReportsFailureWithoutChangingContext()
    {
        using HostDirectory fixture = new();
        string previous = Environment.CurrentDirectory;
        string directory = Path.Combine(fixture.Root, "absent", "nested");

        using (TestSourceHost host = new(Path.Combine(directory, "tests.dll"), DisableAppDomain))
        {
            Environment.CurrentDirectory.Should().Be(previous);
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("MSTestExecutor.SetWorkingDirectory: Failed to set the working directory to '{0}'. {1}", directory, It.IsAny<DirectoryNotFoundException>()),
                Times.Once);
        }

        Environment.CurrentDirectory.Should().Be(previous);
    }

    public void DisappearingOriginalDirectoryIsReportedAndDisposeRemainsSafe()
    {
        using HostDirectory fixture = new();
        string previous = Environment.CurrentDirectory;
        string original = Directory.CreateDirectory(Path.Combine(fixture.Root, "original")).FullName;
        string target = Directory.CreateDirectory(Path.Combine(fixture.Root, "target")).FullName;
        TestSourceHost? host = null;
        try
        {
            Environment.CurrentDirectory = original;
            host = new TestSourceHost(Path.Combine(target, "tests.dll"), DisableAppDomain);
            Environment.CurrentDirectory.Should().Be(target);
            Directory.Delete(original);

            host.Dispose();

            Environment.CurrentDirectory.Should().Be(target);
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("MSTestExecutor.ResetWorkingDirectory: Failed to restore the working directory to '{0}'. {1}", original, It.IsAny<DirectoryNotFoundException>()),
                Times.Once);
        }
        finally
        {
            host?.Dispose();
            Environment.CurrentDirectory = previous;
        }
    }

    public void HostConstructionAndDisposalRestoreDirectoryAndForwardConstructorArguments()
    {
        using HostDirectory fixture = new();
        string previous = Environment.CurrentDirectory;
        using (TestSourceHost host = new(Path.Combine(fixture.Root, "tests.dll"), DisableAppDomain))
        {
            Environment.CurrentDirectory.Should().Be(fixture.Root);
            var payload = (HostPayload)host.CreateInstanceForType(typeof(HostPayload), ["sentinel", 7])!;
            payload.Describe().Should().Be("sentinel:7");
            host.UsesAppDomain.Should().BeFalse();
        }

        Environment.CurrentDirectory.Should().Be(previous);
    }

#if !NETFRAMEWORK
    public void CoreSetupUsesTheSuppliedResolutionPathsAndRetainsActivation()
    {
        using HostDirectory fixture = new();
        string previous = Environment.CurrentDirectory;
        string source = Path.Combine(fixture.Root, "tests.dll");
        Mock<TestSourceHost> host = new(source, DisableAppDomain) { CallBase = true };
        host.Setup(value => value.GetResolutionPaths(source, false)).Returns([fixture.Root]);
        using (host.Object)
        {
            host.Object.SetupHost();
            var payload = (HostPayload)host.Object.CreateInstanceForType(typeof(HostPayload), ["configured", 4])!;
            payload.Describe().Should().Be("configured:4");
            host.Verify(value => value.GetResolutionPaths(source, false), Times.Once);
            _provider.MockTraceLogger.Verify(
                logger => logger.Info("DesktopTestSourceHost.SetupHost(): Creating assembly resolver with resolution paths {0}.", fixture.Root),
                Times.Once);
        }

        Environment.CurrentDirectory.Should().Be(previous);
    }
#endif

#if WIN_UI
    public void WinUiRootSourceUsesTheAdapterDirectoryFallback()
    {
        string previous = Environment.CurrentDirectory;
        string root = Path.GetPathRoot(previous)!;
        using (TestSourceHost host = new(root, DisableAppDomain))
        {
            Environment.CurrentDirectory.Should().Be(Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location));
            var payload = (HostPayload)host.CreateInstanceForType(typeof(HostPayload), ["root fallback", 3])!;
            payload.Describe().Should().Be("root fallback:3");
        }

        Environment.CurrentDirectory.Should().Be(previous);
    }
#endif

#if NETFRAMEWORK
    public void DeniedWorkingDirectoryReportsUnauthorizedAccessWithoutChangingContext()
    {
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
                    It.Is<UnauthorizedAccessException>(exception => exception.HResult == unchecked((int)0x80070005))),
                Times.Once);
        }

        Directory.GetFiles(directory).Should().BeEmpty("the owned directory's access rules must have been restored");
        Environment.CurrentDirectory.Should().Be(previous);
    }

    public void DeniedOriginalDirectoryIsReportedAndCanBeRestoredAfterAccessReturns()
    {
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
                        It.Is<UnauthorizedAccessException>(exception => exception.HResult == unchecked((int)0x80070005))),
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

    public void ResolutionPathsIncludeADistinctLoadedObjectModelDirectory()
    {
        using HostDirectory fixture = new();
        string directory = Directory.CreateDirectory(Path.Combine(fixture.Root, "object-model")).FullName;
        string original = typeof(Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase).Assembly.Location;
        string copied = Path.Combine(directory, Path.GetFileName(original));
        File.Copy(original, copied);
        HostWorker worker = fixture.CreateWorker(directory);
        string source = Path.Combine(fixture.Root, "tests.dll");

        string[] paths = worker.ReadSeparatedResolutionPaths(copied, source);

        paths.Should().Equal(copied, fixture.Root, Path.GetDirectoryName(typeof(TestSourceHost).Assembly.Location), directory);
    }

    public void UnloadFailureIsLoggedAndClearedWithoutASecondUnload()
    {
        Mock<IAppDomain> domains = new();
        AppDomain? created = null;
        domains.Setup(value => value.CreateDomain(It.IsAny<string>(), It.IsAny<Evidence>(), It.IsAny<AppDomainSetup>()))
            .Returns((string name, Evidence _, AppDomainSetup setup) => created = AppDomain.CreateDomain(name, null, setup));
        CannotUnloadAppDomainException failure = new("test-owned unload failure");
        domains.Setup(value => value.Unload(It.IsAny<AppDomain>())).Throws(failure);
        TestSourceHost host = new(typeof(TestSourceHostContinuationTests).Assembly.Location, null, domains.Object);
        try
        {
            host.SetupHost();
            AppDomain child = created!;
            host.AppDomain.Should().BeSameAs(child);

            host.Dispose();
            host.Dispose();

            host.AppDomain.Should().BeNull();
            child.GetAssemblies().Should().NotBeEmpty("the collaborator rejected unload; the test still owns this live domain");
            domains.Verify(value => value.Unload(child), Times.Once);
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("DesktopTestSourceHost.Dispose(): The app domain running tests could not be unloaded. Exception: {0}", failure),
                Times.Once);
        }
        finally
        {
            if (created is not null)
            {
                AppDomain.Unload(created);
            }

            host.Dispose();
        }
    }

    public void AdditionalSearchDirectoriesAreUsedByTheHostResolver()
    {
        using HostDirectory fixture = new();
        HostWorker worker = fixture.CreateWorker();
        string previous = Environment.CurrentDirectory;

        string[] observed = worker.ResolveAdditional(fixture.Root);

        observed[0].Should().Be(Path.Combine(fixture.Root, "extra", "nested", observed[1] + ".dll"));
        observed[2].Should().Be("HostDependency");
        observed[3].Should().Be(fixture.Root);
        observed[4].Should().Be(previous);
        Environment.CurrentDirectory.Should().Be(previous);
    }

    public void SearchDirectoryLoggingFailuresDoNotPreventHostUse()
    {
        using HostDirectory fixture = new();
        HostWorker worker = fixture.CreateWorker();
        string previous = Environment.CurrentDirectory;

        string[] observed = worker.ReadWithFailingDirectoryLogger(fixture.Root);

        observed.Should().Equal(
            "still usable:9",
            "DesktopTestSourceHost.AddSearchDirectoriesSpecifiedInRunSettingsToAssemblyResolver(): Exception hit while trying to set assembly resolver for domain. Exception : {0} \n Message : {1}",
            typeof(InvalidOperationException).FullName,
            "warning collaborator failed");
        Environment.CurrentDirectory.Should().Be(previous);
    }

    public sealed class HostWorker : MarshalByRefObject
    {
        public string[] ReadSeparatedResolutionPaths(string objectModel, string source)
        {
            var loaded = Assembly.LoadFrom(objectModel);
            using TestSourceHost host = new(null!, DisableAppDomain);
            return [loaded.Location, .. host.GetResolutionPaths(source, isPortableMode: true)];
        }

        public string[] ResolveAdditional(string root)
        {
            string directory = Directory.CreateDirectory(Path.Combine(root, "extra", "nested")).FullName;
            string name = "Ad03HostDependency" + Guid.NewGuid().ToString("N");
            AssemblyBuilder image = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Save, directory);
            ModuleBuilder module = image.DefineDynamicModule(name, name + ".dll");
            module.DefineType("HostDependency", TypeAttributes.Public).CreateType();
            image.Save(name + ".dll");
            Configure(Path.Combine(root, "extra"));
            string[] result;
            using (TestSourceHost host = new(Path.Combine(root, "host.txt"), DisableAppDomain))
            {
                host.SetupHost();
                var loaded = Assembly.Load(new AssemblyName(name));
                object instance = host.CreateInstanceForType(loaded.GetType("HostDependency", throwOnError: true)!, null)!;
                result = [loaded.Location, name, instance.GetType().FullName, Environment.CurrentDirectory, string.Empty];
            }

            result[4] = Environment.CurrentDirectory;
            return result;
        }

        public string[] ReadWithFailingDirectoryLogger(string root)
        {
            Configure(Path.Combine(root, "absent"));
            TestablePlatformServiceProvider provider = new();
            provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
            provider.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>()))
                .Throws(new InvalidOperationException("warning collaborator failed"));
            string? observedFormat = null;
            Exception? observedException = null;
            provider.MockTraceLogger.Setup(logger => logger.Error(It.IsAny<string>(), It.IsAny<object?[]>()))
                .Callback<string, object?[]>((format, arguments) =>
                {
                    observedFormat = format;
                    observedException = (Exception)arguments[0]!;
                });
            PlatformServiceProvider.Instance = provider;
            using TestSourceHost host = new(Path.Combine(root, "host.txt"), DisableAppDomain);
            host.SetupHost();
            var payload = (HostPayload)host.CreateInstanceForType(typeof(HostPayload), ["still usable", 9])!;
            return [payload.Describe(), observedFormat!, observedException!.GetType().FullName, observedException.Message];
        }

        private static void Configure(string search)
        {
            MSTestSettingsProvider.Reset();
            using StringReader text = new($"<MSTest><AssemblyResolution><Directory path='{SecurityElement.Escape(search)}' includeSubDirectories='true' /></AssemblyResolution></MSTest>");
            using var reader = XmlReader.Create(text);
            reader.MoveToContent();
            MSTestAdapterSettings.ToSettings(reader);
        }
    }

    private sealed class DirectoryReadDenial : IDisposable
    {
        private readonly string _path;
        private readonly byte[] _original;

        public DirectoryReadDenial(string path)
        {
            _path = path;
            DirectorySecurity access = Directory.GetAccessControl(path, AccessControlSections.Access);
            _original = access.GetSecurityDescriptorBinaryForm();
            using var identity = WindowsIdentity.GetCurrent();
            access.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.ReadAndExecute, AccessControlType.Deny));
            Directory.SetAccessControl(path, access);
        }

        public void Dispose()
        {
            DirectorySecurity original = new();
            original.SetSecurityDescriptorBinaryForm(_original, AccessControlSections.Access);
            Directory.SetAccessControl(_path, original);
        }
    }
#endif

    public sealed class HostPayload(string token, int count)
    {
        public string Describe() => $"{token}:{count}";
    }

    private sealed class HostDirectory : IDisposable
    {
#if NETFRAMEWORK
        private AppDomain? _domain;
#endif

        public HostDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-host-" + Guid.NewGuid().ToString("N"));

#if NETFRAMEWORK
        public HostWorker CreateWorker(string? applicationBase = null)
        {
            _domain = AppDomain.CreateDomain("Host continuation " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
            {
                ApplicationBase = applicationBase ?? AppDomain.CurrentDomain.BaseDirectory,
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
            });
            return (HostWorker)_domain.CreateInstanceFromAndUnwrap(typeof(HostWorker).Assembly.Location, typeof(HostWorker).FullName);
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
