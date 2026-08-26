// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class AbortAtDeadlineTests : AcceptanceTestBase<AbortAtDeadlineTests.TestAssetFixture>
{
    private const string AssetName = nameof(AbortAtDeadlineTests);

    private const string StopMessage = "gracefully stopping the test run so reports can be finalized";

    [TestMethod]
    public async Task WhenDeadlineIsInThePast_GracefullyStopsImmediately()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                // A deadline already in the past means the stop instant is also in the past, so the
                // graceful stop fires as soon as the extension arms its timer.
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["WAIT_FOR_STOP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        // A deadline-truncated run reports its own exit code so CI/tooling can tell it apart from a
        // clean pass, even though the in-flight test finished and the summary shows it as passed.
        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task WhenDeadlineIsInThePast_WithDotnetTest_GracefullyStopsImmediately()
    {
        DotnetMuxerResult testResult = await DotnetCli.RunAsync(
            $"test --project \"{AssetFixture.TargetAssetPath}\" --no-build -c Release -f {TargetFrameworks.NetCurrent}",
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["WAIT_FOR_STOP"] = "1",
            },
            workingDirectory: AssetFixture.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        testResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testResult.AssertOutputContains(StopMessage);
        testResult.AssertOutputContains("Test run summary: Failed!");
        testResult.AssertOutputContains("error: 1");
        testResult.AssertOutputContains("total: 1");
        testResult.AssertOutputContains("failed: 0");
        testResult.AssertOutputContains("succeeded: 1");
        testResult.AssertOutputContains("skipped: 0");
    }

    [TestMethod]
    public async Task WhenDeadlineStopsAHotReloadRun_ReportersFinalize()
    {
        string reportFileName = $"{Guid.NewGuid():N}.html";
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            $"--report-html --report-html-filename {reportFileName}",
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["TESTINGPLATFORM_HOTRELOAD_ENABLED"] = "1",
                ["TESTINGPLATFORM_TEST_SET_DEADLINE_ON_START"] = "1",
                ["WAIT_FOR_STOP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        string reportPath = Path.Combine(testHost.DirectoryName, "TestResults", reportFileName);
        Assert.IsTrue(File.Exists(reportPath), $"HTML report should be generated at: {reportPath}");
        string reportContent = File.ReadAllText(reportPath);
        Assert.Contains("<!DOCTYPE html>", reportContent);
        Assert.Contains("id=\"mtp-data\"", reportContent);
    }

    [TestMethod]
    public async Task WhenDeadlineStopsIdleHotReload_ReportersFinalize()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["TESTINGPLATFORM_HOTRELOAD_ENABLED"] = "1",
                ["TESTINGPLATFORM_TEST_SET_DEADLINE_ON_START"] = "1",
                ["REJECT_STOP_AFTER_EXECUTION"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task WhenPastDeadlineStopIsRejectedBeforeHotReloadStarts_FallbackStopsHost()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["TESTINGPLATFORM_HOTRELOAD_ENABLED"] = "1",
                ["REJECT_STOP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
    }

    [TestMethod]
    public async Task WhenDeadlineIsInTheFuture_GracefullyStopsWhenReached()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                // Stop margin 0 means the graceful stop is scheduled for the deadline itself, a few
                // seconds out. The framework blocks until the stop is requested, so this proves the
                // timer fires on schedule (not only when the deadline is already past).
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddSeconds(6).ToString("o"),
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "0",
                ["WAIT_FOR_STOP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task WhenStopMarginIsSubtracted_GracefullyStopsBeforeDeadline()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                // Deadline is a minute out, but a 60s stop margin pulls the stop instant back to
                // roughly now, exercising the margin subtraction against the absolute deadline.
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddSeconds(60).ToString("o"),
                ["TESTINGPLATFORM_DEADLINE_STOP_MARGIN"] = "60",
                ["WAIT_FOR_STOP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedAtDeadline);
        testHostResult.AssertOutputContains(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task WhenNoDeadlineIsSet_DoesNotStop()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // No deadline environment variable, and the framework does not wait for a stop, so the run
        // completes normally and the extension stays silent (it is strictly opt-in).
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE"] = null,
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputDoesNotContain(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task WhenGracefulStopCapabilityIsMissing_DoesNotStopAndRunsToCompletion()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        // A deadline is set, but the framework does not expose IGracefulStopTestExecutionCapability,
        // so the extension degrades to a no-op instead of failing the command line.
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["TESTINGPLATFORM_DEADLINE"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
                ["DO_NOT_ADD_CAPABILITY"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputDoesNotContain(StopMessage);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file AbortAtDeadlineTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("TESTINGPLATFORM_TEST_SET_DEADLINE_ON_START") == "1")
        {
            Environment.SetEnvironmentVariable(
                "TESTINGPLATFORM_DEADLINE",
                DateTimeOffset.UtcNow.AddSeconds(15).ToString("o"));
        }

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
        builder.AddHotReloadProvider();
        builder.AddHtmlReportProvider();
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        // When asked to, block until the deadline-driven graceful stop is requested. This mimics a
        // long-running suite whose remaining tests are cut short by the approaching CI deadline. Cap
        // the wait so a broken stop path fails the assertions fast instead of hanging until the harness
        // times out; the StopMessage and exit-code assertions still catch a stop that never happened.
        if (Environment.GetEnvironmentVariable("WAIT_FOR_STOP") == "1")
        {
            Task completed = await Task.WhenAny(GracefulStop.Instance.TCS.Task, Task.Delay(TimeSpan.FromMinutes(2)));
            if (completed != GracefulStop.Instance.TCS.Task)
            {
                throw new TimeoutException("Timed out waiting for graceful stop.");
            }
        }

        GracefulStop.Instance.NotifyExecutionCompleted();
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities
    {
        get
        {
            if (Environment.GetEnvironmentVariable("DO_NOT_ADD_CAPABILITY") == "1")
            {
                return [];
            }

            return [GracefulStop.Instance];
        }
    }
}

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class GracefulStop : IGracefulStopTestExecutionResultCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    private GracefulStop()
    {
    }

    public static GracefulStop Instance { get; } = new();

    public TaskCompletionSource TCS { get; } = new();

    private bool _executionCompleted;

    public void NotifyExecutionCompleted()
        => _executionCompleted = true;

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
        => TryStopTestExecutionAsync(cancellationToken);

    public Task<bool> TryStopTestExecutionAsync(CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("REJECT_STOP") == "1")
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            Environment.GetEnvironmentVariable("REJECT_STOP_AFTER_EXECUTION") != "1"
            || !_executionCompleted
                ? TCS.TrySetResult()
                : false);
    }
}

""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
