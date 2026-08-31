// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

[TestClass]
public sealed class DotnetTestPipeArtifactPostProcessingTests
    : AcceptanceTestBase<DotnetTestPipeArtifactPostProcessingTests.TestAssetFixture>
{
    private const string AssetName = "DotnetTestPipeArtifactPostProcessingTest";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Dispatcher_AdvertisesCapabilityAndReturnsMergedArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.trx");
            string secondPath = Path.Combine(directory, "second.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    inputs = new[]
                    {
                        new { path = firstPath, kind = "microsoft.testing.trx", executionId = "execution-1" },
                        new { path = secondPath, kind = "microsoft.testing.trx", executionId = "execution-2" },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            Assert.IsNotNull(result.ReceivedHandshake);
            Assert.AreEqual(
                "ArtifactPostProcessor",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.HostType]);
            Assert.AreEqual(
                "microsoft.testing.ctrf;microsoft.testing.html;microsoft.testing.trx;test.summary;test.xml",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.SupportedPostProcessorKinds]);
            Assert.AreEqual(
                "test.summary",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.SupportedTruncatedRunPostProcessorKinds]);
            Assert.AreEqual(
                "test.summary",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.RequiredPostProcessorKinds]);

            RawMessage[] artifactFrames = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages)];
            Assert.HasCount(1, artifactFrames);
            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrames[0].Body);
            Assert.HasCount(1, artifacts);
            Assert.AreEqual("microsoft.testing.trx", artifacts[0].Kind);
            Assert.IsNotNull(artifacts[0].FullPath);
            Assert.IsTrue(File.Exists(artifacts[0].FullPath));
            Assert.IsNotNull(artifacts[0].InputArtifactPaths);
            Assert.AreSequenceEqual(
                [Path.GetFullPath(firstPath), Path.GetFullPath(secondPath)],
                artifacts[0].InputArtifactPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_AdvertisesCtrfCapabilityAndReturnsMergedArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-ctrf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.ctrf.json");
            string secondPath = Path.Combine(directory, "second.ctrf.json");
            WriteMinimalCtrfReport(firstPath, "first");
            WriteMinimalCtrfReport(secondPath, "second");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    inputs = new[]
                    {
                        new { path = firstPath, kind = "microsoft.testing.ctrf", executionId = "execution-1" },
                        new { path = secondPath, kind = "microsoft.testing.ctrf", executionId = "execution-2" },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            Assert.IsNotNull(result.ReceivedHandshake);
            Assert.AreEqual(
                "microsoft.testing.ctrf;microsoft.testing.html;microsoft.testing.trx;test.summary;test.xml",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.SupportedPostProcessorKinds]);

            RawMessage[] artifactFrames = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages)];
            Assert.HasCount(1, artifactFrames);
            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrames[0].Body);
            Assert.HasCount(1, artifacts);
            Assert.AreEqual("microsoft.testing.ctrf", artifacts[0].Kind);
            Assert.IsNotNull(artifacts[0].FullPath);
            Assert.IsTrue(File.Exists(artifacts[0].FullPath!));
            Assert.IsNotNull(artifacts[0].InputArtifactPaths);
            Assert.AreSequenceEqual(
                [Path.GetFullPath(firstPath), Path.GetFullPath(secondPath)],
                artifacts[0].InputArtifactPaths);

            using var merged = JsonDocument.Parse(File.ReadAllText(artifacts[0].FullPath!));
            Assert.AreEqual(2, merged.RootElement.GetProperty("results").GetProperty("tests").GetArrayLength());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_AdvertisesHtmlCapabilityAndReturnsMergedArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-html-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.html");
            string secondPath = Path.Combine(directory, "second.html");
            WriteMinimalHtmlReport(firstPath, "first");
            WriteMinimalHtmlReport(secondPath, "second");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    inputs = new[]
                    {
                        new { path = firstPath, kind = "microsoft.testing.html", executionId = "execution-1" },
                        new { path = secondPath, kind = "microsoft.testing.html", executionId = "execution-2" },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            Assert.IsNotNull(result.ReceivedHandshake);
            Assert.AreEqual(
                "microsoft.testing.ctrf;microsoft.testing.html;microsoft.testing.trx;test.summary;test.xml",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.SupportedPostProcessorKinds]);

            RawMessage artifactFrame = Assert.ContainsSingle(
                result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages));
            FileArtifact artifact = Assert.ContainsSingle(DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrame.Body));
            Assert.AreEqual("microsoft.testing.html", artifact.Kind);
            Assert.IsNotNull(artifact.FullPath);
            Assert.IsTrue(File.Exists(artifact.FullPath));
            Assert.IsNotNull(artifact.InputArtifactPaths);
            Assert.AreSequenceEqual(
                [Path.GetFullPath(firstPath), Path.GetFullPath(secondPath)],
                artifact.InputArtifactPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_ReportsOnlyInputsRepresentedByEachOutput()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.xml");
            string secondPath = Path.Combine(directory, "second.xml");
            string unrelatedPath = Path.Combine(directory, "unrelated.xml");
            string firstManifestPath = firstPath.Replace('\\', '/');
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");
            File.WriteAllText(unrelatedPath, "unrelated");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    inputs = new[]
                    {
                        new { path = firstManifestPath, kind = (string?)"test.xml" },
                        new { path = secondPath, kind = (string?)"test.xml" },
                        new { path = unrelatedPath, kind = (string?)null },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            RawMessage[] artifactFrames = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages)];
            Assert.HasCount(1, artifactFrames);
            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrames[0].Body);
            Assert.HasCount(1, artifacts);
            Assert.AreEqual("test.xml.processed", artifacts[0].Kind);
            Assert.IsNotNull(artifacts[0].InputArtifactPaths);
            Assert.AreSequenceEqual(
                [firstManifestPath, secondPath],
                artifacts[0].InputArtifactPaths);
            Assert.DoesNotContain(unrelatedPath, artifacts[0].InputArtifactPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_ReportsDisjointInputsForMultipleOutputsAndLegacyReaderSkipsProvenance()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-multiple-provenance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstTaggedPath = Path.Combine(directory, "first-tagged.xml");
            string secondTaggedPath = Path.Combine(directory, "second-tagged.xml");
            string firstLegacyPath = Path.Combine(directory, "first-legacy.xml");
            string secondLegacyPath = Path.Combine(directory, "second-legacy.xml");
            File.WriteAllText(firstTaggedPath, "first tagged");
            File.WriteAllText(secondTaggedPath, "second tagged");
            File.WriteAllText(firstLegacyPath, "first legacy");
            File.WriteAllText(secondLegacyPath, "second legacy");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    inputs = new[]
                    {
                        new { path = firstTaggedPath, kind = (string?)"test.xml" },
                        new { path = secondTaggedPath, kind = (string?)"test.xml" },
                        new { path = firstLegacyPath, kind = (string?)null },
                        new { path = secondLegacyPath, kind = (string?)null },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            RawMessage artifactFrame = Assert.ContainsSingle(
                result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages));

            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrame.Body);
            Assert.HasCount(2, artifacts);
            Assert.AreEqual("test.xml.processed", artifacts[0].Kind);
            Assert.AreSequenceEqual([firstTaggedPath, secondTaggedPath], artifacts[0].InputArtifactPaths);
            Assert.AreEqual("legacy.xml.processed", artifacts[1].Kind);
            Assert.AreSequenceEqual([firstLegacyPath, secondLegacyPath], artifacts[1].InputArtifactPaths);

            IReadOnlyList<FileArtifact> legacyReaderArtifacts =
                DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrame.Body, readInputArtifactPaths: false);
            Assert.HasCount(2, legacyReaderArtifacts);
            Assert.AreEqual("test.xml.processed", legacyReaderArtifacts[0].Kind);
            Assert.AreEqual("legacy.xml.processed", legacyReaderArtifacts[1].Kind);
            Assert.IsNull(legacyReaderArtifacts[0].InputArtifactPaths);
            Assert.IsNull(legacyReaderArtifacts[1].InputArtifactPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_TruncatedRunInvokesOnlySupportingProcessor()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-dispatcher-truncated-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string summaryPath = Path.Combine(directory, "summary.fragment");
            string firstTrxPath = Path.Combine(directory, "first.trx");
            string secondTrxPath = Path.Combine(directory, "second.trx");
            File.WriteAllText(summaryPath, "summary");
            WriteMinimalReport(firstTrxPath, "first");
            WriteMinimalReport(secondTrxPath, "second");
            string manifestPath = Path.Combine(directory, "manifest.json");
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = directory,
                    truncationReason = "maximumFailedTests",
                    runSummary = new
                    {
                        totalTests = 10,
                        passedTests = 7,
                        failedTests = 2,
                        skippedTests = 1,
                        durationTicks = 1234567,
                        exitCode = 2,
                        testModuleCount = 2,
                    },
                    inputs = new[]
                    {
                        new { path = summaryPath, kind = "test.summary", executionId = "execution-1" },
                        new { path = firstTrxPath, kind = "microsoft.testing.trx", executionId = "execution-1" },
                        new { path = secondTrxPath, kind = "microsoft.testing.trx", executionId = "execution-2" },
                    },
                }));

            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);
            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.Success);
            RawMessage[] artifactFrames = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages)];
            Assert.HasCount(1, artifactFrames);
            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrames[0].Body);
            Assert.HasCount(1, artifacts);
            Assert.AreEqual("test.summary.processed", artifacts[0].Kind);
            string? outputPath = artifacts[0].FullPath;
            Assert.IsNotNull(outputPath);
            Assert.AreEqual(
                "True|MaximumFailedTests|1|10|7|2|1|1234567|2|2",
                File.ReadAllText(outputPath));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, "merged")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Dispatcher_InvalidOutputDirectory_ReturnsInvalidCommandLine()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    outputDirectory = "invalid\0directory",
                    inputs = Array.Empty<object>(),
                }));
            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);

            FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
                testHost,
                extraArguments: $"--manifest \"{manifestPath}\"",
                supportedProtocolVersions: "1.4.0",
                toolName: "internal-merge-artifacts",
                cancellationToken: TestContext.CancellationToken);

            result.TestHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public async Task MergeTool_UnexpectedPositionalArgument_ReturnsInvalidCommandLine()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(
            AssetFixture.TargetAssetPath,
            AssetName,
            TargetFrameworks.NetCurrent);

        TestHostResult result = await testHost.ExecuteAsync(
            "unexpected",
            toolName: "merge-trx",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.InvalidCommandLine);
    }

    [TestMethod]
    public async Task MergeTool_RepeatedInputs_WritesOutputTrx()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"merge-trx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.trx");
            string secondPath = Path.Combine(directory, "second.trx");
            string outputPath = Path.Combine(directory, "merged.trx");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);

            TestHostResult result = await testHost.ExecuteAsync(
                $"--input \"{firstPath}\" --input \"{secondPath}\" --output-trx \"{outputPath}\"",
                toolName: "merge-trx",
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.Success);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.AreEqual("TestRun", XDocument.Load(outputPath).Root?.Name.LocalName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task MergeTool_OptionsFromTestConfig_WritesOutputTrx()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"merge-trx-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string firstPath = Path.Combine(directory, "first.trx");
            string secondPath = Path.Combine(directory, "second.trx");
            string outputPath = Path.Combine(directory, "merged.trx");
            string configPath = Path.Combine(directory, "testconfig.json");
            WriteMinimalReport(firstPath, "first");
            WriteMinimalReport(secondPath, "second");
            File.WriteAllText(
                configPath,
                JsonSerializer.Serialize(new
                {
                    commandLineOptions = new Dictionary<string, object>
                    {
                        ["input"] = new[] { firstPath, secondPath },
                        ["output-trx"] = outputPath,
                    },
                }));
            var testHost = TestInfrastructure.TestHost.LocateFrom(
                AssetFixture.TargetAssetPath,
                AssetName,
                TargetFrameworks.NetCurrent);

            TestHostResult result = await testHost.ExecuteAsync(
                $"--config-file \"{configPath}\"",
                toolName: "merge-trx",
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.Success);
            Assert.IsTrue(File.Exists(outputPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void WriteMinimalReport(string path, string name)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        new XDocument(
            new XElement(
                ns + "TestRun",
                new XAttribute("id", Guid.NewGuid()),
                new XAttribute("name", name),
                new XElement(
                    ns + "ResultSummary",
                    new XAttribute("outcome", "Completed"),
                    new XElement(ns + "Counters", new XAttribute("total", 0)))))
            .Save(path);
    }

    private static void WriteMinimalHtmlReport(string path, string name)
        => File.WriteAllText(
            path,
            $"<!DOCTYPE html><script id=\"mtp-data\" type=\"application/json\">{JsonSerializer.Serialize(new
            {
                schemaVersion = "1",
                generator = "Microsoft.Testing.Extensions.HtmlReport",
                generatorVersion = "1.0.0",
                testApplication = $"{name}.dll",
                machineName = "machine",
                userName = "user",
                framework = "MSTest",
                frameworkUid = "MSTest",
                frameworkVersion = "1.0.0",
                startTime = "2026-08-09T10:00:00.0000000+00:00",
                endTime = "2026-08-09T10:00:01.0000000+00:00",
                exitCode = 0,
                tests = new[]
                {
                    new { rowKey = 0, uid = name, displayName = name, outcome = "passed", durationMs = 1d },
                },
                summary = new { },
            })}</script>");

    private static void WriteMinimalCtrfReport(string path, string name)
        => File.WriteAllText(
            path,
            JsonSerializer.Serialize(new
            {
                reportFormat = "CTRF",
                specVersion = "0.0.0",
                reportId = Guid.NewGuid().ToString("D"),
                results = new
                {
                    summary = new
                    {
                        tests = 1,
                        passed = 1,
                        start = 1000,
                        stop = 2000,
                    },
                    tests = new[]
                    {
                        new { name, status = "passed" },
                    },
                },
            }));

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetCode = """
            #file DotnetTestPipeArtifactPostProcessingTest.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
                <UseAppHost>true</UseAppHost>
                <LangVersion>preview</LangVersion>
                <NoWarn>$(NoWarn);TPEXP</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.CtrfReport" Version="$MicrosoftTestingExtensionsCtrfReportVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
                <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Extensions;
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Capabilities.TestFramework;
            using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
            using Microsoft.Testing.Platform.Extensions.TestFramework;

            public static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
                    builder.AddCtrfReportProvider();
                    builder.AddHtmlReportProvider();
                    builder.AddTrxReportProvider();
                    ((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing
                        .AddArtifactPostProcessor(_ => new SummaryArtifactPostProcessor());
                    ((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing
                        .AddArtifactPostProcessor(_ => new XmlArtifactPostProcessor());
                    ((IArtifactPostProcessingApplicationBuilder)builder).ArtifactPostProcessing
                        .AddArtifactPostProcessor(_ => new LegacyXmlArtifactPostProcessor());
                    builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DummyTestFramework());
                    using ITestApplication app = await builder.BuildAsync();
                    return await app.RunAsync();
                }
            }

            public sealed class DummyTestFramework : ITestFramework
            {
                public string Uid => nameof(DummyTestFramework);
                public string Version => "1.0.0";
                public string DisplayName => nameof(DummyTestFramework);
                public string Description => nameof(DummyTestFramework);
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
                    => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
                public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
                    => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
                public Task ExecuteRequestAsync(ExecuteRequestContext context)
                {
                    context.Complete();
                    return Task.CompletedTask;
                }
            }

            public sealed class SummaryArtifactPostProcessor : IArtifactPostProcessorRequiresPostProcessing
            {
                public string Uid => nameof(SummaryArtifactPostProcessor);
                public string Version => "1.0.0";
                public string DisplayName => nameof(SummaryArtifactPostProcessor);
                public string Description => nameof(SummaryArtifactPostProcessor);
                public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => [ArtifactPostProcessingMode.TestModules];
                public bool SupportsTruncatedRuns => true;
                public IReadOnlyList<string> SupportedKinds => ["test.summary"];
                public IReadOnlyList<string> SupportedFileExtensionsFallback => [];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);

                public async Task<ProcessedArtifact?> ProcessAsync(
                    IReadOnlyList<InputArtifact> inputs,
                    string outputDirectory,
                    ArtifactPostProcessingContext context,
                    CancellationToken cancellationToken)
                {
                    string outputPath = Path.Combine(outputDirectory, "partial-summary.txt");
                    await File.WriteAllTextAsync(
                        outputPath,
                        $"{context.IsTruncated}|{context.TruncationReason}|{inputs.Count}|{context.RunSummary?.TotalTests}|{context.RunSummary?.PassedTests}|{context.RunSummary?.FailedTests}|{context.RunSummary?.SkippedTests}|{context.RunSummary?.Duration.Ticks}|{context.RunSummary?.ExitCode}|{context.RunSummary?.TestModuleCount}",
                        cancellationToken);
                    return new ProcessedArtifact(
                        outputPath,
                        "test.summary.processed",
                        "Partial summary",
                        null);
                }
            }

            public sealed class XmlArtifactPostProcessor : IArtifactPostProcessor
            {
                public string Uid => nameof(XmlArtifactPostProcessor);
                public string Version => "1.0.0";
                public string DisplayName => nameof(XmlArtifactPostProcessor);
                public string Description => nameof(XmlArtifactPostProcessor);
                public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => [ArtifactPostProcessingMode.TestModules];
                public bool SupportsTruncatedRuns => false;
                public IReadOnlyList<string> SupportedKinds => ["test.xml"];
                public IReadOnlyList<string> SupportedFileExtensionsFallback => [];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);

                public async Task<ProcessedArtifact?> ProcessAsync(
                    IReadOnlyList<InputArtifact> inputs,
                    string outputDirectory,
                    ArtifactPostProcessingContext context,
                    CancellationToken cancellationToken)
                {
                    string outputPath = Path.Combine(outputDirectory, "merged.xml");
                    await File.WriteAllTextAsync(
                        outputPath,
                        string.Join("|", inputs.Select(input => File.ReadAllText(input.Path))),
                        cancellationToken);
                    return new ProcessedArtifact(outputPath, "test.xml.processed", "Merged XML", null);
                }
            }

            public sealed class LegacyXmlArtifactPostProcessor : IArtifactPostProcessor
            {
                public string Uid => nameof(LegacyXmlArtifactPostProcessor);
                public string Version => "1.0.0";
                public string DisplayName => nameof(LegacyXmlArtifactPostProcessor);
                public string Description => nameof(LegacyXmlArtifactPostProcessor);
                public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => [ArtifactPostProcessingMode.TestModules];
                public bool SupportsTruncatedRuns => false;
                public IReadOnlyList<string> SupportedKinds => [];
                public IReadOnlyList<string> SupportedFileExtensionsFallback => [".xml"];
                public Task<bool> IsEnabledAsync() => Task.FromResult(true);

                public async Task<ProcessedArtifact?> ProcessAsync(
                    IReadOnlyList<InputArtifact> inputs,
                    string outputDirectory,
                    ArtifactPostProcessingContext context,
                    CancellationToken cancellationToken)
                {
                    if (inputs.Count < 2)
                    {
                        return null;
                    }

                    string outputPath = Path.Combine(outputDirectory, "legacy-merged.xml");
                    await File.WriteAllTextAsync(
                        outputPath,
                        string.Join("|", inputs.Select(input => File.ReadAllText(input.Path))),
                        cancellationToken);
                    return new ProcessedArtifact(outputPath, "legacy.xml.processed", "Merged legacy XML", null);
                }
            }
            """;

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsCtrfReportVersion$", MicrosoftTestingExtensionsCtrfReportVersion));
    }
}
