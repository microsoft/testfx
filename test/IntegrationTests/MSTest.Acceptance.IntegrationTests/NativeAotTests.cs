// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        <!--
            This makes sure that the project is referencing MSTest.TestAdapter.dll when MSTest.TestAdapter nuget is imported,
            without this the dll is just copied into the output folder.
        -->
        <EnableMSTestRunner>true</EnableMSTestRunner>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="MSTest.SourceGeneration" Version="$MSTestSourceGenerationVersion$" />
        <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
        <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
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

    private readonly AcceptanceFixture _acceptanceFixture;

    public NativeAotTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext) => _acceptanceFixture = acceptanceFixture;

    public async Task NativeAotTests_WillRunWithExitCodeZero()
        // The native AOT publication is pretty flaky and is often failing on CI with "fatal error LNK1136: invalid or corrupt file",
        // or sometimes doesn't fail but the native code generation is not done.
        // Retrying the restore/publish on fresh asset seems to be more effective than retrying on the same asset.
        => await RetryHelper.RetryAsync(
            async () =>
            {
                using TestAsset generator = await TestAsset.GenerateAssetAsync(
                    "NativeAotTests",
                    SourceCode
                    .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                    .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion)
                    .PatchCodeWithReplace("$TargetFramework$", TargetFrameworks.NetCurrent.Arguments)
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                    .PatchCodeWithReplace("$MSTestSourceGenerationVersion$", MSTestSourceGenerationVersion),
                    addPublicFeeds: true);

                await DotnetCli.RunAsync(
                    $"restore -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    retryCount: 0);
                DotnetMuxerResult compilationResult = await DotnetCli.RunAsync(
                    $"publish -m:1 -nodeReuse:false {generator.TargetAssetPath} -r {RID}",
                    _acceptanceFixture.NuGetGlobalPackagesFolder.Path,
                    retryCount: 0);
                compilationResult.AssertOutputContains("Generating native code");

                var testHost = TestHost.LocateFrom(generator.TargetAssetPath, "NativeAotTests", TargetFrameworks.NetCurrent.Arguments, RID, Verb.publish);

                TestHostResult result = await testHost.ExecuteAsync();
                result.AssertExitCodeIs(0);
            }, times: 15, every: TimeSpan.FromSeconds(5));
}
