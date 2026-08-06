// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
