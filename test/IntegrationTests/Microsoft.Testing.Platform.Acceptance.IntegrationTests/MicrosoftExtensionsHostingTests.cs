// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class MicrosoftExtensionsHostingTests : AcceptanceTestBase<MicrosoftExtensionsHostingTests.TestAssetFixture>
{
    private const string AssetName = "MicrosoftExtensionsHostingTest";
    private static readonly string[] HostingCompatibilityFrameworks =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["net462", "net8.0", "net9.0", "net10.0"]
            : ["net8.0", "net9.0", "net10.0"];

    public static IEnumerable<object[]> HostingCompatibilityFrameworksForDynamicData { get; } =
        HostingCompatibilityFrameworks.Select(static tfm => new object[] { tfm });

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task RunTestingPlatformAsync_ConsumesPackageAndOwnsHostLifecycle(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.ZeroTests);
        result.AssertOutputContains("HOST_STARTED");
        result.AssertOutputContains("CONFIGURATION_VALUE=from-host");
        result.AssertOutputContains("HOST_STOPPED");
    }

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task RunTestingPlatformAsync_CallerCancellation_CancelsActiveRun(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(
            environmentVariables: new() { ["HOSTING_CANCELLATION_MODE"] = "caller-token" },
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.TestSessionAborted);
        result.AssertOutputContains("TEST_STARTED");
        result.AssertOutputContains("TEST_CANCELLED");
        result.AssertOutputContains("HOST_STOPPED");
    }

    [DynamicData(nameof(HostingCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task RunTestingPlatformAsync_ApplicationStopping_CancelsControllerAndActiveTestHost(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        string markerPath = Path.Combine(Path.GetTempPath(), $"{nameof(MicrosoftExtensionsHostingTests)}-{Guid.NewGuid():N}.started");

        try
        {
            TestHostResult result = await testHost.ExecuteAsync(
                environmentVariables: new()
                {
                    ["HOSTING_CANCELLATION_MODE"] = "controller-host",
                    ["HOSTING_CANCELLATION_MARKER"] = markerPath,
                },
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(ExitCode.TestSessionAborted);
            result.AssertOutputContains("TEST_STARTED");
            result.AssertOutputContains("CONTROLLER_REQUESTED_STOP");
            result.AssertOutputContains("TEST_CANCELLED");
            result.AssertOutputContains("HOST_STOPPED");
        }
        finally
        {
            File.Delete(markerPath);
        }
    }

    public TestContext TestContext { get; set; } = null!;

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file MicrosoftExtensionsHostingTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <NoWarn>$(NoWarn);TPEXP</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.Hosting" Version="$MicrosoftTestingExtensionsHostingVersion$" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net462'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
        <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.12" Condition="'$(TargetFramework)' == 'net10.0'" />
        <PackageReference Include="System.Text.Json" Version="10.0.12" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Services;

string? cancellationMode = Environment.GetEnvironmentVariable("HOSTING_CANCELLATION_MODE");
string? cancellationMarker = Environment.GetEnvironmentVariable("HOSTING_CANCELLATION_MARKER");
bool isControlledTestHost = args.Any(static arg =>
    arg.TrimStart('-').Equals("internal-testhostcontroller-pid", StringComparison.OrdinalIgnoreCase));
using var callerCancellationTokenSource = new CancellationTokenSource();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
        new Dictionary<string, string?> { ["bridge:value"] = "from-host" }))
    .ConfigureServices(services =>
    {
        services.AddSingleton(new HostedCancellationOptions(cancellationMode, cancellationMarker, isControlledTestHost));
        services.AddHostedService<MarkerHostedService>();
        services.AddHostedService<ControllerCancellationHostedService>();
    })
    .Build();

using (host)
{
    return await host.RunTestingPlatformAsync(
        args,
        tests =>
        {
            tests.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
                (_, serviceProvider) => new DummyTestFramework(
                    serviceProvider,
                    cancellationMode,
                    cancellationMarker,
                    callerCancellationTokenSource));

            if (cancellationMode == "controller-host")
            {
                tests.TestHostControllers.AddEnvironmentVariableProvider(_ => new ForceControllerEnvironmentVariableProvider());
            }
        },
        cancellationMode == "caller-token" ? callerCancellationTokenSource.Token : CancellationToken.None);
}

public sealed class HostedCancellationOptions(string? mode, string? markerPath, bool isControlledTestHost)
{
    public string? Mode { get; } = mode;
    public string? MarkerPath { get; } = markerPath;
    public bool IsControlledTestHost { get; } = isControlledTestHost;
}

public sealed class MarkerHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("HOST_STARTED");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("HOST_STOPPED");
        return Task.CompletedTask;
    }
}

public sealed class ControllerCancellationHostedService(
    HostedCancellationOptions options,
    IHostApplicationLifetime hostApplicationLifetime) : IHostedService
{
    private Task _monitorTask = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options is { Mode: "controller-host", IsControlledTestHost: false, MarkerPath: not null })
        {
            _monitorTask = MonitorTestStartAsync(options.MarkerPath, hostApplicationLifetime);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => _monitorTask;

    private static async Task MonitorTestStartAsync(string markerPath, IHostApplicationLifetime hostApplicationLifetime)
    {
        try
        {
            while (!File.Exists(markerPath))
            {
                await Task.Delay(20, hostApplicationLifetime.ApplicationStopping);
            }

            Console.WriteLine("CONTROLLER_REQUESTED_STOP");
            hostApplicationLifetime.StopApplication();
        }
        catch (OperationCanceledException) when (hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
        }
    }
}

public sealed class ForceControllerEnvironmentVariableProvider : ITestHostEnvironmentVariableProvider
{
    public string Uid => nameof(ForceControllerEnvironmentVariableProvider);
    public string Version => "1.0.0";
    public string DisplayName => nameof(ForceControllerEnvironmentVariableProvider);
    public string Description => nameof(ForceControllerEnvironmentVariableProvider);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(new("HOSTING_CONTROLLER_ACTIVE", "1", isSecret: false, isLocked: false));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
        => ValidationResult.ValidTask;
}

public sealed class DummyTestFramework(
    IServiceProvider serviceProvider,
    string? cancellationMode,
    string? cancellationMarker,
    CancellationTokenSource callerCancellationTokenSource) : ITestFramework
{
    public string Uid => nameof(DummyTestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(DummyTestFramework);
    public string Description => nameof(DummyTestFramework);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Console.WriteLine($"CONFIGURATION_VALUE={serviceProvider.GetConfiguration()["bridge:value"]}");
        if (cancellationMode is null)
        {
            context.Complete();
            return;
        }

        Console.WriteLine("TEST_STARTED");
        if (cancellationMode == "caller-token")
        {
            callerCancellationTokenSource.Cancel();
        }
        else if (cancellationMode == "controller-host")
        {
            File.WriteAllText(cancellationMarker!, "started");
        }

        try
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("TEST_CANCELLED");
        }
        finally
        {
            context.Complete();
        }
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(HostingCompatibilityFrameworks)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsHostingVersion$", MicrosoftTestingExtensionsHostingVersion));
    }
}
