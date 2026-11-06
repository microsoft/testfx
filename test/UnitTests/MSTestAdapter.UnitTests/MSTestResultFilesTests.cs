// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

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
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using NativeConfiguration = Microsoft.Testing.Platform.Configurations.IConfiguration;
using NativeServiceProvider = Microsoft.Testing.Platform.Services.ServiceProvider;
using TestSessionContext = Microsoft.Testing.Platform.TestHost.TestSessionContext;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestResultFilesTests : TestContainer
{
    public async Task PublishedResultFilesSurviveFrameworkAndServiceDisposal()
    {
        foreach (bool missingFirst in new[] { false, true })
        {
            using var harness = new NativeHarness();
            string source = Path.Combine(harness.Directory, "original.bin");
            string missing = Path.Combine(harness.Directory, "missing.bin");
            byte[] bytes = [0, 17, 128, 255, 42];
            File.WriteAllBytes(source, bytes);
            harness.SetAttachmentPaths(missingFirst ? missing : source, missingFirst ? source : missing);
            await harness.OpenAsync();

            await harness.RunAsync();

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
            File.Exists(staged).Should().BeTrue("published result files outlive framework disposal");
            File.ReadAllBytes(source).Should().Equal(bytes);
            foreach (IDisposable cleanup in harness.Services.Services.OfType<IDisposable>())
            {
                cleanup.Dispose();
                cleanup.Dispose();
            }

            File.Exists(staged).Should().BeTrue("published result files outlive service disposal");
            File.ReadAllBytes(staged).Should().Equal(bytes);
            string resultDirectory = Directory.GetDirectories(harness.Directory, "MSTest-*").Single();
            Path.GetFileName(resultDirectory).Should().MatchRegex(@"^MSTest-[0-9a-f]{20}$");
        }
    }

    public async Task DisposalPreservesOpenPublishedAttachmentsWithoutWarnings()
    {
        using var harness = new NativeHarness();
        string source = Path.Combine(harness.Directory, "locked-source.bin");
        string missing = Path.Combine(harness.Directory, "missing.bin");
        File.WriteAllBytes(source, [17, 23, 42]);
        harness.SetAttachmentPaths(source, missing);
        List<string> warnings = harness.CaptureWarnings();
        await harness.OpenAsync();
        await harness.RunAsync();
        string staged = harness.Messages.OfType<TestNodeUpdateMessage>()
            .SelectMany(message => message.TestNode.Properties.OfType<FileArtifactProperty>())
            .Single(artifact => artifact.FileInfo.Name == "locked-source.bin").FileInfo.FullName;
        await harness.CloseAsync();

        using (var held = new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            harness.Framework.Dispose();
            File.Exists(staged).Should().BeTrue();
            warnings.Should().BeEmpty();

            foreach (IDisposable cleanup in harness.Services.Services.OfType<IDisposable>())
            {
                cleanup.Dispose();
                cleanup.Dispose();
            }

            File.Exists(staged).Should().BeTrue();
            warnings.Should().BeEmpty();
        }

        File.ReadAllBytes(staged).Should().Equal(17, 23, 42);
        File.ReadAllBytes(source).Should().Equal(17, 23, 42);
    }

    public async Task FailedResultFileCopiesRemainEligibleForCleanup()
    {
        using var harness = new NativeHarness();
        string missing = Path.Combine(harness.Directory, "missing.bin");
        harness.SetAttachmentPaths(missing, missing);
        List<string> warnings = harness.CaptureWarnings();
        await harness.OpenAsync();
        await harness.RunAsync();
        await harness.CloseAsync();
        FileArtifactProperty[] artifacts = harness.Messages.OfType<TestNodeUpdateMessage>()
            .SelectMany(message => message.TestNode.Properties.OfType<FileArtifactProperty>())
            .ToArray();
        artifacts.Should().HaveCount(2);
        artifacts.Should().OnlyContain(artifact => artifact.FileInfo.FullName == missing && !artifact.FileInfo.Exists);
        string resultDirectory = Directory.GetDirectories(harness.Directory, "MSTest-*").Single();
        string heldPath = Path.Combine(resultDirectory, "cleanup-lock.bin");
        File.WriteAllBytes(heldPath, [17, 23, 42]);

        using (var held = new FileStream(heldPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            foreach (IDisposable cleanup in harness.Services.Services.OfType<IDisposable>())
            {
                cleanup.Dispose();
                cleanup.Dispose();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.Exists(heldPath).Should().BeTrue();
                warnings.Should().ContainSingle();
                warnings[0].Should().StartWith("Failed to delete result-file staging directory '").And.Contain("IOException");
            }
            else
            {
                File.Exists(heldPath).Should().BeFalse();
                warnings.Should().BeEmpty();
            }
        }

        harness.Framework.Dispose();
        harness.Framework.Dispose();
        Directory.GetDirectories(harness.Directory, "MSTest-*").Should().BeEmpty();
        File.Exists(missing).Should().BeFalse();
    }

    private sealed class NativeHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;
        private readonly Mock<IFileSystem> _files = new();
        private readonly Mock<ICommandLineOptions> _options = new();

        internal NativeHarness()
        {
            Directory = Path.Combine(Path.GetTempPath(), "native-result-files-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            _previous = PlatformServiceProvider.Instance;
            PlatformServiceProvider.Instance = null!;
            var configuration = new Mock<NativeConfiguration>();
            configuration.Setup(value => value[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(Directory);
            configuration.Setup(value => value[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory]).Returns(Directory);
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(value => value.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
            string[]? noArguments = null;
            _options.Setup(value => value.TryGetOptionArgumentList(It.IsAny<string>(), out noArguments)).Returns(false);
            Services = new NativeServiceProvider();
            Services.AddServices([
                configuration.Object, _options.Object, _files.Object, loggerFactory.Object,
                Mock.Of<IOutputDevice>(), new ClientInfoService("unit-client", "1.0", new ClientCapabilitiesService(false)),
                Mock.Of<ITelemetryInformation>(), Mock.Of<IPlatformInformation>(),
            ]);
            Framework = new(new MSTestExtension(), () => [typeof(AttachmentFixture).Assembly], Services,
                new TestFrameworkCapabilities(MSTestGracefulStopTestExecutionCapability.Create()));
            Bus.Setup(value => value.PublishAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>()))
                .Callback<IDataProducer, IData>((_, message) => Messages.Add(message)).Returns(Task.CompletedTask);
        }

        internal string Directory { get; }

        internal NativeServiceProvider Services { get; }

        internal MSTestTestFramework Framework { get; }

        internal SessionUid Session { get; } = new("native-result-files-session");

        internal Mock<IMessageBus> Bus { get; } = new();

        internal List<IData> Messages { get; } = [];

        internal void SetAttachmentPaths(string first, string second)
        {
            string xml = new XDocument(
                new XElement(
                    "RunSettings",
                    new XElement("MSTest", new XElement("DeploymentEnabled", false)),
                    new XElement("RunConfiguration", new XElement("DisableAppDomain", true)),
                    new XElement(
                        "TestRunParameters",
                        new XElement("Parameter", new XAttribute("name", "attachment"), new XAttribute("value", first)),
                        new XElement("Parameter", new XAttribute("name", "missing"), new XAttribute("value", second))))).ToString();
            string[]? arguments = ["native.runsettings"];
            _options.Setup(value => value.TryGetOptionArgumentList("settings", out arguments)).Returns(true);
            _files.Setup(value => value.ExistFile("native.runsettings")).Returns(true);
            _files.Setup(value => value.ReadAllText("native.runsettings")).Returns(xml);
        }

        internal List<string> CaptureWarnings()
        {
            var logger = Mock.Get(Services.Services.OfType<ILoggerFactory>().Single().CreateLogger("mstest-trace"));
            List<string> warnings = [];
            logger.Setup(value => value.Log(
                LogLevel.Warning, It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
                .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                    (_, state, exception, formatter) => warnings.Add(formatter(state, exception)));
            return warnings;
        }

        internal async Task OpenAsync()
            => (await Framework.CreateTestSessionAsync(new(Session, CancellationToken.None))).IsSuccess.Should().BeTrue();

        internal async Task CloseAsync()
            => (await Framework.CloseTestSessionAsync(new(Session, CancellationToken.None))).IsSuccess.Should().BeTrue();

        internal Task RunAsync()
            => Framework.ExecuteRequestAsync(new(
                new RunTestExecutionRequest(
                    new TestSessionContext(Session),
                    new TestNodeUidListFilter([new TestNodeUid(typeof(AttachmentFixture).FullName + "." + nameof(AttachmentFixture.AttachesFiles))])),
                Bus.Object, Mock.Of<IExecuteRequestCompletionNotifier>(), CancellationToken.None));

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

    [TestClass]
    public sealed class AttachmentFixture
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void AttachesFiles()
        {
            TestContext.AddResultFile((string)TestContext.Properties["attachment"]!);
            TestContext.AddResultFile((string)TestContext.Properties["missing"]!);
        }
    }
}
