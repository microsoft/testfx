// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class NativeAotTests : AcceptanceTestBase
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

    private readonly AcceptanceFixture _acceptanceFixture;

    public NativeAotTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    public async Task NativeAotTests_WillRunWithExitCodeZero()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
           "NativeAotTests",
           SourceCode
           .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
           .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
           .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments)
           .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
           .PatchCodeWithReplace("$MSTestEngineVersion$", MSTestEngineVersion),
           addPublicFeeds: true);

        // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
        // or sometimes doesn't fail but the native code generation is not done.
        // So, we retry the publication a few times.
        await RetryHelper.RetryAsync(
            async () =>
            {
                await DotnetCli.RunAsync(
                    $"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    retryCount: 0);
                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                    $"publish -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    retryCount: 0);
                compilationResult.AssertOutputContains("Generating native code");
            }, times: 5, every: TimeSpan.FromSeconds(3));

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "NativeAotTests", TargetFrameworks.NetCurrent.Arguments, RID, Verb.publish);

        TestHostResult result = await testHost.ExecuteAsync();
        result.AssertExitCodeIs(0);
    }
}
