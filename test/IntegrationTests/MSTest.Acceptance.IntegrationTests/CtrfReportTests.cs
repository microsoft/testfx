// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class CtrfReportTests : AcceptanceTestBase<CtrfReportTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CtrfReport_WhenTestsArePassingAndFailing_CtrfFileIsGeneratedWithBothOutcomes(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N") + ".ctrf.json";
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-ctrf --report-ctrf-filename {fileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        string ctrfFile = Directory.GetFiles(testHost.DirectoryName, fileName, SearchOption.AllDirectories).Single();
        string ctrfContent = File.ReadAllText(ctrfFile);

        // Top-level CTRF document shape
        Assert.Contains(@"""reportFormat"": ""CTRF""", ctrfContent, ctrfContent);
        Assert.Contains(@"""specVersion""", ctrfContent, ctrfContent);
        Assert.Contains(@"""generatedBy"": ""Microsoft.Testing.Extensions.CtrfReport@", ctrfContent, ctrfContent);

        // Summary counts include both outcomes
        Assert.Contains(@"""tests"": 2", ctrfContent, ctrfContent);
        Assert.Contains(@"""passed"": 1", ctrfContent, ctrfContent);
        Assert.Contains(@"""failed"": 1", ctrfContent, ctrfContent);

        // Both per-test entries are present
        Assert.Contains(@"""name"": ""PassingTest""", ctrfContent, ctrfContent);
        Assert.Contains(@"""name"": ""FailingTest""", ctrfContent, ctrfContent);
        Assert.Contains(@"""status"": ""passed""", ctrfContent, ctrfContent);
        Assert.Contains(@"""status"": ""failed""", ctrfContent, ctrfContent);

        // The failed test exposes the assertion message in CTRF's `message` field.
        Assert.Contains(@"""message"":", ctrfContent, ctrfContent);
        Assert.Contains("Assert.AreEqual", ctrfContent, ctrfContent);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "MSTestCtrfReport";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsCtrfReportVersion$", MicrosoftTestingExtensionsCtrfReportVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file MSTestCtrfReport.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.CtrfReport" Version="$MicrosoftTestingExtensionsCtrfReportVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestCtrfReport;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void PassingTest()
    {
    }

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
