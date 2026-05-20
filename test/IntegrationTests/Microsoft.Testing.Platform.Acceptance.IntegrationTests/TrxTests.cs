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
    public async Task Trx_WhenReportTrxAndResultsDirectoryAreSpecifiedWithArtifact_ArtifactIsCopiedUnderRelativeResultsDirectory(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
            new() { ["WITH_ARTIFACT"] = "1" },
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

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Trx_WhenTestHostCrash_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--crashdump --report-trx --report-trx-filename {fileName}.trx",
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
    public async Task Trx_WhenTestHostCrash_RunningUnderDotnetTest_ErrorIsDisplayedInsideTheTrx(string tfm)
    {
        string fileName = Guid.NewGuid().ToString("N");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, Guid.NewGuid().ToString("N"));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {tfm} --crashdump --report-trx --report-trx-filename {fileName}.trx --results-directory \"{testResultsPath}\"",
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
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "aaa", "Release", tfm, "TestResults");
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

        var testMethodIdentifier = new TestMethodIdentifierProperty(string.Empty, string.Empty, "DummyClassName", "Test", 0, Array.Empty<string>(), string.Empty);
        PropertyBag properties = new(PassedTestNodeStateProperty.CachedInstance, testMethodIdentifier);
        if (Environment.GetEnvironmentVariable("WITH_ARTIFACT") == "1")
        {
            string artifactPath = Path.Combine(Directory.GetCurrentDirectory(), "test-artifact.txt");
            File.WriteAllText(artifactPath, "artifact");
            properties.Add(new FileArtifactProperty(new FileInfo(artifactPath), "TestMethod", "description"));
        }

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "0", DisplayName = "Test", Properties = properties }));
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
