// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

/// <summary>
/// Package-consuming transport regressions for dotnet/sdk#56796. The SDK peer and
/// coverage producer are fixtures, not the real SDK or Microsoft.CodeCoverage.
/// </summary>
[TestClass]
public sealed class DotnetTestPipeCoverageSummaryTests
{
    private const string AssetName = "DotnetTestPipeCoverageSummary";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("1.0.0", false, DisplayName = "PackedCoverageOnBase10")]
    [DataRow("1.3.0", true, DisplayName = "PackedCoveragePreservesFailingTestExit")]
    public async Task SessionFinalization_NegotiatedCoverageReachesPackedConsumer(string baseVersion, bool failTest)
    {
        using TempDirectory results = new();
        using TestAsset asset = await CreateAssetAsync();
        string executionId = Guid.NewGuid().ToString("N");
        var host = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: $"--results-directory \"{results.Path}\"",
            environmentVariables: new()
            {
                [EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID] = executionId,
                ["COVERAGE_PIPE_CONTROLLER"] = "0",
                ["COVERAGE_PIPE_FAIL_TEST"] = failTest ? "1" : "0",
            },
            supportedProtocolVersions: baseVersion,
            coverageSummaryVersion: "1",
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(failTest ? ExitCode.AtLeastOneTestFailed : ExitCode.Success);
        AssertPackedPlatformLoaded(result.TestHostResult, asset);
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.IsNotNull(result.SentHandshakeReply);
        Assert.AreEqual("1", result.ReceivedHandshake[20]);
        Assert.AreEqual("1", result.SentHandshakeReply[20]);
        Assert.AreEqual("1.0.0;1.1.0;1.2.0;1.3.0;1.4.0", result.ReceivedHandshake[4]);
        Assert.AreEqual(baseVersion, result.NegotiatedProtocolVersion);
        Assert.AreEqual(executionId, result.ReceivedHandshake[7]);
        Assert.IsFalse(result.SentHandshakeReply.ContainsKey(12), "Coverage must not opt into the server-control pipe.");
        Assert.IsFalse(result.ServerControlPipeConnected);
        Assert.IsEmpty(result.MessagesWithSerializerId(13));
        Assert.IsEmpty(result.MessagesWithSerializerId(14));
        Assert.ContainsSingle(result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestResultMessages));

        (byte? SessionType, string? SessionUid, string? ExecutionId) sessionEnd = Assert.ContainsSingle(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestSessionEvent)
                .Select(frame => DotnetTestPipeProtocol.DecodeTestSessionEventBody(frame.Body))
                .Where(session => session.SessionType == DotnetTestPipeProtocol.SessionEventTypes.TestSessionEnd));
        Assert.IsNotNull(sessionEnd.SessionUid);
        CoverageSummaryBatch[] batches =
        [
            .. result.MessagesWithSerializerId(15).Select(frame => DotnetTestPipeProtocol.DecodeCoverageSummaryMessagesBody(frame.Body)),
        ];
        Assert.HasCount(2, batches, "The File-scoped final measurement must not be forwarded.");
        Assert.IsTrue(batches.All(batch => batch.ExecutionId == executionId && batch.InstanceId == result.ReceivedHandshake[8]));
        Assert.AreEqual(
            new CoverageSummary(sessionEnd.SessionUid, "coverage.session.publisher", 0, null, 0, null, 3_000_000_000L, long.MaxValue),
            Assert.ContainsSingle(batches[0].Summaries));
        Assert.AreEqual(
            new CoverageSummary(sessionEnd.SessionUid, "coverage.secondary", 1, "module with space.dll", 255, "empty-custom", 0, 0),
            Assert.ContainsSingle(batches[1].Summaries));
    }

    [TestMethod]
    [DataRow("1.0.0", null, (int)ExitCode.Success, DisplayName = "PackedOldPeerOmitsCapability")]
    [DataRow("1.3.0", "false", (int)ExitCode.Success, DisplayName = "PackedPeerOptsOut")]
    [DataRow("1.3.0", "2", (int)ExitCode.Success, DisplayName = "PackedUnknownCoverageVersion")]
    [DataRow("2.0.0", "1", (int)ExitCode.IncompatibleProtocolVersion, DisplayName = "PackedIncompatibleBase")]
    public async Task SessionFinalization_UnsupportedPeerReceivesNoCoverage(string baseVersion, string? coverageVersion, int expectedExit)
    {
        using TempDirectory results = new();
        using TestAsset asset = await CreateAssetAsync();
        var host = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: $"--results-directory \"{results.Path}\"",
            environmentVariables: new() { ["COVERAGE_PIPE_CONTROLLER"] = "0", ["COVERAGE_PIPE_FAIL_TEST"] = "0" },
            supportedProtocolVersions: baseVersion,
            coverageSummaryVersion: coverageVersion,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs((ExitCode)expectedExit);
        AssertPackedPlatformLoaded(result.TestHostResult, asset);
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.IsNotNull(result.SentHandshakeReply);
        Assert.AreEqual("1", result.ReceivedHandshake[20]);
        Assert.IsEmpty(result.MessagesWithSerializerId(15));
        if (coverageVersion is null)
        {
            Assert.IsFalse(result.SentHandshakeReply.ContainsKey(20));
        }
        else
        {
            Assert.AreEqual(coverageVersion, result.SentHandshakeReply[20]);
        }

        if (expectedExit == (int)ExitCode.Success)
        {
            Assert.ContainsSingle(result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestResultMessages));
        }
        else
        {
            Assert.IsEmpty(result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.TestResultMessages));
        }
    }

    [TestMethod]
    [DataRow("1", true, DisplayName = "PackedControllerCoverageDrains")]
    [DataRow(null, false, DisplayName = "PackedControllerOldPeer")]
    public async Task ControllerFinalization_CoverageIsDrainedToPackedConsumer(string? coverageVersion, bool enabled)
    {
        using TempDirectory results = new();
        using TestAsset asset = await CreateAssetAsync();
        string executionId = Guid.NewGuid().ToString("N");
        var host = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkMultiConnectionResult result = await FakeDotnetTestSdkMultiConnection.RunAsync(
            host,
            extraArguments: $"--results-directory \"{results.Path}\"",
            environmentVariables: new()
            {
                [EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID] = executionId,
                ["COVERAGE_PIPE_CONTROLLER"] = "1",
                ["COVERAGE_PIPE_FAIL_TEST"] = "0",
            },
            supportedProtocolVersions: "1.3.0",
            coverageSummaryVersion: coverageVersion,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
        AssertPackedPlatformLoaded(result.TestHostResult, asset);
        Assert.HasCount(2, result.ReceivedHandshakes);
        Dictionary<byte, string> controller = Assert.ContainsSingle(result.HandshakesWithHostType("TestHostController"));
        Dictionary<byte, string> child = Assert.ContainsSingle(
            result.ReceivedHandshakes.Where(handshake => handshake[5] is "TestHost" or "ServerTestHost"));
        Assert.AreNotEqual(controller[8], child[8]);
        Assert.AreEqual("1", controller[20]);
        Assert.AreEqual("1", child[20]);
        CoverageSummaryBatch[] batches =
        [
            .. result.ReceivedMessages.Where(frame => frame.SerializerId == 15)
                .Select(frame => DotnetTestPipeProtocol.DecodeCoverageSummaryMessagesBody(frame.Body)),
        ];
        if (enabled)
        {
            Assert.HasCount(3, batches);
            Assert.IsTrue(batches.All(batch => batch.ExecutionId == executionId));
            CoverageSummaryBatch controllerBatch = Assert.ContainsSingle(batches.Where(batch => batch.InstanceId == controller[8]));
            Assert.AreEqual(
                new CoverageSummary("controller-session", "coverage.controller.publisher", 0, null, 0, null, 11, 17),
                Assert.ContainsSingle(controllerBatch.Summaries));
            Assert.HasCount(2, batches.Where(batch => batch.InstanceId == child[8]));
        }
        else
        {
            Assert.IsEmpty(batches);
        }
    }

    [TestMethod]
    public Task Dispatcher_CompleteFixtureMergeCouplesCoverageToExactProvenance()
        => VerifyDispatcherAsync("1", truncation: null, expectCoverage: true);

    [TestMethod]
    [DataRow(null, null, DisplayName = "PackedArtifactOldPeer")]
    [DataRow("false", null, DisplayName = "PackedArtifactPeerOptOut")]
    [DataRow("2", null, DisplayName = "PackedArtifactUnknownCoverageVersion")]
    [DataRow("1", "timeout", DisplayName = "PackedArtifactTimeoutIsNotAuthoritative")]
    [DataRow("1", "maximumFailedTests", DisplayName = "PackedArtifactPartialIsNotAuthoritative")]
    public Task Dispatcher_UnsupportedOrTruncatedJobOmitsCoverageMetadata(string? coverageVersion, string? truncation)
        => VerifyDispatcherAsync(coverageVersion, truncation, expectCoverage: false);

    private async Task VerifyDispatcherAsync(string? coverageVersion, string? truncation, bool expectCoverage)
    {
        using TempDirectory directory = new();
        using TestAsset asset = await CreateAssetAsync();
        string first = Path.Combine(directory.Path, "first.fixture");
        string second = Path.Combine(directory.Path, "second.fixture");
        string unrelated = Path.Combine(directory.Path, "unrelated.fixture");
        string firstManifestPath = first.Replace('\\', '/');
        string outputDirectory = Path.Combine(directory.Path, "processed");
        string manifest = Path.Combine(directory.Path, "manifest.json");
        // This is an explicitly synthetic fixture format, not a coverage collector report.
        File.WriteAllLines(first, ["A=covered", "B=missed"]);
        File.WriteAllLines(second, ["A=covered", "C=covered", "D=missed"]);
        File.WriteAllText(unrelated, "unrelated original");
        Dictionary<string, object> manifestContents = new()
        {
            ["schemaVersion"] = 1,
            ["outputDirectory"] = outputDirectory,
            ["inputs"] = new[]
            {
                new { path = firstManifestPath, kind = "test.coverage.fixture" },
                new { path = second, kind = "test.coverage.fixture" },
                new { path = unrelated, kind = "test.unrelated" },
            },
        };
        if (truncation is not null)
        {
            manifestContents["truncationReason"] = truncation;
        }

        File.WriteAllText(manifest, JsonSerializer.Serialize(manifestContents));
        var host = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: $"--manifest \"{manifest}\"",
            environmentVariables: new() { ["COVERAGE_PIPE_CONTROLLER"] = "0", ["COVERAGE_PIPE_FAIL_TEST"] = "0" },
            supportedProtocolVersions: "1.3.0",
            toolName: "internal-merge-artifacts",
            coverageSummaryVersion: coverageVersion,
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
        AssertPackedPlatformLoaded(result.TestHostResult, asset);
        Assert.IsNotNull(result.ReceivedHandshake);
        Assert.AreEqual("1", result.ReceivedHandshake[20]);
        Assert.AreEqual("ArtifactPostProcessor", result.ReceivedHandshake[5]);
        Assert.IsEmpty(result.MessagesWithSerializerId(15), "Tool mode couples coverage to the artifact, not the session bus.");
        RawMessage frame = Assert.ContainsSingle(result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages));
        FileArtifact artifact = Assert.ContainsSingle(DotnetTestPipeProtocol.DecodeFileArtifacts(frame.Body));
        Assert.AreEqual(Path.Combine(outputDirectory, "merged.fixture"), artifact.FullPath);
        Assert.AreEqual("test.coverage.fixture.processed", artifact.Kind);
        Assert.AreSequenceEqual([firstManifestPath, second], artifact.InputArtifactPaths);
        Assert.AreSequenceEqual(["A=covered", "B=missed", "C=covered", "D=missed"], File.ReadAllLines(artifact.FullPath!));
        if (expectCoverage)
        {
            Assert.IsNotNull(artifact.CoverageSummary);
            Assert.AreEqual(
                new CoverageSummary(null, "coverage.fixture.processor", 0, null, 0, null, 2, 4),
                Assert.ContainsSingle(artifact.CoverageSummary));
        }
        else
        {
            Assert.IsNull(artifact.CoverageSummary, "Unavailable metadata must not become an explicit 0/0 measurement.");
        }

        FileArtifact legacy = Assert.ContainsSingle(DotnetTestPipeProtocol.DecodeFileArtifacts(frame.Body, readCoverageSummary: false));
        Assert.AreEqual(artifact.FullPath, legacy.FullPath);
        Assert.AreEqual("test.coverage.fixture.processed", legacy.Kind);
        Assert.AreSequenceEqual([firstManifestPath, second], legacy.InputArtifactPaths);
        Assert.IsNull(legacy.CoverageSummary);
        Assert.AreSequenceEqual(["A=covered", "B=missed"], File.ReadAllLines(first));
        Assert.AreSequenceEqual(["A=covered", "C=covered", "D=missed"], File.ReadAllLines(second));
        Assert.AreEqual("unrelated original", File.ReadAllText(unrelated));
    }

    private async Task<TestAsset> CreateAssetAsync()
    {
        // Do not touch AcceptanceTestBase: its static initialization requires many
        // unrelated packages even when a test only needs the core MTP version.
        Match package = Assert.ContainsSingle(
            Directory.EnumerateFiles(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Platform.*.nupkg")
                .Select(path => Regex.Match(
                    Path.GetFileName(path),
                    @"^Microsoft\.Testing\.Platform\.(?<version>\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.+-]+)?)\.nupkg$",
                    RegexOptions.CultureInvariant))
                .Where(match => match.Success));
        TestAsset asset = await TestAsset.GenerateAssetAsync(
            "coverage-pipe-" + Guid.NewGuid().ToString("N"),
            TestSources.AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", package.Groups["version"].Value));
        try
        {
            await DotnetCli.RunAsync(
                $"build \"{asset.TargetAssetPath}\" -c Release",
                cancellationToken: TestContext.CancellationToken);
            string version = package.Groups["version"].Value;
            string packagePath = Path.Combine(Constants.ArtifactsPackagesShipping, $"Microsoft.Testing.Platform.{version}.nupkg");
            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(asset.TargetAssetPath, "obj", "project.assets.json")));
            JsonElement library = assets.RootElement.GetProperty("libraries").GetProperty($"Microsoft.Testing.Platform/{version}");
            Assert.AreEqual("package", library.GetProperty("type").GetString());
            string? libraryPath = library.GetProperty("path").GetString();
            Assert.IsNotNull(libraryPath);
            string resolvedPackage = Assert.ContainsSingle(
                assets.RootElement.GetProperty("packageFolders").EnumerateObject()
                    .Select(folder => Path.Combine(folder.Name, libraryPath))
                    .Where(Directory.Exists));
            using FileStream originalPackage = File.OpenRead(packagePath);
            using FileStream cachedPackage = File.OpenRead(Path.Combine(resolvedPackage, $"microsoft.testing.platform.{version}.nupkg"));
            Assert.AreSequenceEqual(SHA256.HashData(originalPackage), SHA256.HashData(cachedPackage));
            using ZipArchive archive = ZipFile.OpenRead(packagePath);
            using Stream packedDll = archive.GetEntry($"lib/{TargetFrameworks.NetCurrent}/Microsoft.Testing.Platform.dll")!.Open();
            using FileStream outputDll = File.OpenRead(GetPlatformPath(asset));
            byte[] binaryHash = SHA256.HashData(packedDll);
            Assert.AreSequenceEqual(binaryHash, SHA256.HashData(outputDll));
            TestContext.WriteLine($"Packed MTP {version}; resolved {resolvedPackage}; DLL SHA256 {Convert.ToHexString(binaryHash)}.");
            return asset;
        }
        catch
        {
            asset.Dispose();
            throw;
        }
    }

    private static string GetPlatformPath(TestAsset asset)
        => Path.Combine(asset.TargetAssetPath, "bin", "Release", TargetFrameworks.NetCurrent, "Microsoft.Testing.Platform.dll");

    private static void AssertPackedPlatformLoaded(TestHostResult result, TestAsset asset)
        => Assert.Contains($"fixture-core: {GetPlatformPath(asset)}", result.StandardOutput, StringComparison.Ordinal);

    private static class TestSources
    {
        // Hand-authored MTP-only framework. No MSTest or source-generation package is needed.
        public const string AssetCode = """
            #file DotnetTestPipeCoverageSummary.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <LangVersion>12.0</LangVersion>
                <NoWarn>$(NoWarn);TPEXP</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Capabilities.TestFramework;
            using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
            using Microsoft.Testing.Platform.Extensions.Messages;
            using Microsoft.Testing.Platform.Extensions.TestFramework;
            using Microsoft.Testing.Platform.Extensions.TestHost;
            using Microsoft.Testing.Platform.Extensions.TestHostControllers;
            using Microsoft.Testing.Platform.Messages;
            using Microsoft.Testing.Platform.Services;
            using Microsoft.Testing.Platform.TestHost;

            public static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    Console.WriteLine("fixture-core: " + typeof(ProcessedArtifact).Assembly.Location);
                    var legacyArtifact = new ProcessedArtifact("compatibility", "test.coverage.fixture", "Compatibility", null);
                    if (legacyArtifact.CoverageSummary is not null)
                    {
                        throw new InvalidOperationException("The legacy artifact constructor must leave coverage unavailable.");
                    }

                    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
                    builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new FixtureFramework());
                    builder.TestHost.AddTestSessionLifetimeHandler(services => new SessionCoveragePublisher(services.GetMessageBus()));
                    if (Environment.GetEnvironmentVariable("COVERAGE_PIPE_CONTROLLER") == "1")
                    {
                        builder.TestHostControllers.AddProcessLifetimeHandler(services => new ControllerCoveragePublisher(services.GetMessageBus()));
                    }

                    ((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing
                        .AddArtifactPostProcessor(_ => new FixtureCoverageProcessor());
                    using ITestApplication app = await builder.BuildAsync();
                    return await app.RunAsync();
                }
            }

            public sealed class FixtureFramework : ITestFramework, IDataProducer
            {
                public string Uid => "coverage.fixture.framework";
                public string Version => "1.0.0";
                public string DisplayName => "Coverage fixture framework";
                public string Description => "Publishes one fixture test result.";
                public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
                    => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
                public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
                    => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
                public async Task ExecuteRequestAsync(ExecuteRequestContext context)
                {
                    TestNodeStateProperty state = Environment.GetEnvironmentVariable("COVERAGE_PIPE_FAIL_TEST") == "1"
                        ? new FailedTestNodeStateProperty("fixture failure")
                        : PassedTestNodeStateProperty.CachedInstance;
                    await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                        context.Request.Session.SessionUid,
                        new TestNode { Uid = "fixture-test", DisplayName = "Fixture test", Properties = new PropertyBag(state) }));
                    context.Complete();
                }
            }

            public sealed class SessionCoveragePublisher(IMessageBus bus) : ITestSessionLifetimeHandler, IDataProducer
            {
                public string Uid => "coverage.session.publisher";
                public string Version => "1.0.0";
                public string DisplayName => "Session coverage fixture";
                public string Description => "Publishes coverage during regular session finalization.";
                public Type[] DataTypesProduced => [typeof(TestCoverageMessage)];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task OnTestSessionStartingAsync(ITestSessionContext context) => Task.CompletedTask;
                public async Task OnTestSessionFinishingAsync(ITestSessionContext context)
                {
                    await bus.PublishAsync(this, new TestCoverageMessage(
                        context.SessionUid, CoverageScope.Overall, CoverageMetric.Line, 3_000_000_000L, long.MaxValue, Uid));
                    await bus.PublishAsync(this, new TestCoverageMessage(
                        context.SessionUid, new CoverageScope(CoverageScopeLevel.Module, "module with space.dll"),
                        CoverageMetric.Custom, 0, 0, "coverage.secondary", "empty-custom"));
                    await bus.PublishAsync(this, new TestCoverageMessage(
                        context.SessionUid, new CoverageScope(CoverageScopeLevel.File, "ignored.fixture"),
                        CoverageMetric.Line, 1, 2, Uid));
                }
            }

            public sealed class ControllerCoveragePublisher(IMessageBus bus) : ITestHostProcessLifetimeHandler, IDataProducer
            {
                public string Uid => "coverage.controller.publisher";
                public string Version => "1.0.0";
                public string DisplayName => "Controller coverage fixture";
                public string Description => "Publishes coverage after the child exits.";
                public Type[] DataTypesProduced => [typeof(TestCoverageMessage)];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
                public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation information, CancellationToken cancellationToken) => Task.CompletedTask;
                public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation information, CancellationToken cancellationToken)
                    => bus.PublishAsync(this, new TestCoverageMessage(
                        new SessionUid("controller-session"), CoverageScope.Overall, CoverageMetric.Line, 11, 17, Uid));
            }

            public sealed class FixtureCoverageProcessor : IArtifactPostProcessor
            {
                public string Uid => "coverage.fixture.processor";
                public string Version => "1.0.0";
                public string DisplayName => "Synthetic coverage fixture processor";
                public string Description => "Merges the test-only text format, not CodeCoverage reports.";
                public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => [ArtifactPostProcessingMode.TestModules];
                public bool SupportsTruncatedRuns => true;
                public IReadOnlyList<string> SupportedKinds => ["test.coverage.fixture"];
                public IReadOnlyList<string> SupportedFileExtensionsFallback => [];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public async Task<ProcessedArtifact?> ProcessAsync(
                    IReadOnlyList<InputArtifact> inputs, string outputDirectory,
                    ArtifactPostProcessingContext context, CancellationToken cancellationToken)
                {
                    var records = new SortedDictionary<string, bool>(StringComparer.Ordinal);
                    foreach (InputArtifact input in inputs)
                    {
                        foreach (string row in await File.ReadAllLinesAsync(input.Path, cancellationToken))
                        {
                            string[] parts = row.Split('=');
                            records.TryGetValue(parts[0], out bool alreadyCovered);
                            records[parts[0]] = alreadyCovered || parts[1] == "covered";
                        }
                    }

                    string path = Path.Combine(outputDirectory, "merged.fixture");
                    await File.WriteAllLinesAsync(
                        path, records.Select(record => $"{record.Key}={(record.Value ? "covered" : "missed")}"), cancellationToken);
                    // Counts describe the actual complete output, not sums of input summaries.
                    string[] actualOutput = await File.ReadAllLinesAsync(path, cancellationToken);
                    long covered = actualOutput.LongCount(row => row.EndsWith("=covered", StringComparison.Ordinal));
                    return new ProcessedArtifact(
                        path, "test.coverage.fixture.processed", "Merged fixture", null,
                        [new CoverageMetricResult(CoverageMetric.Line, covered, actualOutput.LongLength, Uid)]);
                }
            }
            """;
    }
}
