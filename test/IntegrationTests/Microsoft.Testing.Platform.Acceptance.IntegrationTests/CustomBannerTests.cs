// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class CustomBannerTests : AcceptanceTestBase<CustomBannerTests.TestAssetFixture>
{
    private const string AssetName = "CustomBannerTest";

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task UsingNoBanner_TheBannerDoesNotAppear(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--no-banner");

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain(TestAssetFixture.CustomBannerPrefix);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task UsingNoBanner_InTheEnvironmentVars_TheBannerDoesNotAppear(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { "TESTINGPLATFORM_NOBANNER", "true" },
            });

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain(TestAssetFixture.CustomBannerPrefix);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task UsingDotnetNoLogo_InTheEnvironmentVars_TheBannerDoesNotAppear(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string?>
            {
                { "DOTNET_NOLOGO", "true" },
            });

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotContain(TestAssetFixture.CustomBannerPrefix);
    }

    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    [TestMethod]
    public async Task WithoutUsingNoBanner_TheBannerAppears(string tfm)
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputMatchesRegex($"{TestAssetFixture.CustomBannerPrefix} Platform info: Name: Microsoft.Testing.Platform, Version: .+?, Hash: .*?");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string CustomBannerPrefix = "Custom banner |";

        private const string CustomBannerTestCode = $$"""
#file CustomBannerTest.csproj
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
using System.Text;
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
            sp => new TestFrameworkCapabilities(new DummyBannerMessageOwnerCapability(sp)),
            (_,__) => new DummyTestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class DummyBannerMessageOwnerCapability : IBannerMessageOwnerCapability
{
    private readonly IServiceProvider _serviceProvider;

    public DummyBannerMessageOwnerCapability(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<string?> GetBannerMessageAsync()
    {
        var platformInformation = _serviceProvider.GetRequiredService<IPlatformInformation>();
        StringBuilder sb = new();
        sb.Append("{{CustomBannerPrefix}} Platform info: ");
        sb.Append($"Name: {platformInformation.Name}");
        sb.Append($", Version: {platformInformation.Version}");
        sb.Append($", Hash: {platformInformation.CommitHash}");

        return Task.FromResult<string?>(sb.ToString());
    }
}
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

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
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                CustomBannerTestCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion));
        }
    }
}
