// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Covers the scenario the feature actually exists for: an extension living in its own assembly, in its own
/// directory, with its own private dependency, declared by a manifest and never referenced by the test project.
/// </summary>
/// <remarks>
/// The test application and the extension both depend on an assembly called <c>Contoso.Shared</c>, but on two
/// *different and incompatible* builds of it — the application's build exposes <c>Value</c> returning
/// <c>"app"</c>, the extension's returns <c>"extension"</c>. Without isolation one of the two would silently
/// win, which is precisely the VSTest failure mode this design exists to avoid. Asserting that each side
/// reports its own value is therefore a direct check that the load contexts are separate, while the extension
/// simultaneously registers a real extension through the shared platform contract.
/// </remarks>
[TestClass]
public sealed class DynamicExtensionIsolationTests : AcceptanceTestBase<DynamicExtensionIsolationTests.TestAssetFixture>
{
    private const string AssetName = "DynamicExtensionIsolation";

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task ExtensionAndApplicationEachKeepTheirOwnDependencyVersion(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
        testHostResult.AssertOutputContains("APP_SEES=app");
        testHostResult.AssertOutputContains("EXTENSION_SEES=extension");
    }

    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task ExtensionRegistrationCrossesTheLoadContextBoundary(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--enable-dynamic-extensions --info", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        // The provider is instantiated inside the isolated context but consumed by the host, which only works
        // because Microsoft.Testing.Platform has a single identity across the boundary — even though the
        // extension folder contains its own copy of that assembly.
        testHostResult.AssertOutputContains("Contoso isolated options");
        testHostResult.AssertOutputContains("--contoso-isolated");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        // These assets are plain Microsoft.Testing.Platform applications with no test framework metadata, so
        // building source-generation variants of them would only double the build cost.
        protected override IReadOnlyList<MetadataMode> SourceGenMetadataModes => [];

        private const string SourceCode = """
#file DynamicExtensionIsolation.csproj
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
        <ProjectReference Include="Contoso.Shared.App/Contoso.Shared.App.csproj" />
        <!-- Built so its output exists, but deliberately NOT referenced: the manifest is the only link. -->
        <ProjectReference Include="Contoso.Extension/Contoso.Extension.csproj" ReferenceOutputAssembly="false" />
    </ItemGroup>

    <ItemGroup>
        <!-- The extension projects live in sub-directories of this one, so the default globs would compile
             their sources into the application. That would defeat the whole point: the application must not
             know anything about the extension. -->
        <Compile Remove="Contoso.Shared.App/**/*.cs" />
        <Compile Remove="Contoso.Shared.Extension/**/*.cs" />
        <Compile Remove="Contoso.Extension/**/*.cs" />
    </ItemGroup>

    <ItemGroup>
        <Content Include="contoso.testingplatformextensions.json" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>

    <!-- Deploy the extension into its own sub-directory of the application output, which is the layout the
         RFC recommends: the extension's private dependencies never sit next to the application's. -->
    <Target Name="DeployContosoExtension" AfterTargets="Build">
        <ItemGroup>
            <_ContosoExtensionFiles Include="$(MSBuildThisFileDirectory)Contoso.Extension/bin/$(Configuration)/$(TargetFramework)/*.dll" />
            <_ContosoExtensionFiles Include="$(MSBuildThisFileDirectory)Contoso.Extension/bin/$(Configuration)/$(TargetFramework)/*.json" />
        </ItemGroup>
        <Copy SourceFiles="@(_ContosoExtensionFiles)"
              DestinationFolder="$(OutDir)contoso-extension"
              SkipUnchangedFiles="true" />
    </Target>
</Project>

#file contoso.testingplatformextensions.json
{
  "$schema": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/testingplatformextensions.schema.json",
  "extensions": [
    {
      "id": "5C7A1B2D-3E4F-4A5B-8C9D-0E1F2A3B4C5D",
      "displayName": "Contoso isolated policy",
      "assemblyPath": "contoso-extension/Contoso.Extension.dll",
      "typeFullName": "Contoso.Extension.TestingPlatformBuilderHook"
    }
  ]
}

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Load the application's own copy of Contoso.Shared before the extension is resolved, so that a
        // non-isolated extension would observe this one instead of its own.
        Console.WriteLine($"APP_SEES={Contoso.Shared.Marker.Value}");

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        context.Complete();
        return Task.CompletedTask;
    }
}

#file Contoso.Shared.App/Contoso.Shared.App.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <Nullable>enable</Nullable>
        <LangVersion>preview</LangVersion>
        <AssemblyName>Contoso.Shared</AssemblyName>
        <RootNamespace>Contoso.Shared</RootNamespace>
    </PropertyGroup>
</Project>

#file Contoso.Shared.App/Marker.cs
namespace Contoso.Shared;

public static class Marker
{
    public static string Value => "app";
}

#file Contoso.Shared.Extension/Contoso.Shared.Extension.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <Nullable>enable</Nullable>
        <LangVersion>preview</LangVersion>
        <AssemblyName>Contoso.Shared</AssemblyName>
        <RootNamespace>Contoso.Shared</RootNamespace>
    </PropertyGroup>
</Project>

#file Contoso.Shared.Extension/Marker.cs
namespace Contoso.Shared;

public static class Marker
{
    public static string Value => "extension";
}

#file Contoso.Extension/Contoso.Extension.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <Nullable>enable</Nullable>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <ProjectReference Include="../Contoso.Shared.Extension/Contoso.Shared.Extension.csproj" />
    </ItemGroup>
</Project>

#file Contoso.Extension/TestingPlatformBuilderHook.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Contoso.Extension;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
    {
        Console.WriteLine($"EXTENSION_SEES={Contoso.Shared.Marker.Value}");
        builder.CommandLine.AddProvider(() => new ContosoIsolatedCommandLineProvider());
    }
}

public sealed class ContosoIsolatedCommandLineProvider : ICommandLineOptionsProvider
{
    public string Uid => "Contoso.IsolatedOptions";

    public string Version => "1.0.0";

    public string DisplayName => "Contoso isolated options";

    public string Description => "Options contributed by an isolated, dynamically loaded extension.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new[] { new CommandLineOption("contoso-isolated", "An option from the isolated extension.", ArgumentArity.ExactlyOne, false) };

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.Net)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
