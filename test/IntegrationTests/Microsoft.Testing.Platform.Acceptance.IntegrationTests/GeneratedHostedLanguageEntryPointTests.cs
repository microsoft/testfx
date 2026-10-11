// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class GeneratedHostedLanguageEntryPointTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    public Task VisualBasicGeneratedHostedEntryPointBuildsAndRuns()
        => BuildAndRunAsync("GeneratedHostedVB", VisualBasicAsset);

    [TestMethod]
    public Task FSharpGeneratedHostedEntryPointBuildsAndRuns()
        => BuildAndRunAsync("GeneratedHostedFS", FSharpAsset);

    public TestContext TestContext { get; set; } = null!;

    private async Task BuildAndRunAsync(string assetName, string asset)
    {
        string source = asset
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion)
            .PatchCodeWithReplace("$MicrosoftTestingExtensionsOpenTelemetryVersion$", MicrosoftTestingExtensionsOpenTelemetryVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(assetName, source);

        await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -v:n",
            cancellationToken: TestContext.CancellationToken);

        var testHost = TestInfrastructure.TestHost.LocateFrom(
            testAsset.TargetAssetPath,
            assetName,
            TargetFrameworks.NetCurrent,
            buildConfiguration: BuildConfiguration.Release);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("Passed!");
        Assert.Contains("HOST_STARTED", result.StandardError);
        Assert.Contains("HOST_STOPPED", result.StandardError);
    }

    private const string VisualBasicAsset = """
#file GeneratedHostedVB.vbproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>GeneratedHostedVB</RootNamespace>
    <MicrosoftTestingPlatformVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingPlatformVersion>
    <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
    <TestingPlatformHostFactory>GeneratedHostedVB.GeneratedTestHost.CreateHost</TestingPlatformHostFactory>
    <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingExtensionsOpenTelemetryVersion$" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" />
  </ItemGroup>
</Project>

#file Tests.vb
Imports System.Threading
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Microsoft.VisualStudio.TestTools.UnitTesting

Friend Module GeneratedTestHost
    Public Function CreateHost() As Task(Of IHost)
        Dim builder = Host.CreateApplicationBuilder()
        builder.Logging.ClearProviders()
        builder.Services.AddHostedService(Of MarkerHostedService)()
        Return Task.FromResult(Of IHost)(builder.Build())
    End Function
End Module

Friend NotInheritable Class MarkerHostedService
    Implements IHostedService

    Public Function StartAsync(cancellationToken As CancellationToken) As Task Implements IHostedService.StartAsync
        Global.System.Console.Error.WriteLine("HOST_STARTED")
        Return Task.CompletedTask
    End Function

    Public Function StopAsync(cancellationToken As CancellationToken) As Task Implements IHostedService.StopAsync
        Global.System.Console.Error.WriteLine("HOST_STOPPED")
        Return Task.CompletedTask
    End Function
End Class

<TestClass>
Public NotInheritable Class Tests
    <TestMethod>
    Public Sub Pass()
    End Sub
End Class
""";

    private const string FSharpAsset = """
#file GeneratedHostedFS.fsproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>GeneratedHostedFS</RootNamespace>
    <MicrosoftTestingPlatformVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingPlatformVersion>
    <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
    <TestingPlatformHostFactory>GeneratedHostedFS.GeneratedTestHost.CreateHost</TestingPlatformHostFactory>
    <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
    <NoWarn>$(NoWarn);TPEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingExtensionsOpenTelemetryVersion$" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Tests.fs" />
  </ItemGroup>
</Project>

#file Tests.fs
namespace GeneratedHostedFS

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.VisualStudio.TestTools.UnitTesting

type MarkerHostedService() =
    interface IHostedService with
        member _.StartAsync(_: CancellationToken) =
            Console.Error.WriteLine("HOST_STARTED")
            Task.CompletedTask

        member _.StopAsync(_: CancellationToken) =
            Console.Error.WriteLine("HOST_STOPPED")
            Task.CompletedTask

module GeneratedTestHost =
    let CreateHost() =
        let builder = Host.CreateApplicationBuilder()
        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddHostedService<MarkerHostedService>() |> ignore
        builder.Build() |> Task.FromResult<IHost>

[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Pass() = ()
""";
}
