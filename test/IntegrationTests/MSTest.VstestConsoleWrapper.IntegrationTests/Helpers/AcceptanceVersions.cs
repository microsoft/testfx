// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml.Linq;

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

/// <summary>
/// Resolves the package versions the generated acceptance assets need to reference (the locally
/// packed MSTest packages and the pinned Microsoft.NET.Test.Sdk version). Mirrors the version
/// discovery in the acceptance test base so the generated assets bind to the just-built packages.
/// </summary>
internal static class AcceptanceVersions
{
    private const string NuGetPackageExtensionName = ".nupkg";

    static AcceptanceVersions()
    {
        var cpmPropFileDoc = XDocument.Load(Path.Combine(Constants.Root, "Directory.Packages.props"));
        MicrosoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;
        MSTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.");
    }

    public static string MSTestVersion { get; }

    public static string MicrosoftNETTestSdkVersion { get; }

    private static string ExtractVersionFromPackage(string rootFolder, string packagePrefixName)
    {
        string[] matches = Directory.GetFiles(rootFolder, packagePrefixName + "*" + NuGetPackageExtensionName, SearchOption.TopDirectoryOnly);

        if (matches.Length > 1)
        {
            matches = [.. matches
                .Select(path => (path, fileName: Path.GetFileName(path)[packagePrefixName.Length..]))
                .Where(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _))
                .Select(tuple => tuple.path)];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Was expecting to find a single NuGet package named '{packagePrefixName}' in '{rootFolder}', but found {matches.Length}: '{string.Join("', '", matches.Select(m => Path.GetFileName(m)))}'.");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(packagePrefixName.Length, packageFullName.Length - packagePrefixName.Length - NuGetPackageExtensionName.Length);
    }
}
