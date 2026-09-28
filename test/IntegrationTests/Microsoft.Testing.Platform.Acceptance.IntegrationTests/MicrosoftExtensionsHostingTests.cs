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
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
        new Dictionary<string, string?> { ["bridge:value"] = "from-host" }))
    .ConfigureServices(services => services.AddHostedService<MarkerHostedService>())
    .Build();

using (host)
{
    return await host.RunTestingPlatformAsync(args, tests =>
        tests.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new DummyTestFramework(serviceProvider)));
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

public sealed class DummyTestFramework(IServiceProvider serviceProvider) : ITestFramework
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

    public Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Console.WriteLine($"CONFIGURATION_VALUE={serviceProvider.GetConfiguration()["bridge:value"]}");
        context.Complete();
        return Task.CompletedTask;
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
