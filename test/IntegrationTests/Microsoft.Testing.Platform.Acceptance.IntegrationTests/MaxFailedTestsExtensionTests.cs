// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MaxFailedTestsExtensionTests : AcceptanceTestBase
{
    private const string AssetName = nameof(MaxFailedTestsExtensionTests);
    private readonly TestAssetFixture _testAssetFixture;

    public MaxFailedTestsExtensionTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    public async Task TestMaxFailedTestsShouldCallStopTestExecutionAsync()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--maximum-failed-tests 2");

        testHostResult.AssertExitCodeIs(ExitCodes.TestExecutionStoppedForMaxFailedTests);

        testHostResult.AssertOutputContains("Test session is aborting due to reaching failures ('2') specified by the '--maximum-failed-tests' option.");
        testHostResult.AssertOutputContainsSummary(failed: 3, passed: 3, skipped: 0);
    }

    public async Task WhenCapabilityIsMissingShouldFail()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--maximum-failed-tests 2", environmentVariables: new()
        {
            ["DO_NOT_ADD_CAPABILITY"] = "1",
        });

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("The current test framework does not implement 'IGracefulStopTestExecutionCapability' which is required for '--maximum-failed-tests' feature.");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file MaxFailedTestsExtensionTests.csproj
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
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;

internal sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        var adapter = new DummyAdapter();
        builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => adapter);

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        builder.AddMaximumFailedTestsService(adapter);
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

internal class DummyAdapter : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyAdapter);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // First fail.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "Test1", Properties = new(new FailedTestNodeStateProperty()) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "2", DisplayName = "Test2", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "3", DisplayName = "Test3", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "4", DisplayName = "Test4", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

        if (GracefulStop.Instance.IsStopRequested) throw new InvalidOperationException("Unexpected stop request!");

        // Second fail.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "5", DisplayName = "Test5", Properties = new(new FailedTestNodeStateProperty()) }));

        await GracefulStop.Instance.TCS.Task;

        if (!GracefulStop.Instance.IsStopRequested) throw new InvalidOperationException("Expected stop request!");

        // Third fail.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "6", DisplayName = "Test6", Properties = new(new FailedTestNodeStateProperty()) }));

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
internal sealed class GracefulStop : IGracefulStopTestExecutionCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    private GracefulStop()
    {
    }

    public static GracefulStop Instance { get; } = new();

    public TaskCompletionSource TCS { get; } = new();

    public bool IsStopRequested { get; private set; }

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        IsStopRequested = true;
        TCS.SetResult();
        return Task.CompletedTask;
    }
}

""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }
}
