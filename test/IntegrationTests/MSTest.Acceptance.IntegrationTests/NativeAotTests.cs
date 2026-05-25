// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class NativeAotTests : AcceptanceTestBase<NopAssetFixture>
{
    // Source code for a project that validates MSTest supporting Native AOT.
    // Uses MSTest.SourceGeneration to emit reflection-free metadata so the runtime
    // does not need to fall back to reflection for test discovery or invocation.
    private const string SourceCode = """
#file MSTestNativeAotTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>$TargetFramework$</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <PublishAot>true</PublishAot>
        <!-- Show individual trim/AOT warnings instead of a single IL2104 per assembly -->
        <TrimmerSingleWarn>false</TrimmerSingleWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

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

    [TestMethod]
    // The hosted AzDO agents for Mac OS don't have the required tooling for us to test Native AOT.
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    [DynamicData(nameof(TargetFrameworks.NetForDynamicData), typeof(TargetFrameworks))]
    public async Task NativeAotTests_WillRunWithExitCodeZero(string tfm)
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            $"MSTestNativeAotTests_{tfm}",
            SourceCode
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
            .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
            addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
            $"publish {generator.TargetAssetPath} -r {RID} -f {tfm}",
            cancellationToken: TestContext.CancellationToken);
        compilationResult.AssertOutputContains("Generating native code");

        var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "MSTestNativeAotTests", tfm, RID, Verb.publish);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        result.AssertOutputContains("Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3");
        result.AssertExitCodeIs(0);
    }

    public TestContext TestContext { get; set; }
}
