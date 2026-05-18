// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsArtifactUploaderTests
{
    private const string ArtifactUploadOptionsRequireUploadArtifactsMessage = "'--report-azdo-upload-artifact-include', '--report-azdo-upload-artifact-exclude', and '--report-azdo-upload-artifact-name' require '--report-azdo-upload-artifacts' to be set to a value other than 'off'.";
    private static readonly string ResultsDirectory = Path.Combine(Path.GetTempPath(), "azdo-artifact-uploader-tests");

    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IOutputDevice> _outputDeviceMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly List<IOutputDeviceData> _outputData = [];

    public AzureDevOpsArtifactUploaderTests()
    {
        _ = _configurationMock.SetupGet(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(ResultsDirectory);
        _ = _testApplicationModuleInfoMock.Setup(testApplicationModuleInfo => testApplicationModuleInfo.TryGetAssemblyName()).Returns("MyAssembly");
        _ = _loggerFactoryMock.Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _ = _outputDeviceMock
            .Setup(outputDevice => outputDevice.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _outputData.Add(data))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task OffMode_IsDisabledAndNoOps()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader([]);

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("test.trx")]);

        Assert.IsFalse(await uploader.IsEnabledAsync().ConfigureAwait(false));

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("CrashDumpProcessLifetimeHandler", "Crash dump"), CreateFileArtifact(InResults("dump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreateFailedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
        Assert.IsEmpty(GetWarnings());
    }

    [TestMethod]
    public async Task TagsOnlyMode_EmitsOnlyBuildTags()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("CrashDumpProcessLifetimeHandler", "Crash dump"), CreateFileArtifact(InResults("crashdump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("HangDumpProcessLifetimeHandler", "Hang dump"), CreateFileArtifact(InResults("hang", "hangdump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreateFailedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "##vso[build.addbuildtag]has-crashdump",
                "##vso[build.addbuildtag]has-hangdump",
                "##vso[build.addbuildtag]has-test-failures",
            },
            GetFormattedLines());
        Assert.DoesNotContain(line => line.Contains("artifact.upload", StringComparison.Ordinal), GetFormattedLines());
        Assert.IsEmpty(GetWarnings());
    }

    [TestMethod]
    public async Task FilesMode_EmitsOnlyArtifactLines()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["Artifacts"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("a.trx"), InResults("b.dmp")]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreateFailedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string[] lines = GetFormattedLines();
        Assert.HasCount(2, lines);
        Assert.IsTrue(lines.All(line => line.StartsWith("##vso[artifact.upload containerfolder=Artifacts;artifactname=Artifacts]", StringComparison.Ordinal)));
        Assert.DoesNotContain(line => line.Contains("build.addbuildtag", StringComparison.Ordinal), lines);
    }

    [TestMethod]
    public async Task FilesMode_SkipsArtifactsOutsideResultsDirectory()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["Artifacts"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("inside.trx"), OutsideResults("outside.trx")]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { $"##vso[artifact.upload containerfolder=Artifacts;artifactname=Artifacts]{InResults("inside.trx")}" },
            GetFormattedLines());
    }

    [TestMethod]
    public async Task AllMode_EmitsBuildTagsAndArtifactLines()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["Artifacts"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("test.trx")]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("CrashDumpProcessLifetimeHandler", "Crash dump"), CreateFileArtifact(InResults("dump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreateFailedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[]
            {
                "##vso[build.addbuildtag]has-crashdump",
                "##vso[build.addbuildtag]has-test-failures",
                $"##vso[artifact.upload containerfolder=Artifacts;artifactname=Artifacts]{InResults("test.trx")}",
            },
            GetFormattedLines());
    }

    [TestMethod]
    public async Task IncludeAndExcludeGlobs_AreAppliedToArtifactUploads()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude] = ["skip/**"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude] = ["**/*.trx"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["Artifacts"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("keep.trx"), InResults("keep.coverage"), InResults("skip", "ignored.trx")]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { $"##vso[artifact.upload containerfolder=Artifacts;artifactname=Artifacts]{InResults("keep.trx")}" },
            GetFormattedLines());
    }

    [TestMethod]
    public async Task ConsumedArtifacts_DetectCrashAndHangDumps()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("CrashDumpProcessLifetimeHandler", "Crash dump"), CreateFileArtifact(InResults("dump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("HangDumpProcessLifetimeHandler", "Hang dump"), CreateFileArtifact(InResults("hang", "dump.log")), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "##vso[build.addbuildtag]has-crashdump",
                "##vso[build.addbuildtag]has-hangdump",
            },
            GetFormattedLines());
    }

    [TestMethod]
    public async Task NonDumpProducer_DoesNotEmitDumpTags()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("GenericProducer", "Generic"), CreateFileArtifact(InResults("hang", "dump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
    }

    [TestMethod]
    public async Task HasTestFailuresTag_IsNotEmittedWithoutFailures()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeTagsOnly],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreatePassedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.DoesNotContain("##vso[build.addbuildtag]has-test-failures", GetFormattedLines());
    }

    [TestMethod]
    public async Task MissingTfBuild_EmitsWarningAndSkipsOutput()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeAll],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories))
            .Returns([InResults("test.trx")]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer("CrashDumpProcessLifetimeHandler", "Crash dump"), CreateFileArtifact(InResults("dump.dmp")), CancellationToken.None).ConfigureAwait(false);
        await uploader.ConsumeAsync(CreateProducer(), CreateFailedTestNodeUpdateMessage(), CancellationToken.None).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { "Azure DevOps artifact upload was requested, but TF_BUILD is not set to 'true'; skipping Azure DevOps artifact upload and build tags." },
            GetWarnings());
        Assert.IsEmpty(GetFormattedLines());
    }

    [TestMethod]
    public async Task EmptyTestResultsDirectory_DoesNotEmitArtifactLines()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories)).Returns([]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
        Assert.IsEmpty(GetWarnings());
    }

    [TestMethod]
    public async Task WhitespaceTestResultsDirectory_DoesNotEmitArtifactLines()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        _ = _configurationMock.SetupGet(configuration => configuration[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("   ");
        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
        Assert.IsEmpty(GetWarnings());
    }

    [TestMethod]
    public async Task ArtifactUploadPaths_AreEscaped()
    {
        AzureDevOpsArtifactUploader uploader = CreateUploader(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName] = ["Artifacts"],
            [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeFiles],
        });

        string specialPath = $"{ResultsDirectory}{Path.DirectorySeparatorChar}semi;line\r\nname.trx";
        string escapedSpecialPath = specialPath.Replace(";", "%3B", StringComparison.Ordinal).Replace("\r", "%0D", StringComparison.Ordinal).Replace("\n", "%0A", StringComparison.Ordinal);
        _ = _environmentMock.Setup(environment => environment.GetEnvironmentVariable("TF_BUILD")).Returns("true");
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.ExistDirectory(ResultsDirectory)).Returns(true);
        _ = _fileSystemMock.Setup(fileSystem => fileSystem.GetFiles(ResultsDirectory, "*", SearchOption.AllDirectories)).Returns([specialPath]);

        await uploader.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await uploader.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        CollectionAssert.AreEqual(
            new[] { $"##vso[artifact.upload containerfolder=Artifacts;artifactname=Artifacts]{escapedSpecialPath}" },
            GetFormattedLines());
    }

    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, @"C:\absolute\*.trx")]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, "/absolute/*.trx")]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, @"\absolute\*.trx")]
    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_RejectsAbsoluteGlobPatterns(string optionName, string pattern)
    {
        var provider = new AzureDevOpsCommandLineProvider();
        Microsoft.Testing.Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().Single(commandLineOption => commandLineOption.Name == optionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, [pattern]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual($"Invalid glob pattern {pattern}.", result.ErrorMessage);
    }

    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, false)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, false)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName, false)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactInclude, true)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactExclude, true)]
    [DataRow(AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName, true)]
    [TestMethod]
    public async Task ValidateCommandLineOptionsAsync_RejectsArtifactSettingsWhenUploadsAreDisabled(string optionName, bool setOffMode)
    {
        var provider = new AzureDevOpsCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            [optionName] = optionName == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactName ? ["Artifacts"] : ["**/*.trx"],
        };

        if (setOffMode)
        {
            options[AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts] = [AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifactsModeOff];
        }

        ValidationResult result = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(ArtifactUploadOptionsRequireUploadArtifactsMessage, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ValidateOptionArgumentsAsync_RejectsUnknownUploadMode()
    {
        var provider = new AzureDevOpsCommandLineProvider();
        Microsoft.Testing.Platform.Extensions.CommandLine.CommandLineOption option = provider.GetCommandLineOptions().Single(commandLineOption => commandLineOption.Name == AzureDevOpsCommandLineOptions.AzureDevOpsUploadArtifacts);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, ["unexpected"]).ConfigureAwait(false);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("Invalid artifact upload mode unexpected.", result.ErrorMessage);
    }

    private AzureDevOpsArtifactUploader CreateUploader(Dictionary<string, string[]> options)
        => new(
            new TestCommandLineOptions(options),
            _configurationMock.Object,
            _environmentMock.Object,
            _fileSystemMock.Object,
            _outputDeviceMock.Object,
            _testApplicationModuleInfoMock.Object,
            _loggerFactoryMock.Object);

    private static FileArtifact CreateFileArtifact(string path)
        => new(new FileInfo(path), "Artifact");

    private static TestNodeUpdateMessage CreateFailedTestNodeUpdateMessage()
        => CreateTestNodeUpdateMessage(new FailedTestNodeStateProperty());

    private static TestNodeUpdateMessage CreatePassedTestNodeUpdateMessage()
        => CreateTestNodeUpdateMessage(new PassedTestNodeStateProperty());

    private static TestNodeUpdateMessage CreateTestNodeUpdateMessage(TestNodeStateProperty state)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = "test",
                DisplayName = "TestDisplayName",
                Properties = new PropertyBag(state),
            });

    private static IDataProducer CreateProducer(string uid = "Producer", string displayName = "Producer")
        => new TestProducer(uid, displayName);

    private static string InResults(params string[] segments)
        => segments.Aggregate(ResultsDirectory, Path.Combine);

    private static string OutsideResults(params string[] segments)
        => segments.Aggregate(Path.Combine(Path.GetTempPath(), "azdo-artifact-uploader-tests-outside"), Path.Combine);

    private string[] GetFormattedLines()
        => [.. _outputData.OfType<FormattedTextOutputDeviceData>().Select(output => output.Text)];

    private string[] GetWarnings()
        => [.. _outputData.OfType<WarningMessageOutputDeviceData>().Select(output => output.Message)];

    private sealed class TestProducer(string uid, string displayName) : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(FileArtifact)];

        public string Uid { get; } = uid;

        public string Version { get; } = "1.0.0";

        public string DisplayName { get; } = displayName;

        public string Description { get; } = displayName;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class TestSessionContext : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }
}
