// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestHostProcessLifetimeHandlerTests : AcceptanceTestBase<TestHostProcessLifetimeHandlerTests.TestAssetFixture>
{
    private const string AssetName = "TestHostProcessLifetimeHandler";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task All_Interface_Methods_ShouldBe_Invoked(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        Assert.AreEqual("TestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "BeforeTestHostProcessStartAsync.txt")));
        Assert.AreEqual("TestHostProcessLifetimeHandler.OnTestHostProcessStartedAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "OnTestHostProcessStartedAsync.txt")));
        Assert.AreEqual("TestHostProcessLifetimeHandler.OnTestHostProcessExitedAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "OnTestHostProcessExitedAsync.txt")));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file TestHostProcessLifetimeHandler.csproj

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        testApplicationBuilder.TestHostControllers.AddProcessLifetimeHandler(_ => new TestHostProcessLifetimeHandler());
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public class TestHostProcessLifetimeHandler : ITestHostProcessLifetimeHandler
{
    public string Uid => nameof(TestHostProcessLifetimeHandler);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        System.IO.File.WriteAllText("BeforeTestHostProcessStartAsync.txt", "TestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync");
        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync()
    {
        return Task.FromResult(true);
    }

    public Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        System.IO.File.WriteAllText("OnTestHostProcessExitedAsync.txt", "TestHostProcessLifetimeHandler.OnTestHostProcessExitedAsync");
        return Task.CompletedTask;
    }

    public Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        System.IO.File.WriteAllText("OnTestHostProcessStartedAsync.txt", "TestHostProcessLifetimeHandler.OnTestHostProcessStartedAsync");
        return Task.CompletedTask;
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode() 
        {
            Uid = "Test1",
            DisplayName = "Test1",
            Properties = new PropertyBag(new PassedTestNodeStateProperty()),
        }));

        context.Complete();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
