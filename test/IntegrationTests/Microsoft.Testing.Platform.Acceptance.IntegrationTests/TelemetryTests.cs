// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TelemetryTests : AcceptanceTestBase<TelemetryTests.TestAssetFixture>
{
    private const string AssetName = "TelemetryTest";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_Context_IsIncludedInEveryUsageEvent(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        (string? Agent, string? SessionId, string ExpectedLLM, string ExpectedSessionId)[] contexts =
        [
            ("github_copilot_app_agent", "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012", "true", "aBCDEfAB-1234-4567-8ABC-DEFaBCDEF012"),
            ("github_copilot_app_agent", "build-investigation-42", "true", "a5b5273cea99d1a7df8e81f234369666daf7d810c64d852df129ae276d5167b2"),
            ("github_copilot_vscode_agent", null, "true", "not_present"),
            (null, null, "false", "not_present"),
        ];

        foreach ((string? Agent, string? SessionId, string ExpectedLLM, string ExpectedSessionId) context in contexts)
        {
            using TempDirectory diagnostics = new();
            TestHostResult result = await testHost.ExecuteAsync(
                $"--diagnostic --no-banner --diagnostic-output-directory \"{diagnostics.Path}\"",
                new Dictionary<string, string?>
                {
                    ["AI_AGENT"] = context.Agent,
                    ["COPILOT_AGENT_SESSION_ID"] = context.SessionId,
                    ["TESTINGPLATFORM_APPINSIGHTS_SESSIONID"] = "11111111-2222-4333-8444-555555555555",
                },
                disableTelemetry: false,
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.ZeroTests);
            result.AssertOutputContains($"TelemetryAssemblyPath: {Path.Combine(testHost.DirectoryName, "Microsoft.Testing.Extensions.Telemetry.dll")}");
            result.AssertOutputContains($"PlatformAssemblyPath: {Path.Combine(testHost.DirectoryName, "Microsoft.Testing.Platform.dll")}");
            string[] reports = Directory.GetFiles(diagnostics.Path, "*.diag");
            Assert.HasCount(1, reports);
            TestContext.WriteLine($"Telemetry diagnostics: {reports[0]}");
            string content = await File.ReadAllTextAsync(reports[0], TestContext.CancellationToken);
            MatchCollection events = Regex.Matches(content, @"Send telemetry event: (?<name>[^\r\n]+)\r?\n(?<properties>(?:    [^\r\n]*\r?\n)+)");
            Assert.HasCount(Regex.Matches(content, "Send telemetry event:").Count, events);
            Assert.Contains("dotnet/testingplatform/host/testhostbuilt", events.Select(e => e.Groups["name"].Value));
            Assert.Contains("dotnet/testingplatform/host/consoletesthostexit", events.Select(e => e.Groups["name"].Value));
            foreach (Match telemetryEvent in events)
            {
                var properties = Regex.Matches(telemetryEvent.Groups["properties"].Value, @"(?m)^    (?<key>[^:\r\n]+): (?<value>[^\r\n]*)")
                    .ToDictionary(m => m.Groups["key"].Value, m => m.Groups["value"].Value);
                Assert.Contains("is llm", properties.Keys, telemetryEvent.Value);
                Assert.AreEqual(context.ExpectedLLM, properties["is llm"], telemetryEvent.Value);
                Assert.Contains("copilot agent session id", properties.Keys, telemetryEvent.Value);
                Assert.AreEqual(context.ExpectedSessionId, properties["copilot agent session id"], telemetryEvent.Value);
                Assert.AreEqual("11111111-2222-4333-8444-555555555555", properties["session id"], telemetryEvent.Value);
            }
        }
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_ByDefault_TelemetryIsEnabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = BuildDefaultDiagnosticFilePathPattern(diagPath, AssetName, tfm);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", disableTelemetry: false, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'ENABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_WhenOptingOutTelemetry_WithEnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = BuildDefaultDiagnosticFilePathPattern(diagPath, AssetName, tfm);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
                { "COPILOT_AGENT_SESSION_ID", "build-investigation-42" },
            },
            disableTelemetry: false,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        string report = await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
        string content = await File.ReadAllTextAsync(report, TestContext.CancellationToken);
        Assert.DoesNotContain("Send telemetry event:", content);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_WhenOptingOutTelemetry_With_DOTNET_CLI_EnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = BuildDefaultDiagnosticFilePathPattern(diagPath, AssetName, tfm);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "1" },
                { "COPILOT_AGENT_SESSION_ID", "build-investigation-42" },
            },
            disableTelemetry: false,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        string report = await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
        string content = await File.ReadAllTextAsync(report, TestContext.CancellationToken);
        Assert.DoesNotContain("Send telemetry event:", content);
    }

    private static async Task<string> AssertDiagnosticReportAsync(TestHostResult testHostResult, string diagPathPattern, string diagContentsPattern, string level = "Trace", string flushType = "async")
    {
        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        string outputPattern = $"""
Diagnostic file \(level '{level}' with {flushType} flush\): {diagPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);
        Match match = Regex.Match(testHostResult.StandardOutput, diagPathPattern);
        Assert.IsTrue(match.Success, $"{testHostResult}\n{diagPathPattern}");

        (bool success, string content) = await CheckDiagnosticContentsMatchAsync(match.Value, diagContentsPattern);
        Assert.IsTrue(success, $"{content}\n{diagContentsPattern}");
        return match.Value;
    }

    private static async Task<(bool IsMatch, string Content)> CheckDiagnosticContentsMatchAsync(string path, string pattern)
    {
        using var reader = new StreamReader(path);
        string content = await reader.ReadToEndAsync();
        return (Regex.IsMatch(content, pattern), content);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string WithTelemetry = nameof(WithTelemetry);

        private const string TestCode = """
#file TelemetryTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Telemetry" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"TelemetryAssemblyPath: {typeof(AppInsightsTelemetryProviderExtensions).Assembly.Location}");
        Console.WriteLine($"PlatformAssemblyPath: {typeof(ITestApplicationBuilder).Assembly.Location}");
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args$TelemetryArg$);
        builder.AddAppInsightsTelemetryProvider();
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
       context.Complete();
       return Task.CompletedTask;
    }
}
""";

        public string TargetAssetPath => GetAssetPath(WithTelemetry);

        // This custom framework has no MSTest metadata for the source generator to process.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (WithTelemetry, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$TelemetryArg$", string.Empty));
    }

    public TestContext TestContext { get; set; }
}
