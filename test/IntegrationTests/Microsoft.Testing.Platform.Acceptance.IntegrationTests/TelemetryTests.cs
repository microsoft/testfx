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
    public async Task Telemetry_ByDefault_TelemetryIsEnabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", disableTelemetry: false, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

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
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
            },
            disableTelemetry: false, TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_WhenOptingOutTelemetry_With_DOTNET_CLI_EnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "1" },
            },
            disableTelemetry: false, TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Telemetry_WhenEnableTelemetryIsFalse_WithTestApplicationOptions_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPathWithDisableTelemetry, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPathWithDisableTelemetry, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", disableTelemetry: false, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: False
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    private static async Task<string> AssertDiagnosticReportAsync(TestHostResult testHostResult, string diagPathPattern, string diagContentsPattern, string level = "Trace", string flushType = "async")
    {
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

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

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string WithTelemetry = nameof(WithTelemetry);
        private const string WithoutTelemetry = nameof(WithoutTelemetry);

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
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args$TelemetryArg$);
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

        public string TargetAssetPathWithDisableTelemetry => GetAssetPath(WithoutTelemetry);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (WithTelemetry, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$TelemetryArg$", string.Empty));

            yield return (WithoutTelemetry, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$TelemetryArg$", ", new TestApplicationOptions() { EnableTelemetry = false }"));
        }
    }

    public TestContext TestContext { get; set; }
}
