// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class AssemblyResolverTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;
    private const string AssetName = "AssemblyResolverCrash";

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public AssemblyResolverTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    public async Task DiscoverTests_FindsAllTests()
    {
        var testHost =  TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetFramework[0].Arguments);

        var testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("Test1");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetFramework)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file AssemblyResolverCrash.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RecursiveResourceLookupCrash
{
    [TestClass]
    public class RecursiveResourceLookupCrashTests
    {
        [TestInitialize]
        public void TestInitialize()
        {

        }

        [TestMethod]
        public void CrashesOnResourcesLookupWhenNotHandledByAssemblyResolver()
        {
            // You need to set non-English culture explicitly to reproduce recursive resource
            // lookup bug in English environment.
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");

            try
            {
                // This will internally trigger file not found exception, and try to find ja-JP resources
                // for the string, which will trigger Resolve in AssemblyResolver, which will
                // use File.Exists call and that will trigger another round of looking up ja-JP
                // resources, until this is detected by .NET Framework, and Environment.FailFast
                // is called to crash the testhost.
                var stream = new IsolatedStorageFileStream("non-existent-filename", FileMode.Open);
            }
            catch (Exception)
            {
            }
        }
    }
}
""";
    }
}
