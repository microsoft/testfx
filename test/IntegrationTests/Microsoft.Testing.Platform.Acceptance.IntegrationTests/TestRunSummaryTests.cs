// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestRunSummaryTests : AcceptanceTestBase<TestRunSummaryTests.TestAssetFixture>
{
    private const string AssetName = "TestRunSummary";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(null, null, 8, 31)]
    [DataRow("8", null, 0, 39)]
    [DataRow(null, "8", 0, 39)]
    [DataRow("2; 8;9", null, 0, 39)]
    [DataRow("18", null, 8, 31)]
    [DataRow("8", "2", 8, 31)]
    [DataRow("2", "8", 0, 39)]
    [DataRow("8", "", 0, 39)]
    public async Task ZeroTests_HonorsIgnorePolicyAndProcessExitCode(string? commandLineIgnore, string? environmentIgnore, int expectedExitCode, int expectedColor)
    {
        TestHostResult result = await RunAsync("zero", commandLineIgnore: commandLineIgnore, environmentIgnore: environmentIgnore);

        AssertSummary(result, expectedExitCode, expectedColor, "Zero tests ran", passed: 0, failed: 0, skipped: 0);
        Assert.DoesNotContain("Test run summary: Passed!", result.StandardOutput);
    }

    [TestMethod]
    [DataRow(null, 8, 31)]
    [DataRow("8", 0, 39)]
    public async Task StrictAllSkipped_HonorsZeroTestsOptOut(string? commandLineIgnore, int expectedExitCode, int expectedColor)
    {
        TestHostResult result = await RunAsync("skipped", "--zero-tests-policy strict", commandLineIgnore);

        AssertSummary(result, expectedExitCode, expectedColor, "Zero tests ran", passed: 0, failed: 0, skipped: 1);
    }

    [TestMethod]
    [DataRow("skipped", "allow-skipped", 1, 0, 9)]
    [DataRow("skipped", "strict", 1, 0, 9)]
    [DataRow("mixed", "allow-skipped", 2, 1, 9)]
    [DataRow("mixed", "strict", 2, 1, 9)]
    [DataRow("mixed", "allow-skipped", 1, 1, 0)]
    [DataRow("mixed", "strict", 1, 1, 0)]
    public async Task ExplicitMinimum_CountsOnlyExecutedTests(string scenario, string policy, int minimum, int passed, int expectedExitCode)
    {
        TestHostResult result = await RunAsync(scenario, $"--zero-tests-policy {policy} --minimum-expected-tests {minimum}", commandLineIgnore: "8");

        string verdict = expectedExitCode == 0
            ? "Passed!"
            : $"Minimum expected tests policy violation, tests ran {passed}, minimum expected {minimum}";
        AssertSummary(result, expectedExitCode, expectedExitCode == 0 ? 32 : 31, verdict, passed, failed: 0, skipped: 1);
    }

    [TestMethod]
    [DataRow("create", false, 0)]
    [DataRow("create", true, 0)]
    [DataRow("close", false, 0)]
    [DataRow("close", true, 0)]
    [DataRow("create-passed", false, 1)]
    [DataRow("create-passed", true, 1)]
    [DataRow("close-passed", false, 1)]
    [DataRow("close-passed", true, 1)]
    public async Task FrameworkSessionFailure_RemainsFailedWithoutFailedTestNodes(string scenario, bool ignoreViaEnvironment, int passed)
    {
        TestHostResult result = await RunAsync(
            scenario,
            commandLineIgnore: ignoreViaEnvironment ? null : "8",
            environmentIgnore: ignoreViaEnvironment ? "8" : null);

        AssertSummary(result, (int)ExitCode.TestAdapterTestSessionFailure, 31, "Failed!", passed, failed: 0, skipped: 0);
        Assert.Contains(scenario.StartsWith("create", StringComparison.Ordinal) ? "session creation failed" : "session closure failed", result.StandardOutput);
        Assert.DoesNotContain("Test run summary: Passed!", result.StandardOutput);
    }

    [TestMethod]
    [DataRow("passed", "8", 0, 32, "Passed!", 1, 0, 0)]
    [DataRow("skipped", "8", 0, 32, "Passed!", 0, 0, 1)]
    [DataRow("failed", "8", 2, 31, "Failed!", 0, 1, 0)]
    [DataRow("failed", "2", 0, 31, "Failed!", 0, 1, 0)]
    public async Task IgnorePolicy_PreservesActualResultsAndCounters(string scenario, string commandLineIgnore, int expectedExitCode, int expectedColor, string verdict, int passed, int failed, int skipped)
    {
        TestHostResult result = await RunAsync(scenario, commandLineIgnore: commandLineIgnore);

        AssertSummary(result, expectedExitCode, expectedColor, verdict, passed, failed, skipped);
    }

    private async Task<TestHostResult> RunAsync(string scenario, string arguments = "", string? commandLineIgnore = null, string? environmentIgnore = null)
    {
        var host = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        string ignoreOption = commandLineIgnore is null ? string.Empty : $"--ignore-exit-code \"{commandLineIgnore}\"";

        TestHostResult result = await host.ExecuteAsync(
            $"--ansi on {arguments} {ignoreOption}",
            environmentVariables: new()
            {
                ["SUMMARY_SCENARIO"] = scenario,
                ["TESTINGPLATFORM_UI_LANGUAGE"] = "en-US",
                [EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE] = environmentIgnore,
                [EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER] = null,
            },
            cancellationToken: TestContext.CancellationToken);

        TestContext.WriteLine($"Microsoft.Testing.Platform package: {MicrosoftTestingPlatformVersion}");
        TestContext.WriteLine(result.ToString());
        return result;
    }

    private static void AssertSummary(TestHostResult result, int exitCode, int color, string verdict, int passed, int failed, int skipped)
    {
        result.AssertExitCodeIs(exitCode);
        Assert.Contains($"\u001B[{color}mTest run summary: {verdict}", result.StandardOutput);
        Assert.AreEqual(1, result.StandardOutput.Split(["Test run summary:"], StringSplitOptions.None).Length - 1);

        string plainOutput = Regex.Replace(result.StandardOutput, @"\x1B\[[0-9;]*m", string.Empty).Replace("\r\n", "\n");
        string counts = $"""
              total: {passed + failed + skipped}
              failed: {failed}
              succeeded: {passed}
              skipped: {skipped}
            """;
        Assert.Contains(counts.Replace("\r\n", "\n"), plainOutput);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private TestAsset _asset = null!;

        public string TargetAssetPath => _asset.TargetAssetPath;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _asset = await TestAsset.GenerateAssetAsync(
                AssetName,
                Sources
                    .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
            await DotnetCli.RunAsync(
                $"build {_asset.TargetAssetPath} -c Release -m:2",
                environmentVariables: new() { ["NUGET_PACKAGES"] = Path.Combine(_asset.TargetAssetPath, ".packages") },
                cancellationToken: cancellationToken);
        }

        public void Dispose() => _asset?.Dispose();

        private const string Sources = """
            #file TestRunSummary.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>$TargetFramework$</TargetFramework>
                <OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
              </ItemGroup>
            </Project>

            #file Program.cs
            using Microsoft.Testing.Platform.Builder;
            using Microsoft.Testing.Platform.Capabilities.TestFramework;
            using Microsoft.Testing.Platform.Extensions.Messages;
            using Microsoft.Testing.Platform.Extensions.TestFramework;

            ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
            builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new SummaryFramework());
            using ITestApplication app = await builder.BuildAsync();
            return await app.RunAsync();

            public sealed class SummaryFramework : ITestFramework, IDataProducer
            {
                private readonly string _scenario = Environment.GetEnvironmentVariable("SUMMARY_SCENARIO")!;

                public string Uid => nameof(SummaryFramework);
                public string Version => "1.0.0";
                public string DisplayName => nameof(SummaryFramework);
                public string Description => nameof(SummaryFramework);
                public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

                public Task<bool> IsEnabledAsync() => Task.FromResult(true);

                public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
                    => Task.FromResult(new CreateTestSessionResult
                    {
                        IsSuccess = !_scenario.StartsWith("create", StringComparison.Ordinal),
                        ErrorMessage = _scenario.StartsWith("create", StringComparison.Ordinal) ? "session creation failed" : null,
                    });

                public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
                    => Task.FromResult(new CloseTestSessionResult
                    {
                        IsSuccess = !_scenario.StartsWith("close", StringComparison.Ordinal),
                        ErrorMessage = _scenario.StartsWith("close", StringComparison.Ordinal) ? "session closure failed" : null,
                    });

                public async Task ExecuteRequestAsync(ExecuteRequestContext context)
                {
                    if (_scenario is "passed" or "mixed" or "create-passed" or "close-passed")
                    {
                        await PublishAsync(context, "passed", PassedTestNodeStateProperty.CachedInstance);
                    }

                    if (_scenario is "skipped" or "mixed")
                    {
                        await PublishAsync(context, "skipped", new SkippedTestNodeStateProperty("skip reason"));
                    }

                    if (_scenario == "failed")
                    {
                        await PublishAsync(context, "failed", new FailedTestNodeStateProperty("test failed"));
                    }

                    context.Complete();
                }

                private Task PublishAsync(ExecuteRequestContext context, string uid, TestNodeStateProperty state)
                    => context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode
                    {
                        Uid = uid,
                        DisplayName = uid,
                        Properties = new PropertyBag(state),
                    }));
            }
            """;
    }
}
