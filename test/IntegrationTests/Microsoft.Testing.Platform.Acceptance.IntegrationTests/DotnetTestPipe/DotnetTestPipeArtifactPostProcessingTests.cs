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
                "microsoft.testing.trx",
                result.ReceivedHandshake[DotnetTestPipeProtocol.HandshakeProperties.SupportedPostProcessorKinds]);

            RawMessage[] artifactFrames = [.. result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.FileArtifactMessages)];
            Assert.HasCount(1, artifactFrames);
            IReadOnlyList<FileArtifact> artifacts = DotnetTestPipeProtocol.DecodeFileArtifacts(artifactFrames[0].Body);
            Assert.HasCount(1, artifacts);
            Assert.AreEqual("microsoft.testing.trx", artifacts[0].Kind);
            Assert.IsNotNull(artifacts[0].FullPath);
            Assert.IsTrue(File.Exists(artifacts[0].FullPath));
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
                <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Extensions;
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Capabilities.TestFramework;
            using Microsoft.Testing.Platform.Extensions.TestFramework;

            public static class Program
            {
                public static async Task<int> Main(string[] args)
                {
                    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
                    builder.AddTrxReportProvider();
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
            """;

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetName, AssetName, AssetCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
