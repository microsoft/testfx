// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class TypeForwardingTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string AssetName = "TypeForwardingTests";

    // The idea of this test is to have a netstandard2.0 library that sets an init-only property.
    // The library is compiled against netstandard2.0 API of MTP. So, IsExternalInit is coming through Polyfill.
    // Then, console app is consuming the library and uses the latest TFM for MTP, which has IsExternalInit from BCL.
    // What happens now is:
    // At IL-level (compile-time), IsExternalInit from Polyfill is accessed.
    // At runtime, IsExternalInit doesn't exist from Polyfill and exists only through BCL.
    // For this situation to work, a TypeForwardedTo(typeof(IsExternalInit)) is needed in MTP when
    // compiling for a TFM that has IsExternalInit from BCL.
    // See https://github.com/SimonCropp/Polyfill/issues/290
    private const string Sources = """
        #file ClassLib/ClassLib.csproj
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
            <OutputType>Library</OutputType>
            <Nullable>enable</Nullable>
            <LangVersion>preview</LangVersion>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
          </ItemGroup>
        </Project>

        #file ClassLib/MyClassCompiledAgainstNetStandardBinary.cs
        using Microsoft.Testing.Platform.Extensions.Messages;

        public static class MyClassCompiledAgainstNetStandardBinary
        {
            public static TestNode M()
                => new TestNode() { DisplayName = "MyDisplayName", Uid = new("MyUid") };
        }

        #file ConsoleApp/ConsoleApp.csproj

        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>$TargetFrameworks$</TargetFramework>
            <OutputType>Exe</OutputType>
            <Nullable>enable</Nullable>
            <LangVersion>preview</LangVersion>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="..\ClassLib\ClassLib.csproj" Version="$MicrosoftTestingPlatformVersion$" />
          </ItemGroup>
        </Project>

        #file ConsoleApp/Program.cs
        using System;

        Console.WriteLine(MyClassCompiledAgainstNetStandardBinary.M().DisplayName);
        """;

    [TestMethod]
    public async Task SettingDisplayNameFromNetStandardLibraryDuringNetCurrentRuntimeExecutionShouldNotCrash()
    {
        string patchedSources = Sources
            .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion);

        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(AssetName, patchedSources);
        await DotnetCli.RunAsync($"build -m:1 -nodeReuse:false {testAsset.TargetAssetPath}/ConsoleApp -c Release", AcceptanceFixture.NuGetGlobalPackagesFolder.Path, cancellationToken: TestContext.CancellationToken);

        var testHost = TestInfrastructure.TestHost.LocateFrom($"{testAsset.TargetAssetPath}/ConsoleApp", "ConsoleApp", TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("MyDisplayName");
    }

    public TestContext TestContext { get; set; }
}
