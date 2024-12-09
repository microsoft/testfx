// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class TrxTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public TrxTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsNotSpecified_TrxReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string outputPattern = """
Out of process file artifacts produced:
- .+?\.trx
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecified_TrxReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(_testAssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 1);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenTestHostCrash_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --report-trx --report-trx-filename {fileName}.trx",
            new() { { "CRASHPROCESS", "1" } });

        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();
        string trxContent = File.ReadAllText(trxFile);
        Assert.That(Regex.IsMatch(trxContent, @"Test host process pid: .* crashed\."), trxContent);
        Assert.That(trxContent.Contains("""<ResultSummary outcome="Failed">"""), trxContent);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenSkipTest_ItAppearsAsExpectedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPathWithSkippedTest, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();

        string trxContent = File.ReadAllText(trxFile);

        // check if the tests have been added to Results, TestDefinitions, TestEntries and ResultSummary.
        Assert.That(trxContent.Contains(@"<UnitTestResult "), trxContent);
        Assert.That(trxContent.Contains(@"outcome=""NotExecuted"""), trxContent);

        Assert.That(trxContent.Contains(@"<UnitTest name=""TestMethod1"), trxContent);
        Assert.That(trxContent.Contains(@"<TestEntry "), trxContent);
        Assert.That(trxContent.Contains("""<ResultSummary outcome="Failed">"""), trxContent);
        Assert.That(trxContent.Contains("""<Counters total="2" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="2" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />"""), trxContent);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenTheTestNameHasInvalidXmlChar_TheTrxCreatedSuccessfully(string tfm)
    {
        string testResultsPath = Path.Combine(_testAssetFixture.TargetAssetPathWithDataRow, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPathWithDataRow, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 2);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_UsingDataDriven_CreatesUnitTestTagForEachOneInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPathWithSkippedTest, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();

        string trxContent = File.ReadAllText(trxFile);

        // check if the test have been added to TestDefinitions twice as the number of the data driven tests.
        string trxContentsPattern = """
\s*<UnitTest.*
\s*<UnitTest
""";
        Assert.That(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecifiedWithFullPath_TrxReportShouldFail(string tfm)
    {
        string testResultsPath = Path.Combine(_testAssetFixture.TargetAssetPath, "aaa", "Release", tfm, "TestResults");

        Assert.IsFalse(Directory.Exists(testResultsPath));

        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine(testResultsPath, "report.trx")}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecifiedWithRelativePath_TrxReportShouldFail(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine("aaa", "report.trx")}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsNotSpecifiedAndReportTrxPathIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx-filename report.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx-filename' requires '--report-trx' to be enabled");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecifiedAndListTestsIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --list-tests");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx' cannot be enabled when using '--list-tests'");
    }

    private async Task AssertTrxReportWasGeneratedAsync(TestHostResult testHostResult, string trxPathPattern, int numberOfTests)
    {
        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {trxPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Match match = Regex.Match(testHostResult.StandardOutput, trxPathPattern);
        Assert.IsTrue(match.Success);

        string trxContentsPattern = $"""
\s*<ResultSummary outcome="Completed">
\s*<Counters total="{numberOfTests}" executed="{numberOfTests}" passed="{numberOfTests}" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />
\s*</ResultSummary>
""";
        Assert.IsTrue(await CheckTrxContentsMatchAsync(match.Value, trxContentsPattern), $"Output of the test host is:\n{testHostResult}");
    }

    private async Task<bool> CheckTrxContentsMatchAsync(string path, string pattern)
    {
        using StreamReader reader = new(path);
        return Regex.IsMatch(await reader.ReadToEndAsync(), pattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AssetName = "TrxTest";
        public const string AssetNameUsingMSTest = "TrxTestUsingMSTest";
        private const string WithSkippedTest = nameof(WithSkippedTest);
        private const string WithDataRow = nameof(WithDataRow);

        private const string TestCode = """
#file TrxTest.csproj
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
        <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingInternalFrameworkVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingInternalFrameworkVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using TrxTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
builder.AddCrashDumpProvider();
builder.AddTrxReportProvider();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace TrxTest;

[TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        if (Environment.GetEnvironmentVariable("CRASHPROCESS") == "1")
        {
            Environment.FailFast("CRASHPROCESS");
        }

        Assert.IsTrue(true);
    }
}

#file Usings.cs
global using System;
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Extensions;
""";

        private const string MSTestCode = """
#file TrxTestUsingMSTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using TrxTestUsingMSTest;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { typeof(Program).Assembly });
builder.AddTrxReportProvider();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace TrxTestUsingMSTest;

[TestClass]
public class UnitTest1
{
    $IgnoreTestAttributeOrNothing$
    [TestMethod]
    [DataRow("data\0")]
    [DataRow("data")]
    public void TestMethod1(string s)
    {
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public string TargetAssetPathWithSkippedTest => GetAssetPath(WithSkippedTest);

        public string TargetAssetPathWithDataRow => GetAssetPath(WithDataRow);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                .PatchCodeWithReplace("$MicrosoftTestingInternalFrameworkVersion$", MicrosoftTestingInternalFrameworkVersion));
            yield return (WithSkippedTest, AssetNameUsingMSTest,
                MSTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$IgnoreTestAttributeOrNothing$", "[Ignore]"));
            yield return (WithDataRow, AssetNameUsingMSTest,
                MSTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$IgnoreTestAttributeOrNothing$", string.Empty));
        }
    }
}
