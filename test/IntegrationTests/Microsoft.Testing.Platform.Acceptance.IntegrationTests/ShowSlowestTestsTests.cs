// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// End-to-end coverage for the terminal reporter's opt-in <c>--show-slowest-tests</c> section: drives a real
/// Microsoft.Testing.Platform session whose tests report distinct, deterministic durations and asserts that the
/// section is rendered, is capped at the requested count, and ranks the tests from slowest to fastest.
/// </summary>
[TestClass]
public sealed class ShowSlowestTestsTests : AcceptanceTestBase<ShowSlowestTestsTests.TestAssetFixture>
{
    private const string AssetName = "ShowSlowestTestsBehavior";

    public TestContext TestContext { get; set; } = null!;

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenEnabled_RanksTestsSlowestFirstAndHonorsTheRequestedCount(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(
            "--show-slowest-tests 3",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);

        // The opt-in section header is emitted because at least one timed test was recorded.
        result.AssertOutputContains("Slowest tests:");

        // Only the three slowest tests are listed; the fastest test is dropped by the count cap.
        result.AssertOutputContains("AlphaSlow");
        result.AssertOutputContains("BravoMedium");
        result.AssertOutputContains("CharlieFast");
        result.AssertOutputDoesNotContain("DeltaTiny");

        // The ranking is deterministic: 5s > 3s > 1s, so the names appear slowest-first.
        string output = result.StandardOutput;
        int slowIndex = output.IndexOf("AlphaSlow", StringComparison.Ordinal);
        int mediumIndex = output.IndexOf("BravoMedium", StringComparison.Ordinal);
        int fastIndex = output.IndexOf("CharlieFast", StringComparison.Ordinal);

        Assert.IsGreaterThan(-1, slowIndex);
        Assert.IsLessThan(mediumIndex, slowIndex);
        Assert.IsLessThan(fastIndex, mediumIndex);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WhenNotRequested_NoSlowestTestsSectionIsRendered(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputDoesNotContain("Slowest tests:");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file ShowSlowestTestsBehavior.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
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

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, __) => new DummyTestFramework());
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
        // Deterministic, well-separated durations so the ranking is unambiguous:
        //   AlphaSlow (5s) > BravoMedium (3s) > CharlieFast (1s) > DeltaTiny (0.2s).
        // With --show-slowest-tests 3 the fastest (DeltaTiny) must be dropped by the count cap.
        await PublishTimedTestAsync(context, "test-1", "AlphaSlow", TimeSpan.FromSeconds(5));
        await PublishTimedTestAsync(context, "test-2", "BravoMedium", TimeSpan.FromSeconds(3));
        await PublishTimedTestAsync(context, "test-3", "CharlieFast", TimeSpan.FromSeconds(1));
        await PublishTimedTestAsync(context, "test-4", "DeltaTiny", TimeSpan.FromMilliseconds(200));

        context.Complete();
    }

    private Task PublishTimedTestAsync(ExecuteRequestContext context, string uid, string displayName, TimeSpan duration)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        return context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
            context.Request.Session.SessionUid,
            new TestNode()
            {
                Uid = uid,
                DisplayName = displayName,
                Properties = new PropertyBag(
                    PassedTestNodeStateProperty.CachedInstance,
                    new TimingProperty(new TimingInfo(start, start + duration, duration))),
            }));
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }
}
