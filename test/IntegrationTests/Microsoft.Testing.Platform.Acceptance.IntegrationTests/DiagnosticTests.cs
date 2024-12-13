// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class DiagnosticTests : AcceptanceTestBase
{
    private const string AssetName = "DiagnosticTest";

    private readonly TestAssetFixture _testAssetFixture;

    public DiagnosticTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticIsSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAreSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"abcd_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic --diagnostic-output-fileprefix abcd");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, "test1");
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-directory {diagPath}");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, "test2");
        string diagPathPattern = Path.Combine(diagPath, @"abcde_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-fileprefix abcde --diagnostic-output-directory {diagPath}");

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticOutputFilePrefixButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix cccc");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-fileprefix' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticOutputDirectoryButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-directory cccc");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_WhenDiagnosticFilePrefixAndDiagnosticOutputDirectoryButNotDiagnosticAreSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix aaaa --diagnostic-output-directory cccc");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Verbosity_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY, "Trace" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_CustomPrefix_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"MyPrefix_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX, "MyPrefix" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_SynchronousWrite_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE, "1" },
            });

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern, flushType: "sync");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Diag_EnableWithEnvironmentVariables_Disable_Succeeded(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "0" },
            });
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputDoesNotContain("Diagnostic file");

        testHostResult = await testHost.ExecuteAsync("--diagnostic");
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("Diagnostic file");
    }

    private async Task<string> AssertDiagnosticReportWasGeneratedAsync(TestHostResult testHostResult, string diagPathPattern, string level = "Trace", string flushType = "async")
    {
        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string outputPattern = $"""
Diagnostic file \(level '{level}' with {flushType} flush\): {diagPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);
        Match match = Regex.Match(testHostResult.StandardOutput, diagPathPattern);
        Assert.IsTrue(match.Success, $"{testHostResult}\n{diagPathPattern}");

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Builder.TestApplication INFORMATION Version: .+
.+ Microsoft.Testing.Platform.Builder.TestApplication INFORMATION Logging mode: .+
.+ Microsoft.Testing.Platform.Builder.TestApplication INFORMATION Logging level: .+
.+ Microsoft.Testing.Platform.Builder.TestApplication INFORMATION CreateBuilderAsync entry time: .+
.+ Microsoft.Testing.Platform.Builder.TestApplication INFORMATION PID: .+
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
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string TestCode = """
#file DiagnosticTest.csproj
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
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingInternalFrameworkVersion$" />
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
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Extensions;
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
        }
    }
}
