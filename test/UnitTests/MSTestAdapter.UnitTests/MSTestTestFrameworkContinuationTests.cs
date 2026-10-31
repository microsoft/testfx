// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using NativeConfiguration = Microsoft.Testing.Platform.Configurations.IConfiguration;
using NativeServiceProvider = Microsoft.Testing.Platform.Services.ServiceProvider;
using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestTestFrameworkContinuationTests : TestContainer
{
    public async Task SessionLifecycleRejectsDuplicatesAndAlwaysCompletesRequests()
    {
        using var harness = new NativeHarness();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Action createCanceled = () => _ = harness.Framework.CreateTestSessionAsync(new(harness.Session, canceled.Token));
        createCanceled.Should().ThrowExactly<OperationCanceledException>().Which.CancellationToken.Should().Be(canceled.Token);
        (await harness.Framework.CreateTestSessionAsync(new(harness.Session, CancellationToken.None))).IsSuccess.Should().BeTrue();
        (await harness.Framework.IsEnabledAsync()).Should().BeTrue();

        Action duplicate = () => _ = harness.Framework.CreateTestSessionAsync(new(new SessionUid("duplicate"), CancellationToken.None));
        duplicate.Should().ThrowExactly<InvalidOperationException>().WithMessage(
            string.Format(CultureInfo.CurrentCulture, PlatformAdapterResources.VSTestBridgedTestFrameworkSessionAlreadyCreatedErrorMessage, harness.Session.Value));

        var notifier = new Mock<IExecuteRequestCompletionNotifier>();
        var unsupported = new UnsupportedRequest();
        Func<Task> execute = () => harness.Framework.ExecuteRequestAsync(new(unsupported, harness.Bus.Object, notifier.Object, CancellationToken.None));
        (await execute.Should().ThrowExactlyAsync<NotSupportedException>()).WithMessage(
            $"The native MSTest framework does not support requests of type '{typeof(UnsupportedRequest)}'.");
        notifier.Verify(value => value.Complete(), Times.Once);
        harness.Messages.Should().BeEmpty();
        (await harness.Framework.CloseTestSessionAsync(new(harness.Session, CancellationToken.None))).IsSuccess.Should().BeTrue();
    }

    public async Task DiscoveryAndRunPublishExactSelectedNodes()
    {
        using var harness = new NativeHarness();
        await harness.OpenAsync();
        await harness.DiscoverAsync(nameof(NativeCoverageFixture.Succeeds));
        TestNodeUpdateMessage discovered = harness.Messages.OfType<TestNodeUpdateMessage>().Single();
        discovered.TestNode.DisplayName.Should().Be("Succeeds");
        discovered.TestNode.Properties.Any<DiscoveredTestNodeStateProperty>().Should().BeTrue();
        discovered.SessionUid.Should().Be(harness.Session);
        harness.Messages.Clear();

        await harness.RunAsync(new TestNodeUidListFilter([discovered.TestNode.Uid]));

        TestNodeUpdateMessage[] nodes = harness.Messages.OfType<TestNodeUpdateMessage>().ToArray();
        nodes.Should().HaveCount(2);
        nodes[0].TestNode.Uid.Should().Be(discovered.TestNode.Uid);
        nodes[0].TestNode.Properties.Any<InProgressTestNodeStateProperty>().Should().BeTrue();
        nodes[1].TestNode.Uid.Should().Be(discovered.TestNode.Uid);
        nodes[1].TestNode.Properties.Any<PassedTestNodeStateProperty>().Should().BeTrue();
        nodes[1].TestNode.DisplayName.Should().Be("Succeeds");
        harness.Notifier.Verify(value => value.Complete(), Times.Exactly(2));
        harness.Framework.DataTypesProduced.Should().Equal(typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact));
        await harness.CloseAsync();
    }

    public async Task AttachmentStagingPreservesBytesAndCleansUpDurableCopies()
    {
        using var harness = new NativeHarness();
        string source = Path.Combine(harness.Directory, "original.bin");
        string missing = Path.Combine(harness.Directory, "missing.bin");
        byte[] bytes = [0, 17, 128, 255, 42];
        File.WriteAllBytes(source, bytes);
        harness.SetRunSettings(new XDocument(
            new XElement("RunSettings",
                new XElement("MSTest", new XElement("DeploymentEnabled", false)),
                new XElement("RunConfiguration", new XElement("DisableAppDomain", true)),
                new XElement("TestRunParameters",
                    new XElement("Parameter", new XAttribute("name", "attachment"), new XAttribute("value", source)),
                    new XElement("Parameter", new XAttribute("name", "missing"), new XAttribute("value", missing))))).ToString());
        await harness.OpenAsync();

        await harness.RunAsync(harness.Filter(nameof(NativeCoverageFixture.AttachesFiles)));

        TestNodeUpdateMessage result = harness.Messages.OfType<TestNodeUpdateMessage>()
            .Single(message => !message.TestNode.Properties.Any<InProgressTestNodeStateProperty>());
        result.TestNode.Properties.Any<PassedTestNodeStateProperty>().Should().BeTrue();
        FileArtifactProperty[] artifacts = result.TestNode.Properties.OfType<FileArtifactProperty>().ToArray();
        artifacts.Should().HaveCount(2);
        string staged = artifacts.Single(artifact => artifact.FileInfo.Name == "original.bin").FileInfo.FullName;
        staged.Should().NotBe(source);
        Path.GetFileName(staged).Should().Be("original.bin");
        File.ReadAllBytes(staged).Should().Equal(bytes);
        FileArtifactProperty missingArtifact = artifacts.Single(artifact => artifact.FileInfo.Name == "missing.bin");
        missingArtifact.FileInfo.FullName.Should().Be(missing);
        missingArtifact.FileInfo.Exists.Should().BeFalse();

        await harness.CloseAsync();
        harness.Framework.Dispose();
        harness.Framework.Dispose();
        File.Exists(staged).Should().BeFalse();
        File.ReadAllBytes(source).Should().Equal(bytes);
        foreach (IDisposable cleanup in harness.Services.Services.OfType<IDisposable>())
        {
            cleanup.Dispose();
            cleanup.Dispose();
        }

        Directory.GetDirectories(harness.Directory, ".mstest-*").Should().BeEmpty();
    }

    public async Task ClosingSessionWaitsForTheOutstandingRequest()
    {
        using var harness = new NativeHarness();
        await harness.OpenAsync();
        var published = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
            .Callback(() => published.TrySetResult(null))
            .Returns(release.Task);

        Task request = Task.Run(() => harness.DiscoverAsync(nameof(NativeCoverageFixture.Succeeds)));
        await published.Task;
        Task<CloseTestSessionResult> close = harness.Framework.CloseTestSessionAsync(new(harness.Session, CancellationToken.None));
        try
        {
            close.IsCompleted.Should().BeFalse();
        }
        finally
        {
            release.TrySetResult(null);
        }

        await request;
        (await close).IsSuccess.Should().BeTrue();
        harness.Notifier.Verify(value => value.Complete(), Times.Once);
    }

    public async Task EmbeddedAssemblyIdentityUsesTheDocumentedFallback()
    {
        using var harness = new NativeHarness(() => [new EmbeddedAssembly("Embedded.Coverage")]);
        await harness.OpenAsync();
        Func<Task> loadMissing = () => harness.DiscoverAsync(nameof(NativeCoverageFixture.Succeeds));
        FileNotFoundException missing = (await loadMissing.Should().ThrowExactlyAsync<FileNotFoundException>()).Which;
#if NETFRAMEWORK || WIN_UI
        missing.Message.Should().Contain("Embedded.Coverage.dll");
#else
        missing.FileName.Should().StartWith("Embedded.Coverage");
#endif
        harness.Messages.Should().BeEmpty();
        harness.Notifier.Verify(value => value.Complete(), Times.Once);

        using var unnamed = new NativeHarness(() => [new EmbeddedAssembly(null)]);
        await unnamed.OpenAsync();
        Func<Task> discover = () => unnamed.DiscoverAsync(nameof(NativeCoverageFixture.Succeeds));
        (await discover.Should().ThrowExactlyAsync<InvalidOperationException>()).WithMessage("Cannot determine the name of assembly 'embedded'.");
        unnamed.Notifier.Verify(value => value.Complete(), Times.Once);
    }

    public async Task RootCleanupRecoversFromTheHostSpecificOpenAttachmentContract()
    {
        using var harness = new NativeHarness();
        string source = Path.Combine(harness.Directory, "locked-source.bin");
        string missing = Path.Combine(harness.Directory, "missing.bin");
        File.WriteAllBytes(source, [17, 23, 42]);
        harness.SetRunSettings(new XDocument(
            new XElement("RunSettings",
                new XElement("MSTest", new XElement("DeploymentEnabled", false)),
                new XElement("RunConfiguration", new XElement("DisableAppDomain", true)),
                new XElement("TestRunParameters",
                    new XElement("Parameter", new XAttribute("name", "attachment"), new XAttribute("value", source)),
                    new XElement("Parameter", new XAttribute("name", "missing"), new XAttribute("value", missing))))).ToString());
        var logger = Mock.Get(harness.Services.Services.OfType<ILoggerFactory>().Single().CreateLogger("mstest-trace"));
        List<string> warnings = [];
        logger.Setup(value => value.Log(
            LogLevel.Warning, It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, state, exception, formatter) => warnings.Add(formatter(state, exception)));
        await harness.OpenAsync();
        await harness.RunAsync(harness.Filter(nameof(NativeCoverageFixture.AttachesFiles)));
        string staged = harness.Messages.OfType<TestNodeUpdateMessage>()
            .SelectMany(message => message.TestNode.Properties.OfType<FileArtifactProperty>())
            .Single(artifact => artifact.FileInfo.Name == "locked-source.bin").FileInfo.FullName;

        using (var held = new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            harness.Framework.Dispose();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.Exists(staged).Should().BeTrue();
                warnings.Should().ContainSingle();
                warnings[0].Should().StartWith("Failed to delete result-file staging directory '").And.Contain("IOException");
            }
            else
            {
                File.Exists(staged).Should().BeFalse();
                warnings.Should().BeEmpty();
            }
        }

        foreach (IDisposable cleanup in harness.Services.Services.OfType<IDisposable>())
        {
            cleanup.Dispose();
            cleanup.Dispose();
        }

        File.Exists(staged).Should().BeFalse();
        File.ReadAllBytes(source).Should().Equal(17, 23, 42);
    }

    internal static NativeServiceProvider CreateServices(string resultsDirectory, ICommandLineOptions options, IFileSystem fileSystem)
    {
        var configuration = new Mock<NativeConfiguration>();
        configuration.Setup(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(resultsDirectory);
        configuration.Setup(value => value[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]).Returns(resultsDirectory);
        var logger = new Mock<ILogger>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        var services = new NativeServiceProvider();
        services.AddServices([
            configuration.Object, options, fileSystem, loggerFactory.Object,
            Mock.Of<IOutputDevice>(), new ClientInfoService("unit-client", "1.0", new ClientCapabilitiesService(false)),
            Mock.Of<ITelemetryInformation>(), Mock.Of<IPlatformInformation>(),
        ]);
        return services;
    }

    private sealed class NativeHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;
        private readonly Mock<IFileSystem> _files = new();
        private readonly Mock<ICommandLineOptions> _options = new();

        internal NativeHarness(Func<IEnumerable<Assembly>>? assemblies = null)
        {
            Directory = Path.Combine(Path.GetTempPath(), "native-adapter-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            _previous = PlatformServiceProvider.Instance;
            PlatformServiceProvider.Instance = null!;
            string[]? noArguments = null;
            _options.Setup(value => value.TryGetOptionArgumentList(It.IsAny<string>(), out noArguments)).Returns(false);
            Services = CreateServices(Directory, _options.Object, _files.Object);
            Framework = new(new MSTestExtension(), assemblies ?? (() => [typeof(NativeCoverageFixture).Assembly]), Services,
                new TestFrameworkCapabilities(MSTestGracefulStopTestExecutionCapability.Create()));
            Bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, message) => Messages.Add(message)).Returns(Task.CompletedTask);
            SetRunSettings("<RunSettings><MSTest><DeploymentEnabled>false</DeploymentEnabled></MSTest><RunConfiguration><DisableAppDomain>true</DisableAppDomain></RunConfiguration></RunSettings>");
        }

        internal string Directory { get; }

        internal NativeServiceProvider Services { get; }

        internal MSTestTestFramework Framework { get; }

        internal SessionUid Session { get; } = new("native-coverage-session");

        internal Mock<IMessageBus> Bus { get; } = new();

        internal Mock<IExecuteRequestCompletionNotifier> Notifier { get; } = new();

        internal List<IData> Messages { get; } = [];

        internal void SetRunSettings(string xml)
        {
            string[]? arguments = ["native.runsettings"];
            _options.Setup(value => value.TryGetOptionArgumentList("settings", out arguments)).Returns(true);
            _files.Setup(value => value.ExistFile("native.runsettings")).Returns(true);
            _files.Setup(value => value.ReadAllText("native.runsettings")).Returns(xml);
        }

        internal async Task OpenAsync()
            => (await Framework.CreateTestSessionAsync(new(Session, CancellationToken.None))).IsSuccess.Should().BeTrue();

        internal async Task CloseAsync()
            => (await Framework.CloseTestSessionAsync(new(Session, CancellationToken.None))).IsSuccess.Should().BeTrue();

        internal TestNodeUidListFilter Filter(string method)
            => new([new TestNodeUid(typeof(NativeCoverageFixture).FullName + "." + method)]);

        internal Task DiscoverAsync(string method)
            => Framework.ExecuteRequestAsync(new(
                new DiscoverTestExecutionRequest(new TestSessionContext(Session), Filter(method)),
                Bus.Object, Notifier.Object, CancellationToken.None));

        internal Task RunAsync(ITestExecutionFilter filter)
            => Framework.ExecuteRequestAsync(new(
                new RunTestExecutionRequest(new TestSessionContext(Session), filter),
                Bus.Object, Notifier.Object, CancellationToken.None));

        public void Dispose()
        {
            Framework.Dispose();
            MSTestInstrumentation.SetActivityFactory(null);
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private sealed class UnsupportedRequest : IRequest
    {
        public TestSessionContext Session { get; } = new(new SessionUid("unsupported-session"));
    }

    private sealed class EmbeddedAssembly(string? name) : Assembly
    {
        public override string Location => string.Empty;

        public override AssemblyName GetName() => name is null ? new AssemblyName() : new AssemblyName(name);

        public override string ToString() => "embedded";
    }

    [TestClass]
    public sealed class NativeCoverageFixture
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void Succeeds() => TestContext.WriteLine("native fixture completed");

        [TestMethod]
        public void AttachesFiles()
        {
            TestContext.AddResultFile((string)TestContext.Properties["attachment"]!);
            TestContext.AddResultFile((string)TestContext.Properties["missing"]!);
        }
    }
}
