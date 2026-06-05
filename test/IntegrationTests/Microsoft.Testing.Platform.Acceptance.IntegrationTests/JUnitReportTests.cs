// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;

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

        var document = XDocument.Load(match.Value);
        AssertWellFormedJUnitReport(document, expectedTestCount: 1, expectedFailures: 0, expectedErrors: 0, expectedSkipped: 0);
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

        var document = XDocument.Load(customFilePath);
        AssertWellFormedJUnitReport(document, expectedTestCount: 1, expectedFailures: 0, expectedErrors: 0, expectedSkipped: 0);
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

        var document = XDocument.Load(customFilePath);

        // 1 passing + 1 failing + 1 skipped + 1 errored = 4 tests across the assembly.
        AssertWellFormedJUnitReport(document, expectedTestCount: 4, expectedFailures: 1, expectedErrors: 1, expectedSkipped: 1);

        XElement[] testcases = document.Descendants("testcase").ToArray();
        Dictionary<string, XElement> testcasesByName = testcases.ToDictionary(tc => tc.Attribute("name")!.Value);

        // Outcome mappings per RFC 016 — every outcome lands in the right child element.
        XElement failingChild = testcasesByName["FailingChild"];
        Assert.HasCount(1, failingChild.Elements("failure"), "FailingChild should have exactly one <failure> child.");
        Assert.IsEmpty(failingChild.Elements("error"));
        Assert.IsEmpty(failingChild.Elements("skipped"));

        XElement skippedChild = testcasesByName["SkippedChild"];
        Assert.HasCount(1, skippedChild.Elements("skipped"), "SkippedChild should have exactly one <skipped> child.");
        Assert.IsEmpty(skippedChild.Elements("error"));
        Assert.IsEmpty(skippedChild.Elements("failure"));

        XElement erroredChild = testcasesByName["ErroredChild"];
        Assert.HasCount(1, erroredChild.Elements("error"), "ErroredChild should have exactly one <error> child.");
        Assert.IsEmpty(erroredChild.Elements("failure"));
        Assert.IsEmpty(erroredChild.Elements("skipped"));

        XElement passingTest = testcasesByName["PassingTest"];
        Assert.IsEmpty(passingTest.Elements("failure"));
        Assert.IsEmpty(passingTest.Elements("error"));
        Assert.IsEmpty(passingTest.Elements("skipped"));

        // Per-RFC child ordering inside <testcase>: properties?, skipped?, error*, failure*, system-out*, system-err*.
        foreach (XElement testcase in testcases)
        {
            AssertTestcaseChildOrdering(testcase);
        }

        // Per-testcase metadata is emitted with the RFC-documented property names.
        XElement[] uidProperties = document.Descendants("property")
            .Where(p => p.Attribute("name")?.Value == "uid")
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(4, uidProperties.Length, $"Expected at least one <property name=\"uid\"/> per testcase, but found {uidProperties.Length}.");

        XElement[] legacyUidProperties = document.Descendants("property")
            .Where(p => p.Attribute("name")?.Value == "test-uid")
            .ToArray();
        Assert.IsEmpty(legacyUidProperties);

        // testpath always includes the leaf display name; parented children are prefixed by their container.
        Assert.AreEqual(
            "Container1/FailingChild",
            failingChild.Element("properties")!.Elements("property").Single(p => p.Attribute("name")?.Value == "testpath").Attribute("value")!.Value);
        Assert.AreEqual(
            "Container1/SkippedChild",
            skippedChild.Element("properties")!.Elements("property").Single(p => p.Attribute("name")?.Value == "testpath").Attribute("value")!.Value);
        Assert.AreEqual(
            "Container1/ErroredChild",
            erroredChild.Element("properties")!.Elements("property").Single(p => p.Attribute("name")?.Value == "testpath").Attribute("value")!.Value);
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

    private static void AssertWellFormedJUnitReport(XDocument document, int expectedTestCount, int expectedFailures, int expectedErrors, int expectedSkipped)
    {
        // Document is well-formed XML and uses the no-namespace JUnit shape per RFC 016.
        Assert.IsNotNull(document.Root);
        Assert.AreEqual("testsuites", document.Root!.Name.LocalName);
        Assert.AreEqual(string.Empty, document.Root.Name.Namespace.NamespaceName, "JUnit report must not use an XML namespace per RFC 016.");

        // <testsuites> attributes reflect the aggregated counts.
        Assert.AreEqual(expectedTestCount.ToString(CultureInfo.InvariantCulture), document.Root.Attribute("tests")!.Value);
        Assert.AreEqual(expectedFailures.ToString(CultureInfo.InvariantCulture), document.Root.Attribute("failures")!.Value);
        Assert.AreEqual(expectedErrors.ToString(CultureInfo.InvariantCulture), document.Root.Attribute("errors")!.Value);
        Assert.AreEqual(expectedSkipped.ToString(CultureInfo.InvariantCulture), document.Root.Attribute("skipped")!.Value);

        // At least one <testsuite> with the expected attributes; <testcase> count matches the document total.
        XElement[] testsuites = document.Root.Elements("testsuite").ToArray();
        Assert.IsNotEmpty(testsuites, "Expected at least one <testsuite> child.");
        foreach (XElement testsuite in testsuites)
        {
            Assert.IsNotNull(testsuite.Attribute("name"));
            Assert.IsNotNull(testsuite.Attribute("tests"));
        }

        XElement[] testcases = document.Descendants("testcase").ToArray();
        Assert.HasCount(expectedTestCount, testcases);
        foreach (XElement testcase in testcases)
        {
            Assert.IsNotNull(testcase.Attribute("name"));
            Assert.IsNotNull(testcase.Attribute("classname"));
        }
    }

    private static void AssertTestcaseChildOrdering(XElement testcase)
    {
        // RFC 016: children of <testcase> appear in the order
        //   properties?, skipped?, error*, failure*, system-out*, system-err*
        string[] expectedOrder = ["properties", "skipped", "error", "failure", "system-out", "system-err"];

        int currentIndex = 0;
        foreach (XElement child in testcase.Elements())
        {
            int childIndex = Array.IndexOf(expectedOrder, child.Name.LocalName);
            Assert.IsGreaterThanOrEqualTo(
                0,
                childIndex,
                $"Unexpected child <{child.Name.LocalName}> under <testcase name=\"{testcase.Attribute("name")?.Value}\">.");
            Assert.IsGreaterThanOrEqualTo(
                currentIndex,
                childIndex,
                $"Child <{child.Name.LocalName}> appears out of order under <testcase name=\"{testcase.Attribute("name")?.Value}\">; expected ordering is {string.Join(", ", expectedOrder)}.");
            currentIndex = childIndex;
        }

        // properties and skipped, when present, must be unique.
        Assert.IsLessThanOrEqualTo(1, testcase.Elements("properties").Count(), "<testcase> may have at most one <properties> child.");
        Assert.IsLessThanOrEqualTo(1, testcase.Elements("skipped").Count(), "<testcase> may have at most one <skipped> child.");
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
