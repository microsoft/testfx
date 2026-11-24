// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

internal static class CollectionPackageCache
{
    internal const string EnvironmentVariableName = "TESTFX_COLLECTION_NUGET_CACHE";
    internal const string MarkerFileName = ".testfx-collection-cache";
    internal const string MarkerContent = "testfx-collection-cache-v1";

    internal static string Select(string? sharedPath, string freshDirectory)
    {
        if (sharedPath is null)
        {
            return Path.Combine(freshDirectory, ".packages");
        }

        if (string.IsNullOrWhiteSpace(sharedPath) || !Path.IsPathFullyQualified(sharedPath))
        {
            throw new InvalidOperationException($"{EnvironmentVariableName} must identify an absolute, parent-owned collection cache.");
        }

        string fullPath = Path.GetFullPath(sharedPath);
        string markerPath = Path.Combine(fullPath, MarkerFileName);
        return !File.Exists(markerPath) || File.ReadAllText(markerPath).Trim() != MarkerContent
            ? throw new InvalidOperationException($"{EnvironmentVariableName} does not identify a marked collection-run cache.")
            : fullPath;
    }
}
