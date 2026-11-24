// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class AppDomainUtilitiesContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public AppDomainUtilitiesContinuationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void FrameworkVersionFailuresAreReportedAndTemporaryDomainsAreUnloaded()
    {
        using DomainDirectory fixture = new();
        AppDomainUtilities.GetTargetFrameworkVersionString(Path.Combine(fixture.Root, "absent.dll")).Should().BeEmpty();
        string invalidImage = Path.Combine(fixture.Root, "invalid.dll");
        File.WriteAllText(invalidImage, "not a PE image");

        AppDomainUtilities.GetTargetFrameworkVersionString(invalidImage).Should().BeEmpty();
        _provider.MockTraceLogger.Verify(
            logger => logger.Error("AssemblyHelper:GetTargetFrameworkVersionString() caught BadImageFormatException. Falling to native binary."),
            Times.Once);

        AppDomain? created = null;
        InvalidOperationException failure = new("remote trace listener setup failed");
        _provider.MockTraceLogger.Setup(logger => logger.SetupRemoteEqtTraceListeners(It.IsAny<AppDomain>()))
            .Callback<AppDomain>(domain =>
            {
                created = domain;
                throw failure;
            });
        AppDomainUtilities.GetTargetFrameworkVersionString(typeof(AppDomainUtilitiesContinuationTests).Assembly.Location).Should().BeEmpty();
        _provider.MockTraceLogger.Verify(logger => logger.Error(failure), Times.Once);
        created.Should().NotBeNull();
        Action queryUnloadedDomain = () => created!.GetAssemblies();
        queryUnloadedDomain.Should().ThrowExactly<AppDomainUnloadedException>();
    }

    public void ConfigurationFailurePreservesCanonicalPathAndReportsOriginalException()
    {
        using DomainDirectory fixture = new();
        string path = Path.Combine(fixture.Root, "source.config");
        InvalidDataException failure = new("invalid redirection input");
        XmlUtilities previous = AppDomainUtilities.XmlUtilities;
        AppDomainUtilities.XmlUtilities = new FailingXmlUtilities(failure);
        try
        {
            AppDomainSetup setup = new();

            AppDomainUtilities.SetConfigurationFile(setup, path);

            setup.ConfigurationFile.Should().Be(path);
            setup.GetConfigurationBytes().Should().BeNull();
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("Exception hit while adding binding redirects to test source config file. Exception : {0}", failure),
                Times.Once);
            _provider.MockTraceLogger.Verify(
                logger => logger.Info("UnitTestAdapter: Using configuration file {0} to setup appdomain for test source {1}.", path, "source"),
                Times.Once);
        }
        finally
        {
            AppDomainUtilities.XmlUtilities = previous;
        }
    }

    public void FrameworkBoundaryUpdatesOnlyNewerTargetNames()
    {
        const string previous = ".NETFramework,Version=v4.0";
        AppDomainSetup setup = new() { TargetFrameworkName = previous };
        foreach (string version in new[] { string.Empty, ".NETFramework,Version=v4.0", ".NETFramework,Version=v4.5" })
        {
            AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(setup, version);
            setup.TargetFrameworkName.Should().Be(previous);
        }

        AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(setup, ".NETFramework,Version=v4.5.1");
        setup.TargetFrameworkName.Should().Be(".NETFramework,Version=v4.5.1");
        AppDomainUtilities.SetAppDomainFrameworkVersionBasedOnTestSource(setup, ".NETFramework,Version=v4.8");
        setup.TargetFrameworkName.Should().Be(".NETFramework,Version=v4.8");
    }

    public void CrossDomainActivationUsesLoadFromAndRetainsCultureWhenLoggerCannotBeMarshaled()
    {
        using DomainDirectory fixture = new();
        AppDomain domain = fixture.CreateDomain();
        string domainName = domain.FriendlyName;
        CultureInfo? previous = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
            var worker = (DomainValueWorker)AppDomainUtilities.CreateInstance(domain, typeof(DomainValueWorker), ["prefix", 7]);

            worker.Render("abc").Should().Be("prefix:7:cba:fr-FR");
            worker.DomainId().Should().Be(domain.Id).And.NotBe(AppDomain.CurrentDomain.Id);
            File.Exists(Path.Combine(fixture.Root, Path.GetFileName(typeof(DomainValueWorker).Assembly.Location))).Should().BeFalse();
            _provider.MockTraceLogger.Verify(
                logger => logger.Warning(It.Is<string>(message => message.StartsWith("Failed to set trace logger '", StringComparison.Ordinal)
                    && message.Contains(domainName))),
                Times.Once);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previous;
        }
    }

    private sealed class FailingXmlUtilities(Exception failure) : XmlUtilities
    {
        internal override XmlDocument GetXmlDocument(string configFile) => throw failure;
    }

    public sealed class DomainValueWorker(string prefix, int offset) : MarshalByRefObject
    {
        public string Render(string value) => $"{prefix}:{offset}:{new string(value.Reverse().ToArray())}:{CultureInfo.DefaultThreadCurrentUICulture?.Name}";

        public int DomainId() => AppDomain.CurrentDomain.Id;
    }

    private sealed class DomainDirectory : IDisposable
    {
        private AppDomain? _domain;

        public DomainDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-domain-" + Guid.NewGuid().ToString("N"));

        public AppDomain CreateDomain() => _domain = AppDomain.CreateDomain("Adapter activation " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
        {
            ApplicationBase = Root,
            ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
        });

        public void Dispose()
        {
            if (_domain is not null)
            {
                AppDomain.Unload(_domain);
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
#endif
