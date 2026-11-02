// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Verifies that the <c>_ValidatePackageMetadata</c> guard in the repository's <c>Directory.Build.targets</c>
/// actually fails a pack when the nuget.org metadata is missing.
/// </summary>
/// <remarks>
/// <para>
/// Every project in the repository authors both values, so the guard never fires during a normal build and
/// deleting it would leave the produced packages - and therefore
/// <see cref="PackageMetadataCompletenessTests"/> - unchanged. These tests pack a throwaway project that omits
/// one value at a time, plus a valid control, so a regression in the enforcement itself is caught.
/// </para>
/// <para>
/// The generated project lives under <c>artifacts</c> rather than in the temp directory precisely so that it
/// inherits the repository's <c>Directory.Build.props</c>/<c>.targets</c>, which is what puts the guard in
/// scope. It packs into its own output directory so a deliberately malformed package can never land in
/// <c>artifacts/packages</c>, where <see cref="PackageMetadataCompletenessTests"/> would then read it.
/// </para>
/// </remarks>
[TestClass]
public sealed class PackageMetadataGuardTests
{
    /// <summary>
    /// Arcade requires a repository commit at pack time and normally gets one from SourceLink. Pinning it keeps
    /// the fixture independent of the checkout's source control state.
    /// </summary>
    private const string RepositoryCommit = "0123456789abcdef0123456789abcdef01234567";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Pack_WithCompletePackageMetadata_SucceedsAndWritesPackage()
    {
        using TestAssetDirectory asset = CreateAsset(hasDescription: true, hasReadme: true);

        DotnetMuxerResult result = await PackAsync(asset);

        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.IsNotEmpty(Directory.EnumerateFiles(asset.PackageOutputPath, "*.nupkg", SearchOption.TopDirectoryOnly).ToArray());
    }

    [TestMethod]
    public async Task Pack_WithoutPackageDescription_FailsBeforeWritingPackage()
    {
        using TestAssetDirectory asset = CreateAsset(hasDescription: false, hasReadme: true);

        DotnetMuxerResult result = await PackAsync(asset);

        AssertPackFailedBeforeWritingPackage(
            asset,
            result,
            "is packable but has no package description, so it would be published to nuget.org with NuGet's 'Package Description' placeholder");
    }

    [TestMethod]
    public async Task Pack_WithoutPackageReadme_FailsBeforeWritingPackage()
    {
        using TestAssetDirectory asset = CreateAsset(hasDescription: true, hasReadme: false);

        DotnetMuxerResult result = await PackAsync(asset);

        AssertPackFailedBeforeWritingPackage(
            asset,
            result,
            "is packable but has no package README, so its nuget.org page would show no rendered documentation");
    }

