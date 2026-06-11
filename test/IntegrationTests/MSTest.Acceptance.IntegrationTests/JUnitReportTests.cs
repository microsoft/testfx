// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class JUnitReportRetryAttributeTests : AcceptanceTestBase<JUnitReportRetryAttributeTests.TestAssetFixture>
{
    [TestMethod]
    public async Task JUnitReport_WithMSTestRetryAttribute_EmitsFinalOutcomePerTest()
    {
        string fileName = $"{Guid.NewGuid():N}.xml";
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--settings my.runsettings --report-junit --report-junit-filename {fileName}",
            cancellationToken: TestContext.CancellationToken);

        // The always-failing test still fails, so the overall exit code is non-success.
        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // MSTest's [Retry] attribute only surfaces the FINAL outcome to MTP (see UnitTestRunner — only
        // the last attempt is returned). Each test should therefore appear once in the JUnit report
        // with its eventual outcome — no per-attempt disambiguation.
        string junitFile = Directory.GetFiles(testHost.DirectoryName, fileName, SearchOption.AllDirectories).Single();
        var document = XDocument.Load(junitFile);
        Assert.IsNotNull(document.Root);
        Assert.AreEqual("testsuites", document.Root!.Name.LocalName);

        XElement[] testcases = document.Descendants("testcase").ToArray();
        Assert.HasCount(4, testcases);

        // Aggregate counts on the root <testsuites> element.
        Assert.AreEqual("4", document.Root.Attribute("tests")!.Value);
        Assert.AreEqual("1", document.Root.Attribute("failures")!.Value);
        Assert.AreEqual("0", document.Root.Attribute("errors")!.Value);
        Assert.AreEqual("0", document.Root.Attribute("skipped")!.Value);

        AssertSingleOutcome(testcases, "PassesFirstTry", expectFailure: false);
        AssertSingleOutcome(testcases, "PassesAfterOneRetry", expectFailure: false);
        AssertSingleOutcome(testcases, "PassesAfterTwoRetries", expectFailure: false);
        AssertSingleOutcome(testcases, "AlwaysFails", expectFailure: true);

        // No per-attempt disambiguation properties should appear since each test has a single row.
        Assert.DoesNotContain(
            tc => tc.Element("properties")?.Elements("property").Any(p => p.Attribute("name")?.Value is "attempt-index" or "attempt-of" or "original-name") == true,
            testcases,
            "No <testcase> should carry retry-disambiguation properties when MSTest's [Retry] is used (only the final outcome is surfaced).");
    }

    private static void AssertSingleOutcome(IReadOnlyCollection<XElement> testcases, string name, bool expectFailure)
    {
        XElement tc = testcases.Single(t => t.Attribute("name")?.Value == name);
        bool hasFailure = tc.Elements("failure").Any();
        bool hasError = tc.Elements("error").Any();
        bool hasSkipped = tc.Elements("skipped").Any();

        if (expectFailure)
        {
            Assert.IsTrue(hasFailure, $"Expected '{name}' to have a <failure> child.");
            Assert.IsFalse(hasError, $"Expected '{name}' to have no <error> child.");
            Assert.IsFalse(hasSkipped, $"Expected '{name}' to have no <skipped> child.");
        }
        else
        {
            Assert.IsFalse(hasFailure, $"Expected '{name}' to have no <failure> child (passed eventually).");
            Assert.IsFalse(hasError, $"Expected '{name}' to have no <error> child (passed eventually).");
            Assert.IsFalse(hasSkipped, $"Expected '{name}' to have no <skipped> child (passed eventually).");
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "MSTestJUnitReportRetry";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
            SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsJUnitReportVersion$", MicrosoftTestingExtensionsJUnitReportVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file MSTestJUnitReportRetry.csproj
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
    <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingExtensionsJUnitReportVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestJUnitReportRetry;

[TestClass]
public class UnitTest1
{
    private static int _firstTry;
    private static int _afterOneRetry;
    private static int _afterTwoRetries;
    private static int _alwaysFails;

    [TestMethod]
    [Retry(3)]
    public void PassesFirstTry()
    {
        _firstTry++;
    }

    [TestMethod]
    [Retry(3)]
    public void PassesAfterOneRetry()
    {
        _afterOneRetry++;
        if (_afterOneRetry <= 1) Assert.Fail("Failing PassesAfterOneRetry");
    }

    [TestMethod]
    [Retry(3)]
    public void PassesAfterTwoRetries()
    {
        _afterTwoRetries++;
        if (_afterTwoRetries <= 2) Assert.Fail("Failing PassesAfterTwoRetries");
    }

    [TestMethod]
    [Retry(3)]
    public void AlwaysFails()
    {
        _alwaysFails++;
        Assert.Fail("Always failing");
    }
}

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>
""";
    }

    public TestContext TestContext { get; set; } = null!;
}

[TestClass]
[DoNotParallelize]
public sealed class JUnitReportMTPRetryExtensionTests : AcceptanceTestBase<JUnitReportMTPRetryExtensionTests.TestAssetFixture>
{
    [TestMethod]
    public async Task JUnitReport_WithMTPRetryFailedTestsExtension_EmitsOneReportPerAttempt()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        string resultDirectory = Path.Combine(testHost.DirectoryName, Guid.NewGuid().ToString("N"));
        string sentinelPath = Path.Combine(testHost.DirectoryName, ".junit-mtp-retry.sentinel");
        // Sentinel may persist across local re-runs of this acceptance test (fixture bin folder is reused).
        if (File.Exists(sentinelPath))
        {
            File.Delete(sentinelPath);
        }

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--retry-failed-tests 3 --results-directory \"{resultDirectory}\" --report-junit",
            cancellationToken: TestContext.CancellationToken);

        // Test fails the first time then passes on retry — orchestrator should succeed.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Tests suite completed successfully in 2 attempts");

        // Each attempt is a separate test-host child process and produces its own JUnit XML file.
        string[] junitFiles = Directory.GetFiles(resultDirectory, "*.xml", SearchOption.AllDirectories);
        Assert.HasCount(2, junitFiles, $"Expected 2 JUnit XML files (one per attempt) but found {junitFiles.Length}.");

        foreach (string file in junitFiles)
        {
            var document = XDocument.Load(file);
            Assert.IsNotNull(document.Root);
            Assert.AreEqual("testsuites", document.Root!.Name.LocalName);
            Assert.IsNotEmpty(document.Descendants("testcase"), $"File '{file}' contains no <testcase> elements.");
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "MSTestJUnitReportMTPRetry";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
            SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsJUnitReportVersion$", MicrosoftTestingExtensionsJUnitReportVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        // Uses the MTP --retry-failed-tests orchestrator (different from MSTest's [Retry] attribute).
        // The orchestrator re-runs the test-host process when at least one test fails, so each
        // attempt produces its OWN JUnit XML file under the --results-directory subdirectories.
        private const string SourceCode = """
#file MSTestJUnitReportMTPRetry.csproj
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
    <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingExtensionsJUnitReportVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestJUnitReportMTPRetry;

[TestClass]
public class UnitTest1
{
    // Sentinel file in the project base directory so it persists across the orchestrator's child processes.
    private static readonly string SentinelPath = Path.Combine(AppContext.BaseDirectory, ".junit-mtp-retry.sentinel");

    [TestMethod]
    public void Flaky_FailsOnceThenPasses()
    {
        if (!File.Exists(SentinelPath))
        {
            File.WriteAllText(SentinelPath, "ran");
            Assert.Fail("Intentional first-attempt failure for MTP retry-failed-tests");
        }
    }

    [TestMethod]
    public void AlwaysPasses()
    {
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
