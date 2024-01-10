// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
internal class AssemblyAndClassInitializeTimeout : AcceptanceTestBase
{
    private readonly string _assetName = "AssemblyAndClassInitializeTimeout";
    private readonly AcceptanceFixture _acceptanceFixture;

    public AssemblyAndClassInitializeTimeout(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    [ArgumentsProvider(nameof(GetBuildMatrixTfmBuildConfiguration))]
    public async Task AssemblyInitialize_Timeout_Should_Stop_Execution(string tfm, BuildConfiguration buildConfiguration)
    {
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(_assetName, SourceCode
            .PatchCodeWithReplace("$TargetFramework$", tfm)
            .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion), addPublicFeeds: true);
        DotnetMuxerResult result = await DotnetCli.RunAsync($"build -nodeReuse:false {testAsset.TargetAssetPath} -c {buildConfiguration}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        var testHost = TestInfrastructure.TestHost.LocateFrom(testAsset.TargetAssetPath, _assetName, tfm, buildConfiguration: buildConfiguration);

        foreach (string initMethodToTimeout in new[] { "TIMEOUT_CLASSINIT", "TIMEOUT_ASSEMBLYINIT", "TIMEOUT_BASE_CLASSINIT" })
        {
            var testhostResult = await testHost.ExecuteAsync(environmentVariables: new Dictionary<string, string>() { { initMethodToTimeout, "1" } });

            if (initMethodToTimeout == "TIMEOUT_CLASSINIT")
            {
                testhostResult.AssertOutputContains("Class Initialization method TestClass.ClassInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
                continue;
            }

            if (initMethodToTimeout == "TIMEOUT_ASSEMBLYINIT")
            {
                testhostResult.AssertOutputContains("Assembly Initialization method TestClass.AssemblyInit threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled.. Aborting test execution.");
                continue;
            }

            if (initMethodToTimeout == "TIMEOUT_BASE_CLASSINIT")
            {
                testhostResult.AssertOutputContains("Class Initialization method TestClass.ClassInitBase threw exception. System.Threading.Tasks.TaskCanceledException: A task was canceled..");
                continue;
            }
        }
    }

    protected const string SourceCode = """
#file AssemblyAndClassInitializeTimeout.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <PlatformTarget>x64</PlatformTarget>
    <TargetFramework>$TargetFramework$</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestClassBase
{
    [ClassInitialize(inheritanceBehavior: InheritanceBehavior.BeforeEachDerivedClass)]
    [Timeout(2000)]
    public static async Task ClassInitBase(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_BASE_CLASSINIT") == "1")
        {
            await Task.Delay(10_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

}

[TestClass]
public class TestClass : TestClassBase
{
    [AssemblyInitialize]
    [Timeout(2000)]
    public static async Task AssemblyInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_ASSEMBLYINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [ClassInitialize]
    [Timeout(2000)]
    public static async Task ClassInit(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("TIMEOUT_CLASSINIT") == "1")
        {
            await Task.Delay(60_000, testContext.CancellationTokenSource.Token);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [TestMethod]
    public Task Test1() => Task.CompletedTask;
}
""";
}
