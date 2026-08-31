// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TrxTests : AcceptanceTestBase<TrxTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsNotSpecified_TrxReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

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
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx", cancellationToken: TestContext.CancellationToken);

        // number of test is the third param because we have two different test code with different number of tests.
        await AssertTrxReportWasGeneratedAsync(testHostResult, trxPathPattern, 1);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenOnlyReportTrxIsSpecified_UsesControllerBackedRecoveryByDefault(string tfm)
    {
        // Plain --report-trx (no --crashdump, no --timeout, no other extension requiring isolation) must
        // be controller-backed by default on this platform. The "Out of process" heading is only emitted
        // when the surviving controller (rather than the test host itself) reports the TRX file artifact,
        // so its presence here is direct proof that a bare --report-trx run went through the controller.
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {fileName}.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("Out of process file artifacts produced:");

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenOutOfProcessReportHasNoSelectedTests_LifetimeHandshakeCompletes(string tfm)
    {
        string fileName = $"{Guid.NewGuid():N}.trx";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--filter-uid 2 --ignore-exit-code 8 --report-trx --report-trx-filename {fileName} --results-directory \"{testResultsPath}\"",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputDoesNotContain("The operation has timed out.");
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 0);

        string[] trxFiles = Directory.GetFiles(testResultsPath, fileName, SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");

        var trxDocument = XDocument.Parse(File.ReadAllText(trxFiles[0]));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement counters = trxDocument.Descendants(ns + "Counters").Single();
        Assert.AreEqual("0", counters.Attribute("total")?.Value, trxDocument.ToString());
        Assert.AreEqual("0", counters.Attribute("executed")?.Value, trxDocument.ToString());
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxAndResultsDirectoryAreSpecifiedWithArtifact_ArtifactIsCopiedUnderRelativeResultsDirectory(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
            new() { ["WITH_ARTIFACT"] = $"{Guid.NewGuid():N}.txt" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string[] trxFiles = Directory.GetFiles(testResultsPath, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");

        var trxDocument = XDocument.Parse(File.ReadAllText(trxFiles[0]));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement unitTestResult = trxDocument.Descendants(ns + "UnitTestResult").Single();
        string relativeResultsDirectory = unitTestResult.Attribute("relativeResultsDirectory")!.Value;
        string resultFilePath = unitTestResult.Descendants(ns + "ResultFile").Single().Attribute("path")!.Value;
        string runDeploymentRoot = trxDocument.Descendants(ns + "Deployment").Single().Attribute("runDeploymentRoot")!.Value;
        string normalizedResultFilePath = resultFilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        string copiedArtifactPath = Path.Combine(testResultsPath, runDeploymentRoot, "In", relativeResultsDirectory, normalizedResultFilePath);
        Assert.IsTrue(File.Exists(copiedArtifactPath), $"Expected copied artifact at '{copiedArtifactPath}' but it was not found.");

        string legacyArtifactPath = Path.Combine(testResultsPath, runDeploymentRoot, "In", normalizedResultFilePath);
        Assert.IsFalse(File.Exists(legacyArtifactPath), $"Artifact was copied to legacy path '{legacyArtifactPath}'.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenArtifactDestinationExceedsWindowsMaxPath_ArtifactIsStillCopied(string tfm)
    {
        // The per-test attachment layout appends '<runDeploymentRoot>/In/<executionId>/<machineName>/'
        // to the results directory, which adds roughly 100 characters. Pad the results directory so the
        // attachment lands beyond the Windows MAX_PATH limit while the TRX file itself (results
        // directory + a 36 character name) stays comfortably below it. On .NET Framework the copy used
        // to fail there with a DirectoryNotFoundException that was swallowed into a warning, so the
        // ResultFile silently disappeared from the report while the run still reported success.
        // See https://github.com/microsoft/testfx/issues/10312.
        const int PaddedResultsDirectoryLength = 180;
        string fileName = Guid.NewGuid().ToString("N");

        // The padded segment carries a unique prefix, and the artifact file name is unique per run:
        // acceptance tests use method-level parallelism, so a fixed results directory would race the
        // TFM cases against each other, and a fixed artifact name would race this test against the
        // other WITH_ARTIFACT test for the same TFM (they share the test host's output folder).
        string uniqueSegment = Guid.NewGuid().ToString("N");
        int paddingLength = Math.Max(1, PaddedResultsDirectoryLength - AssetFixture.TargetAssetPath.Length - 1 - uniqueSegment.Length);
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, uniqueSegment + new string('p', paddingLength));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
            new() { ["WITH_ARTIFACT"] = $"{Guid.NewGuid():N}.txt" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputDoesNotContain("The attachment will be skipped.");

        string[] trxFiles = Directory.GetFiles(testResultsPath, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");

        var trxDocument = XDocument.Parse(File.ReadAllText(trxFiles[0]));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement unitTestResult = trxDocument.Descendants(ns + "UnitTestResult").Single();
        string relativeResultsDirectory = unitTestResult.Attribute("relativeResultsDirectory")!.Value;
        string resultFilePath = unitTestResult.Descendants(ns + "ResultFile").Single().Attribute("path")!.Value;
        string runDeploymentRoot = trxDocument.Descendants(ns + "Deployment").Single().Attribute("runDeploymentRoot")!.Value;
        string normalizedResultFilePath = resultFilePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

        string copiedArtifactPath = Path.Combine(testResultsPath, runDeploymentRoot, "In", relativeResultsDirectory, normalizedResultFilePath);
        Assert.IsGreaterThan(260, copiedArtifactPath.Length, $"Expected the artifact destination to exceed MAX_PATH but it was {copiedArtifactPath.Length} characters: '{copiedArtifactPath}'.");
        Assert.IsTrue(File.Exists(copiedArtifactPath), $"Expected copied artifact at '{copiedArtifactPath}' but it was not found.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenPerTestArtifactCannotBeCopied_WarningIsSurfacedAndResultFileIsSkipped(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
            new() { ["WITH_MISSING_ARTIFACT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        // Losing an attachment is not a run failure, but it must not be silent either: the warning has
        // to reach the console and not only the RunInfos section of the generated TRX.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains("missing-test-artifact.txt");
        testHostResult.AssertOutputContains("The attachment will be skipped.");

        string[] trxFiles = Directory.GetFiles(testResultsPath, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");

        var trxDocument = XDocument.Parse(File.ReadAllText(trxFiles[0]));
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        Assert.IsEmpty(
            trxDocument.Descendants(ns + "ResultFile"),
            $"Expected no ResultFile element because the attachment could not be copied. TRX was:{Environment.NewLine}{trxDocument}");

        XElement warningRunInfo = trxDocument.Descendants(ns + "RunInfo").Single(runInfo => runInfo.Attribute("outcome")?.Value == "Warning");
        Assert.Contains("missing-test-artifact.txt", warningRunInfo.Value);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTestHostCrash_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        // Plain --report-trx (no --crashdump, no other extension) is controller-backed by default on
        // this platform, so it alone is enough to recover a failed-run TRX when the test host crashes.
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {fileName}.trx",
            new() { ["CRASHPROCESS"] = "1" }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxFile = trxFiles[0];
        string trxContent = File.ReadAllText(trxFile);
        Assert.IsTrue(Regex.IsMatch(trxContent, @"Test host process pid: .* crashed\."), trxContent);
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTimeoutTerminatesTestHost_RecoversCompletedResults(string tfm)
    {
        string fileName = $"{Guid.NewGuid():N}.trx";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --report-trx --report-trx-filename {fileName} --results-directory \"{testResultsPath}\" --timeout 2s",
            new() { ["WAIT_FOR_TIMEOUT"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);
        testHostResult.AssertOutputContains("Test session was aborted; recovered 1 test result(s)");
        testHostResult.AssertOutputContains("Canceling the test session");
        testHostResult.AssertOutputDoesNotContain("Test run summary: Passed!");

        string[] trxFiles = Directory.GetFiles(testResultsPath, fileName, SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxContent = File.ReadAllText(trxFiles[0]);
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);
        Assert.Contains("was terminated because the test session was aborted", trxContent, trxContent);
        var trxDocument = XDocument.Parse(trxContent);
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        XElement recoveredResult = trxDocument.Descendants(ns + "UnitTestResult")
            .Single(result => result.Attribute("testName")?.Value == "Test");
        Assert.AreEqual("Passed", recoveredResult.Attribute("outcome")?.Value, trxContent);
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTestHostCrash_RunningUnderDotnetTest_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {tfm} --report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
            workingDirectory: AssetFixture.TargetAssetPath,
            environmentVariables: new() { ["CRASHPROCESS"] = "1" },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.TestHostProcessExitedNonGracefully);

        string[] trxFiles = Directory.GetFiles(testResultsPath, $"{fileName}.trx", SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        string trxFile = trxFiles[0];
        string trxContent = File.ReadAllText(trxFile);
        Assert.Contains("""<ResultSummary outcome="Failed">""", trxContent, trxContent);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedWithFullPath_TrxReportIsGeneratedAtThatPath(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"), "Release", tfm, "TestResults");
        string fileName = $"{Guid.NewGuid():N}.trx";
        string fullPath = Path.Combine(testResultsPath, fileName);

        Assert.IsFalse(Directory.Exists(testResultsPath));

        try
        {
            var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
            TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename \"{fullPath}\"", cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(ExitCode.Success);
            Assert.IsTrue(File.Exists(fullPath), $"Expected TRX report at '{fullPath}' but it was not found.");
        }
        finally
        {
            if (Directory.Exists(testResultsPath))
            {
                Directory.Delete(testResultsPath, recursive: true);
            }
        }
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedWithRelativePath_TrxReportIsGeneratedUnderResultsDirectory(string tfm)
    {
        string fileName = $"{Guid.NewGuid():N}.trx";
        string relativePath = Path.Combine("nested", "sub", fileName);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {relativePath}", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string[] trxFiles = Directory.GetFiles(testHost.DirectoryName, fileName, SearchOption.AllDirectories);
        Assert.HasCount(1, trxFiles, $"Expected exactly one trx file but found {trxFiles.Length}: {string.Join(", ", trxFiles)}");
        Assert.Contains(Path.Combine("nested", "sub", fileName), trxFiles[0]);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedWithRelativeParentTraversal_ErrorIsDisplayed(string tfm)
    {
        string relativePath = Path.Combine("nested", "..", $"{Guid.NewGuid():N}.trx");

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {relativePath}", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--report-trx-filename' relative paths must stay under the test results directory");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsNotSpecifiedAndReportTrxPathIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx-filename report.trx", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx-filename' requires '--report-trx' to be enabled");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedAndReportTrxPathIsSpecified_Overwritten(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        string reportFileName = $"report-{tfm}.trx";
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {reportFileName}", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        string warningMessage = $"Warning: Trx file '{Path.Combine(testHost.DirectoryName, "TestResults", reportFileName)}' already exists and will be overwritten.";
        testHostResult.AssertOutputDoesNotContain(warningMessage);

        testHostResult = await testHost.ExecuteAsync($"--report-trx --report-trx-filename {reportFileName}", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContains(warningMessage);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenReportTrxIsSpecifiedAndListTestsIsSpecified_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-trx --list-tests", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--report-trx' cannot be enabled when using '--list-tests'");
    }

    private async Task AssertTrxReportWasGeneratedAsync(TestHostResult testHostResult, string trxPathPattern, int numberOfTests)
    {
        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Plain --report-trx is controller-backed by default on this platform: the TRX artifact is
        // reported by the surviving controller process, not the test host, hence "Out of process".
        string outputPattern = $"""
  Out of process file artifacts produced:
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

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "TrxTest";

        private const string TestCode = """
#file TrxTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
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
using Microsoft.Testing.Platform.Requests;
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

        TestExecutionRequest request = (TestExecutionRequest)context.Request;
        if (request.Filter is TestNodeUidListFilter uidFilter &&
            !uidFilter.TestNodeUids.Any(nodeUid => nodeUid.Value == "0"))
        {
            context.Complete();
            return;
        }

        var testMethodIdentifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test", 0, Array.Empty<string>(), string.Empty);
        PropertyBag properties = new(PassedTestNodeStateProperty.CachedInstance, testMethodIdentifier);
        // WITH_ARTIFACT carries the file name rather than a flag: the working directory is the test
        // host's own output folder, which is shared by every test method using this asset for a given
        // target framework, so a fixed name would have concurrent hosts writing and copying the same
        // file under method-level parallelism.
        if (Environment.GetEnvironmentVariable("WITH_ARTIFACT") is { Length: > 0 } artifactFileName)
        {
            string artifactPath = Path.Combine(Directory.GetCurrentDirectory(), artifactFileName);
            File.WriteAllText(artifactPath, "artifact");
            properties.Add(new FileArtifactProperty(new FileInfo(artifactPath), "TestMethod", "description"));
        }

        if (Environment.GetEnvironmentVariable("WITH_MISSING_ARTIFACT") == "1")
        {
            // Deliberately never created so that the TRX attachment copy fails with an IOException.
            string missingArtifactPath = Path.Combine(Directory.GetCurrentDirectory(), "missing-test-artifact.txt");
            properties.Add(new FileArtifactProperty(new FileInfo(missingArtifactPath), "TestMethod", "description"));
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test", Properties = properties }));
        if (Environment.GetEnvironmentVariable("WAIT_FOR_TIMEOUT") == "1")
        {
            Thread.Sleep(10000);
        }

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
