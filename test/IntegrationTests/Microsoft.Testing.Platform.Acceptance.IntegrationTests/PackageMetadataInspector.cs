// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Finds the nuget.org metadata problems that would make a produced package unusable on its package page.
/// </summary>
/// <remarks>
/// This is deliberately a pure function over a parsed nuspec and the set of paths in the archive, so that
/// <see cref="PackageMetadataCompletenessTests"/> can run it against the real packages while
/// <see cref="PackageMetadataInspectorTests"/> covers the violation branches that no correct package ever
/// reaches. Both exercise this same code rather than a reimplementation of it.
/// </remarks>
internal static class PackageMetadataInspector
{
    internal const string NuGetPlaceholderDescription = "Package Description";

    internal static IReadOnlyList<string> InspectPackage(string packageFileName, XDocument nuspec, IReadOnlySet<string> archiveEntries)
    {
        List<string> violations = [];

        AddDescriptionViolations(packageFileName, nuspec, violations);
        AddEmbeddedMetadataFileViolations(packageFileName, nuspec, archiveEntries, "readme", "Add a PACKAGE.md next to the project file.", violations);
        AddEmbeddedMetadataFileViolations(packageFileName, nuspec, archiveEntries, "icon", "Arcade sets PackageIcon centrally for packable projects.", violations);

        return violations;
    }

    private static void AddDescriptionViolations(string packageFileName, XDocument nuspec, List<string> violations)
    {
        string? description = GetMetadataValue(nuspec, "description");
        if (string.IsNullOrWhiteSpace(description))
        {
            violations.Add($"{packageFileName}: has no <description>.");
        }
        else if (description.Trim() == NuGetPlaceholderDescription)
        {
            violations.Add($"{packageFileName}: ships NuGet's '{NuGetPlaceholderDescription}' placeholder. Set PackageDescription in the project.");
        }
        else if (HasIndentedLine(description))
        {
            // MSBuild trims a property value, but not the indentation of its inner lines, so a
            // multi-line <PackageDescription> authored inline in a .csproj leaks that indentation into
            // the published text.
            violations.Add($"{packageFileName}: description contains lines indented with the .csproj whitespace. Author a multi-line description as a CDATA PackageDescription so the project indentation does not leak into the published text.");
        }
    }

    private static void AddEmbeddedMetadataFileViolations(
        string packageFileName,
        XDocument nuspec,
        IReadOnlySet<string> archiveEntries,
        string metadataName,
        string hint,
        List<string> violations)
    {
        string? declaredPath = GetMetadataValue(nuspec, metadataName);
        if (string.IsNullOrWhiteSpace(declaredPath))
        {
            violations.Add($"{packageFileName}: has no <{metadataName}>. {hint}");
            return;
        }

        // Declaring the file in the nuspec is not enough: it also has to be inside the package. NuGet writes
        // the packaged path, which uses the platform separator, while zip entries always use '/'.
        string entryPath = declaredPath.Replace('\\', '/');
        if (!archiveEntries.Contains(entryPath))
        {
            violations.Add($"{packageFileName}: declares <{metadataName}>{declaredPath}</{metadataName}> but does not contain that file.");
        }
    }

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
}
