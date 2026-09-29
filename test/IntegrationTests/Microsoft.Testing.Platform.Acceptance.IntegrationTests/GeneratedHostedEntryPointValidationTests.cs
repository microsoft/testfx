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
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsOpenTelemetryVersion$", MicrosoftTestingExtensionsOpenTelemetryVersion);
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

    public TestContext TestContext { get; set; } = null!;

    private const string HostingPackage = """
        <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
    """;

    private const string OpenTelemetryPackage = """
        <PackageReference Include="Microsoft.Testing.Extensions.OpenTelemetry" Version="$MicrosoftTestingExtensionsOpenTelemetryVersion$" />
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
