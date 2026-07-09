// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Guards the pack-time target in <c>src/Platform/Directory.Build.targets</c> that caps the packed
/// <c>Microsoft.Testing.Platform</c> package dependency of the in-repo extensions at a <c>[self, nextMajor)</c>
/// range. That target hooks NuGet-internal targets (<c>_GetProjectReferenceVersions</c> /
/// <c>_ProjectReferencesWithVersions</c>); if a future NuGet SDK renames them, the target would silently stop
/// firing and the dependency would revert to an open-ended minimum-version (<c>2.4.0</c> == <c>[2.4.0, )</c>).
/// These tests inspect the actually-produced <c>.nupkg</c> files so such a regression fails the build.
/// </summary>
[TestClass]
public sealed class PackageDependencyUpperBoundTests
{
    private const string PlatformPackageId = "Microsoft.Testing.Platform";

    // The exclusive upper bound the pack-time target applies is the next major after the current Testing
    // Platform version. Read the source of truth (eng/Versions.props) rather than a packed version so the test
    // stays independent of which packages happen to be present.
    private static readonly string ExpectedUpperBound = $"{GetTestingPlatformMajorVersion() + 1}.0.0";

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Every package that declares a direct <c>Microsoft.Testing.Platform</c> dependency must express it as a
    /// closed <c>[min, max)</c> range whose upper bound is the next major, never an open-ended minimum version.
    /// At least one such package is expected, so an empty artifacts folder (or a target that silently dropped
    /// all dependencies) fails rather than vacuously passing.
    /// </summary>
    [TestMethod]
    public void PackedExtensions_CapMicrosoftTestingPlatformDependency_AtNextMajor()
    {
        List<string> checkedPackages = [];
        List<string> violations = [];

        foreach (string nupkg in EnumerateProducedPackages())
        {
            foreach (string version in GetPlatformDependencyVersions(nupkg))
            {
                string packageName = Path.GetFileName(nupkg);
                checkedPackages.Add($"{packageName} -> {version}");

                // Expected shape: "[<min>, <nextMajor>)". A bare version like "2.4.0" (no brackets) means the
                // upper-bound target did not run — exactly the regression we are guarding against.
                if (!version.StartsWith('[') || !version.EndsWith($", {ExpectedUpperBound})", StringComparison.Ordinal))
                {
                    violations.Add($"{packageName}: expected a '[<min>, {ExpectedUpperBound})' range but found '{version}'.");
                }
            }
        }

        Assert.IsNotEmpty(
            checkedPackages,
            $"Expected at least one produced package to declare a '{PlatformPackageId}' dependency, but none was found. " +
            $"Ensure the solution was packed (build with '-pack') before running this test. Searched:{Environment.NewLine}" +
            $"  {Constants.ArtifactsPackagesShipping}{Environment.NewLine}  {Constants.ArtifactsPackagesNonShipping}");

        Assert.IsEmpty(
            violations,
            $"The following packages do not cap their '{PlatformPackageId}' dependency at '[<min>, {ExpectedUpperBound})':{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static int GetTestingPlatformMajorVersion()
    {
        var versionsProps = XDocument.Load(Path.Combine(Constants.Root, "eng", "Versions.props"));
        string versionPrefix = versionsProps.Descendants()
            .Single(e => e.Name.LocalName == "TestingPlatformVersionPrefix")
            .Value;
        return int.Parse(versionPrefix.Split('.')[0], CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> EnumerateProducedPackages()
    {
        foreach (string folder in new[] { Constants.ArtifactsPackagesShipping, Constants.ArtifactsPackagesNonShipping })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            // Only the Microsoft.Testing.* family can depend on Microsoft.Testing.Platform; scoping the search
            // keeps unrelated packages (and the platform package itself, which has no self-dependency) out.
            foreach (string nupkg in Directory.EnumerateFiles(folder, "Microsoft.Testing.*.nupkg", SearchOption.TopDirectoryOnly))
            {
                yield return nupkg;
            }
        }
    }

    private static IEnumerable<string> GetPlatformDependencyVersions(string nupkgPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);
        ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            yield break;
        }

        XDocument nuspec;
        using (Stream stream = nuspecEntry.Open())
        {
            nuspec = XDocument.Load(stream);
        }

        // Match by LocalName to ignore the nuspec XML namespace, and target the exact dependency id so
        // siblings such as 'Microsoft.Testing.Platform.MSBuild' or 'Microsoft.Testing.Platform.AI' are excluded.
        foreach (XElement dependency in nuspec.Descendants().Where(e => e.Name.LocalName == "dependency"))
        {
            if (string.Equals((string?)dependency.Attribute("id"), PlatformPackageId, StringComparison.Ordinal)
                && (string?)dependency.Attribute("version") is { } version)
            {
                yield return version;
            }
        }
    }
}
