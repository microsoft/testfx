// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class MaxFailedTestsExtensionTests : AcceptanceTestBase
{
    private const string AssetName = nameof(MaxFailedTestsExtensionTests);
    private readonly TestAssetFixture _testAssetFixture;

    public MaxFailedTestsExtensionTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task SimpleMaxFailedTestsScenario(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--maximum-failed-tests 3");
        testHostResult.AssertExitCodeIs(ExitCodes.TestExecutionStoppedForMaxFailedTests);

        int total = int.Parse(Regex.Match(testHostResult.StandardOutput, @"total: (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);

        // We can't know the number of tests that will be executed exactly due to the async
        // nature of publish/consume on the platform side. But we expect the cancellation to
        // happen "fast" enough that we don't execute all tests.
        Assert.IsTrue(total < 12);
        Assert.IsTrue(total >= 5);

        testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);

        total = int.Parse(Regex.Match(testHostResult.StandardOutput, @"total: (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.AreEqual(12, total);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file MaxFailedTestsExtensionTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void Test1()
    {
        Assert.Fail();
    }

    [TestMethod]
    public void Test2()
    {
    }

    [TestMethod]
    public void Test3()
    {
    }

    [TestMethod]
    public void Test4()
    {
        Assert.Fail();
    }

    [TestMethod]
    public void Test5()
    {
        Assert.Fail();
    }

    [TestMethod]
    public async Task Test6()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test7()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test8()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test9()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test10()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test11()
    {
        await Task.Delay(10);
    }

    [TestMethod]
    public async Task Test12()
    {
        await Task.Delay(10);
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
