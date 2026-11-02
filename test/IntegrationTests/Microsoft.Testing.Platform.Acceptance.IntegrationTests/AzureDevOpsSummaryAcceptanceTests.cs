// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.DotnetTestPipe;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class AzureDevOpsSummaryAcceptanceTests
{
    private const string AssetName = "AzureDevOpsSummaryBehavior";
    private const string TargetFramework = "net8.0";
    private static TestAssetFixture? s_assetFixture;

    public TestContext TestContext { get; set; } = null!;

    private static TestAssetFixture AssetFixture
        => s_assetFixture ?? throw new InvalidOperationException("The summary fixture has not been initialized.");

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        // The general acceptance base eagerly requires every shipping package. This bounded
        // fixture needs only AzureDevOpsReport and its dependencies, not the full package matrix.
        var fixture = new TestAssetFixture();
        try
        {
            await fixture.InitializeAsync(context.CancellationToken);
            s_assetFixture = fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    [ClassCleanup]
    public static void ClassCleanup()
        => s_assetFixture?.Dispose();

    [TestMethod]
    public async Task PackedStandalone_ReportsOutcomesSkipReasonsAndSummedTestTime()
    {
        using var directory = new TempDirectory();
        string summaryPath = Path.Combine(directory.Path, "summary.md");
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFramework);

        TestHostResult result = await host.ExecuteAsync(
            $"--report-azdo --report-azdo-annotations off --report-azdo-summary \"{summaryPath}\" --results-directory \"{directory.Path}\"",
            CreateEnvironment(),
            cancellationToken: TestContext.CancellationToken);

        // The child deliberately reports a failed test; the outer acceptance case must still pass.
        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        AssertStandaloneSummary(File.ReadAllText(summaryPath));
        string upload = Assert.ContainsSingle(result.StandardOutputLines.Where(static line =>
            line.StartsWith("##vso[task.addattachment ", StringComparison.Ordinal)));
        Assert.StartsWith("##vso[task.addattachment type=Distributedtask.Core.Summary;name=Test results - AzureDevOpsSummaryBehavior (net8.0", upload);
        Assert.EndsWith($";]{summaryPath}", upload);
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, ".ci-summary-fragments")));
    }

    [TestMethod]
    public async Task FixtureSdkWithoutRequiredArtifactSupport_PreservesPerModuleFallback()
    {
        using var directory = new TempDirectory();
        string summaryPath = Path.Combine(directory.Path, "summary.md");
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFramework);

        // This is the repository's SDK protocol fixture, not a native shipping SDK invocation.
        // A modern protocol version alone does not promise required artifact postprocessing.
        FakeDotnetTestSdkResult result = await FakeDotnetTestSdk.RunAsync(
            host,
            extraArguments: $"--report-azdo --report-azdo-annotations off --report-azdo-summary \"{summaryPath}\" --results-directory \"{directory.Path}\"",
            environmentVariables: CreateEnvironment(),
            supportedProtocolVersions: "1.4.0",
            cancellationToken: TestContext.CancellationToken);

        result.TestHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        Assert.AreEqual("1.4.0", result.NegotiatedProtocolVersion);
        Assert.IsNotNull(result.SentHandshakeReply);
        Assert.IsFalse(result.SentHandshakeReply.ContainsKey(DotnetTestPipeProtocol.HandshakeProperties.RequiredPostProcessingSupported));
        AssertStandaloneSummary(File.ReadAllText(summaryPath));
        string? upload = Assert.ContainsSingle(
            result.MessagesWithSerializerId(DotnetTestPipeProtocol.SerializerIds.AzureDevOpsLogMessage)
                .Select(frame => DotnetTestPipeProtocol.DecodeAzureDevOpsLogMessageBody(frame.Body).LogText)
                .Where(static line => line?.StartsWith("##vso[task.addattachment ", StringComparison.Ordinal) == true));
        Assert.IsNotNull(upload);
        Assert.EndsWith($";]{summaryPath}", upload);
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Path, ".ci-summary-fragments")));
    }

    private static Dictionary<string, string?> CreateEnvironment() => new()
    {
        ["TF_BUILD"] = "true",
        ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
        ["TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER"] = "1",
        ["TESTINGPLATFORM_DOTNETTEST_EXECUTIONID"] = Guid.NewGuid().ToString("N"),
    };

    private static void AssertStandaloneSummary(string markdown)
    {
        string operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "Linux"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown";
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        Assert.AreEqual(
            $"""
            ## ❌ AzureDevOpsSummaryBehavior (net8.0)

            **4 tests** · ✅ **1 passed** · ❌ **1 failed** · ⏭ **2 skipped** · ⏱ Test time: **17.50s** · exit code **2** (AtLeastOneTestFailed — At least one test failed.)

            > Test time is the sum of reported test durations, not elapsed time; tests may run concurrently.

            ### Skipped tests by reason

            | Skipped | Reason | Platform | Test module |
            | ---: | --- | --- | --- |
            | 2 | Known issue https://example.test/issues/42 | {operatingSystem} | AzureDevOpsSummaryBehavior (net8.0, {architecture}) |

            ### Top failing classes

            | Class | Failures |
            | --- | ---: |
            | Tests.Suite | 1 |

            ### Failure details

            **`Tests.Suite.Failed` — 15.00s**

            **Message:**

            > Expected failure.

            > Duration history unavailable for the observed tests; current timings are not a historical baseline.

            ### ⏱ Slowest tests

            - **15.00s** — `Failed` — failed
            - **2.50s** — `Passed` — passed


            """.ReplaceLineEndings("\n"),
            markdown.ReplaceLineEndings("\n"));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
            #file AzureDevOpsSummaryBehavior.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
                <OutputType>Exe</OutputType>
                <IsTestProject>true</IsTestProject>
                <UseAppHost>true</UseAppHost>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <LangVersion>preview</LangVersion>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="$AzureDevOpsReportVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Extensions;
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Capabilities.TestFramework;
            using Microsoft.Testing.Platform.Extensions.Messages;
            using Microsoft.Testing.Platform.Extensions.TestFramework;

            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new SummaryTestFramework());
            builder.AddAzureDevOpsProvider();
            using ITestApplication application = await builder.BuildAsync();
            return await application.RunAsync();

            public sealed class SummaryTestFramework : ITestFramework, IDataProducer
            {
                public string Uid => nameof(SummaryTestFramework);
                public string Version => "1.0.0";
                public string DisplayName => nameof(SummaryTestFramework);
                public string Description => nameof(SummaryTestFramework);
                public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

                public Task<bool> IsEnabledAsync() => Task.FromResult(true);
                public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
                    => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
                public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
                    => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

                public async Task ExecuteRequestAsync(ExecuteRequestContext context)
                {
                    await PublishAsync(context, "Passed", PassedTestNodeStateProperty.CachedInstance, TimeSpan.FromSeconds(2.5));
                    await PublishAsync(context, "Failed", new FailedTestNodeStateProperty("Expected failure."), TimeSpan.FromSeconds(15));
                    await PublishAsync(context, "PrivateSkippedName1",
                        new SkippedTestNodeStateProperty(" Known issue\nhttps://example.test/issues/42 "), TimeSpan.Zero);
                    await PublishAsync(context, "PrivateSkippedName2",
                        new SkippedTestNodeStateProperty("Known issue https://example.test/issues/42"), TimeSpan.Zero);
                    context.Complete();
                }

                private async Task PublishAsync(ExecuteRequestContext context, string name, TestNodeStateProperty state, TimeSpan duration)
                {
                    DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                        context.Request.Session.SessionUid,
                        new TestNode
                        {
                            Uid = name,
                            DisplayName = name,
                            Properties = new PropertyBag(
                                state,
                                new TimingProperty(new TimingInfo(start, start + duration, duration)),
                                new TestMethodIdentifierProperty("AzureDevOpsSummaryBehavior.dll", "Tests", "Suite", name, 0, [], "System.Void")),
                        }));
                }
            }
            """;

        public string TargetAssetPath => GetAssetPath(AssetName);

        // This fixture implements ITestFramework directly; there is no MSTest metadata to generate.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            const string PackagePrefix = "Microsoft.Testing.Extensions.AzureDevOpsReport.";
            string packagePath = Assert.ContainsSingle(Directory.GetFiles(Constants.ArtifactsPackagesShipping, PackagePrefix + "*.nupkg"));
            string fileName = Path.GetFileName(packagePath);
            string packageVersion = fileName.Substring(PackagePrefix.Length, fileName.Length - PackagePrefix.Length - ".nupkg".Length);
            return (AssetName, AssetName, Sources
                .PatchTargetFrameworks(TargetFramework)
                .PatchCodeWithReplace("$AzureDevOpsReportVersion$", packageVersion));
        }
    }
}
