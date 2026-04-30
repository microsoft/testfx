// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

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

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

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

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "MSTestTrxReport";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

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

[TestClass]
public sealed class TrxReportDataDrivenOutputTests : AcceptanceTestBase<TrxReportDataDrivenOutputTests.TestAssetFixture>
{
    /// <summary>
    /// Regression test for https://github.com/microsoft/testfx/issues/7908.
    /// Verifies that data-driven test output does not accumulate across data rows in the TRX file.
    /// Each data row's StdOut in the TRX should contain only that row's output, not output from previous rows.
    /// </summary>
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TrxReport_DataDrivenTestOutput_DoesNotAccumulateAcrossRows(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string trxFile = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories).Single();
        string trxContent = File.ReadAllText(trxFile);

        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var trxDoc = XDocument.Parse(trxContent);
        var results = trxDoc.Descendants(ns + "UnitTestResult").ToList();

        // We have 3 data rows, each writing a unique marker like "UNIQUE_ROW_0_MARKER", "UNIQUE_ROW_1_MARKER", "UNIQUE_ROW_2_MARKER"
        Assert.IsGreaterThanOrEqualTo(3, results.Count, $"Expected at least 3 test results but found {results.Count}. TRX content:\n{trxContent}");

        int resultsWithOutput = 0;
        foreach (XElement result in results)
        {
            string? stdOut = result.Descendants(ns + "StdOut").FirstOrDefault()?.Value;
            if (stdOut is null)
            {
                continue;
            }

            resultsWithOutput++;

            // Count how many unique row markers appear in this single result's StdOut.
            // Each result should contain exactly ONE marker (its own row's output).
            int markerCount = 0;
            for (int i = 0; i < 3; i++)
            {
                if (stdOut.Contains($"UNIQUE_ROW_{i}_MARKER"))
                {
                    markerCount++;
                }
            }

            Assert.AreEqual(
                1,
                markerCount,
                $"Test result '{result.Attribute("testName")?.Value}' contains output from {markerCount} data rows. " +
                $"Each row should only contain its own output. StdOut:\n{stdOut}");
        }

        Assert.IsGreaterThanOrEqualTo(3, resultsWithOutput, $"Expected at least 3 test results to have StdOut output but only {resultsWithOutput} did. TRX content:\n{trxContent}");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "MSTestTrxDataDriven";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file MSTestTrxDataDriven.csproj
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestTrxDataDriven;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void DataDrivenTestWithOutput(int row)
    {
        Console.WriteLine($"UNIQUE_ROW_{row}_MARKER");
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
