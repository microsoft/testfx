// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Guards the nuget.org metadata of every package this repository produces.
/// </summary>
/// <remarks>
/// NuGet's pack targets silently default an unset <c>PackageDescription</c> to the literal string
/// <c>Package Description</c> during evaluation, which means Arcade's own "PackageDescription must be specified"
/// check (a target, so it runs after that default has been applied) can never fire. Three packages
/// (<c>Microsoft.Testing.Extensions.TrxReport.Abstractions</c>, <c>Microsoft.Testing.Platform.AI</c> and
/// <c>Microsoft.Testing.Extensions.AzureFoundry</c>) reached nuget.org showing that placeholder as their
/// description. The repository now guards this at pack time in <c>Directory.Build.targets</c>, but that guard
/// hooks <c>GenerateNuspec</c> and is scoped to <c>IsPackable</c>, so it could silently stop firing. These tests
/// inspect the actually-produced <c>.nupkg</c> files so such a regression fails the build instead.
/// </remarks>
[TestClass]
public sealed class PackageMetadataCompletenessTests
{
    /// <summary>
    /// The description NuGet substitutes when a project forgets to set one.
    /// </summary>
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

        foreach ((string nupkg, XDocument nuspec) in EnumerateProducedPackages())
        {
            string packageName = Path.GetFileName(nupkg);
            checkedPackages.Add(packageName);

            string? description = GetMetadataValue(nuspec, "description");
            if (string.IsNullOrWhiteSpace(description))
            {
                violations.Add($"{packageName}: has no <description>.");
            }
            else if (description.Trim() == NuGetPlaceholderDescription)
            {
                violations.Add($"{packageName}: ships NuGet's '{NuGetPlaceholderDescription}' placeholder. Set PackageDescriptionDetail in the project.");
            }
            else if (HasIndentedLine(description))
            {
                // MSBuild trims a property value, but not the indentation of its inner lines, so a
                // multi-line <PackageDescription> authored inline in a .csproj leaks that indentation into
                // the published text. Multi-line descriptions must use PackageDescriptionDetail with CDATA.
                violations.Add($"{packageName}: description contains lines indented with the .csproj whitespace. Author a multi-line description as a CDATA PackageDescriptionDetail so the project indentation does not leak into the published text.");
            }
        }

        AssertAllPackagesChecked(checkedPackages);
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
    {
        List<string> checkedPackages = [];
        List<string> violations = [];

        foreach ((string nupkg, XDocument nuspec) in EnumerateProducedPackages())
        {
            string packageName = Path.GetFileName(nupkg);
            checkedPackages.Add(packageName);

            string? readme = GetMetadataValue(nuspec, "readme");
            if (string.IsNullOrWhiteSpace(readme))
            {
                violations.Add($"{packageName}: has no <readme>. Add a PACKAGE.md next to the project file.");
                continue;
            }

            // The nuspec pointing at a README is not enough: the file itself has to be inside the package.
            using ZipArchive archive = ZipFile.OpenRead(nupkg);
            if (!archive.Entries.Any(e => string.Equals(e.FullName, readme, StringComparison.Ordinal)))
            {
                violations.Add($"{packageName}: declares <readme>{readme}</readme> but does not contain that file.");
            }
        }

        AssertAllPackagesChecked(checkedPackages);
        Assert.IsEmpty(
            violations,
            $"The following produced packages do not embed a README for nuget.org:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static void AssertAllPackagesChecked(List<string> checkedPackages)
        => Assert.IsNotEmpty(
            checkedPackages,
            $"Expected the produced packages to be inspected, but none was found. " +
            $"Ensure the solution was packed (build with '-pack') before running this test. Searched:{Environment.NewLine}" +
            $"  {Constants.ArtifactsPackagesShipping}{Environment.NewLine}  {Constants.ArtifactsPackagesNonShipping}");

    private static bool HasIndentedLine(string description)
        => description.Split('\n').Any(line => line.Length > 0 && char.IsWhiteSpace(line[0]) && line.Trim().Length > 0);

    /// <summary>
    /// Reads a nuspec metadata element by local name, ignoring the nuspec XML namespace (which varies by schema
    /// version).
    /// </summary>
    private static string? GetMetadataValue(XDocument nuspec, string name)
        => nuspec.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    private static IEnumerable<(string NupkgPath, XDocument Nuspec)> EnumerateProducedPackages()
    {
        foreach (string folder in new[] { Constants.ArtifactsPackagesShipping, Constants.ArtifactsPackagesNonShipping })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (string nupkg in Directory.EnumerateFiles(folder, "*.nupkg", SearchOption.TopDirectoryOnly))
            {
                using ZipArchive archive = ZipFile.OpenRead(nupkg);
                ZipArchiveEntry? nuspecEntry = archive.Entries.FirstOrDefault(
                    e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/'));
                if (nuspecEntry is null)
                {
                    continue;
                }

                using Stream stream = nuspecEntry.Open();
                yield return (nupkg, XDocument.Load(stream));
            }
        }
    }
}
