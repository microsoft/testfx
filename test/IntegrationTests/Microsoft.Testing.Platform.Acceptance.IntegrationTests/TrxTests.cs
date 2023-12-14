// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class TrxTests : BaseAcceptanceTests
{
    private readonly BuildFixture _buildFixture;

    public TrxTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, BuildFixture buildFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _buildFixture = buildFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsNotSpecified_TrxReportIsNotGenerated(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertHasExitCode(ExitCodes.Success);

        string outputPattern = """
Out of process file artifacts produced:
- .+?\.trx
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecified_TrxReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(_buildFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGenerated(testHostResult, trxPathPattern, 1);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenTestHostCrash_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --report-trx --report-trx-filename {fileName}.trx",
            new() { { "CRASHPROCESS", "1" } });

        testHostResult.AssertHasExitCode(ExitCodes.TestHostProcessExitedNonGracefully);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();
        string trxContent = File.ReadAllText(trxFile);
        Assert.That(Regex.IsMatch(trxContent, @"Test host process pid: .* crashed\."), trxContent);
        Assert.That(Regex.IsMatch(trxContent, @"<ResultSummary outcome=""Failed"">"), trxContent);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenSkipTest_ItAppearsAsExpectedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPathWithSkippedTest, BuildFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertHasExitCode(ExitCodes.Success);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();

        string trxContent = File.ReadAllText(trxFile);

        // check if the tests have been added to Results, TestDefinitions, TestEntries and ResultSummary.
        Assert.That(Regex.IsMatch(trxContent, @"<UnitTestResult "), trxContent);
        Assert.That(Regex.IsMatch(trxContent, @"outcome=""NotExecuted"""), trxContent);

        Assert.That(Regex.IsMatch(trxContent, @"<UnitTest name=""TestMethod1"), trxContent);
        Assert.That(Regex.IsMatch(trxContent, @"<TestEntry "), trxContent);
        Assert.That(Regex.IsMatch(trxContent, @"<ResultSummary outcome=""Completed"">"), trxContent);
        Assert.That(Regex.IsMatch(trxContent, @"<Counters total=""2"" executed=""0"" passed=""0"" failed=""0"" error=""0"" timeout=""0"" aborted=""0"" inconclusive=""0"" passedButRunAborted=""0"" notRunnable=""0"" notExecuted=""0"" disconnected=""0"" warning=""0"" completed=""0"" inProgress=""0"" pending=""0"" />"), trxContent);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_WhenTheTestNameHasInvalidXmlChar_TheTrxCreatedSuccessfully(string tfm)
    {
        string testResultsPath = Path.Combine(_buildFixture.TargetAssetPathWithDataRow, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPathWithDataRow, BuildFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGenerated(testHostResult, trxPathPattern, 2);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.Net), typeof(TargetFrameworks))]
    public async Task Trx_UsingDataDriven_CreatesUnitTestTagForEachOneInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPathWithSkippedTest, BuildFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertHasExitCode(ExitCodes.Success);

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
        string testResultsPath = Path.Combine(_buildFixture.TargetAssetPath, "aaa", "Release", tfm, "TestResults");

        Assert.IsFalse(Directory.Exists(testResultsPath));

        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine(testResultsPath, "report.trx")}");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecifiedWithRelativePath_TrxReportShouldFail(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine("aaa", "report.trx")}");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsNotSpecifiedAndReportTrxPathIsSpecified_ErrorIsDisplayed(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx-filename report.trx");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx-filename' requires '--report-trx' to be enabled");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Trx_WhenReportTrxIsSpecifiedAndListTestsIsSpecified_ErrorIsDisplayed(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, BuildFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --list-tests");

        testHostResult.AssertHasExitCode(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx' cannot be enabled when using '--list-tests'");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class BuildFixture : IAsyncInitializable, IDisposable
    {
        public const string AssetName = "TrxTest";
        public const string AssetNameUsingMSTest = "TrxTestUsingMSTest";

        private readonly AcceptanceFixture _acceptanceFixture;

        private TestAsset? _testAsset;
        private TestAsset? _testAssetWithSkippedTest;
        private TestAsset? _testAssetWithDataRow;

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public string TargetAssetPathWithSkippedTest => _testAssetWithSkippedTest!.TargetAssetPath;

        public string TargetAssetPathWithDataRow => _testAssetWithDataRow!.TargetAssetPath;

        public BuildFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            await Task.WhenAll(
                GenerateTrxAsset(),
                GenerateMSTest(),
                GenerateMSTestWithIgnore());

            async Task GenerateTrxAsset()
            {
                _testAsset = await TestAsset.GenerateAssetAsync(
                    AssetName,
                    TestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()));
                await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
            }

            async Task GenerateMSTestWithIgnore()
            {
                _testAssetWithSkippedTest = await TestAsset.GenerateAssetAsync(
                    AssetNameUsingMSTest,
                    MSTestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()).PatchCodeWithRegularExpression("ignoreTest", "[Ignore]"));
                await DotnetCli.RunAsync($"build -nodeReuse:false {_testAssetWithSkippedTest.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
            }

            async Task GenerateMSTest()
            {
                _testAssetWithDataRow = await TestAsset.GenerateAssetAsync(
                    AssetNameUsingMSTest,
                    MSTestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToMSBuildTargetFrameworks()).PatchCodeWithRegularExpression("ignoreTest", string.Empty));
                await DotnetCli.RunAsync($"build -nodeReuse:false {_testAssetWithDataRow.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
            }
        }

        public void Dispose()
        {
            _testAsset?.Dispose();
            _testAssetWithSkippedTest?.Dispose();
            _testAssetWithDataRow?.Dispose();
        }
    }

    private async Task AssertTrxReportWasGenerated(TestHostResult testHostResult, string trxPathPattern, int numberOfTests)
    {
        testHostResult.AssertHasExitCode(ExitCodes.Success);

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
        using var reader = new StreamReader(path);
        return Regex.IsMatch(await reader.ReadToEndAsync(), pattern);
    }

    private const string TestCode = """
#file TrxTest.csproj
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
using TrxTest;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
builder.AddCrashDumpGenerator();
builder.AddTrxReportGenerator();
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
global using Microsoft.Testing.Framework;
global using Microsoft.Testing.Platform.Extensions;
""";

    private const string MSTestCode = """
#file TrxTestUsingMSTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>tfms</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
        <PackageReference Include="Microsoft.Testing.Platform.Extensions" Version="[1.0.0-*,)" />
        <PackageReference Include="MSTest" Version="[1.0.0-*,)" />
    </ItemGroup>
</Project>

#file Program.cs
using TrxTestUsingMSTest;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddMSTest(() => new[] { typeof(Program).Assembly });
builder.AddTrxReportGenerator();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace TrxTestUsingMSTest;

[TestClass]
public class UnitTest1
{
    ignoreTest
    [TestMethod]
    [DataRow("data\0")]
    [DataRow("data")]
    public void TestMethod1(string s)
    {
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Platform.Extensions;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
""";
}
