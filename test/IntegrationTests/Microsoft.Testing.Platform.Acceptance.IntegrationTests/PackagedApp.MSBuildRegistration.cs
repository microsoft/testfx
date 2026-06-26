// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SL = Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class PackagedAppMSBuildRegistrationTests : AcceptanceTestBase<NopAssetFixture>
{
    // Validates that the PackagedApp extension's build/buildTransitive props correctly contribute its
    // TestingPlatformBuilderHook to a consuming project, so the MSBuild integration auto-registers it.
    // This is a build-time assertion (it reads the generated self-registration source from the
    // binlog) and does not run the deployed test host, so it is safe on every OS.
    [TestMethod]
    public async Task PackagedApp_TestingPlatformBuilderHook_IsRegistered_ViaBuildProps()
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(PackagedApp_TestingPlatformBuilderHook_IsRegistered_ViaBuildProps),
            SourceCode
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingExtensionsPackagedAppVersion$", MicrosoftTestingExtensionsPackagedAppVersion));

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -v:n",
            cancellationToken: TestContext.CancellationToken);

        SL.Build binLog = SL.Serialization.Read(result.BinlogPath!);
        SL.Target generateSelfRegisteredExtensions = binLog.FindChildrenRecursive<SL.Target>().Single(t => t.Name == "_GenerateSelfRegisteredExtensions");
        SL.Task testingPlatformSelfRegisteredExtensions = generateSelfRegisteredExtensions.FindChildrenRecursive<SL.Task>().Single(t => t.Name == "TestingPlatformSelfRegisteredExtensions");
        SL.Message generatedSource = testingPlatformSelfRegisteredExtensions.FindChildrenRecursive<SL.Message>().Single(m => m.Text.Contains("SelfRegisteredExtensions source:"));

        Assert.Contains("Microsoft.Testing.Extensions.PackagedApp.TestingPlatformBuilderHook.AddExtensions", generatedSource.Text, generatedSource.Text);
    }

    public TestContext TestContext { get; set; }

    private const string SourceCode = """
#file PackagedAppRegistration.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <TestingPlatformBuilderHook Include="A">
      <DisplayName>DummyTestFramework</DisplayName>
      <TypeFullName>PackagedAppRegistration.DummyTestFrameworkRegistration</TypeFullName>
    </TestingPlatformBuilderHook>
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
    <OutputType>Exe</OutputType>
    <!-- The PackagedApp package is an experimental package with a downgraded (alpha) version. -->
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="$MicrosoftTestingExtensionsPackagedAppVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

namespace PackagedAppRegistration;

public static class DummyTestFrameworkRegistration
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] args)
        => testApplicationBuilder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
}

internal sealed class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
            new TestNode() { Uid = "1", DisplayName = "DummyTest", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        context.Complete();
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal sealed class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}
""";
}
