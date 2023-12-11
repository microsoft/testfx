// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class TelemetryTests : BaseAcceptanceTests
{
    private const string AssetName = "TelemetryTest";

    private readonly BuildFixture _buildFixture;

    public TelemetryTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture,
        BuildFixture buildFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _buildFixture = buildFixture;
    }

    [ArgumentsProvider(nameof(All_Tfms))]
    public async Task Telemetry_ByDefault_TelemetryIsEnabled(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic");

        AcceptanceAssert.HasExitCode(ExitCodes.ZeroTests, testHostResult);

        string diagContentsPattern =
"""
\[.* - Information\] TestApplicationOptions.EnableTelemetry: True
\[.* - Information\] TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] TESTINGPLATFORM_NOBANNER environment variable: ''
\[.* - Information\] DOTNET_NOLOGO environment variable: ''
\[.* - Information\] Telemetry is 'ENABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(All_Tfms))]
    public async Task Telemetry_WhenOptingOutTelemetry_WithEnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_TELEMETRY_OPTOUT, "1" },
            });

        AcceptanceAssert.HasExitCode(ExitCodes.ZeroTests, testHostResult);

        string diagContentsPattern =
"""
\[.* - Information\] TestApplicationOptions.EnableTelemetry: True
\[.* - Information\] TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: '1'
\[.* - Information\] DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] TESTINGPLATFORM_NOBANNER environment variable: ''
\[.* - Information\] DOTNET_NOLOGO environment variable: ''
\[.* - Information\] Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(All_Tfms))]
    public async Task Telemetry_WhenOptingOutTelemetry_With_DOTNET_CLI_EnvironmentVariable_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.DOTNET_CLI_TELEMETRY_OPTOUT, "1" },
            });

        AcceptanceAssert.HasExitCode(ExitCodes.ZeroTests, testHostResult);

        string diagContentsPattern =
"""
\[.* - Information\] TestApplicationOptions.EnableTelemetry: True
\[.* - Information\] TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] DOTNET_CLI_TELEMETRY_OPTOUT environment variable: '1'
\[.* - Information\] TESTINGPLATFORM_NOBANNER environment variable: ''
\[.* - Information\] DOTNET_NOLOGO environment variable: ''
\[.* - Information\] Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    [ArgumentsProvider(nameof(All_Tfms))]
    public async Task Telemetry_WhenEnableTelemetryIsFalse_WithTestApplicationOptions_TelemetryIsDisabled(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPathWithDisableTelemetry, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPathWithDisableTelemetry, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic");

        AcceptanceAssert.HasExitCode(ExitCodes.ZeroTests, testHostResult);

        string diagContentsPattern =
"""
\[.* - Information\] TestApplicationOptions.EnableTelemetry: False
\[.* - Information\] TESTINGPLATFORM_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] DOTNET_CLI_TELEMETRY_OPTOUT environment variable: ''
\[.* - Information\] TESTINGPLATFORM_NOBANNER environment variable: ''
\[.* - Information\] DOTNET_NOLOGO environment variable: ''
\[.* - Information\] Telemetry is 'DISABLED'
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    private async Task<string> AssertDiagnosticReportAsync(TestHostResult testHostResult, string diagPathPattern, string diagContentsPattern, string level = "Information", string flushType = "async")
    {
        AcceptanceAssert.HasExitCode(ExitCodes.ZeroTests, testHostResult);

        string outputPattern = $"""
Diagnostic file \(level '{level}' with {flushType} flush\): {diagPathPattern}
""";
        AcceptanceAssert.OutputMatchesRegex(outputPattern, testHostResult);
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
    public sealed class BuildFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;

        private TestAsset? _testAsset;
        private TestAsset? _testAssetWithDisableTelemetry;

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public string TargetAssetPathWithDisableTelemetry => _testAssetWithDisableTelemetry!.TargetAssetPath;

        public BuildFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync(
                AssetName,
                TestCode.PatchCodeWithRegularExpression("tfms", All_Tfms.ToTargetFrameworksElementContent()).PatchCodeWithRegularExpression("disableTelemetry", string.Empty));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);

            _testAssetWithDisableTelemetry = await TestAsset.GenerateAssetAsync(
                AssetName,
                TestCode.PatchCodeWithRegularExpression("tfms", All_Tfms.ToTargetFrameworksElementContent()).PatchCodeWithRegularExpression("disableTelemetry", ", new TestApplicationOptions() { EnableTelemetry = false }"));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAssetWithDisableTelemetry.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose()
        {
            _testAsset?.Dispose();
            _testAssetWithDisableTelemetry?.Dispose();
        }
    }

    private const string TestCode = """
#file TelemetryTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>tfms</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args disableTelemetry);
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
    public Task ExecuteRequestAsync(ExecuteRequestContext context) => Task.CompletedTask;
}
""";
}
