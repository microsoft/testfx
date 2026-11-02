// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class CoverageCapabilitiesTests : AcceptanceTestBase<CoverageCapabilitiesTests.TestAssetFixture>
{
    private const string AssetName = "CoverageCapabilities";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CoverageCapabilities_EnabledProducer_IsVisibleToReportExtension(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                ["DECLARE_COVERAGE_PRODUCER"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
        result.AssertOutputContains("Coverage producer available: DummyTestFramework");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CoverageCapabilities_NoEnabledProducer_AllowsReportExtensionToWarn(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                ["DECLARE_COVERAGE_PRODUCER"] = "0",
            },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
        result.AssertOutputContains("No coverage producer is enabled.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file CoverageCapabilities.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new DummyTestFramework());
        builder.TestHost.AddDataConsumer(serviceProvider =>
            new CoverageReportExtension(
                serviceProvider.GetRequiredService<ITestCoverageCapabilities>(),
                serviceProvider.GetOutputDevice()));
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class CoverageReportExtension(
    ITestCoverageCapabilities coverageCapabilities,
    IOutputDevice outputDevice)
    : IDataConsumer, IOutputDeviceDataProducer
{
    public string Uid => nameof(CoverageReportExtension);

    public string Version => "1.0.0";

    public string DisplayName => Uid;

    public string Description => Uid;

    public Type[] DataTypesConsumed => new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        string? producer = coverageCapabilities.EnabledProducerUids.FirstOrDefault(uid => uid != Uid);
        if (producer is null)
        {
            await outputDevice.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData("No coverage producer is enabled."),
                cancellationToken);
        }
        else
        {
            await outputDevice.DisplayAsync(
                this,
                new TextOutputDeviceData($"Coverage producer available: {producer}"),
                cancellationToken);
        }
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => Uid;

    public string Description => Uid;

    public Type[] DataTypesProduced => Environment.GetEnvironmentVariable("DECLARE_COVERAGE_PRODUCER") == "1"
        ? new[] { typeof(TestNodeUpdateMessage), typeof(TestCoverageMessage) }
        : new[] { typeof(TestNodeUpdateMessage) };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        }));
        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
