// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TrxTests : AcceptanceTestBase<TrxTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsNotSpecified_TrxReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string outputPattern = """
Out of process file artifacts produced:
- .+?\.trx
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecified_TrxReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 1);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTestHostCrash_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Investigate failures on macos
            return;
        }

        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --report-trx --report-trx-filename {fileName}.trx",
            new() { { "CRASHPROCESS", "1" } });

        testHostResult.AssertExitCodeIs(ExitCodes.TestHostProcessExitedNonGracefully);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();
        string trxContent = File.ReadAllText(trxFile);
        Assert.IsTrue(Regex.IsMatch(trxContent, @"Test host process pid: .* crashed\."), trxContent);
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenSkipTest_ItAppearsAsExpectedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPathWithSkippedTest, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();

        string trxContent = File.ReadAllText(trxFile);
        Assert.Contains(@"<UnitTestResult ", trxContent, trxContent);
        Assert.Contains(@"outcome=""NotExecuted""", trxContent, trxContent);
        Assert.Contains(@"<UnitTest name=""TestMethod1", trxContent, trxContent);
        Assert.Contains(@"<TestEntry ", trxContent, trxContent);
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);
        Assert.Contains("""<Counters total="2" executed="0" passed="0" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="2" disconnected="0" warning="0" completed="0" inProgress="0" pending="0" />""", trxContent, trxContent);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTheTestNameHasInvalidXmlChar_TheTrxCreatedSuccessfully(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPathWithDataRow, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPathWithDataRow, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx");

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 2);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_UsingDataDriven_CreatesUnitTestTagForEachOneInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPathWithSkippedTest, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();

        string trxContent = File.ReadAllText(trxFile);

        // check if the test have been added to TestDefinitions twice as the number of the data driven tests.
        string trxContentsPattern = """
\s*<UnitTest.*
\s*<UnitTest
""";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedWithFullPath_TrxReportShouldFail(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "aaa", "Release", tfm, "TestResults");

        Assert.IsFalse(Directory.Exists(testResultsPath));

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine(testResultsPath, "report.trx")}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedWithRelativePath_TrxReportShouldFail(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {Path.Combine("aaa", "report.trx")}");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--report-trx-filename' has invalid arguments: file name argument must not contain path (e.g. --report-trx-filename myreport.trx)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsNotSpecifiedAndReportTrxPathIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx-filename report.trx");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx-filename' requires '--report-trx' to be enabled");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedAndReportTrxPathIsSpecified_Overwritten(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        string reportFileName = $"report-{tfm}.trx";
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {reportFileName}");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        string warningMessage = $"Warning: Trx file '{Path.Combine(testHost.DirectoryName, "TestResults", reportFileName)}' already exists and will be overwritten.";
        testHostResult.AssertOutputDoesNotContain(warningMessage);

        testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {reportFileName}");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains(warningMessage);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedAndListTestsIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx --list-tests");

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

    private static async Task<bool> CheckTrxContentsMatchAsync(string path, string pattern)
    {
        using StreamReader reader = new(path);
        return Regex.IsMatch(await reader.ReadToEndAsync(), pattern);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
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
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(new TrxReportCapability()),
            (_,__) => new DummyTestFramework());
        builder.AddCrashDumpProvider();
        builder.AddTrxReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TrxReportCapability : ITrxReportCapability
{
    bool ITrxReportCapability.IsSupported { get; } = true;
    void ITrxReportCapability.Enable()
    {
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        if (Environment.GetEnvironmentVariable("CRASHPROCESS") == "1")
        {
            Environment.FailFast("CRASHPROCESS");
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }
}
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
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>

        <!--
            This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
            we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
            ensure we are testing with locally built version, we force adding the platform dependency.
        -->
        <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
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
