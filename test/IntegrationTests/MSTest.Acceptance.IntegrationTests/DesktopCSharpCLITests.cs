// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance-style rewrite of the former CLITestBase-based desktop tests. Instead of referencing
/// pre-built desktop test-asset projects, the test asset is generated inline and built for every
/// (platform, configuration) combination, then executed out-of-process through the MSTest runner
/// (Microsoft.Testing.Platform) host.
/// </summary>
[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class DesktopCSharpCLITests : AcceptanceTestBase<DesktopCSharpCLITests.TestAssetFixture>
{
    private static readonly string[] Platforms = ["x86", "x64"];

    public TestContext TestContext { get; set; } = default!;

    public static IEnumerable<object[]> PlatformAndConfiguration()
    {
        foreach (string platform in Platforms)
        {
            foreach (BuildConfiguration configuration in new[] { BuildConfiguration.Debug, BuildConfiguration.Release })
            {
                yield return [platform, configuration];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(PlatformAndConfiguration))]
    public async Task DiscoverAllTests(string platform, BuildConfiguration configuration)
    {
        TestHost testHost = AssetFixture.GetTestHost(platform, configuration);

        TestHostResult result = await testHost.ExecuteAsync("--list-tests json", cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        using var document = JsonDocument.Parse(result.StandardOutput);
        string[] discoveredTests =
        [
            .. document.RootElement.GetProperty("tests")
                .EnumerateArray()
                .Select(test => test.GetProperty("displayName").GetString()!),
        ];

        Assert.HasCount(3, discoveredTests);
        Assert.Contains("PassingTest", discoveredTests);
        Assert.Contains("FailingTest", discoveredTests);
        Assert.Contains("SkippingTest", discoveredTests);
    }

    [TestMethod]
    [DynamicData(nameof(PlatformAndConfiguration))]
    public async Task RunAllTests(string platform, BuildConfiguration configuration)
    {
        TestHost testHost = AssetFixture.GetTestHost(platform, configuration);

        TestHostResult result = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("passed PassingTest", result.StandardOutput);
        Assert.Contains("failed FailingTest", result.StandardOutput);
        Assert.Contains("skipped SkippingTest", result.StandardOutput);
        Assert.Contains("Test run summary: Failed!", result.StandardOutput);
        Assert.Contains("failed: 1", result.StandardOutput);
        Assert.Contains("succeeded: 1", result.StandardOutput);
        Assert.Contains("skipped: 1", result.StandardOutput);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private readonly Dictionary<string, TestAsset> _assetsByPlatform = [];

        private static string ProjectName(string platform) => $"DesktopTestProject{platform}";

        public TestHost GetTestHost(string platform, BuildConfiguration configuration)
            => TestHost.LocateFrom(_assetsByPlatform[platform].TargetAssetPath, ProjectName(platform), "net462", buildConfiguration: configuration);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            foreach (string platform in Platforms)
            {
                string projectName = ProjectName(platform);
                string code = SourceCode
                    .PatchCodeWithReplace("$ProjectName$", projectName)
                    .PatchCodeWithReplace("$Platform$", platform)
                    .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);

                TestAsset asset = await TestAsset.GenerateAssetAsync(projectName, code, _tempDirectory);
                await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Debug", callerMemberName: $"{projectName}_Debug", cancellationToken: cancellationToken);
                await DotnetCli.RunAsync($"build \"{asset.TargetAssetPath}\" -c Release", callerMemberName: $"{projectName}_Release", cancellationToken: cancellationToken);
                _assetsByPlatform.Add(platform, asset);
            }
        }

        public void Dispose()
        {
            foreach (TestAsset asset in _assetsByPlatform.Values)
            {
                asset.Dispose();
            }

            _tempDirectory.Dispose();
        }

        private const string SourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>net462</TargetFramework>
    <PlatformTarget>$Platform$</PlatformTarget>
    <AssemblyName>$ProjectName$</AssemblyName>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SampleUnitTestProject;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void PassingTest() => Assert.AreEqual(2, 2);

    [TestMethod]
    public void FailingTest() => Assert.AreEqual(2, 3);

    [Ignore]
    [TestMethod]
    public void SkippingTest()
    {
    }
}
""";
    }
}
