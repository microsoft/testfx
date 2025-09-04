// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class DiagnosticTests : AcceptanceTestBase<DiagnosticTests.TestAssetFixture>
{
    private const string AssetName = "DiagnosticTest";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticIsSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic", cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAreSpecified_ReportIsGeneratedInDefaultLocation(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"abcd_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic --diagnostic-output-fileprefix abcd", cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "test1");
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-directory {diagPath}", cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticAndOutputFilePrefixAndOutputDirectoryAreSpecified_ReportIsGeneratedInSpecifiedLocation(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "test2");
        string diagPathPattern = Path.Combine(diagPath, @"abcde_.*.diag").Replace(@"\", @"\\");

        Assert.IsTrue(Directory.CreateDirectory(diagPath).Exists);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--diagnostic --diagnostic-output-fileprefix abcde --diagnostic-output-directory {diagPath}", cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticOutputFilePrefixButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix cccc", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-fileprefix' requires '--diagnostic' to be provided");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticOutputDirectoryButNotDiagnosticIsSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-directory cccc", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_WhenDiagnosticFilePrefixAndDiagnosticOutputDirectoryButNotDiagnosticAreSpecified_ReportGenerationFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--diagnostic-output-fileprefix aaaa --diagnostic-output-directory cccc", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--diagnostic-output-directory' requires '--diagnostic' to be provided");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_EnableWithEnvironmentVariables_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
            }, cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_EnableWithEnvironmentVariables_Verbosity_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY, "Trace" },
            },
            cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_EnableWithEnvironmentVariables_CustomPrefix_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"MyPrefix_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_FILEPREFIX, "MyPrefix" },
            },
            cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_EnableWithEnvironmentVariables_SynchronousWrite_Succeeded(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, AggregatedConfiguration.DefaultTestResultFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "1" },
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC_FILELOGGER_SYNCHRONOUSWRITE, "1" },
            },
            cancellationToken: TestContext.CancellationToken);

        await AssertDiagnosticReportWasGeneratedAsync(testHostResult, diagPathPattern, flushType: "sync");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Diag_EnableWithEnvironmentVariables_Disable_Succeeded(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { EnvironmentVariableConstants.TESTINGPLATFORM_DIAGNOSTIC, "0" },
            },
            cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain("Diagnostic file");

        testHostResult = await testHost.ExecuteAsync("--diagnostic", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputContains("Diagnostic file");
    }

    private static async Task<string> AssertDiagnosticReportWasGeneratedAsync(TestHostResult testHostResult, string diagPathPattern, string level = "Trace", string flushType = "async")
    {
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

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

    private static async Task<(bool IsMatch, string Content)> CheckDiagnosticContentsMatchAsync(string path, string pattern)
    {
        using var reader = new StreamReader(path);
        string content = await reader.ReadToEndAsync();
        return (Regex.IsMatch(content, pattern), content);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
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
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
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

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
       context.Complete();
       return Task.CompletedTask;
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
