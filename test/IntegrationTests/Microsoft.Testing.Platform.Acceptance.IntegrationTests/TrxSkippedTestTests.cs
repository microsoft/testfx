// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TrxSkippedTestTests : AcceptanceTestBase<TrxSkippedTestTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenSkipTest_ItAppearsAsExpectedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxFile = trxFiles[0];

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
    public async Task Trx_UsingDataDriven_CreatesUnitTestTagForEachOneInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxFile = trxFiles[0];

        string trxContent = File.ReadAllText(trxFile);

        // check if the test have been added to TestDefinitions twice as the number of the data driven tests.
        string trxContentsPattern = """
\s*<UnitTest.*
\s*<UnitTest
""";
        Assert.IsTrue(Regex.IsMatch(trxContent, trxContentsPattern));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetNameUsingMSTest = "TrxTestUsingMSTest";
        private const string WithSkippedTest = nameof(WithSkippedTest);

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

        public string TargetAssetPath => GetAssetPath(WithSkippedTest);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (WithSkippedTest, AssetNameUsingMSTest,
                MSTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$IgnoreTestAttributeOrNothing$", "[Ignore]"));
    }

    public TestContext TestContext { get; set; }
}
