// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class MicrosoftExtensionsConfigurationTests : AcceptanceTestBase<MicrosoftExtensionsConfigurationTests.TestAssetFixture>
{
    private const string AssetName = "MicrosoftExtensionsConfigurationTest";
    private static readonly string[] ConfigurationCompatibilityFrameworks =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["net462", "net8.0", "net9.0", "net10.0"]
            : ["net8.0", "net9.0", "net10.0"];

    public static IEnumerable<object[]> ConfigurationCompatibilityFrameworksForDynamicData { get; } =
        ConfigurationCompatibilityFrameworks.Select(static tfm => new object[] { tfm });

    [DynamicData(nameof(ConfigurationCompatibilityFrameworksForDynamicData))]
    [TestMethod]
    public async Task ConfigurationSnapshot_ConsumesPackageAndCapturesAtBuildTime(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);

        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.ZeroTests);
        result.AssertOutputContains("CONFIGURATION_VALUE=external-before-build");
        result.AssertOutputDoesNotContain("CONFIGURATION_VALUE=json");
        result.AssertOutputDoesNotContain("CONFIGURATION_VALUE=external-after-build");
    }

    public TestContext TestContext { get; set; } = null!;

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        private const string TestCode = """
#file MicrosoftExtensionsConfigurationTest.csproj
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
        <PackageReference Include="Microsoft.Testing.Extensions.Configuration" Version="$MicrosoftTestingExtensionsConfigurationVersion$" />
        <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" Condition="'$(TargetFramework)' == 'net462'" />
        <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" Condition="'$(TargetFramework)' == 'net8.0'" />
        <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
        <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0" Condition="'$(TargetFramework)' == 'net10.0'" />
    </ItemGroup>
    <ItemGroup>
        <None Update="$(AssemblyName).testconfig.json" CopyToOutputDirectory="PreserveNewest" />
    </ItemGroup>
</Project>

#file MicrosoftExtensionsConfigurationTest.testconfig.json
{
  "bridge": {
    "value": "json"
  }
}

#file Program.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Microsoft.Extensions.Configuration.IConfigurationRoot externalConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["bridge:value"] = "external-before-build",
            })
            .Build();

        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddMicrosoftExtensionsConfigurationSnapshot(externalConfiguration);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, serviceProvider) => new DummyTestFramework(serviceProvider));

        using ITestApplication app = await builder.BuildAsync();
        externalConfiguration["bridge:value"] = "external-after-build";
        return await app.RunAsync();
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
        Microsoft.Testing.Platform.Configurations.IConfiguration configuration = serviceProvider.GetConfiguration();
        Console.WriteLine($"CONFIGURATION_VALUE={configuration["bridge:value"]}");
        context.Complete();
        return Task.CompletedTask;
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
            TestCode
                .PatchTargetFrameworks(ConfigurationCompatibilityFrameworks)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsConfigurationVersion$", MicrosoftTestingExtensionsConfigurationVersion));
    }
}
