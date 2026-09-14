// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AssertionFailureDiagnosticsTests : AcceptanceTestBase<AssertionFailureDiagnosticsTests.TestAssetFixture>
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    private static readonly Regex ArtifactShapeRegex = new(
        """
        \A\{
          "schemaVersion": 1,
          "captureIndex": 1,
          "capturedAtUtc": "\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00",
          "assertion": \{
            "message": "(?:\\.|[^"\\])*",
            "expected": "42",
            "actual": "41"
          \},
          "test": \{
            "fullyQualifiedName": "AssertionFailureDiagnosticsAsset\.AssertionFailureTests\.FailingAssertion",
            "displayName": "FailingAssertion",
            "attempt": 1,
            "elapsedMilliseconds": (?:0|[1-9]\d*)(?:\.\d+)?(?:[Ee][+-]?\d+)?
          \},
          "thread": \{
            "managedThreadId": [1-9]\d*(?:,
            "name": "(?:\\.|[^"\\])*")?
          \},
          "activeTests": \[
            \{
              "fullyQualifiedName": "AssertionFailureDiagnosticsAsset\.AssertionFailureTests\.FailingAssertion",
              "displayName": "FailingAssertion",
              "attempt": 1,
              "startedAtUtc": "\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00",
              "elapsedMilliseconds": (?:0|[1-9]\d*)(?:\.\d+)?(?:[Ee][+-]?\d+)?,
              "isFailingTest": true
            \}
          \],
          "activeTestsTruncated": false,
          "process": \{
            [\s\S]+?
          \},
          "stackFrames": \[
            [\s\S]+?
          \]
        \}\z
        """.ReplaceLineEndings("\n"),
        RegexOptions.CultureInvariant);

    [TestMethod]
    public async Task AssertionFailureDiagnostics_WhenEnabled_AttachesPersistedArtifactToFailedTest()
    {
        string trxFileName = $"{Guid.NewGuid():N}.trx";
        string testResultsPath = Path.Combine(AssetFixture.ProjectPath, Guid.NewGuid().ToString("N"));
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--filter FailingAssertion --report-trx --report-trx-filename {trxFileName} --results-directory \"{testResultsPath}\"",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);

        string trxFile = Directory.GetFiles(testResultsPath, trxFileName, SearchOption.AllDirectories).Single();
        var trxDocument = XDocument.Load(trxFile);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement unitTestResult = trxDocument.Descendants(ns + "UnitTestResult").Single();
        Assert.AreEqual("FailingAssertion", unitTestResult.Attribute("testName")?.Value, trxDocument.ToString());
        Assert.AreEqual("Failed", unitTestResult.Attribute("outcome")?.Value, trxDocument.ToString());

        string relativeResultsDirectory = unitTestResult.Attribute("relativeResultsDirectory")!.Value;
        string resultFilePath = unitTestResult.Descendants(ns + "ResultFile").Single().Attribute("path")!.Value;
        string artifactFileName = Path.GetFileName(resultFilePath);
        Assert.IsTrue(
            artifactFileName.StartsWith("mstest-assertion-failure-state-", StringComparison.Ordinal)
            && artifactFileName.EndsWith(".json", StringComparison.Ordinal),
            $"Expected an assertion failure diagnostics result file, but found '{resultFilePath}'.");

        string runDeploymentRoot = trxDocument.Descendants(ns + "Deployment").Single().Attribute("runDeploymentRoot")!.Value;
        string normalizedResultFilePath = resultFilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        string persistedArtifactPath = Path.Combine(testResultsPath, runDeploymentRoot, "In", relativeResultsDirectory, normalizedResultFilePath);
        Assert.IsTrue(File.Exists(persistedArtifactPath), $"Expected persisted diagnostics artifact at '{persistedArtifactPath}'.");

        string artifactJson = File.ReadAllText(persistedArtifactPath);
        using var artifact = JsonDocument.Parse(artifactJson);
        JsonElement root = artifact.RootElement;
        string formattedArtifact = JsonSerializer.Serialize(root, IndentedJsonOptions).ReplaceLineEndings("\n");
        Assert.MatchesRegex(
            ArtifactShapeRegex,
            formattedArtifact,
            $"Unexpected assertion failure artifact shape:{Environment.NewLine}{formattedArtifact}");

        const string FullyQualifiedTestName = "AssertionFailureDiagnosticsAsset.AssertionFailureTests.FailingAssertion";
        JsonElement[] activeTests = [.. root.GetProperty("activeTests").EnumerateArray()];
        Assert.IsTrue(
            activeTests.Any(activeTest =>
                activeTest.GetProperty("fullyQualifiedName").GetString() == FullyQualifiedTestName
                && activeTest.GetProperty("isFailingTest").GetBoolean()),
            "Expected the failing test to be present in the activeTests snapshot.");

        JsonElement[] stackFrames = [.. root.GetProperty("stackFrames").EnumerateArray()];
        Assert.IsTrue(
            stackFrames.Any(frame => frame.TryGetProperty("method", out JsonElement method)
                && method.GetString()?.EndsWith(".FailingAssertion", StringComparison.Ordinal) == true),
            "Expected the assertion diagnostics stack to include the failing test method.");

        JsonElement process = root.GetProperty("process");
        Assert.IsTrue(process.GetProperty("processId").GetInt32() > 0);
        Assert.IsTrue(process.GetProperty("processorCount").GetInt32() > 0);
        Assert.IsTrue(process.GetProperty("totalProcessorTimeMilliseconds").GetDouble() >= 0);
        Assert.IsTrue(process.GetProperty("workingSetBytes").GetInt64() > 0);
        Assert.IsTrue(process.GetProperty("managedHeapBytes").GetInt64() >= 0);
        Assert.IsTrue(process.GetProperty("processIoAvailable").ValueKind is JsonValueKind.True or JsonValueKind.False);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "AssertionFailureDiagnosticsAsset";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
            => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

        private const string SourceCode = """
#file AssertionFailureDiagnosticsAsset.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="AssertionFailureDiagnosticsAsset.testconfig.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file AssertionFailureDiagnosticsAsset.testconfig.json
{
  "mstest": {
    "execution": {
      "captureAssertionFailureDiagnostics": true
    }
  }
}

#file AssertionFailureTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssertionFailureDiagnosticsAsset;

[TestClass]
public class AssertionFailureTests
{
    [TestMethod]
    public void FailingAssertion()
        => Assert.AreEqual(42, 41, "Expected answer to match actual answer.");
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
