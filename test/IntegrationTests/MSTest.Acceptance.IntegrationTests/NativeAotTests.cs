// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class NativeAotTests : AcceptanceTestBase<NopAssetFixture>
{
    private const string SourceCode = """
#file NativeAotTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <PublishAot>true</PublishAot>
        <!-- Show individual trim/AOT warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.Engine" Version="$MSTestEngineVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestEngineVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Testing.Framework;
using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

using NativeAotTests;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file TestClass1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }

    [TestMethod]
    [DataRow(0, 1)]
    public void TestMethod2(int a, int b)
    {
    }

    [TestMethod]
    [DynamicData(nameof(Data))]
    public void TestMethod3(int a, int b)
    {
    }

    public static IEnumerable<object[]> Data { get; }
        = new[]
        {
           new object[] { 1, 2 }
        };
}
""";

    // Source code for a minimal project that deeply validates trim/AOT compatibility of
    // Microsoft.Testing.Platform by using TrimmerRootAssembly to force the trimmer to analyze
    // all code paths in the assembly, not just those reachable from the test entry point.
    // See https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming
    private const string TrimAnalysisSourceCode = """
#file TrimAnalysisTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <OutputType>Exe</OutputType>
        <PublishAot>true</PublishAot>
        <!-- Show individual trim/AOT warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    </ItemGroup>
    <!-- Force the trimmer to analyze the full assembly surface, not just reachable code paths -->
    <ItemGroup>
        <TrimmerRootAssembly Include="Microsoft.Testing.Platform" />
    </ItemGroup>
</Project>

#file Program.cs
System.Console.WriteLine("This project validates trim/AOT compatibility via dotnet publish.");
""";

    [TestMethod]
    // The hosted AzDO agents for Mac OS don't have the required tooling for us to test Native AOT.
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task NativeAotTests_WillRunWithExitCodeZero()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            "NativeAotTests",
            SourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestEngineVersion$", MSTestEngineVersion),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"restore {generator.TargetAssetPath} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");
        compilationResult.AssertOutputDoesNotContain("warning");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "NativeAotTests", TargetFrameworks.NetCurrent, RID, Verb.publish);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContains($"MSTest.Engine v{MSTestEngineVersion}");
        result.AssertExitCodeIs(0);
    }

    [TestMethod]
    [DynamicData(nameof(NativeAotTfmsForDynamicData))]
    // The hosted AzDO agents for Mac OS don't have the required tooling for us to test Native AOT.
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task NativeAotPublish_ShouldNotProduceTrimWarnings(string tfm)
    {
        // See https://github.com/microsoft/testfx/issues/7153
        // This test forces deep trim analysis of Microsoft.Testing.Platform using TrimmerRootAssembly
        // to catch trim warnings that would not be caught by only testing reachable code paths.
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"TrimAnalysisTest_{tfm}",
            TrimAnalysisSourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm),
            addPublicFeeds: true);

        await DotnetCli.RunAsync(
            $"restore {generator.TargetAssetPath} -r {RID}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            retryCount: 0,
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");
        compilationResult.AssertOutputDoesNotContain("warning");
    }

    // Native AOT is supported on net8.0+. We test each supported TFM to catch
    // framework-version-specific trim issues (e.g. the net8.0-specific IL2104 in #7153).
    public static IEnumerable<object[]> NativeAotTfmsForDynamicData =>
        TargetFrameworks.Net
            .Where(tfm => tfm is not ("net6.0" or "net7.0"))
            .Select(tfm => new object[] { tfm });

    public TestContext TestContext { get; set; }
}
