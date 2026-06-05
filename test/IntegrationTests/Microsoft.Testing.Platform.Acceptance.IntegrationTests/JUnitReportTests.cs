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
