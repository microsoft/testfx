// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class TelemetryTests : AcceptanceTestBase
{
    private const string AssetName = "TelemetryTest";

    private readonly TestAssetFixture _testAssetFixture;

    public TelemetryTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Telemetry_ByDefault_TelemetryIsEnabled(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", disableTelemetry: false);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_NOBANNER environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_NOLOGO environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'ENABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Telemetry_WhenOptingOutTelemetry_WithEnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
            },
            disableTelemetry: false);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_NOBANNER environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_NOLOGO environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Telemetry_WhenOptingOutTelemetry_With_DOTNET_CLI_EnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string>
            {
                { EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "1" },
            },
            disableTelemetry: false);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: True
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: '1'
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_NOBANNER environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_NOLOGO environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Telemetry_WhenEnableTelemetryIsFalse_WithTestApplicationOptions_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPathWithDisableTelemetry, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPathWithDisableTelemetry, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", disableTelemetry: false);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TestApplicationOptions.EnableTelemetry: False
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG TESTINGPLATFORM_NOBANNER environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG DOTNET_NOLOGO environment variable: ''
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    private async Task<string> AssertDiagnosticReportAsync(TestHostResult testHostResult, string diagPathPattern, string diagContentsPattern, string level = "Trace", string flushType = "async")
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

    private async Task<(bool IsMatch, string Content)> CheckDiagnosticContentsMatchAsync(string path, string pattern)
    {
        using var reader = new StreamReader(path);
        string content = await reader.ReadToEndAsync();
        return (Regex.IsMatch(content, pattern), content);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
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
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestAdapter : ITestFramework
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

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
}
