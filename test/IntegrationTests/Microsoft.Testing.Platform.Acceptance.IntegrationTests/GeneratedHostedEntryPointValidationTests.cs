// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class GeneratedHostedEntryPointValidationTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    public async Task InvalidHostedEntryPointSettingsFailWithActionableErrors()
    {
        (string Name, string Properties, string Packages, string ExpectedError)[] scenarios =
        [
            (
                "EntryPointDisabled",
                """
                <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                """,
                HostingPackage,
                "TestingPlatformHostFactory requires GenerateTestingPlatformEntryPoint to be true."),
            (
                "HelperDisabled",
                """
                <GenerateTestingPlatformApplicationHelper>false</GenerateTestingPlatformApplicationHelper>
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                """,
                HostingPackage,
                "TestingPlatformHostFactory requires GenerateTestingPlatformApplicationHelper to be true."),
            (
                "SelfRegistrationDisabled",
                """
                <GenerateSelfRegisteredExtensions>false</GenerateSelfRegisteredExtensions>
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                """,
                HostingPackage,
                "TestingPlatformHostFactory requires GenerateSelfRegisteredExtensions to be true"),
            (
                "VSTest",
                """
                <UseVSTest>true</UseVSTest>
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                """,
                HostingPackage,
                "TestingPlatformHostFactory is not supported when UseVSTest is true."),
            (
                "MissingHosting",
                "<TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>",
                string.Empty,
                "TestingPlatformHostFactory requires a compatible Microsoft.Testing.Extensions.Hosting package reference."),
            (
                "MismatchedHosting",
                """
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                <MicrosoftTestingExtensionsHostingEntryPointProtocolVersion>999</MicrosoftTestingExtensionsHostingEntryPointProtocolVersion>
                """,
                HostingPackage,
                "hosted-entrypoint protocol version '999' is incompatible"),
            (
                "InvalidTelemetryMode",
                """
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                <TestingPlatformOpenTelemetryMode>Automatic</TestingPlatformOpenTelemetryMode>
                """,
                HostingPackage,
                "TestingPlatformOpenTelemetryMode 'Automatic' is invalid."),
            (
                "HostOwnedWithoutFactory",
                "<TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>",
                OpenTelemetryPackage,
                "TestingPlatformOpenTelemetryMode=HostOwned requires TestingPlatformHostFactory."),
            (
                "HostOwnedWithoutOpenTelemetry",
                """
                <TestingPlatformHostFactory>Contoso.TestHost.CreateHost</TestingPlatformHostFactory>
                <TestingPlatformOpenTelemetryMode>HostOwned</TestingPlatformOpenTelemetryMode>
                """,
                HostingPackage,
                "TestingPlatformOpenTelemetryMode=HostOwned requires a compatible Microsoft.Testing.Extensions.OpenTelemetry package reference."),
        ];

        foreach ((string name, string properties, string packages, string expectedError) in scenarios)
        {
            string source = Asset
                .PatchCodeWithReplace("$Properties$", properties)
                .PatchCodeWithReplace("$Packages$", packages)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsOpenTelemetryVersion$", MicrosoftTestingExtensionsOpenTelemetryVersion)
                .PatchCodeWithReplace("$MSTestExtensionsHostingVersion$", MSTestExtensionsHostingVersion);
            using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                $"{nameof(InvalidHostedEntryPointSettingsFailWithActionableErrors)}_{name}",
                source);

            DotnetMuxerResult result = await DotnetCli.RunAsync(
                $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -v:n",
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIsNot(0);
            result.AssertOutputContains(expectedError);
        }
    }

    [TestMethod]
    public async Task MSTestHostingPackageReferenceWithoutOptInBuilds()
    {
        string source = Asset
            .PatchCodeWithReplace("$Properties$", """
                <OutputType>Library</OutputType>
                <IsTestingPlatformApplication>false</IsTestingPlatformApplication>
                """)
            .PatchCodeWithReplace("$Packages$", MSTestHostingPackage)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion)
            .PatchCodeWithReplace("$MicrosoftTestingExtensionsOpenTelemetryVersion$", MicrosoftTestingExtensionsOpenTelemetryVersion)
            .PatchCodeWithReplace("$MSTestExtensionsHostingVersion$", MSTestExtensionsHostingVersion);
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            nameof(MSTestHostingPackageReferenceWithoutOptInBuilds),
            source);

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -v:n",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(0);
    }

    [TestMethod]
    public async Task MSTestHostingInjectionUnsupportedBuildModesFailAtTheOptInCall()
    {
        (string Name, string Properties, string ExpectedBuildMode)[] scenarios =
        [
            ("NativeAot", "<PublishAot>true</PublishAot>", "PublishAot"),
            ("AotCompilation", "<RunAOTCompilation>true</RunAOTCompilation>", "RunAOTCompilation"),
            ("SourceGeneration", "<EnableMSTestSourceGeneration>true</EnableMSTestSourceGeneration>", "EnableMSTestSourceGeneration"),
            ("Browser", "<TargetPlatformIdentifier>browser</TargetPlatformIdentifier>", "browser-wasm"),
        ];

        foreach ((string name, string properties, string expectedBuildMode) in scenarios)
        {
            string source = MSTestHostInjectionAsset
                .PatchCodeWithReplace("$Properties$", properties)
                .PatchCodeWithReplace("$MSTestExtensionsHostingVersion$", MSTestExtensionsHostingVersion);
            using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
                $"{nameof(MSTestHostingInjectionUnsupportedBuildModesFailAtTheOptInCall)}_{name}",
                source);

            DotnetMuxerResult result = await DotnetCli.RunAsync(
                $"build -c {BuildConfiguration.Release} {testAsset.TargetAssetPath} -v:n",
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIsNot(0);
            result.AssertOutputContains(
                $"AddMSTestTestClassInjection is not supported when '{expectedBuildMode}' is enabled");
        }
    }

    [TestMethod]
    public async Task MSTestHostingInjectionOptInFlowsAcrossProjectReferences()
    {
        string source = CrossProjectMSTestHostInjectionAsset
            .PatchCodeWithReplace("$MSTestExtensionsHostingVersion$", MSTestExtensionsHostingVersion)
            .PatchCodeWithReplace("$Properties$", string.Empty);
        using TestAsset supportedAsset = await TestAsset.GenerateAssetAsync(
            $"{nameof(MSTestHostingInjectionOptInFlowsAcrossProjectReferences)}_Supported",
            source);

        DotnetMuxerResult supportedResult = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {supportedAsset.TargetAssetPath} -v:n",
            cancellationToken: TestContext.CancellationToken);

        supportedResult.AssertExitCodeIs(0);

        source = CrossProjectMSTestHostInjectionAsset
            .PatchCodeWithReplace("$MSTestExtensionsHostingVersion$", MSTestExtensionsHostingVersion)
            .PatchCodeWithReplace("$Properties$", """
                <PublishAot>true</PublishAot>
                <RunAnalyzers>false</RunAnalyzers>
                """);
        using TestAsset unsupportedAsset = await TestAsset.GenerateAssetAsync(
            $"{nameof(MSTestHostingInjectionOptInFlowsAcrossProjectReferences)}_NativeAot",
            source);

        DotnetMuxerResult unsupportedResult = await DotnetCli.RunAsync(
            $"build -c {BuildConfiguration.Release} {unsupportedAsset.TargetAssetPath} -v:n",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        unsupportedResult.AssertExitCodeIsNot(0);
        unsupportedResult.AssertOutputContains(
            "AddMSTestTestClassInjection is not supported when 'PublishAot' is enabled");
    }

    public TestContext TestContext { get; set; } = null!;

    private const string HostingPackage = """
        <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
    """;

    private const string OpenTelemetryPackage = """
        <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingExtensionsOpenTelemetryVersion$" />
    """;

    private const string MSTestHostingPackage = """
        <PackageReference Include="MSTest.Extensions.Hosting" Version="$MSTestExtensionsHostingVersion$" />
    """;

    private const string MSTestHostInjectionAsset = """
#file MSTestHostInjectionValidation.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
    <NoWarn>$(NoWarn);MSTESTEXP</NoWarn>
$Properties$
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.Extensions.Hosting" Version="$MSTestExtensionsHostingVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

ServiceCollection services = [];
services.AddMSTestTestClassInjection();

public sealed class ApplicationService
{
}

[TestClass]
public sealed class InjectedOnlyTest(ApplicationService service)
{
    private readonly ApplicationService _service = service;
}
""";

    private const string CrossProjectMSTestHostInjectionAsset = """
#file MSTestHostInjectionValidation.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
    <RunAnalyzers>true</RunAnalyzers>
    <NoWarn>$(NoWarn);MSTESTEXP</NoWarn>
$Properties$
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="HostSetup/HostSetup.csproj" />
    <Compile Remove="HostSetup/**" />
  </ItemGroup>
</Project>

#file Program.cs
using HostSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

ServiceCollection services = [];
HostServices.Configure(services);

[TestClass]
public sealed class InjectedOnlyTest(ApplicationService service)
{
    private readonly ApplicationService _service = service;
}

#file HostSetup/HostSetup.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestorePackagesPath>$(MSBuildProjectDirectory)\..\.packages</RestorePackagesPath>
    <RunAnalyzers>true</RunAnalyzers>
    <NoWarn>$(NoWarn);MSTESTEXP</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest.Extensions.Hosting" Version="$MSTestExtensionsHostingVersion$" />
  </ItemGroup>
</Project>

#file HostSetup/HostServices.cs
using Microsoft.Extensions.DependencyInjection;

namespace HostSetup;

public static class HostServices
{
    public static void Configure(IServiceCollection services)
        => services.AddMSTestTestClassInjection();
}

public sealed class ApplicationService
{
}
""";

    private const string Asset = """
#file HostedValidation.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsTestingPlatformApplication>true</IsTestingPlatformApplication>
    <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
$Properties$
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
$Packages$
  </ItemGroup>
</Project>
""";
}
