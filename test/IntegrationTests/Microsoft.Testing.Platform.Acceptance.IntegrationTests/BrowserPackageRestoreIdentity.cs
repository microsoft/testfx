// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

internal static class BrowserPackageRestoreIdentity
{
    public static void AssertMatches(
        string assetsPath,
        string packageFeed,
        IReadOnlyList<string> packageIds)
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        JsonElement libraries = assets.RootElement.GetProperty("libraries");
        foreach (string packageId in packageIds)
        {
            JsonProperty library = libraries.EnumerateObject().Single(
                library => library.Name.StartsWith(
                    packageId + "/",
                    StringComparison.OrdinalIgnoreCase));
            string version = library.Name[(library.Name.LastIndexOf('/') + 1)..];
            string expectedPackageName = $"{packageId}.{version}.nupkg";
            string package = Directory.EnumerateFiles(
                packageFeed,
                "*.nupkg",
                SearchOption.TopDirectoryOnly).Single(
                    path => string.Equals(
                        Path.GetFileName(path),
                        expectedPackageName,
                        StringComparison.OrdinalIgnoreCase));
            string expectedHash =
                Convert.ToBase64String(SHA512.HashData(File.ReadAllBytes(package)));
            string? actualHash = library.Value.GetProperty("sha512").GetString();
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                throw new AssertFailedException(
                    $"Restored package '{library.Name}' has SHA-512 '{actualHash}', "
                    + $"but the staged source-matched package '{package}' has '{expectedHash}'.");
            }
        }
    }
}
