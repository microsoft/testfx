// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the GitHub Actions report extension: drives the extension through a real
/// Microsoft.Testing.Platform session (with <c>GITHUB_ACTIONS=true</c> and a temporary
/// <c>GITHUB_STEP_SUMMARY</c> file) and asserts that non-test-result exit codes surface both as a
/// job-summary callout and a run-level <c>::error</c> annotation, while an ordinary passing run stays
/// quiet and an ordinary test failure only produces per-test annotations.
/// </summary>
[TestClass]
public sealed class GitHubActionsReportTests : AcceptanceTestBase<GitHubActionsReportTests.TestAssetFixture>
{
    private const string AssetName = "GitHubActionsReportBehavior";

    public TestContext TestContext { get; set; } = null!;

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenRunPasses_NoExitCodeCalloutAndSummaryUsesSuccessIcon(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "pass");

        result.AssertExitCodeIs(ExitCode.Success);

        // A clean run must not emit an exit-code annotation or a warning callout.
        result.AssertOutputDoesNotContain("::error title=Test run failed");
        Assert.Contains("✅ Test Run Summary", summary);
        Assert.DoesNotContain("[!WARNING]", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenTestFails_EmitsPerTestAnnotationButNoExitCodeCallout(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "fail");

        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // The per-test failure annotation is emitted, but AtLeastOneTestFailed is a test-result outcome,
        // so no additional run-level exit-code annotation/callout should appear.
        result.AssertOutputContains("::error");
        result.AssertOutputDoesNotContain("::error title=Test run failed");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.DoesNotContain("[!WARNING]", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenZeroTestsRan_EmitsExitCodeAnnotationAndCallout(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "zero");

        result.AssertExitCodeIs(ExitCode.ZeroTests);

        // The run failed without any failing test: expect both a run-level annotation and a summary callout.
        result.AssertOutputContains("::error title=Test run failed");
        result.AssertOutputContains("ZeroTests");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("[!WARNING]", summary);
        Assert.Contains("ZeroTests", summary);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenMinimumExpectedTestsViolated_EmitsExitCodeAnnotationAndCalloutEvenThoughTestPassed(string tfm)
    {
        (TestHostResult result, string summary) = await RunAsync(tfm, testMode: "pass", extraArgs: "--minimum-expected-tests 5");

        result.AssertExitCodeIs(ExitCode.MinimumExpectedTestsPolicyViolation);

        // A single test passed, yet the run failed the minimum-expected-tests policy — the failure must surface.
        result.AssertOutputContains("::error title=Test run failed");
        result.AssertOutputContains("MinimumExpectedTestsPolicyViolation");
        Assert.Contains("❌ Test Run Summary", summary);
        Assert.Contains("MinimumExpectedTestsPolicyViolation", summary);
    }

    private async Task<(TestHostResult Result, string Summary)> RunAsync(string tfm, string testMode, string extraArgs = "")
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string stepSummaryPath = Path.Combine(TestContext.TestRunDirectory!, $"gh-step-summary-{Guid.NewGuid():N}.md");

        TestHostResult result = await testHost.ExecuteAsync(
            $"--report-gh {extraArgs}".Trim(),
            environmentVariables: new Dictionary<string, string?>
            {
                ["GITHUB_ACTIONS"] = "true",
                ["GITHUB_STEP_SUMMARY"] = stepSummaryPath,
                ["GH_TEST_MODE"] = testMode,
            },
            cancellationToken: TestContext.CancellationToken);

        string summary = File.Exists(stepSummaryPath) ? File.ReadAllText(stepSummaryPath) : string.Empty;
        return (result, summary);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file GitHubActionsReportBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.GitHubActionsReport" Version="$MicrosoftTestingExtensionsGitHubActionsReportVersion$" />
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
            _ => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates.
        builder.AddGitHubActionsProvider();
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
        // The behavior under test is selected by the GH_TEST_MODE environment variable set by the acceptance test:
        //   "zero" -> publish no tests (exit code ZeroTests)
        //   "fail" -> publish a single failing test (exit code AtLeastOneTestFailed)
        //   "pass" -> publish a single passing test (exit code Success, unless --minimum-expected-tests forces a violation)
        string mode = Environment.GetEnvironmentVariable("GH_TEST_MODE") ?? "pass";

        if (mode == "fail")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "FailingTest",
                    Properties = new PropertyBag(new FailedTestNodeStateProperty("Expected 1 but got 2")),
                }));
        }
        else if (mode == "pass")
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode()
                {
                    Uid = "test-1",
                    DisplayName = "PassingTest",
                    Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
                }));
        }

        // mode == "zero": publish nothing.
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsGitHubActionsReportVersion$", MicrosoftTestingExtensionsGitHubActionsReportVersion));
    }
}
