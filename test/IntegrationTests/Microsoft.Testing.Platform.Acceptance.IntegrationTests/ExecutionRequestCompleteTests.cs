// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class ExecutionRequestCompleteTests : AcceptanceTestBase<ExecutionRequestCompleteTests.TestAssetFixture>
{
    private const string AssetName = "ExecutionTests2";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_Honor_Request_Complete(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        stopwatch.Stop();
        Assert.AreEqual(ExitCodes.Success, testHostResult.ExitCode);
        Assert.IsGreaterThan(3, stopwatch.Elapsed.TotalSeconds);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string TestCode = """
#file ExecutionTests2.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using System.Threading.Tasks;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Task.Run(async() =>
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode() { Uid = "0", DisplayName = "Test", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

            Thread.Sleep(3_000);

            context.Complete();
        });

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
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
