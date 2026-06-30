// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

// Regression coverage for https://github.com/microsoft/testfx/issues/6914 — verifies the
// behavior of the retry extension when combined with the platform's --maximum-failed-tests
// option. The retry orchestrator launches the child test host process and relies on its
// exit code to decide whether to retry. When max-failed-tests fires inside the child,
// TestApplicationResult overrides the exit code to TestExecutionStoppedForMaxFailedTests
// (13) before any AtLeastOneTestFailed (2) override can apply (see TestApplicationResult
// GetProcessExitCode precedence). The retry orchestrator currently treats 13 as a "wrong
// exit code" and aborts the retry loop — which is the desired functional outcome (the
// user explicitly asked to stop early), even though the user-visible warning text is
// misleading.
[TestClass]
public class RetryWithMaxFailedTestsTests : AcceptanceTestBase<RetryWithMaxFailedTestsTests.TestAssetFixture>
{
    private const string AssetName = nameof(RetryWithMaxFailedTestsTests);

    [TestMethod]
    public async Task RetryAndMaxFailedTests_MaxFailedTriggers_RetryDoesNotRunAndExitCodeIs13()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--retry-failed-tests 3 --maximum-failed-tests 2",
            cancellationToken: TestContext.CancellationToken);

        // The child test host stops early because of --maximum-failed-tests; its exit code
        // is therefore 13 (TestExecutionStoppedForMaxFailedTests), not 2 (AtLeastOneTestFailed).
        // The retry orchestrator must surface that exit code unchanged — retrying after the
        // user asked the run to stop would defeat the point of --maximum-failed-tests.
        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedForMaxFailedTests);

        // The child must have produced the standard max-failed-tests stop diagnostic.
        testHostResult.AssertOutputContains("Test session is aborting due to reaching failures ('2') specified by the '--maximum-failed-tests' option.");

        // The retry orchestrator must NOT launch a second attempt, and because the child stopped with an
        // unexpected exit code the orchestrator bails out before printing any retry summary verdict.
        testHostResult.AssertOutputDoesNotContain("Retry: attempt 2/");
        testHostResult.AssertOutputDoesNotContain("Retry summary: Failed!");
        testHostResult.AssertOutputDoesNotContain("Retry summary: Passed!");

        // TODO(https://github.com/microsoft/testfx/issues/6914): the retry orchestrator
        // emits "Test suite failed with and exit code different that 2 (failed tests)..."
        // when the child stops via max-failed-tests. The orchestrator could detect exit
        // code 13 specifically and print a clearer message (or stay silent). Asserting the
        // current text here so a future fix is forced to update this test.
        testHostResult.AssertOutputContains("Test suite failed with and exit code different that 2 (failed tests). Failure related to an unexpected condition. Exit code '13'");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file RetryWithMaxFailedTestsTests.csproj
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
    <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="$MicrosoftTestingPlatformVersion$" />
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
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        var testFramework = new DummyTestFramework();
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => testFramework);

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddMaximumFailedTestsService(testFramework);
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddRetryProvider();

        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // When the retry orchestrator restarts the child after a previous failed attempt
        // it injects --filter-uid for the previously failed tests. The test in this file
        // never reaches that path (max-failed-tests should stop retry), but the framework
        // still has to honor the filter so a regression that *does* trigger a retry would
        // surface as exact-count failures rather than as nondeterministic behavior.
        var filter = (context.Request as TestExecutionRequest)?.Filter as TestNodeUidListFilter;

        // First failure.
        await PublishAsync(context, filter, uid: "1", displayName: "Test1", failed: true);

        // Some passing tests (kept low so the run is fast and the stop happens quickly).
        await PublishAsync(context, filter, uid: "2", displayName: "Test2", failed: false);
        await PublishAsync(context, filter, uid: "3", displayName: "Test3", failed: false);

        // Second failure — this reaches --maximum-failed-tests 2 and the platform
        // signals StopTestExecutionAsync on the capability. We then wait for that signal
        // so the rest of the publish sequence is deterministic with respect to the stop.
        await PublishAsync(context, filter, uid: "4", displayName: "Test4", failed: true);

        await GracefulStop.Instance.TCS.Task;

        // A trailing test that wouldn't fail. The platform may or may not propagate this
        // message depending on the stop sequencing — its presence does not affect the
        // assertions made by the acceptance test.
        await PublishAsync(context, filter, uid: "5", displayName: "Test5", failed: false);

        context.Complete();
    }

    private async Task PublishAsync(ExecuteRequestContext context, TestNodeUidListFilter? filter, string uid, string displayName, bool failed)
    {
        if (filter is not null)
        {
            bool included = false;
            foreach (TestNodeUid filteredUid in filter.TestNodeUids)
            {
                if (filteredUid.Value == uid)
                {
                    included = true;
                    break;
                }
            }

            if (!included)
            {
                return;
            }
        }

        IProperty state = failed
            ? new FailedTestNodeStateProperty()
            : PassedTestNodeStateProperty.CachedInstance;

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode() { Uid = uid, DisplayName = displayName, Properties = new(state) }));
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => [GracefulStop.Instance];
}

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class GracefulStop : IGracefulStopTestExecutionCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    private GracefulStop()
    {
    }

    public static GracefulStop Instance { get; } = new();

    public TaskCompletionSource TCS { get; } = new();

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        TCS.TrySetResult();
        return Task.CompletedTask;
    }
}

""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; } = null!;
}
