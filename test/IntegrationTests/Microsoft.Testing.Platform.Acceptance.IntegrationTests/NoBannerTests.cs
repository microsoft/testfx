// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class NoBannerTests : BaseAcceptanceTests
{
    private const string AssetName = "NoBannerTest";
    private readonly BuildFixture _buildFixture;
    private readonly string _bannerRegexMatchPattern = """
\s*Microsoft\(R\) Testing Platform Execution Command Line Tool.*
\s*Version:.*
\s*RuntimeInformation:.*
\s*Copyright\(c\) Microsoft Corporation[.]  All rights reserved[.].*
""";

    public NoBannerTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture, BuildFixture buildFixture)
        : base(testExecutionContext, acceptanceFixture)
    {
        _buildFixture = buildFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task UsingNoBanner_TheBannerDoesNotAppear(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--no-banner");

        testHostResult.AssertHasExitCode(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotMatchRegex(_bannerRegexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task UsingNoBanner_InTheEnvironmentVars_TheBannerDoesNotAppear(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { "TESTINGPLATFORM_NOBANNER", "true" },
            });

        testHostResult.AssertHasExitCode(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotMatchRegex(_bannerRegexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task UsingDotnetNoLogo_InTheEnvironmentVars_TheBannerDoesNotAppear(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            null,
            new Dictionary<string, string>()
            {
                { "DOTNET_NOLOGO", "true" },
            });

        testHostResult.AssertHasExitCode(ExitCodes.ZeroTests);
        testHostResult.AssertOutputDoesNotMatchRegex(_bannerRegexMatchPattern);
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task WithoutUsingNoBanner_TheBannerAppears(string tfm)
    {
        TestInfrastructure.TestHost testHost = TestInfrastructure.TestHost.LocateFrom(_buildFixture.TargetAssetPath, AssetName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        testHostResult.AssertHasExitCode(ExitCodes.ZeroTests);
        testHostResult.AssertOutputMatchesRegex(_bannerRegexMatchPattern);
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class BuildFixture : IAsyncInitializable, IDisposable
    {
        private readonly AcceptanceFixture _acceptanceFixture;
        private TestAsset? _testAsset;

        public string TargetAssetPath => _testAsset!.TargetAssetPath;

        public BuildFixture(AcceptanceFixture acceptanceFixture)
        {
            _acceptanceFixture = acceptanceFixture;
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            _testAsset = await TestAsset.GenerateAssetAsync(
                AssetName,
                NoBannerTestCode.PatchCodeWithRegularExpression("tfms", TargetFrameworks.All.ToJoinedFrameworks()));
            await DotnetCli.RunAsync($"build -nodeReuse:false {_testAsset.TargetAssetPath} -c Release", _acceptanceFixture.NuGetGlobalPackagesFolder);
        }

        public void Dispose() => _testAsset?.Dispose();
    }

    private const string NoBannerTestCode = """
#file NoBannerTest.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>tfms</TargetFrameworks>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform" Version="[1.0.0-*,)" />
    </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_,__) => new DummyTestAdapter());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public class DummyTestAdapter : ITestFramework
{
    public string Uid => nameof(DummyTestAdapter);

    public string Version => "2.0.0";

    public string DisplayName => nameof(DummyTestAdapter);

    public string Description => nameof(DummyTestAdapter);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });
    public Task ExecuteRequestAsync(ExecuteRequestContext context) => Task.CompletedTask;
}
""";
}
