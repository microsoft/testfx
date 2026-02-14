// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = """
  Test1
  Test2
Test discovery summary: found 2 test\(s\)\ - .*\.(dll|exe) \(net.+\|.+\)
  duration:
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenOnlyAssetNameIsSpecified_AllTestsAreRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenUsingUidFilterForRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter-uid NonExistingUid", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 0", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 0 --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenUsingUidFilterForDiscovery(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid NonExistingUid", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 0", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputMatchesRegex("""
              Test1
            Test discovery summary: found 1 test\(s\)
            """);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputMatchesRegex("""
              Test2
            Test discovery summary: found 1 test\(s\)
            """);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 0 --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputMatchesRegex("""
              Test1
              Test2
            Test discovery summary: found 2 test\(s\)
            """);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsAndFilterAreSpecified_OnlyFilteredTestsAreFound(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --treenode-filter \"<whatever>\"", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        const string OutputPattern = """
  Test1
Test discovery summary: found 1 test\(s\)\ - .*\.(dll|exe) \(net.+\|.+\)
  duration:
""";
        testHostResult.AssertOutputMatchesRegex(OutputPattern);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenFilterIsSpecified_OnlyFilteredTestsAreRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--treenode-filter \"<whatever>\"", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndEnoughTestsRun_ResultIsOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 2", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndNotEnoughTestsRun_ResultIsNotOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 3", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.MinimumExpectedTestsPolicyViolation);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0, minimumNumberOfTests: 3);
        testHostResult.AssertOutputMatchesRegex($" - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsAndMinimumExpectedTestsAreSpecified_DiscoveryFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --minimum-expected-tests 2", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--list-tests' and '--minimum-expected-tests' are incompatible options");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_Honor_Request_Complete(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath2, AssetName2, tfm);
        var stopwatch = Stopwatch.StartNew();
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        stopwatch.Stop();
        Assert.AreEqual(ExitCodes.Success, testHostResult.ExitCode);
        Assert.IsGreaterThan(3, stopwatch.Elapsed.TotalSeconds);
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
using System.Linq;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        MyExtension myExtension = new();
        builder.RegisterTestFramework(
            sp => new TestFrameworkCapabilities(),
            (_,sp) => new DummyTestFramework(sp, myExtension));
        builder.AddTreeNodeFilterService(myExtension);
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class MyExtension : IExtension
{
    public string Uid => "MyExtension";
    public string Version => "1.0.0";
    public string DisplayName => "My Extension";
    public string Description => "My Extension Description";
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    private IServiceProvider _sp;
    private MyExtension _myExtension;

    public DummyTestFramework(IServiceProvider sp, MyExtension myExtension)
    {
        _sp = sp;
        _myExtension = myExtension;
    }

    public string Uid => _myExtension.Uid;

    public string Version => _myExtension.Version;

    public string DisplayName => _myExtension.DisplayName;

    public string Description => _myExtension.Description;

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => _myExtension.IsEnabledAsync();

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // Test runner should be able to return discovery events during discovery, and also run and discovery events during run.
        // Simulate that here.
        bool isDiscovery = _sp.GetCommandLineOptions().TryGetOptionArgumentList("--list-tests", out _);

        var uidListFilter = ((TestExecutionRequest)context.Request).Filter as TestNodeUidListFilter;

        // If --filter-uid is used, but it doesn't contain a given Uid, then don't publish TestNodeUpdateMessage for that Uid.
        var excludeUid0 = uidListFilter is not null && !uidListFilter.TestNodeUids.Any(n => n.Value == "0");
        var excludeUid1 = uidListFilter is not null && !uidListFilter.TestNodeUids.Any(n => n.Value == "1");

        if (!excludeUid0)
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));
        }

        if (!isDiscovery && !excludeUid0)
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "0", DisplayName = "Test1", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
        }

        if (!_sp.GetCommandLineOptions().TryGetOptionArgumentList("--treenode-filter", out _) && !excludeUid1)
        {
            await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                new TestNode() { Uid = "1", DisplayName = "Test2", Properties = new(DiscoveredTestNodeStateProperty.CachedInstance) }));

            if (!isDiscovery)
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid,
                    new TestNode() { Uid = "1", DisplayName = "Test2", Properties = new(PassedTestNodeStateProperty.CachedInstance) }));
            }
        }

        context.Complete();
    }
}
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
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));

            yield return (AssetName2, AssetName2,
                TestCode2
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