    private async Task<DotnetMuxerResult> PackAsync(TestAssetDirectory asset)
        => await DotnetCli.RunAsync(
            $"msbuild \"{asset.ProjectPath}\" -restore -t:Pack -p:Configuration={Constants.BuildConfiguration} -p:PackageOutputPath=\"{asset.PackageOutputPath}\" -p:RepositoryCommit={RepositoryCommit} -v:minimal",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

    private static void AssertPackFailedBeforeWritingPackage(TestAssetDirectory asset, DotnetMuxerResult result, string expectedError)
    {
        Assert.AreNotEqual(0, result.ExitCode, result.ToString());
        Assert.Contains(expectedError, result.StandardOutput + result.StandardError);
        Assert.IsEmpty(Directory.EnumerateFiles(asset.PackageOutputPath, "*.nupkg", SearchOption.TopDirectoryOnly).ToArray());
    }

    private static TestAssetDirectory CreateAsset(bool hasDescription, bool hasReadme)
    {
        string assetId = $"PackageMetadataGuard{Guid.NewGuid():N}";
        string assetPath = Path.Combine(Constants.Root, "artifacts", "tmp", Constants.BuildConfiguration, "packageMetadataGuardTests", assetId);
        Directory.CreateDirectory(assetPath);

        string projectPath = Path.Combine(assetPath, $"{assetId}.csproj");
        string packageOutputPath = Path.Combine(assetPath, "packages");
        Directory.CreateDirectory(packageOutputPath);

        string descriptionProperty = hasDescription
            ? $"""
    <PackageDescription>{assetId} test package. Microsoft Testing Platform is a lightweight and portable test runner for .NET.</PackageDescription>
"""
            : string.Empty;
        string projectContents = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>true</IsPackable>
    <PackageId>{assetId}</PackageId>
{descriptionProperty}  </PropertyGroup>
</Project>
""";
        File.WriteAllText(projectPath, projectContents, Encoding.UTF8);

        if (hasReadme)
        {
            File.WriteAllText(Path.Combine(assetPath, "PACKAGE.md"), $"# {assetId}{Environment.NewLine}", Encoding.UTF8);
        }

        return new TestAssetDirectory(assetPath, projectPath, packageOutputPath);
    }

    private sealed class TestAssetDirectory(string path, string projectPath, string packageOutputPath) : IDisposable
    {
        public string ProjectPath { get; } = projectPath;

        public string PackageOutputPath { get; } = packageOutputPath;

        public void Dispose()
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}

/// <summary>
/// Verifies the opt-in dependency-free project guards in the repository's <c>Directory.Build.targets</c>.
/// </summary>
[TestClass]
public sealed class DependencyFreeProjectGuardTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Build_WithoutDependencies_Succeeds()
    {
        using TestAssetDirectory asset = CreateAsset(DependencySource.None);

        DotnetMuxerResult result = await BuildAsync(asset);

        Assert.AreEqual(0, result.ExitCode, result.ToString());
    }

    [TestMethod]
    public async Task Build_WithDependencyDeclaredInProject_Fails()
    {
        using TestAssetDirectory asset = CreateAsset(DependencySource.Project);

        DotnetMuxerResult result = await BuildAsync(asset);

        AssertBuildFailed(
            result,
            "must remain dependency-free and cannot declare dependency-bearing items in its project file",
            "PackageReference \"Microsoft.Extensions.Logging\"");
    }

    [TestMethod]
    public async Task Build_WithDependencyInjectedByImport_FailsOnResolvedClosure()
    {
        using TestAssetDirectory asset = CreateAsset(DependencySource.Import);

        DotnetMuxerResult result = await BuildAsync(asset);

        AssertBuildFailed(
            result,
            "must remain dependency-free for net8.0, but dependency assets were resolved",
            "Microsoft.Extensions.Logging");
    }

    [TestMethod]
    public async Task Build_WithNativeOnlyPackageInjectedByImport_FailsOnResolvedClosure()
    {
        using TestAssetDirectory asset = CreateAsset(DependencySource.NativePackage);

        DotnetMuxerResult result = await BuildAsync(asset);

        AssertBuildFailed(
            result,
            "must remain dependency-free for net8.0, but dependency assets were resolved",
            "native asset");
    }

    [TestMethod]
    public async Task Build_WithNonCopyLocalReferenceInjectedByImport_FailsOnResolvedClosure()
    {
        using TestAssetDirectory asset = CreateAsset(DependencySource.NonCopyLocalReference);

        DotnetMuxerResult result = await BuildAsync(asset);

        AssertBuildFailed(
            result,
            "must remain dependency-free for net8.0, but dependency assets were resolved",
            "assembly reference");
    }

    private async Task<DotnetMuxerResult> BuildAsync(TestAssetDirectory asset)
        => await DotnetCli.RunAsync(
            $"msbuild \"{asset.ProjectPath}\" -restore -t:Build -p:Configuration={Constants.BuildConfiguration} -v:minimal",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

    private static void AssertBuildFailed(DotnetMuxerResult result, string expectedError, string expectedDependency)
    {
        string output = result.StandardOutput + result.StandardError;
        Assert.AreNotEqual(0, result.ExitCode, result.ToString());
        Assert.Contains(expectedError, output);
        Assert.Contains(expectedDependency, output);
    }

    private static TestAssetDirectory CreateAsset(DependencySource dependencySource)
    {
        string assetId = $"DependencyFreeProjectGuard{Guid.NewGuid():N}";
        string assetPath = Path.Combine(Constants.Root, "artifacts", "tmp", Constants.BuildConfiguration, "dependencyFreeProjectGuardTests", assetId);
        Directory.CreateDirectory(assetPath);

        string projectPath = Path.Combine(assetPath, $"{assetId}.csproj");
        string dependencyItem = dependencySource == DependencySource.Project
            ? """
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging" />
  </ItemGroup>
"""
            : string.Empty;
        string import = dependencySource is DependencySource.Import
            or DependencySource.NativePackage
            or DependencySource.NonCopyLocalReference
            ? """  <Import Project="InjectedDependency.props" />"""
            : string.Empty;
        string dependencyProperties = dependencySource == DependencySource.NativePackage
            ? """
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
"""
            : string.Empty;
        string projectContents = $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnforceDependencyFreeProject>true</EnforceDependencyFreeProject>
{dependencyProperties}
  </PropertyGroup>
{dependencyItem}{import}
</Project>
""";
        File.WriteAllText(projectPath, projectContents, Encoding.UTF8);

        if (dependencySource is DependencySource.Import
            or DependencySource.NativePackage
            or DependencySource.NonCopyLocalReference)
        {
            string importedDependency = dependencySource switch
            {
                DependencySource.Import => """    <PackageReference Include="Microsoft.Extensions.Logging" PrivateAssets="all" />""",
                DependencySource.NativePackage => """    <PackageReference Include="DependencyFreeNativePackage" VersionOverride="1.0.0" IncludeAssets="native" PrivateAssets="all" />""",
                DependencySource.NonCopyLocalReference => """
    <ProjectReference Include="Dependency\Dependency.csproj">
      <Private>false</Private>
    </ProjectReference>
""",
                _ => throw new InvalidOperationException(),
            };
            File.WriteAllText(
                Path.Combine(assetPath, "InjectedDependency.props"),
                $"""
<Project>
  <ItemGroup>
{importedDependency}
  </ItemGroup>
</Project>
""",
                Encoding.UTF8);
        }

        if (dependencySource == DependencySource.NativePackage)
        {
            CreateNativePackage(Path.Combine(assetPath, "packages"));
            File.WriteAllText(
                Path.Combine(assetPath, "NuGet.config"),
                """
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="dependency-free-fixtures" value="packages" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="dependency-free-fixtures">
      <package pattern="DependencyFreeNativePackage" />
    </packageSource>
  </packageSourceMapping>
</configuration>
""",
                Encoding.UTF8);
        }

        if (dependencySource == DependencySource.NonCopyLocalReference)
        {
            string dependencyDirectory = Path.Combine(assetPath, "Dependency");
            Directory.CreateDirectory(dependencyDirectory);
            File.WriteAllText(
                Path.Combine(dependencyDirectory, "Dependency.csproj"),
                """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
""",
                Encoding.UTF8);
        }

        return new TestAssetDirectory(assetPath, projectPath);
    }

    private static void CreateNativePackage(string packageDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        using ZipArchive archive = ZipFile.Open(
            Path.Combine(packageDirectory, "DependencyFreeNativePackage.1.0.0.nupkg"),
            ZipArchiveMode.Create);
        ZipArchiveEntry nuspecEntry = archive.CreateEntry("DependencyFreeNativePackage.nuspec");
        using (StreamWriter writer = new(nuspecEntry.Open(), Encoding.UTF8))
        {
            writer.Write(
                """
<?xml version="1.0" encoding="utf-8"?>
<package>
  <metadata>
    <id>DependencyFreeNativePackage</id>
    <version>1.0.0</version>
    <authors>TestFx</authors>
    <description>Native-only dependency fixture.</description>
  </metadata>
</package>
""");
        }

        using Stream nativeAsset = archive.CreateEntry("runtimes/win-x64/native/dependency.dll").Open();
        nativeAsset.WriteByte(0);
    }

    private enum DependencySource
    {
        None,
        Project,
        Import,
        NativePackage,
        NonCopyLocalReference,
    }

    private sealed class TestAssetDirectory(string path, string projectPath) : IDisposable
    {
        public string ProjectPath { get; } = projectPath;

        public void Dispose()
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
