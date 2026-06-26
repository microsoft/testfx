// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestHostLauncherTests : AcceptanceTestBase<TestHostLauncherTests.TestAssetFixture>
{
    private const string AssetName = "TestHostLauncher";

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task CustomLauncher_IsUsedToStartTestHost_AndRunSucceeds(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The custom launcher actually starts the test host, so the dummy test must run and the
        // overall run must succeed.
        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // Marker proves the platform delegated the launch to our ITestHostLauncher rather than
        // doing the default Process.Start itself.
        Assert.AreEqual("TestHostLauncher.LaunchTestHostAsync", File.ReadAllText(Path.Combine(testHost.DirectoryName, "LaunchTestHostAsync.txt")));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file TestHostLauncher.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <!-- The test host launcher extension point is experimental. -->
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        testApplicationBuilder.TestHostControllers.AddTestHostLauncher(_ => new TestHostLauncher());
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
    }
}

public sealed class TestHostLauncher : ITestHostLauncher
{
    public string Uid => nameof(TestHostLauncher);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        System.IO.File.WriteAllText("LaunchTestHostAsync.txt", "TestHostLauncher.LaunchTestHostAsync");

        var startInfo = new ProcessStartInfo(context.FileName)
        {
            UseShellExecute = false,
        };

        foreach (string argument in context.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        if (context.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = context.WorkingDirectory;
        }

        Process process = Process.Start(startInfo)!;
        return Task.FromResult<ITestHostHandle>(new ProcessTestHostHandle(process));
    }
}

public sealed class ProcessTestHostHandle : ITestHostHandle
{
    private readonly Process _process;

    public ProcessTestHostHandle(Process process) => _process = process;

    public string? Identifier => _process.Id.ToString();

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public Task WaitForExitAsync() => _process.WaitForExitAsync();

    public void Terminate() => _process.Kill();
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

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
