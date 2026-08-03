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
    /// <summary>
    /// The description NuGet substitutes when a project forgets to set one.
    /// </summary>
    /// <remarks>
    /// Kept in sync with the same literal in the <c>_ValidatePackageMetadata</c> target of the repository's
    /// <c>Directory.Build.targets</c>.
    /// </remarks>
    private const string NuGetPlaceholderDescription = "Package Description";

    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Every produced package must carry a real description: nuget.org renders it on the package page and uses
    /// it for search, so shipping NuGet's placeholder makes the package effectively undocumented.
    /// </summary>
    [TestMethod]
    public void ProducedPackages_HaveARealDescription()
    {
        List<string> checkedPackages = [];
        List<string> violations = [];

        foreach (ProducedPackage package in EnumerateProducedPackages())
        {
            checkedPackages.Add(package.FileName);

            string? description = GetMetadataValue(package.Nuspec, "description");
            if (string.IsNullOrWhiteSpace(description))
            {
                violations.Add($"{package.FileName}: has no <description>.");
            }
            else if (description.Trim() == NuGetPlaceholderDescription)
            {
                violations.Add($"{package.FileName}: ships NuGet's '{NuGetPlaceholderDescription}' placeholder. Set PackageDescriptionDetail in the project.");
            }
            else if (HasIndentedLine(description))
            {
                // MSBuild trims a property value, but not the indentation of its inner lines, so a
                // multi-line <PackageDescription> authored inline in a .csproj leaks that indentation into
                // the published text.
                violations.Add($"{package.FileName}: description contains lines indented with the .csproj whitespace. Author a multi-line description as a CDATA PackageDescriptionDetail so the project indentation does not leak into the published text.");
            }
        }

        AssertSomethingWasPacked(checkedPackages);
        Assert.IsEmpty(
            violations,
            $"The following produced packages have an unusable nuget.org description:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Every produced package must embed the README that nuget.org renders on the package page. The repository
    /// wires this up centrally by packing a <c>PACKAGE.md</c> found next to the project file, so a missing
    /// README means either the file was not added or the central wiring stopped working.
    /// </summary>
    [TestMethod]
    public void ProducedPackages_EmbedTheirReadme()
        => AssertPackagesEmbedMetadataFile("readme", "Add a PACKAGE.md next to the project file.");

    /// <summary>
    /// Every produced package must embed the icon that nuget.org shows next to the package name. Arcade supplies
    /// it centrally, so a missing icon means that central wiring stopped working.
    /// </summary>
    [TestMethod]
    public void ProducedPackages_EmbedTheirIcon()
        => AssertPackagesEmbedMetadataFile("icon", "Arcade sets PackageIcon centrally for packable projects.");

    private static void AssertPackagesEmbedMetadataFile(string metadataName, string hint)
    {
        List<string> checkedPackages = [];
        List<string> violations = [];

        foreach (ProducedPackage package in EnumerateProducedPackages())
        {
            checkedPackages.Add(package.FileName);

            string? declaredPath = GetMetadataValue(package.Nuspec, metadataName);
            if (string.IsNullOrWhiteSpace(declaredPath))
            {
                violations.Add($"{package.FileName}: has no <{metadataName}>. {hint}");
                continue;
            }

            // Declaring the file in the nuspec is not enough: it also has to be inside the package. NuGet writes
            // the packaged path, which uses the platform separator, while zip entries always use '/'.
            string entryPath = declaredPath.Replace('\\', '/');
            using ZipArchive archive = ZipFile.OpenRead(package.Path);
            if (!archive.Entries.Any(e => string.Equals(e.FullName, entryPath, StringComparison.Ordinal)))
            {
                violations.Add($"{package.FileName}: declares <{metadataName}>{declaredPath}</{metadataName}> but does not contain that file.");
            }
        }

        AssertSomethingWasPacked(checkedPackages);
        Assert.IsEmpty(
            violations,
            $"The following produced packages do not embed the '{metadataName}' they declare:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static void AssertSomethingWasPacked(List<string> checkedPackages)
        => Assert.IsNotEmpty(
            checkedPackages,
            $"Expected the produced packages to be inspected, but none was found. " +
            $"Ensure the solution was packed (build with '-pack') before running this test. Searched:{Environment.NewLine}" +
            $"  {Constants.ArtifactsPackagesShipping}{Environment.NewLine}  {Constants.ArtifactsPackagesNonShipping}");

    // XDocument.Load applies XML end-of-line normalization, so however the nuspec encodes its line breaks the
    // description reaches us with '\n' separators only.
    private static bool HasIndentedLine(string description)
        => description.Split('\n').Any(line => line.Length > 0 && char.IsWhiteSpace(line[0]) && line.Trim().Length > 0);

    /// <summary>
    /// Reads a nuspec metadata element by local name, ignoring the nuspec XML namespace (which varies by schema
    /// version).
    /// </summary>
    private static string? GetMetadataValue(XDocument nuspec, string name)
        => nuspec.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

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
                XDocument? nuspec = ReadNuspec(nupkg);
                if (nuspec is null)
                {
                    continue;
                }

                yield return new ProducedPackage(nupkg, GetMetadataValue(nuspec, "id") ?? Path.GetFileName(nupkg), nuspec);
            }
        }
    }

    private static XDocument? ReadNuspec(string nupkgPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(nupkgPath);
        ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(
            e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/'));
        if (nuspecEntry is null)
        {
            return null;
        }

        // XDocument.Load reads the stream eagerly, so the document outlives the archive.
        using Stream stream = nuspecEntry.Open();
        return XDocument.Load(stream);
    }

    private sealed record ProducedPackage(string Path, string Id, XDocument Nuspec)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
    }
}
