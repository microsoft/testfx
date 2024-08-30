// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public class TimeoutTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public TimeoutTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithoutLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.StandardError.Contains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithInvalidLetterSuffix_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5y");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.StandardError.Contains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithInvalidArg_WithInvalidFormat_OutputInvalidMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 5h6m");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.StandardError.Contains("'timeout' option should have one argument as string in the format <value>[h|m|s] where 'value' is float");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithValidArg_WithTestTimeOut_OutputContainsCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1s");

        testHostResult.AssertExitCodeIsNot(ExitCodes.Success);
        testHostResult.StandardOutput.Contains("Canceling the test session");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithValidArg_WithSecondAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 12.5s");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string output = testHostResult.StandardOutput;
        Assert.IsFalse(output.Contains("Canceling the test session"));
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithValidArg_WithMinuteAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1m");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string output = testHostResult.StandardOutput;
        Assert.IsFalse(output.Contains("Canceling the test session"));
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task TimeoutWithValidArg_WithHourAsSuffix_WithTestNotTimeOut_OutputDoesNotContainCancelingMessage(string tfm)
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.NoExtensionTargetAssetPath, TestAssetFixture.AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--timeout 1h");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        string output = testHostResult.StandardOutput;
        Assert.IsFalse(output.Contains("Canceling the test session"));
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AssetName = "TimeoutTest";

        private const string TestCode = """
#file TimeoutTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <GenerateProgramFile>false</GenerateProgramFile>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs

using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace TimeoutTest;
[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public void TestA()
    {
        Assert.IsTrue(true);
        Thread.Sleep(10000);
    }
}

""";

        public string NoExtensionTargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
