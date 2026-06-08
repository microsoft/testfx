// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class CtrfReportTests : AcceptanceTestBase<CtrfReportTests.TestAssetFixture>
{
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Ctrf_WhenReportCtrfIsNotSpecified_CtrfReportIsNotGenerated(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // The CTRF report is published as an in-process artifact; check the correct block.
        string outputPattern = """
  In process file artifacts produced:
    - .+?\.ctrf\.json
""";
        testHostResult.AssertOutputDoesNotMatchRegex(outputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Ctrf_WhenReportCtrfIsSpecified_CtrfReportIsGeneratedInDefaultLocation(string tfm)
    {
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string ctrfPathPattern = Regex.Escape(testResultsPath + Path.DirectorySeparatorChar) + @".+?\.ctrf\.json";

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--report-ctrf", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {ctrfPathPattern}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Match match = Regex.Match(testHostResult.StandardOutput, ctrfPathPattern);
        Assert.IsTrue(match.Success, $"CTRF report path not found in output:\n{testHostResult.StandardOutput}");

        AssertCtrfReportShape(match.Value);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Ctrf_WhenReportCtrfFilenameIsSpecified_CtrfReportIsGeneratedWithThatName(string tfm)
    {
        const string customFileName = "my-custom-report.ctrf.json";
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-ctrf --report-ctrf-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom CTRF report file '{customFileName}' was not found in '{testResultsPath}'.");

        AssertCtrfReportShape(customFilePath);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Ctrf_WhenReportCtrfFilenameContainsPath_CtrfReportIsGeneratedInThatPath(string tfm)
    {
        string customFileName = Path.Combine("subdir", "report.ctrf.json");
        string testResultsPath = Path.Combine(AssetFixture.TargetAssetPath, "bin", "Release", tfm, "TestResults");
        string customFilePath = Path.Combine(testResultsPath, customFileName);
        string expectedFilePath = Regex.Escape(customFilePath);

        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-ctrf --report-ctrf-filename {customFileName}",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        string outputPattern = $"""
  In process file artifacts produced:
    - {expectedFilePath}
""";
        testHostResult.AssertOutputMatchesRegex(outputPattern);

        Assert.IsTrue(
            File.Exists(customFilePath),
            $"Expected custom CTRF report file '{customFileName}' was not found in '{testResultsPath}'.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Ctrf_WhenReportCtrfFilenameIsSpecifiedWithoutReportCtrf_ErrorIsDisplayed(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--report-ctrf-filename report.ctrf.json",
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("'--report-ctrf-filename' requires '--report-ctrf' to be enabled");
    }

    private static void AssertCtrfReportShape(string filePath)
    {
        // Snapshot the full CTRF JSON against an exact expected document. Runtime-variable
        // fields (GUID report id, ISO timestamp, epoch-ms times, machine name, user name,
        // OS info, the extension's own version, and the absolute path of the test
        // application) are masked with deterministic tokens so the comparison is
        // hermetic across machines and runs. Anything else — including key order,
        // indentation, escaping, conditional-emission shape, and the actual values
        // baked from the dummy test framework — must match byte-for-byte.
        string actual = File.ReadAllText(filePath);
        string normalized = NormalizeCtrfReport(actual);

        const string expected = """
{
  "reportFormat": "CTRF",
  "specVersion": "0.0.0",
  "reportId": "<GUID>",
  "timestamp": "<TIMESTAMP>",
  "generatedBy": "Microsoft.Testing.Extensions.CtrfReport@<VERSION>",
  "results": {
    "tool": {
      "name": "DummyTestFramework",
      "version": "2.0.0",
      "extra": {
        "uid": "DummyTestFramework"
      }
    },
    "summary": {
      "tests": 1,
      "passed": 1,
      "failed": 0,
      "skipped": 0,
      "pending": 0,
      "other": 0,
      "flaky": 0,
      "start": <EPOCH_MS>,
      "stop": <EPOCH_MS>,
      "duration": <DURATION_MS>
    },
    "environment": {
      "osPlatform": "<OS_PLATFORM>",
      "osVersion": "<OS_VERSION>",
      "extra": {
        "user": "<USER>",
        "machine": "<MACHINE>",
        "exitCode": 0,
        "testApplication": "<TEST_APPLICATION_PATH>"
      }
    },
    "tests": [
      {
        "name": "PassingTest",
        "status": "passed",
        "duration": <DURATION_MS>,
        "extra": {
          "uid": "test-1"
        }
      }
    ]
  }
}
""";

        Assert.AreEqual(
            NormalizeLineEndings(expected),
            NormalizeLineEndings(normalized),
            $"Generated CTRF JSON does not match the expected snapshot.\n\nNormalized actual:\n{normalized}\n\nRaw actual:\n{actual}");
    }

    private static string NormalizeCtrfReport(string actual)
    {
        // Field-scoped regexes so per-test attribute order and JSON shape are still
        // anchored, but runtime-variable values are folded into stable tokens.
        string normalized = actual;
        normalized = Regex.Replace(normalized, @"""reportId"": ""[^""]+""", @"""reportId"": ""<GUID>""");
        normalized = Regex.Replace(normalized, @"""timestamp"": ""[^""]+""", @"""timestamp"": ""<TIMESTAMP>""");
        normalized = Regex.Replace(normalized, @"""generatedBy"": ""Microsoft\.Testing\.Extensions\.CtrfReport@[^""]+""", @"""generatedBy"": ""Microsoft.Testing.Extensions.CtrfReport@<VERSION>""");
        normalized = Regex.Replace(normalized, @"""start"": \d+", @"""start"": <EPOCH_MS>");
        normalized = Regex.Replace(normalized, @"""stop"": \d+", @"""stop"": <EPOCH_MS>");
        normalized = Regex.Replace(normalized, @"""duration"": \d+", @"""duration"": <DURATION_MS>");
        normalized = Regex.Replace(normalized, @"""osPlatform"": ""[^""]*""", @"""osPlatform"": ""<OS_PLATFORM>""");
        normalized = Regex.Replace(normalized, @"""osVersion"": ""[^""]*""", @"""osVersion"": ""<OS_VERSION>""");
        normalized = Regex.Replace(normalized, @"""user"": ""[^""]*""", @"""user"": ""<USER>""");
        normalized = Regex.Replace(normalized, @"""machine"": ""[^""]*""", @"""machine"": ""<MACHINE>""");
        normalized = Regex.Replace(normalized, @"""testApplication"": ""[^""]*""", @"""testApplication"": ""<TEST_APPLICATION_PATH>""");
        return normalized;
    }

    private static string NormalizeLineEndings(string s) => s.Replace("\r\n", "\n").Trim('\n');

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string AssetName = "CtrfReportTest";

        private const string TestCode = """
#file CtrfReportTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.CtrfReport" Version="$MicrosoftTestingExtensionsCtrfReportVersion$" />
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
        builder.AddCtrfReportProvider();
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsCtrfReportVersion$", MicrosoftTestingExtensionsCtrfReportVersion));
    }

    public TestContext TestContext { get; set; }
}
