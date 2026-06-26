// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class PackagedAppDeploymentTests : AcceptanceTestBase<PackagedAppDeploymentTests.TestAssetFixture>
{
    private const string AssetName = "PackagedAppDeploymentTest";

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Packaged Windows apps (UWP/WinUI) are a Windows-only scenario.")]
    public async Task PackagedAppDeployment_DeploysAndLaunchesTestHost_WithoutLocalPid(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        // The deployed test host reports the directory it actually ran from into this file (it learns
        // the path from the PACKAGEDAPP_BASEDIR_MARKER env var, which the platform forwards to the
        // launched host). This keeps the proof of deployment in the test asset rather than the
        // shipping extension.
        string markerPath = Path.Combine(testHost.DirectoryName, "deployment-basedir.txt");

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?> { ["PACKAGEDAPP_BASEDIR_MARKER"] = markerPath },
            cancellationToken: TestContext.CancellationToken);

        // The test host is deployed elsewhere and launched through a handle that exposes no local
        // PID. The run must still complete successfully, proving the platform's launch contract works
        // for "not just a dumb process".
        testHostResult.AssertExitCodeIs(ExitCode.Success);

        Assert.IsTrue(File.Exists(markerPath), $"Expected the deployed host to write its base directory to '{markerPath}'.");
        string runtimeBaseDirectory = File.ReadAllText(markerPath).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string originalDirectory = testHost.DirectoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.AreNotEqual(originalDirectory, runtimeBaseDirectory, "The test host must have been deployed to and launched from a different directory.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file PackagedAppDeploymentTest.csproj

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
    <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Startup
{
    public static async Task<int> Main(string[] args)
    {
        // The deployed copy of this app reports the directory it is actually running from, so the
        // acceptance test can confirm it was deployed-and-launched from a different location. Both the
        // controller process (original directory) and the deployed test host (deployment directory)
        // run this Main; the deployed host is launched later, so it is the last writer and the marker
        // ends up pointing at the deployment directory. The marker path comes from the
        // platform-forwarded environment.
        string? markerPath = Environment.GetEnvironmentVariable("PACKAGEDAPP_BASEDIR_MARKER");
        if (!string.IsNullOrEmpty(markerPath))
        {
            System.IO.File.WriteAllText(markerPath, AppContext.BaseDirectory);
        }

        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        testApplicationBuilder.AddPackagedAppDeployment();
        using ITestApplication app = await testApplicationBuilder.BuildAsync();
        return await app.RunAsync();
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

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
