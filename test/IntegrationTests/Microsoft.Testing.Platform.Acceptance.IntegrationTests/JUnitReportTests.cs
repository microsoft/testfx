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

        AssertSinglePassingTestJUnitReportShape(match.Value);
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

        AssertSinglePassingTestJUnitReportShape(customFilePath);
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

        AssertMixedOutcomesJUnitReportShape(customFilePath);
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

    private static void AssertSinglePassingTestJUnitReportShape(string filePath)
    {
        // Single passing test: the dummy framework emits exactly one TestNode with
        // Uid=test-1, DisplayName=PassingTest, status=passed, no parent, no class
        // metadata, no timing. The engine should therefore produce one <testsuite>
        // (named after the module, since there's no parent or class name to fall
        // back to) containing one <testcase> with a single <properties> child
        // holding "uid" and "testpath" entries.
        const string expected = """
<?xml version="1.0" encoding="utf-8"?>
<testsuites name="JUnitReportTest" tests="1" failures="0" errors="0" skipped="0" time="<TIME>" timestamp="<TIMESTAMP>">
  <testsuite name="JUnitReportTest" tests="1" failures="0" errors="0" skipped="0" time="<TIME>" timestamp="<TIMESTAMP>" hostname="<HOST>" id="0">
    <properties>
      <property name="test-framework" value="DummyTestFramework" />
      <property name="test-framework-version" value="2.0.0" />
      <property name="test-framework-uid" value="DummyTestFramework" />
      <property name="exit-code" value="0" />
    </properties>
    <testcase name="PassingTest" classname="JUnitReportTest" time="<TIME>">
      <properties>
        <property name="uid" value="test-1" />
        <property name="testpath" value="PassingTest" />
      </properties>
    </testcase>
  </testsuite>
</testsuites>
""";

        AssertJUnitReportSnapshot(filePath, expected);
    }

    private static void AssertMixedOutcomesJUnitReportShape(string filePath)
    {
        // Mixed outcomes: 1 passing root test + 1 container with 3 parented
        // children (fail, skip, error). The container is emitted as a Discovered
        // node so it is captured in the parent chain but NOT counted as a test
        // result, giving a 4-test report split across 2 suites (the module-named
        // fallback for the root passing test, then Container1 for its children).
        // Exit code is 2 (AtLeastOneTestFailed). The failure/error exceptions are
        // constructed but never thrown, so they have no stack trace and the
        // <failure>/<error> elements are self-closing with just message/type.
        const string expected = """
<?xml version="1.0" encoding="utf-8"?>
<testsuites name="JUnitReportTest" tests="4" failures="1" errors="1" skipped="1" time="<TIME>" timestamp="<TIMESTAMP>">
  <testsuite name="JUnitReportTest" tests="1" failures="0" errors="0" skipped="0" time="<TIME>" timestamp="<TIMESTAMP>" hostname="<HOST>" id="0">
    <properties>
      <property name="test-framework" value="DummyTestFramework" />
      <property name="test-framework-version" value="2.0.0" />
      <property name="test-framework-uid" value="DummyTestFramework" />
      <property name="exit-code" value="2" />
    </properties>
    <testcase name="PassingTest" classname="JUnitReportTest" time="<TIME>">
      <properties>
        <property name="uid" value="test-1" />
        <property name="testpath" value="PassingTest" />
      </properties>
    </testcase>
  </testsuite>
  <testsuite name="Container1" tests="3" failures="1" errors="1" skipped="1" time="<TIME>" timestamp="<TIMESTAMP>" hostname="<HOST>" id="1">
    <properties>
      <property name="test-framework" value="DummyTestFramework" />
      <property name="test-framework-version" value="2.0.0" />
      <property name="test-framework-uid" value="DummyTestFramework" />
      <property name="exit-code" value="2" />
    </properties>
    <testcase name="FailingChild" classname="Container1" time="<TIME>">
      <properties>
        <property name="uid" value="test-fail" />
        <property name="testpath" value="Container1/FailingChild" />
      </properties>
      <failure message="boom" type="System.InvalidOperationException" />
    </testcase>
    <testcase name="SkippedChild" classname="Container1" time="<TIME>">
      <properties>
        <property name="uid" value="test-skip" />
        <property name="testpath" value="Container1/SkippedChild" />
      </properties>
      <skipped message="not today" />
    </testcase>
    <testcase name="ErroredChild" classname="Container1" time="<TIME>">
      <properties>
        <property name="uid" value="test-error" />
        <property name="testpath" value="Container1/ErroredChild" />
      </properties>
      <error message="kaboom" type="System.InvalidProgramException" />
    </testcase>
  </testsuite>
</testsuites>
""";

        AssertJUnitReportSnapshot(filePath, expected);
    }

    private static void AssertJUnitReportSnapshot(string filePath, string expected)
    {
        string actual = File.ReadAllText(filePath);
        string normalized = NormalizeJUnitReport(actual);

        Assert.AreEqual(
            NormalizeLineEndings(expected),
            NormalizeLineEndings(normalized),
            $"Generated JUnit XML does not match the expected snapshot.\n\nNormalized actual:\n{normalized}\n\nRaw actual:\n{actual}");
    }

    private static string NormalizeJUnitReport(string actual)
    {
        // Replace attribute values that vary at runtime with stable tokens so the
        // comparison is hermetic. Use attribute-name-scoped regexes so static
        // numeric attributes like tests/failures/errors/skipped/id stay literal
        // and contribute to the assertion.
        string normalized = actual;
        normalized = Regex.Replace(normalized, @"\btime=""[^""]*""", @"time=""<TIME>""");
        normalized = Regex.Replace(normalized, @"\btimestamp=""[^""]*""", @"timestamp=""<TIMESTAMP>""");
        normalized = Regex.Replace(normalized, @"\bhostname=""[^""]*""", @"hostname=""<HOST>""");
        return normalized;
    }

    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Trim('\n');

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
        <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingExtensionsJUnitReportVersion$" />
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsJUnitReportVersion$", MicrosoftTestingExtensionsJUnitReportVersion));
    }

    public TestContext TestContext { get; set; }
}
