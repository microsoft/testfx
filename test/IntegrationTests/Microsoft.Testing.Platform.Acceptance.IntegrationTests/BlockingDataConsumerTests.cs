// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class BlockingDataConsumerTests : AcceptanceTestBase<BlockingDataConsumerTests.TestAssetFixture>
{
    private const string AssetName = "BlockingDataConsumerTests";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task BlockingDataConsumer_IsConsumedInline_BeforePublishReturns(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The asset publishes an in-progress message to a blocking consumer that sleeps inside
        // ConsumeAsync. It then verifies, right after PublishAsync returns, that the consumption has
        // already completed (proving the publish blocked until ConsumeAsync finished). The asset
        // reports a passing test only when that invariant holds, so a green run validates the
        // blocking behavior end-to-end.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file BlockingDataConsumerTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <NoWarn>$(NoWarn);TPEXP</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(
    _ => new TestFrameworkCapabilities(),
    (_, __) => new DummyTestFramework());

builder.TestHost.AddDataConsumer(_ => new BlockingConsumer());

using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => "Dummy Test Framework";

    public string Description => "A dummy test framework for testing blocking data consumers";

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
            new TestNode() { Uid = "1", DisplayName = "BlockingValidation", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));

        // Publish an in-progress update. Because the consumer is a blocking data consumer that sleeps
        // inside ConsumeAsync, this call must not return until the consumption has completed.
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "BlockingValidation", Properties = new(InProgressTestNodeStateProperty.CachedInstance) }));

        // If consumption was performed inline (i.e. the publish blocked), the flag is already set.
        // With a regular asynchronous consumer this would still be false here because the slow
        // ConsumeAsync would run on the background processing loop after the publish returned.
        bool consumedInline = BlockingConsumer.HasCompletedConsumption;

        TestNodeStateProperty resultState = consumedInline
            ? PassedTestNodeStateProperty.CachedInstance
            : new FailedTestNodeStateProperty("Blocking consumer did not consume the data inline before PublishAsync returned.");

        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "BlockingValidation", Properties = new(resultState) }));

        context.Complete();
    }
}

internal class BlockingConsumer : IBlockingDataConsumer
{
    public static volatile bool HasCompletedConsumption;

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(BlockingConsumer);

    public string Version => "1.0.0";

    public string DisplayName => nameof(BlockingConsumer);

    public string Description => nameof(BlockingConsumer);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        // Only react to the in-progress message; ignore the final result message we publish below.
        if (value is TestNodeUpdateMessage { TestNode.Properties: { } properties }
            && properties.SingleOrDefault<InProgressTestNodeStateProperty>() is not null)
        {
            // Sleep to widen the window: if the publish did not block, the producer would observe
            // HasCompletedConsumption as false right after PublishAsync returned.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            HasCompletedConsumption = true;
        }
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; } = null!;
}
