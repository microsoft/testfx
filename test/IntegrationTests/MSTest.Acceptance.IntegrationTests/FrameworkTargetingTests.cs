// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text.Json;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

using SystemTask = System.Threading.Tasks.Task;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class FrameworkTargetingTests : AcceptanceTestBase<NopAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow("net10.0", 10)]
    [DataRow("net11.0", 11)]
    public async SystemTask NativeAotCompilerPack_MatchesBootstrappedRuntime(string targetFramework, int runtimeMajor)
    {
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            "NativeAotCompilerPack",
            $$"""
            #file NativeAotCompilerPack.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{targetFramework}}</TargetFramework>
                <OutputType>Exe</OutputType>
                <PublishAot>true</PublishAot>
              </PropertyGroup>
            </Project>
            """);

        string projectPath = Path.Combine(asset.TargetAssetPath, "NativeAotCompilerPack.csproj");
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{projectPath}\" -t:ProcessFrameworkReferences -getItem:PackageReference",
            environmentVariables: new() { ["DOTNET_CLI_CONTEXT_VERBOSE"] = "false" },
            cancellationToken: TestContext.CancellationToken);

        using var bootstrap = JsonDocument.Parse(File.ReadAllText(Path.Combine(Constants.Root, "global.json")));
        string? expectedVersion = bootstrap.RootElement.GetProperty("tools").GetProperty("runtimes").GetProperty("dotnet")
            .EnumerateArray()
            .Select(version => version.GetString())
            .Single(version => version is not null && version.StartsWith($"{runtimeMajor}.", StringComparison.Ordinal));

        using var evaluation = JsonDocument.Parse(result.StandardOutput);
        JsonElement compiler = Assert.ContainsSingle(evaluation.RootElement.GetProperty("Items").GetProperty("PackageReference")
            .EnumerateArray()
            .Where(package => package.GetProperty("Identity").GetString() == "Microsoft.DotNet.ILCompiler"));

        Assert.AreEqual(expectedVersion, compiler.GetProperty("Version").GetString());
    }

    [TestMethod]
    [DataRow("net10.0", 10)]
    [DataRow("net11.0", 11)]
    public async SystemTask PackedPackages_UseMatchingFrameworkAssetsAndRuntime(string targetFramework, int runtimeMajor)
    {
        const string assetName = "FrameworkTargeting";
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            assetName,
            $$"""
            #file FrameworkTargeting.csproj
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{targetFramework}}</TargetFramework>
                <OutputType>Exe</OutputType>
                <EnableMSTestRunner>true</EnableMSTestRunner>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="MSTest.TestFramework" Version="{{MSTestVersion}}" />
                <PackageReference Include="MSTest.TestAdapter" Version="{{MSTestVersion}}" />
                <PackageReference Include="Microsoft.Testing.Platform" Version="{{MicrosoftTestingPlatformVersion}}" />
              </ItemGroup>
            </Project>

            #file RuntimeTests.cs
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public sealed class RuntimeTests
            {
                [TestMethod]
                public void ExecutesRequestedRuntime()
                    => Assert.AreEqual({{runtimeMajor}}, Environment.Version.Major);
            }
            """);

        await DotnetCli.RunAsync(
            $"build {asset.TargetAssetPath} -c Release",
            cancellationToken: TestContext.CancellationToken);

        string importedTargets = File.ReadAllText(Path.Combine(asset.TargetAssetPath, "obj", $"{assetName}.csproj.nuget.g.targets"))
            .Replace('\\', '/');
        Assert.Contains($"buildTransitive/{targetFramework}/MSTest.TestAdapter.targets", importedTargets);
        Assert.Contains($"buildTransitive/{targetFramework}/MSTest.TestFramework.targets", importedTargets);

        var host = TestHost.LocateFrom(asset.TargetAssetPath, assetName, targetFramework);
        await AssertMatchingAssemblyAsync("MSTest.TestFramework", MSTestVersion, $"lib/{targetFramework}/MSTest.TestFramework.dll", host.DirectoryName);
        await AssertMatchingAssemblyAsync("MSTest.TestFramework", MSTestVersion, $"buildTransitive/{targetFramework}/MSTest.TestFramework.Extensions.dll", host.DirectoryName);
        await AssertMatchingAssemblyAsync("MSTest.TestAdapter", MSTestVersion, $"buildTransitive/{targetFramework}/MSTest.TestAdapter.dll", host.DirectoryName);
        await AssertMatchingAssemblyAsync("MSTest.TestAdapter", MSTestVersion, $"buildTransitive/{targetFramework}/MSTestAdapter.PlatformServices.dll", host.DirectoryName);
        await AssertMatchingAssemblyAsync("Microsoft.Testing.Platform", MicrosoftTestingPlatformVersion, $"lib/{targetFramework}/Microsoft.Testing.Platform.dll", host.DirectoryName);

        TestHostResult result = await host.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(0);
        result.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    private async SystemTask AssertMatchingAssemblyAsync(string packageId, string version, string packageEntry, string outputDirectory)
    {
        string packagePath = Path.Combine(Constants.ArtifactsPackagesShipping, $"{packageId}.{version}.nupkg");
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry entry = archive.GetEntry(packageEntry)
            ?? throw new AssertFailedException($"Package '{packagePath}' is missing framework-specific asset '{packageEntry}'.");
        using Stream stream = entry.Open();
        using var expected = new MemoryStream();
        await stream.CopyToAsync(expected, TestContext.CancellationToken);

        Assert.AreSequenceEqual(
            expected.ToArray(),
            File.ReadAllBytes(Path.Combine(outputDirectory, Path.GetFileName(packageEntry))),
            $"The consumer must load '{packageEntry}' from the current '{packageId}' package.");
    }
}
