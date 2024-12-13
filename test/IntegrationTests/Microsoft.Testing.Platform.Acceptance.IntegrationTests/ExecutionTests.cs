// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ExecutionTests : AcceptanceTestBase<ExecutionTests.TestAssetFixture>
{
    private const string AssetName = "ExecutionTests";
    private const string AssetName2 = "ExecutionTests2";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsIsSpecified_AllTestsAreFound(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
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

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenOnlyAssetNameIsSpecified_AllTestsAreRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
        testHostResult.AssertOutputContains($"! - {AssetFixture.TargetAssetPath}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsAndFilterAreSpecified_OnlyFilteredTestsAreFound(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
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

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenFilterIsSpecified_OnlyFilteredTestsAreRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--treenode-filter \"/ExecutionTests/ExecutionTests/UnitTest1/TestMethod*\"");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
        testHostResult.AssertOutputContains($"! - {AssetFixture.TargetAssetPath}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndEnoughTestsRun_ResultIsOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 4");

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
        testHostResult.AssertOutputContains($"! - {AssetFixture.TargetAssetPath}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndNotEnoughTestsRun_ResultIsNotOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 5");

        testHostResult.AssertExitCodeIs(ExitCodes.MinimumExpectedTestsPolicyViolation);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0, minimumNumberOfTests: 5);
        testHostResult.AssertOutputContains($" - {AssetFixture.TargetAssetPath}");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsAndMinimumExpectedTestsAreSpecified_DiscoveryFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --minimum-expected-tests 4");

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--list-tests' and '--minimum-expected-tests' are incompatible options");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_Honor_Request_Complete(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath2, AssetName2, tfm);
        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        stopwatch.Stop();
        Assert.AreEqual(ExitCodes.Success, testHostResult.ExitCode);
        Assert.IsTrue(stopwatch.Elapsed.TotalSeconds > 3);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
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
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestFramework : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestFramework);

    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
       context.Complete();
       return Task.CompletedTask;
    }
}

#file UnitTest1.cs
namespace ExecutionTests;

[TestClass]
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
global using Microsoft.Testing.Extensions;
""";

        private const string TestCode2 = """
#file ExecutionTests2.csproj
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
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using System.Threading.Tasks;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new Capabilities(), (_, __) => new DummyTestFramework());
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

internal class DummyTestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DummyTestFramework);

    public string Version => string.Empty;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Task.Run(async() =>
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode() { Uid = "0", DisplayName = "Test", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));

            Thread.Sleep(3_000);

            context.Complete();
        });

        return Task.CompletedTask;
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

internal class Capabilities : ITestFrameworkCapabilities
{
    IReadOnlyCollection<ITestFrameworkCapability> ICapabilities<ITestFrameworkCapability>.Capabilities => Array.Empty<ITestFrameworkCapability>();
}

""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public string TargetAssetPath2 => GetAssetPath(AssetName2);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));

            yield return (AssetName2, AssetName2,
                TestCode2
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));
        }
    }
}
