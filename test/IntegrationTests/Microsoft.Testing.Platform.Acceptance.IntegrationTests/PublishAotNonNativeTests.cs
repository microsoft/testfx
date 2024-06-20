// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// When PublishAOT=true is set on a project, it will set IsDynamicCodeSupported to false, but the code will still run as managed
/// and VSTest is still able to find tests in the dll.
/// </summary>
[TestGroup]
public sealed class PublishAotNonNativeTests : AcceptanceTestBase
{
    private const string AssetName = "PublishAotNonNative";

    public PublishAotNonNativeTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    public async Task RunTests_ThatEnablePublishAOT_ButDontBuildToNative()
    {
        using TestAsset generator = await TestAsset.GenerateAssetAsync(
               AssetName,
               SourceCode
               .PatchCodeWithReplace("$TargetFramework$", $"<TargetFramework>{TargetFrameworks.NetCurrent.Arguments}</TargetFramework>")
               .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
               .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion),
               addPublicFeeds: true);

        DotnetMuxerResult compilationResult = await DotnetCli.RunAsync($"test -c Debug {generator.TargetAssetPath}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path, failIfReturnValueIsNotZero: false);

        // In the real-world issue, access to path C:\Program Files\dotnet\ is denied, but we run this from a local .dotnet folder, where we have write access.
        // So instead of relying on the test run failing because of AccessDenied, we check the output, and see where TestResults were placed.
        // They must not be next to the local dotnet.exe.
        string testsFailed = "error run failed: Tests failed:";
        compilationResult.AssertOutputContains(testsFailed);
        string failedResultsLine = compilationResult.StandardOutputLines.Single(l => l.Contains(testsFailed));
        if (failedResultsLine.Contains("dotnet"))
        {
            Assert.Fail($"TestResults should be placed next to the tested dll or exe, and not next to dotnet.exe, this is an error in determining path of the tested module.\nStandard output of test:{compilationResult.StandardOutput}");
        }
    }

    private const string SourceCode = """
#file PublishAotNonNative.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <EnableMSTestRunner>true</EnableMSTestRunner>
        <PublishAot>true</PublishAot>
        <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>


         $TargetFramework$
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <NoWarn>$(NoWarn);NU1507</NoWarn>
        <PlatformTarget>x64</PlatformTarget>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="MSTest" Version="$MSTestVersion$" />
    </ItemGroup>
</Project>

#file UnitTest1.cs

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTestSdkTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.Fail();
        }
    }
}
""";

    private readonly AcceptanceFixture _acceptanceFixture;
}
