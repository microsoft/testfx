// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TrxReportTests : AcceptanceTestBase<TrxReportTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TrxReport_WhenTestFails_ContainsExceptionInfoInOutput(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();
        string trxContent = File.ReadAllText(trxFile);

        // Verify that the TRX contains the UnitTestResult with outcome="Failed"
        Assert.Contains(@"<UnitTestResult", trxContent, trxContent);
        Assert.Contains(@"outcome=""Failed""", trxContent, trxContent);

        // Verify that the TRX contains the Output element with error info
        Assert.Contains(@"<Output>", trxContent, trxContent);
        Assert.Contains(@"<ErrorInfo>", trxContent, trxContent);

        // Verify that exception message is present
        Assert.Contains(@"<Message>", trxContent, trxContent);
        Assert.Contains("Assert.AreEqual failed. Expected:&lt;1&gt;. Actual:&lt;2&gt;.", trxContent, trxContent);

        // Verify that stack trace is present
        Assert.Contains(@"<StackTrace>", trxContent, trxContent);
        Assert.Contains("at MSTestTrxReport.UnitTest1.FailingTest()", trxContent, trxContent);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "MSTestTrxReport";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file MSTestTrxReport.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestTrxReport;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void FailingTest()
    {
        Assert.AreEqual(1, 2);
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
