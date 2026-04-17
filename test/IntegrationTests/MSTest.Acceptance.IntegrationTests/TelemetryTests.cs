// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TelemetryTests : AcceptanceTestBase<TelemetryTests.TestAssetFixture>
{
    private const string MTPAssetName = "TelemetryMTPProject";
    private const string TestResultsFolderName = "TestResults";

    #region MTP mode - Run

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task MTP_RunTests_SendsTelemetryWithSettingsAndAttributes(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.MTPProjectPath, "bin", "Release", tfm, TestResultsFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestHost.LocateFrom(AssetFixture.MTPProjectPath, MTPAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            disableTelemetry: false,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string diagContentsPattern =
"""
.+ Send telemetry event: dotnet/testingplatform/mstest/sessionexit
.+mstest\.setting\.parallelization_enabled
""";
        string diagFilePath = await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);

        // Verify attribute usage and config source are also present
        string content = await File.ReadAllTextAsync(diagFilePath, TestContext.CancellationToken);
        Assert.IsTrue(Regex.IsMatch(content, "mstest\\.attribute_usage"), $"Expected attribute_usage in telemetry.\n{content}");
        Assert.IsTrue(Regex.IsMatch(content, "mstest\\.config_source"), $"Expected config_source in telemetry.\n{content}");
        Assert.IsTrue(Regex.IsMatch(content, "mstest\\.assertion_usage"), $"Expected assertion_usage in telemetry.\n{content}");
    }

    #endregion

    #region MTP mode - Discovery only

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task MTP_DiscoverTests_SendsTelemetryEvent(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.MTPProjectPath, "bin", "Release", tfm, TestResultsFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestHost.LocateFrom(AssetFixture.MTPProjectPath, MTPAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--list-tests --diagnostic",
            disableTelemetry: false,
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string diagContentsPattern =
"""
.+ Send telemetry event: dotnet/testingplatform/mstest/sessionexit[\s\S]+?mstest\.attribute_usage
""";
        await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);
    }

    #endregion

    #region MTP mode - Telemetry disabled

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task MTP_WhenTelemetryDisabled_DoesNotSendMSTestEvent(string tfm)
    {
        string diagPath = Path.Combine(AssetFixture.MTPProjectPath, "bin", "Release", tfm, TestResultsFolderName);
        string diagPathPattern = Path.Combine(diagPath, @"log_.*.diag").Replace(@"\", @"\\");

        var testHost = TestHost.LocateFrom(AssetFixture.MTPProjectPath, MTPAssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--diagnostic",
            new Dictionary<string, string?>
            {
                { "TESTINGPLATFORM_TELEMETRY_OPTOUT", "1" },
            },
            disableTelemetry: false,
            TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string diagContentsPattern =
"""
.+ Microsoft.Testing.Platform.Telemetry.TelemetryManager DEBUG Telemetry is 'DISABLED'
""";
        string diagFilePath = await AssertDiagnosticReportAsync(testHostResult, diagPathPattern, diagContentsPattern);

        string content = await File.ReadAllTextAsync(diagFilePath, TestContext.CancellationToken);
        Assert.IsFalse(
            Regex.IsMatch(content, "Send telemetry event: dotnet/testingplatform/mstest/sessionexit"),
            "MSTest telemetry event should not be sent when telemetry is disabled.");
    }

    #endregion

    #region VSTest mode - Run

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task VSTest_RunTests_Succeeds(string tfm)
    {
        DotnetMuxerResult testResult = await DotnetCli.RunAsync(
            $"test -c Release {AssetFixture.VSTestProjectPath} --framework {tfm}",
            workingDirectory: AssetFixture.VSTestProjectPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, testResult.ExitCode, $"dotnet test failed:\n{testResult.StandardOutput}\n{testResult.StandardError}");
        testResult.AssertOutputContains("Passed!");
    }

    #endregion

    #region VSTest mode - Discovery only

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task VSTest_DiscoverTests_Succeeds(string tfm)
    {
        DotnetMuxerResult testResult = await DotnetCli.RunAsync(
            $"test -c Release {AssetFixture.VSTestProjectPath} --framework {tfm} --list-tests",
            workingDirectory: AssetFixture.VSTestProjectPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, testResult.ExitCode, $"dotnet test --list-tests failed:\n{testResult.StandardOutput}\n{testResult.StandardError}");
        testResult.AssertOutputContains("PassingTest");
        testResult.AssertOutputContains("DataDrivenTest");
        testResult.AssertOutputContains("TestWithTimeout");
    }

    #endregion

    #region Helpers

    private static async Task<string> AssertDiagnosticReportAsync(TestHostResult testHostResult, string diagPathPattern, string diagContentsPattern, string level = "Trace", string flushType = "async")
    {
        string outputPattern = $"""
Diagnostic file \(level '{level}' with {flushType} flush\): {diagPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);
        Match match = Regex.Match(testHostResult.StandardOutput, diagPathPattern);
        Assert.IsTrue(match.Success, $"{testHostResult}\n{diagPathPattern}");

        (bool success, string content) = await CheckDiagnosticContentsMatchAsync(match.Value, diagContentsPattern);
        Assert.IsTrue(success, $"{content}\n{diagContentsPattern}");

        return match.Value;
    }

    private static async Task<(bool IsMatch, string Content)> CheckDiagnosticContentsMatchAsync(string path, string pattern)
    {
        using var reader = new StreamReader(path);
        string content = await reader.ReadToEndAsync();

        return (Regex.IsMatch(content, pattern), content);
    }

    #endregion

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string AssetId = nameof(TelemetryTests);

        public string MTPProjectPath => GetAssetPath(AssetId);

        public string VSTestProjectPath => Path.Combine(GetAssetPath(AssetId), "vstest");

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (AssetId, MTPAssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftNETTestSdkVersion$", MicrosoftNETTestSdkVersion));

        private const string SourceCode = """
#file TelemetryMTPProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>Preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void PassingTest()
    {
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void DataDrivenTest(int value)
    {
        Assert.IsTrue(value > 0);
    }

    [TestMethod]
    [Timeout(30000)]
    public void TestWithTimeout()
    {
    }
}

#file vstest/TelemetryVSTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>Preview</LangVersion>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file vstest/global.json
{
  "test": {
    "runner": "VSTest"
  }
}

#file vstest/UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void PassingTest()
    {
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void DataDrivenTest(int value)
    {
        Assert.IsTrue(value > 0);
    }

    [TestMethod]
    [Timeout(30000)]
    public void TestWithTimeout()
    {
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
