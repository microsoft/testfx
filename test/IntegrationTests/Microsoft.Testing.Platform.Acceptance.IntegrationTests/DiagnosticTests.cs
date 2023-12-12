// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class DiagnosticTests : BaseAcceptanceTests
{
    private const string AssetName = "DiagnosticTest";

    private readonly BuildFixture _buildFixture;

    public DiagnosticTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture,
        BuildFixture buildFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _buildFixture = buildFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticIsSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAreSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"abcd_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic --diagnostic-output-fileprefix abcd");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, "test1");
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-directory {diagPath}");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, "test2");
        string diagPathPattern = Path.Combine(diagPath, @"abcde_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-fileprefix abcde --diagnostic-output-directory {diagPath}");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticOutputFilePrefixButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix cccc");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-fileprefix' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticOutputDirectoryButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-directory cccc");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticFilePrefixAndDiagnosticOutputDirectoryButNotDiagnosticAreSpecified_ReportGenerationFails(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix aaaa --diagnostic-output-directory cccc");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Verbosity_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY, "Trace" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern, "Trace");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_CustomPrefix_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"MyPrefix_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX, "MyPrefix" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_SynchronousWrite_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE, "1" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern, flushType: "sync");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Disable_Succeeded(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string>()
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "0" },
            });
        testHostResult.AssertHasExitCode(ExitCodes.Success);
        testHostResult.AssertOutputDoesNotContain("Diagnostic file");

        testHostResult = await testHost.ExecuteAsync("--diagnostic");
        testHostResult.AssertHasExitCode(ExitCodes.Success);
        testHostResult.AssertOutputContains("Diagnostic file");
    }

    private async Task<string> AssertDiagnosticReportWasGeneratedAsync(TestHostResult testHostResult, string diagPathPattern, string level = "Information", string flushType = "async")
    {
        testHostResult.AssertHasExitCode(ExitCodes.Success);

        string outputPattern = $"""
Diagnostic file \(level '{level}' with {flushType} flush\): {diagPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);
        Match match = Regex.Match(testHostResult.StandardOutput, diagPathPattern);
        Assert.IsTrue(match.Success, $"{testHostResult}\n{diagPathPattern}");

        string diagContentsPattern =
"""
\[.* - Information\] Version: .*
\[.* - Information] Logging level: .*
\[.* - Information\] CreateBuilderAsync entry time: .*
\[.* - Information\] PID: .*
""";
        (bool isMatch, string content) = await CheckDiagnosticContentsMatchAsync(match.Value, diagContentsPattern);
        Assert.IsTrue(isMatch, $"{content}\n{diagContentsPattern}");
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

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public BuildFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync(
                AssetName,
                TestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string TestCode = """
#file DiagnosticTest.csproj
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
        <PackageReference Include="Microsoft.Testing.Framework" Version="[1.0.0-*,)" />
        <PackageReference Include="Microsoft.Testing.Framework.SourceGeneration" Version="[1.0.0-*,)" />
    </ItemGroup>
</Project>

#file Program.cs
using DiagnosticTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace DiagnosticTest;

[TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        Assert.IsTrue(true);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Framework;
global using Microsoft.Testing.Platform.Extensions;
""";
}
