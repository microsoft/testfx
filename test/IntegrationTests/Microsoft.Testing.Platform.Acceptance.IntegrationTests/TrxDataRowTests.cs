// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TrxDataRowTests : AcceptanceTestBase<TrxDataRowTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTheTestNameHasInvalidXmlChar_TheTrxCreatedSuccessfully(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string trxPathPattern = Path.Combine(testResultsPath, ".*.trx").Replace(@"\", @"\\");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetNameUsingMSTest, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx", cancellationToken: TestContext.CancellationToken);

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 2);
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
        public const string AssetNameUsingMSTest = "TrxTestUsingMSTest";
        private const string WithDataRow = nameof(WithDataRow);

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

        public string TargetAssetPath => GetAssetPath(WithDataRow);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (WithDataRow, AssetNameUsingMSTest,
                MSTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$IgnoreTestAttributeOrNothing$", string.Empty));
        }
    }

    public TestContext TestContext { get; set; }
}
