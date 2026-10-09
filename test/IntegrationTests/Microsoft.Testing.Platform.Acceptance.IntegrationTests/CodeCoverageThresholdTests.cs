// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class CodeCoverageThresholdTests : AcceptanceTestBase<CodeCoverageThresholdTests.TestAssetFixture>
{
    private const string AssetName = "CodeCoverageThresholdConsumer";

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task LineThreshold_WithoutEnabledCollector_FailsRatherThanSkippingRequirement()
    {
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        TestHostResult result = await testHost.ExecuteAsync(
            "--coverage-threshold-line 0",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.CoverageThresholdFailed);
        result.AssertOutputContains("Enable a compatible coverage collector.");
    }

    [TestMethod]
    [DataRow("0", (int)ExitCode.Success)]
    [DataRow("100", (int)ExitCode.CoverageThresholdFailed)]
    public async Task LineThreshold_WithMicrosoftCollector_EvaluatesCoverageAndPreservesReport(string threshold, int expectedExitCode)
    {
        using TempDirectory results = new();
        string report = Path.Combine(results.Path, "coverage.cobertura.xml");
        var testHost = TestInfrastructure.TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);

        TestHostResult result = await testHost.ExecuteAsync(
            $"--coverage --coverage-output-format cobertura --coverage-output \"{report}\" --results-directory \"{results.Path}\" --coverage-threshold-line {threshold}",
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs((ExitCode)expectedExitCode);
        result.AssertOutputContains("Coverage Threshold Results:");
        result.AssertOutputContains("Total - Line:");
        Assert.IsTrue(File.Exists(report), result.ToString());
        var coverage = XDocument.Load(report);
        Assert.IsNotEmpty(coverage.Descendants("line"));
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate()
        {
            string collectorVersion = XDocument.Load(Path.Combine(RootFinder.Find(), "eng", "Versions.props"))
                .Descendants("MicrosoftTestingExtensionsCodeCoverageVersion").Single().Value;

            return (AssetName, AssetName, Source
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$CodeCoverageVersion$", collectorVersion));
        }

        private const string Source = """
#file CodeCoverageThresholdConsumer.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="$CodeCoverageVersion$" />
    <ProjectReference Include="Product\Product.csproj" />
    <Compile Remove="Product\**\*.cs" />
  </ItemGroup>
</Project>

#file Product/Product.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <AssemblyName>CoverageThresholdProduct</AssemblyName>
  </PropertyGroup>
</Project>

#file Program.cs
using System;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.AddCodeCoverageProvider();
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new TestFramework());
        using ITestApplication app = await builder.BuildAsync();
        return await app.RunAsync();
    }
}

public sealed class TestFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(TestFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(TestFramework);
    public string Description => nameof(TestFramework);
    public Type[] DataTypesProduced => new[] { typeof(TestNodeUpdateMessage) };
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        int value = Product.Classify(1);
        await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, new TestNode
        {
            Uid = "test",
            DisplayName = "test",
            Properties = new PropertyBag(value == 1 ? PassedTestNodeStateProperty.CachedInstance : new FailedTestNodeStateProperty()),
        }));
        context.Complete();
    }
}

#file Product/Product.cs
public static class Product
{
    public static int Classify(int value)
    {
        if (value > 0)
        {
            return 1;
        }

        return -1;
    }

    public static int Uncalled()
    {
        return 42;
    }
}
""";
    }
}
