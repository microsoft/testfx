// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class GeneratedMSTestHostingInjectionTests : AcceptanceTestBase<GeneratedMSTestHostingInjectionTests.TestAssetFixture>
{
    private const string AssetName = "GeneratedMSTestHostingInjectionTest";
    private static readonly string[] CompatibilityFrameworks =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["net462", "net8.0", "net9.0", "net10.0"]
            : ["net8.0", "net9.0", "net10.0"];

    public static IEnumerable<object[]> CompatibilityFrameworksForDynamicData { get; } =
        CompatibilityFrameworks.Select(static tfm => new object[] { tfm });

    public TestContext TestContext { get; set; } = null!;

    [DynamicData(nameof(CompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_InjectsHostServicesWithInvocationScopes(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory tempDirectory = new();
        string runSettingsPath = Path.Combine(tempDirectory.Path, "injection.runsettings");
        File.WriteAllText(runSettingsPath, """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>true</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        TestHostResult result = await testHost.ExecuteAsync(
            $"--settings \"{runSettingsPath}\"",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("Passed!");
        Assert.Contains("HOST_DI_VALIDATED", result.StandardError);
        Assert.Contains("RETRY_SCOPES=2", result.StandardError);
        Assert.Contains("PARALLEL_SCOPES=2", result.StandardError);
    }

    [DynamicData(nameof(CompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task GeneratedEntryPoint_DiscoverOnlySupportsInjectedConstructors(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        using TempDirectory tempDirectory = new();
        string runSettingsPath = Path.Combine(tempDirectory.Path, "injection.runsettings");
        File.WriteAllText(runSettingsPath, """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>true</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        TestHostResult result = await testHost.ExecuteAsync(
            $"--settings \"{runSettingsPath}\" --list-tests",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        result.AssertOutputContains("DataRowUsesFreshScope");
        Assert.Contains("HOST_DI_DISCOVERY_ONLY", result.StandardError);
    }

    [OSCondition(OperatingSystems.Windows)]
    [TestMethod]
    public async Task GeneratedEntryPoint_NetFrameworkExplicitAppDomainFailsWithActionableDiagnostic()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, "net462");
        using TempDirectory tempDirectory = new();
        string runSettingsPath = Path.Combine(tempDirectory.Path, "appdomain.runsettings");
        File.WriteAllText(runSettingsPath, """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>false</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """);

        TestHostResult result = await testHost.ExecuteAsync(
            $"--settings \"{runSettingsPath}\" --list-tests",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIsNot(0);
        Assert.Contains(
            "MSTest test-class injection from an application host cannot cross a .NET Framework AppDomain boundary.",
            result.StandardError);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file GeneratedMSTestHostingInjectionTest.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <RestorePackagesPath>$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
        <MicrosoftTestingPlatformVersion>$MicrosoftTestingPlatformVersion$</MicrosoftTestingPlatformVersion>
        <EnableMicrosoftTestingExtensionsCodeCoverage>false</EnableMicrosoftTestingExtensionsCodeCoverage>
        <TestingPlatformHostFactory>InjectedTestHost.CreateHost</TestingPlatformHostFactory>
        <EnableMSTestHostTestClassInjection>true</EnableMSTestHostTestClassInjection>
        <NoWarn>$(NoWarn);MSTESTEXP;TPEXP</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
        <PackageReference Include="MSTest.Extensions.Hosting" Version="$MSTestVersion$" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net462'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" Condition="'$(TargetFramework)' == 'net10.0'" />
        <PackageReference Include="System.Text.Json" Version="10.0.12" />
    </ItemGroup>
</Project>

#file Tests.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

internal static class InjectedTestHost
{
    public static Task<IHost> CreateHost()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddMSTestTestClassInjection();
        builder.Services.AddSingleton<InvocationTracker>();
        builder.Services.AddSingleton<ParallelGate>();
        builder.Services.AddScoped<InvocationScope>();
        builder.Services.AddHostedService<ValidationHostedService>();
        return Task.FromResult(builder.Build());
    }
}

public sealed class InvocationTracker
{
    private readonly ConcurrentDictionary<Guid, byte> _scopeIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _retryScopeIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _parallelScopeIds = new();

    public int CreatedScopes;
    public int DisposedScopes;
    public int InitializedTests;
    public int CleanedTests;
    public int AsyncDisposedTests;
    public int DisposedTests;
    public int ServiceOnlyTests;
    public int RetryAttempts;

    public int ScopeCount => _scopeIds.Count;
    public int RetryScopeCount => _retryScopeIds.Count;
    public int ParallelScopeCount => _parallelScopeIds.Count;

    public void RecordScope(Guid id)
    {
        if (!_scopeIds.TryAdd(id, 0))
        {
            throw new InvalidOperationException($"Scope '{id}' was reused.");
        }
    }

    public void RecordRetryScope(Guid id) => _retryScopeIds.TryAdd(id, 0);

    public void RecordParallelScope(Guid id) => _parallelScopeIds.TryAdd(id, 0);
}

public sealed class InvocationScope(InvocationTracker tracker) : IAsyncDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public void Activate()
    {
        tracker.RecordScope(Id);
        Interlocked.Increment(ref tracker.CreatedScopes);
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref tracker.DisposedScopes);
        return default;
    }
}

public sealed class ParallelGate
{
    private readonly CountdownEvent _arrivals = new(2);
    private readonly ManualResetEventSlim _release = new();

    public void ArriveAndWait()
    {
        if (_arrivals.Signal())
        {
            _release.Set();
        }

        if (!_release.Wait(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("Parallel test methods did not overlap.");
        }
    }
}

internal sealed class ValidationHostedService(InvocationTracker tracker) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (tracker.CreatedScopes == 0)
        {
            Console.Error.WriteLine("HOST_DI_DISCOVERY_ONLY");
            return Task.CompletedTask;
        }

        if (tracker.CreatedScopes != tracker.ScopeCount
            || tracker.CreatedScopes != tracker.DisposedScopes
            || tracker.InitializedTests != tracker.CleanedTests
            || tracker.AsyncDisposedTests != tracker.DisposedTests
            || tracker.ServiceOnlyTests != 1
            || tracker.RetryAttempts != 2
            || tracker.RetryScopeCount != 2
            || tracker.ParallelScopeCount != 2)
        {
            throw new InvalidOperationException(
                $"Invalid hosted DI lifecycle: created={tracker.CreatedScopes}, unique={tracker.ScopeCount}, disposed={tracker.DisposedScopes}, "
                + $"init={tracker.InitializedTests}, cleanup={tracker.CleanedTests}, asyncDispose={tracker.AsyncDisposedTests}, dispose={tracker.DisposedTests}, "
                + $"serviceOnly={tracker.ServiceOnlyTests}, retries={tracker.RetryAttempts}/{tracker.RetryScopeCount}, "
                + $"parallel={tracker.ParallelScopeCount}.");
        }

        Console.Error.WriteLine($"HOST_DI_VALIDATED scopes={tracker.ScopeCount}");
        Console.Error.WriteLine($"RETRY_SCOPES={tracker.RetryScopeCount}");
        Console.Error.WriteLine($"PARALLEL_SCOPES={tracker.ParallelScopeCount}");
        return Task.CompletedTask;
    }
}

[TestClass]
public sealed class InjectedLifecycleTests(TestContext testContext, InvocationScope scope, InvocationTracker tracker) : IAsyncDisposable, IDisposable
{
    private static int s_retryAttempts;

    public TestContext TestContext { get; } = testContext;

    [TestInitialize]
    public void Initialize()
    {
        scope.Activate();
        Interlocked.Increment(ref tracker.InitializedTests);
    }

    [DataRow(1)]
    [DataRow(2)]
    [TestMethod]
    public void DataRowUsesFreshScope(int value)
    {
        Assert.IsNotNull(TestContext);
        Assert.IsGreaterThan(0, value);
    }

    [Retry(1)]
    [TestMethod]
    public void RetryUsesFreshScope()
    {
        tracker.RecordRetryScope(scope.Id);
        int attempt = Interlocked.Increment(ref s_retryAttempts);
        Interlocked.Increment(ref tracker.RetryAttempts);
        if (attempt == 1)
        {
            Assert.Fail("Retry this invocation.");
        }
    }

    [TestCleanup]
    public void Cleanup() => Interlocked.Increment(ref tracker.CleanedTests);

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref tracker.AsyncDisposedTests);
        return default;
    }

    public void Dispose() => Interlocked.Increment(ref tracker.DisposedTests);
}

[TestClass]
public sealed class ServiceOnlyInjectedTests(InvocationScope scope, InvocationTracker tracker)
{
    [TestMethod]
    public void ServiceOnlyConstructorIsSupported()
    {
        scope.Activate();
        Interlocked.Increment(ref tracker.ServiceOnlyTests);
    }
}

[TestClass]
public sealed class ParameterlessTests
{
    [TestMethod]
    public void ExistingParameterlessBehaviorIsPreserved()
        => Assert.IsTrue(true);
}

[TestClass]
public sealed class ParallelInjectedTests(InvocationScope scope, InvocationTracker tracker, ParallelGate gate)
{
    [TestMethod]
    public void First()
    {
        scope.Activate();
        tracker.RecordParallelScope(scope.Id);
        gate.ArriveAndWait();
    }

    [TestMethod]
    public void Second()
    {
        scope.Activate();
        tracker.RecordParallelScope(scope.Id);
        gate.ArriveAndWait();
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(CompatibilityFrameworks)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion));
    }
}
