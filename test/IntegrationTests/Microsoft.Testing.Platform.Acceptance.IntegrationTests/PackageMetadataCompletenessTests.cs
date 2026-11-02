// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Checks the nuget.org metadata (description, README and icon) of the packages this repository produces.
/// </summary>
/// <remarks>
/// <para>
/// NuGet's pack targets silently default an unset <c>PackageDescription</c> to the literal string
/// <c>Package Description</c> during evaluation, which means Arcade's own "PackageDescription must be specified"
/// check (a target, so it runs after that default has been applied) can never fire. Three packages
/// (<c>Microsoft.Testing.Extensions.TrxReport.Abstractions</c>, <c>Microsoft.Testing.Platform.AI</c> and
/// <c>Microsoft.Testing.Extensions.AzureFoundry</c>) reached nuget.org showing that placeholder as their
/// description.
/// </para>
/// <para>
/// The primary guard is now <c>_ValidatePackageMetadata</c> in the repository's <c>Directory.Build.targets</c>,
/// which fails <c>pack</c> itself and therefore applies to every solution filter. These tests are a backstop for
/// it: that target hooks <c>GenerateNuspec</c> and is scoped to <c>IsPackable</c>, so it could silently stop
/// firing, and only inspecting the actually-produced <c>.nupkg</c> files catches that. Because this project is
/// part of <c>TestFx.slnx</c> and <c>Microsoft.Testing.Platform.slnf</c> but not <c>MSTest.slnf</c>, the backstop
/// only covers the <c>MSTest.*</c> packages on a full-solution build.
/// </para>
/// <para>
/// The tests inspect whichever packages are present in the artifacts folders, so they cannot tell a partial pack
/// from a complete one; they only fail the run outright when nothing was packed at all.
/// </para>
/// </remarks>
[TestClass]
public sealed class PackageMetadataCompletenessTests
{
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Every produced package must carry a real description and embed the README and icon that nuget.org renders
    /// on the package page.
    /// </summary>
    [TestMethod]
    public void ProducedPackages_HaveCompleteNugetOrgMetadata()
    {
        List<string> checkedPackages = [];
        List<string> violations = [];

        foreach (ProducedPackage package in EnumerateProducedPackages())
        {
            checkedPackages.Add(package.FileName);
            violations.AddRange(PackageMetadataInspector.InspectPackage(package.FileName, package.Nuspec, package.Entries));
        }

        AssertSomethingWasPacked(checkedPackages);
        Assert.IsEmpty(
            violations,
            $"The following produced packages have unusable nuget.org metadata:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static void AssertSomethingWasPacked(List<string> checkedPackages)
        => Assert.IsNotEmpty(
            checkedPackages,
            $"Expected the produced packages to be inspected, but none was found. " +
            $"Ensure the solution was packed (build with '-pack') before running this test. Searched:{Environment.NewLine}" +
            $"  {Constants.ArtifactsPackagesShipping}{Environment.NewLine}  {Constants.ArtifactsPackagesNonShipping}");

    /// <summary>
    /// Returns the most recently produced package for each package id found in the artifacts folders.
    /// </summary>
    /// <remarks>
    /// The artifacts folders are never cleaned, so a version bump (or a different branch) leaves older
    /// <c>.nupkg</c> files behind indefinitely. Keeping only the newest file per id means a stale package built
    /// before a metadata fix cannot report a violation against a tree that is actually correct.
    /// </remarks>
    private static IEnumerable<ProducedPackage> EnumerateProducedPackages()
        => EnumerateAllProducedPackages()
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(p => File.GetLastWriteTimeUtc(p.Path)).First());

    private static IEnumerable<ProducedPackage> EnumerateAllProducedPackages()
    {
        foreach (string folder in new[] { Constants.ArtifactsPackagesShipping, Constants.ArtifactsPackagesNonShipping })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string nupkg in Directory.EnumerateFiles(folder, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                ProducedPackage? package = ReadPackage(nupkg);
                if (package is null)
                {
                    continue;
                }

                yield return package;
            }
        }
    }

    private static ProducedPackage? ReadPackage(string nupkgPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);
        ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(
            e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/'));
        if (nuspecEntry is null)
        {
            return null;
        }

        // XDocument.Load reads the stream eagerly, so the document outlives the archive.
        XDocument nuspec;
        using (Stream stream = nuspecEntry.Open())
        {
            nuspec = XDocument.Load(stream);
        }

        IReadOnlySet<string> entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.Ordinal);
        string id = nuspec.Descendants().FirstOrDefault(e => e.Name.LocalName == "id")?.Value ?? Path.GetFileName(nupkgPath);
        return new ProducedPackage(nupkgPath, id, nuspec, entries);
    }

    private sealed record ProducedPackage(string Path, string Id, XDocument Nuspec, IReadOnlySet<string> Entries)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
    }
}
