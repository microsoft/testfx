// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class ExecutionTests : AcceptanceTestBase
{
    private const string AssetName = "ExecutionTests";
    private readonly TestAssetFixture _testAssetFixture;

    public ExecutionTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenListTestsIsSpecified_AllTestsAreFound(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = """
The following Tests are available:
TestMethod1
TestMethod2
TestMethod3
FilteredOutTest$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenOnlyAssetNameIsSpecified_AllTestsAreRun(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = $"""
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: .+s - {AssetName}.+$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenListTestsAndFilterAreSpecified_OnlyFilteredTestsAreFound(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --treenode-filter \"/ExecutionTests/ExecutionTests/UnitTest1/TestMethod*\"");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = """
The following Tests are available:
TestMethod1
TestMethod2
TestMethod3$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenFilterIsSpecified_OnlyFilteredTestsAreRun(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--treenode-filter \"/ExecutionTests/ExecutionTests/UnitTest1/TestMethod*\"");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = $"""
Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3, Duration: .+s - {AssetName}.+$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndEnoughTestsRun_ResultIsOk(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 4");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = $"""
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: .+s - {AssetName}.+$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndNotEnoughTestsRun_ResultIsNotOk(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 5");

        testHostResult.AssertExitCodeIs(ExitCodes.MinimumExpectedTestsPolicyViolation);

        const string OutputPattern = $"""
Minimum expected tests policy violation, tests ran 4, minimum expected 5 - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: .+s - {AssetName}.+$
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task Exec_WhenListTestsAndMinimumExpectedTestsAreSpecified_DiscoveryFails(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --minimum-expected-tests 4");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);

        const string OutputPattern = "Error: '--list-tests' and '--minimum-expected-tests' are incompatible options";
        Assert.That(testHostResult.StandardOutput.Contains(OutputPattern), $"Output of the test host is:\n{testHostResult}");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string TestCode = """
#file ExecutionTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Framework" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Framework.SourceGeneration" Version="$MicrosoftTestingPlatformExtensionsVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using ExecutionTests;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace ExecutionTests;

[TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        Assert.IsTrue(true);
    }

    public void TestMethod2()
    {
        Assert.IsTrue(true);
    }

    public void TestMethod3()
    {
        Assert.IsTrue(true);
    }

    public void FilteredOutTest()
    {
        Assert.IsTrue(true);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Framework;
global using Microsoft.Testing.Extensions;
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformExtensionsVersion$", MicrosoftTestingPlatformExtensionsVersion));
        }
    }
}
