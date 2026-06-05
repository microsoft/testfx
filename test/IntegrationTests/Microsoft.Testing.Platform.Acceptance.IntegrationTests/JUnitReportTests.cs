// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class JUnitReportTests : AcceptanceTestBase<JUnitReportTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitIsNotSpecified_JUnitReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // The JUnit report is published as an in-process artifact; check the correct block.
        string outputPattern = """
  In process file artifacts produced:
    - .+?\.xml
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitIsSpecified_JUnitReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string xmlPathPattern = Regex.Escape(testResultsPath + Path.DirectorySeparatorChar) + @".+?\.xml";

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-junit", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {xmlPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Match match = Regex.Match(testHostResult.StandardOutput, xmlPathPattern);
        Assert.IsTrue(match.Success, $"JUnit report path not found in output:\n{testHostResult.StandardOutput}");

        string xmlContent = File.ReadAllText(match.Value);
        Assert.Contains("<?xml version=\"1.0\"", xmlContent, "Generated file does not appear to be a valid XML report.");
        Assert.Contains("<testsuites", xmlContent, "Generated JUnit report does not contain the <testsuites> root element.");
        Assert.Contains("<testsuite", xmlContent, "Generated JUnit report does not contain any <testsuite> element.");
        Assert.Contains("<testcase", xmlContent, "Generated JUnit report does not contain any <testcase> element.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitFilenameIsSpecified_JUnitReportIsGeneratedWithThatName(string tfm)
    {
        const string customFileName = "my-custom-report.xml";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-junit --report-junit-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom JUnit report file '{customFileName}' was not found in '{testResultsPath}'.");

        string xmlContent = File.ReadAllText(customFilePath);
        Assert.Contains("<?xml version=\"1.0\"", xmlContent, "Generated file does not appear to be a valid XML report.");
        Assert.Contains("<testsuites", xmlContent, "Generated JUnit report does not contain the <testsuites> root element.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitFilenameContainsPath_JUnitReportIsGeneratedInThatPath(string tfm)
    {
        string customFileName = Path.Combine("subdir", "report.xml");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-junit --report-junit-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom JUnit report file '{customFileName}' was not found in '{testResultsPath}'.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitFilenameContainsPlaceholders_PlaceholdersAreResolved(string tfm)
    {
        // Use {tfm}, {pname}, and {asm} — all three resolve to predictable, non-time-sensitive values.
        const string fileNameTemplate = "MyReport_{tfm}_{pname}_{asm}.xml";
        string expectedFileName = $"MyReport_{tfm}_{TestAssetFixture.AssetName}_{TestAssetFixture.AssetName}.xml";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string expectedFilePath = Path.Combine(testResultsPath, expectedFileName);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-junit --report-junit-filename {fileNameTemplate}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        Assert.IsTrue(
            File.Exists(expectedFilePath),
            $"Expected JUnit report with resolved placeholders '{expectedFileName}' was not found in '{testResultsPath}'.\nOutput:\n{testHostResult.StandardOutput}");

        // Sanity: ensure no literal placeholder tokens leaked into the produced file name.
        Assert.IsFalse(
            File.Exists(Path.Combine(testResultsPath, fileNameTemplate)),
            $"A file with the literal placeholder template name '{fileNameTemplate}' was produced; placeholders were not substituted.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenTestsFailOrSkip_JUnitReportContainsExpectedOutcomes(string tfm)
    {
        const string customFileName = "outcomes-report.xml";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-junit --report-junit-filename {customFileName}",
            environmentVariables: new() { ["JUNIT_REPORT_EMIT_MIXED"] = "1" },
            cancellationToken: TestContext.CancellationToken);

        // Failing tests cause exit code 2 (AtLeastOneTestFailed); we still want to verify the XML.
        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected JUnit report '{customFileName}' was not found in '{testResultsPath}'.\nOutput:\n{testHostResult.StandardOutput}");

        string xmlContent = File.ReadAllText(customFilePath);

        // Outcome mappings per RFC 016.
        Assert.Contains("<failure", xmlContent, "Expected a <failure> child for the failing test.");
        Assert.Contains("<skipped", xmlContent, "Expected a <skipped> child for the skipped test.");
        Assert.Contains("<error", xmlContent, "Expected an <error> child for the errored test.");

        // Per-testcase metadata is emitted with the RFC-documented property names.
        Assert.Contains("name=\"uid\"", xmlContent, "Per-testcase <property name=\"uid\"/> should be emitted (not 'test-uid').");
        Assert.DoesNotContain("name=\"test-uid\"", xmlContent, "Legacy 'test-uid' property name must no longer be emitted.");

        // testpath now always includes the leaf display name; the parented children
        // must therefore include "Container1/<leaf>".
        Assert.Contains("Container1/FailingChild", xmlContent, "testpath should include the parent chain plus the leaf display name.");
        Assert.Contains("Container1/SkippedChild", xmlContent, "testpath should include the parent chain plus the leaf display name.");

        // Schema-conformance smoke (per RFC 016): parse the produced XML and assert
        // that every <testcase>'s children follow the strict normative ordering
        // (properties?, skipped?, error*, failure*, system-out*, system-err*).
        // Substring checks alone would silently accept a regression that, for instance,
        // emitted <system-out> before <failure>.
        var doc = System.Xml.Linq.XDocument.Parse(xmlContent);
        Assert.AreEqual("testsuites", doc.Root!.Name.LocalName, "Root element must be <testsuites>.");

        int testcasesChecked = 0;
        string[] orderedNames = ["properties", "skipped", "error", "failure", "system-out", "system-err"];
        foreach (System.Xml.Linq.XElement testcase in doc.Descendants("testcase"))
        {
            testcasesChecked++;
            int lastSeen = -1;
            foreach (System.Xml.Linq.XElement child in testcase.Elements())
            {
                int idx = Array.IndexOf(orderedNames, child.Name.LocalName);
                Assert.IsGreaterThanOrEqualTo(
                    0,
                    idx,
                    $"Unexpected <testcase> child element <{child.Name.LocalName}> — RFC 016 only allows: {string.Join(", ", orderedNames)}.");
                Assert.IsGreaterThanOrEqualTo(
                    lastSeen,
                    idx,
                    $"<testcase name=\"{testcase.Attribute("name")?.Value}\"> children are out of order: <{child.Name.LocalName}> appeared after a later-ordered element. Expected order: {string.Join(" -> ", orderedNames)}.");
                lastSeen = idx;
            }
        }

        Assert.IsGreaterThan(0, testcasesChecked, "Expected at least one <testcase> element to be present for ordering validation.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task JUnit_WhenReportJUnitFilenameIsSpecifiedWithoutReportJUnit_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--report-junit-filename report.xml",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--report-junit-filename' requires '--report-junit' to be enabled");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "JUnitReportTest";

        private const string TestCode = """
#file JUnitReportTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddJUnitReportProvider();
#pragma warning restore TPEXP
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "2.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = "test-1",
                DisplayName = "PassingTest",
                Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
            }));

        if (Environment.GetEnvironmentVariable("JUNIT_REPORT_EMIT_MIXED") == "1")
        {
            // Parent container — the engine should pick this up via the parent-chain
            // dictionary so the child testpath becomes "Container1/FailingChild" etc.
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "container-1",
                    DisplayName = "Container1",
                    Properties = new PropertyBag(DiscoveredTestNodeStateProperty.CachedInstance),
                }));

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-fail",
                    DisplayName = "FailingChild",
                    Properties = new PropertyBag(new FailedTestNodeStateProperty(new InvalidOperationException("boom"))),
                },
                parentTestNodeUid: "container-1"));

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-skip",
                    DisplayName = "SkippedChild",
                    Properties = new PropertyBag(new SkippedTestNodeStateProperty("not today")),
                },
                parentTestNodeUid: "container-1"));

            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-error",
                    DisplayName = "ErroredChild",
                    Properties = new PropertyBag(new ErrorTestNodeStateProperty(new InvalidProgramException("kaboom"))),
                },
                parentTestNodeUid: "container-1"));
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
