// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class PackagedAppLaunchNotSupportedTests : AcceptanceTestBase<PackagedAppLaunchNotSupportedTests.TestAssetFixture>
{
    private const string AssetName = "PackagedAppLaunchNotSupportedTest";

    // The asset's AppxManifest.xml declares Name="Contoso.MyTestApp" Publisher="CN=Contoso" and an
    // Application Id="App". The package family name suffix is the deterministic publisher hash of
    // "CN=Contoso", so the AUMID the launcher reports is stable and can be asserted verbatim.
    private const string ExpectedAppUserModelId = "Contoso.MyTestApp_h91ms92gdsmmt!App";
    private const string TrackingIssueUrl = "https://github.com/microsoft/testfx/issues/2784";

    // The sentinel exit code the asset returns after catching the launcher's InvalidOperationException.
    private const int LaunchNotSupportedExitCode = 3;

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "The launcher is only enabled on Windows; on other operating systems it stays disabled and never inspects the layout.")]
    public async Task LaunchTestHost_WithPackagedLayout_FailsFastWithActionableMessage(string currentTfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // The layout contains an AppxManifest.xml, so the launcher must refuse to Process.Start it and
        // instead throw. The asset catches that exception and surfaces it, so the run fails
        // deterministically rather than launching a host that cannot host the run.
        testHostResult.AssertExitCodeIs(LaunchNotSupportedExitCode);

        // The message must stay actionable: it names the exact packaged app (via its AUMID) that could
        // not be launched and points at the tracking issue for the missing activation support.
        testHostResult.AssertOutputContains(ExpectedAppUserModelId);
        testHostResult.AssertOutputContains(TrackingIssueUrl);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string Sources = """
#file PackagedAppLaunchNotSupportedTest.csproj

<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseAppHost>true</UseAppHost>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <!-- AddPackagedAppDeployment is an experimental (TPEXP) API and the PackagedApp package is an
         experimental package with a downgraded (alpha) version (NETSDK1201). -->
    <NoWarn>$(NoWarn);TPEXP;NETSDK1201</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="$MicrosoftTestingExtensionsPackagedAppVersion$" />
  </ItemGroup>
  <ItemGroup>
    <!-- Bake a packaged-app manifest next to the built test host so the launcher classifies the layout
         as packaged (MSIX) and refuses to Process.Start it. -->
    <Content Include="AppxManifest.xml" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>

#file AppxManifest.xml

<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Identity Name="Contoso.MyTestApp" Publisher="CN=Contoso" Version="1.0.0.0" />
  <Applications>
    <Application Id="App" />
  </Applications>
</Package>

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
        var testApplicationBuilder = await TestApplication.CreateBuilderAsync(args);
        testApplicationBuilder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestFramework());
        testApplicationBuilder.AddPackagedAppDeployment();
        using ITestApplication app = await testApplicationBuilder.BuildAsync();

        // The controller process launches the (deployed) test host through the PackagedApp launcher.
        // Because an AppxManifest.xml sits next to this app, the launcher throws instead of doing a
        // plain Process.Start. Catch it and surface it deterministically so the acceptance test can
        // assert on both the exit code and the actionable message.
        try
        {
            return await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return 3;
        }
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsPackagedAppVersion$", MicrosoftTestingExtensionsPackagedAppVersion));
    }

    public TestContext TestContext { get; set; }
}
