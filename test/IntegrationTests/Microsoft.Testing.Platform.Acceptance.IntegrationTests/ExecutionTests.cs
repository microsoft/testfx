// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ExecutionTests : AcceptanceTestBase<ExecutionTests.TestAssetFixture>
{
    private const string AssetName = "ExecutionTests";
    private const string FilterProviderAEnvironmentVariable = "MTP_TEST_FILTER_PROVIDER_A";
    private const string FilterProviderBEnvironmentVariable = "MTP_TEST_FILTER_PROVIDER_B";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsIsSpecified_AllTestsAreFound(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

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

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenUsingUidFilterForRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter-uid NonExistingUid", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 0", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        testHostResult = await testHost.ExecuteAsync("--filter-uid 0 --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenUsingUidFilterForDiscovery(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid NonExistingUid", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 0", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputMatchesRegex("""
              Test1
            Test discovery summary: found 1 test\(s\)
            """);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputMatchesRegex("""
              Test2
            Test discovery summary: found 1 test\(s\)
            """);

        testHostResult = await testHost.ExecuteAsync("--list-tests --filter-uid 0 --filter-uid 1", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(ExitCode.Success);
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

        testHostResult.AssertExitCodeIs(ExitCode.Success);

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

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenBothTreeNodeFilterAndUidFilterAreSpecified_ResultIsNotOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter-uid 0 --treenode-filter \"<whatever>\"", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Passing both '--treenode-filter' and '--filter-uid' is unsupported.");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndEnoughTestsRun_ResultIsOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 2", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputMatchesRegex($"Passed! - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndNotEnoughTestsRun_ResultIsNotOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests 3", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.MinimumExpectedTestsPolicyViolation);

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0, minimumNumberOfTests: 3);
        testHostResult.AssertOutputMatchesRegex($" - .*\\.(dll|exe) \\(net.+\\|.+\\)");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsNegative_ResultIsNotOk(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        // Use the '=' delimiter so the negative value is not parsed as a short option.
        TestHostResult testHostResult = await testHost.ExecuteAsync("--minimum-expected-tests=-1", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Option '--minimum-expected-tests' has invalid arguments: '--minimum-expected-tests' expects a single non-zero positive integer value");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenMinimumExpectedTestsIsSpecifiedAndNoTestsRun_ResultIsMinimumExpectedTestsPolicyViolation(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        // The uid filter matches no test, so zero tests run. With an explicit minimum, the count-based
        // verdict is a minimum-expected violation (exit code 9), not "zero tests ran" (exit code 8). See issue #7457.
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter-uid 2 --minimum-expected-tests 3", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.MinimumExpectedTestsPolicyViolation);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenListTestsAndMinimumExpectedTestsAreSpecified_DiscoveryFails(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--list-tests --minimum-expected-tests 2", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.InvalidCommandLine);
        testHostResult.AssertOutputContains("Error: '--list-tests' and '--minimum-expected-tests' are incompatible options");
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenFilterProviderIsEnabled_OnlyContributedUidRuns(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "0",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenTwoProvidersContributeDisjointUids_RunsNoTests(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "0",
                [FilterProviderBEnvironmentVariable] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenBuiltInAndProviderUidFiltersAreSpecified_UsesIntersection(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--filter-uid 0 --filter-uid 1",
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenProviderOnlyConstrainsRun_DiscoveryRemainsUnfiltered(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult discoveryResult = await testHost.ExecuteAsync(
            "--list-tests",
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "run:0",
            },
            cancellationToken: TestContext.CancellationToken);
        TestHostResult runResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "run:0",
            },
            cancellationToken: TestContext.CancellationToken);

        discoveryResult.AssertExitCodeIs(ExitCode.Success);
        discoveryResult.AssertOutputMatchesRegex(@"Test discovery summary: found 2 test\(s\)");
        runResult.AssertExitCodeIs(ExitCode.Success);
        runResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task Exec_WhenTreeAndProviderConstraintsAreDisjoint_RunsNoTests(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--treenode-filter \"<whatever>\"",
            environmentVariables: new Dictionary<string, string?>
            {
                [FilterProviderAEnvironmentVariable] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.ZeroTests);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
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
            (_, _) => new DummyTestFramework(myExtension));
        builder.AddTreeNodeFilterService(myExtension);
        builder.AddTestExecutionFilterProvider(_ => new EnvironmentFilterProvider("filter-provider-a", "MTP_TEST_FILTER_PROVIDER_A"));
        builder.AddTestExecutionFilterProvider(_ => new EnvironmentFilterProvider("filter-provider-b", "MTP_TEST_FILTER_PROVIDER_B"));
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

public sealed class EnvironmentFilterProvider(string uid, string environmentVariable) : ITestExecutionFilterProvider
{
    public string Uid => uid;

    public string Version => "1.0.0";

    public string DisplayName => uid;

    public string Description => uid;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(Environment.GetEnvironmentVariable(environmentVariable) is not null);

    public Task<ITestExecutionFilter?> GetFilterAsync(
        TestExecutionFilterContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? value = Environment.GetEnvironmentVariable(environmentVariable);
        if (value is null)
        {
            return Task.FromResult<ITestExecutionFilter?>(null);
        }

        string[] requestSpecificValue = value.Split(new[] { ':' }, 2);
        if (requestSpecificValue.Length == 2)
        {
            TestExecutionRequestKind requestedKind = requestSpecificValue[0] switch
            {
                "discovery" => TestExecutionRequestKind.Discovery,
                "run" => TestExecutionRequestKind.Run,
                _ => throw new InvalidOperationException($"Unknown request kind '{requestSpecificValue[0]}'."),
            };

            if (context.RequestKind != requestedKind)
            {
                return Task.FromResult<ITestExecutionFilter?>(null);
            }

            value = requestSpecificValue[1];
        }

        TestNodeUid[] uids =
        [
            .. value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(uidValue => new TestNodeUid(uidValue.Trim())),
        ];
        return Task.FromResult<ITestExecutionFilter?>(new TestNodeUidListFilter(uids));
    }
}

public class DummyTestFramework : ITestFramework, IDataProducer
{
    private readonly MyExtension _myExtension;

    public DummyTestFramework(MyExtension myExtension)
    {
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
        TestExecutionRequest request = (TestExecutionRequest)context.Request;
        bool isDiscovery = request is DiscoverTestExecutionRequest;
        bool excludeUid0 = !MatchesFilter(request.Filter, "0", "/<whatever>");
        bool excludeUid1 = !MatchesFilter(request.Filter, "1", "/other");

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

        if (!excludeUid1)
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

    private static bool MatchesFilter(ITestExecutionFilter filter, string uid, string path)
        => filter switch
        {
            NopFilter => true,
            TestNodeUidListFilter uidFilter => uidFilter.TestNodeUids.Any(nodeUid => nodeUid.Value == uid),
            TreeNodeFilter treeFilter => treeFilter.MatchesFilter(path, new PropertyBag()),
            CompositeTestExecutionFilter { Operator: TestExecutionFilterOperator.And } composite =>
                composite.Filters.All(childFilter => MatchesFilter(childFilter, uid, path)),
            _ => throw new NotSupportedException($"Unsupported filter '{filter.GetType()}'."),
        };
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                TestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
    }

    public TestContext TestContext { get; set; }
}
