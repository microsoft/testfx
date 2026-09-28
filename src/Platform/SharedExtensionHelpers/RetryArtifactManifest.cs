// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal static class RetryArtifactManifest
{
    public const long MaxBytes = 16L * 1024 * 1024;
    public const int MaxLineLength = 64 * 1024;
    public const int MaxRecords = 10_000;

    public static bool TrySplitEntry(string line, out string encodedPath, out string encodedKind)
    {
        int separatorIndex = line.IndexOf('\t');
        if (separatorIndex <= 0)
        {
            encodedPath = string.Empty;
            encodedKind = string.Empty;
            return false;
        }

        encodedPath = line.Substring(0, separatorIndex);
        encodedKind = line.Substring(separatorIndex + 1);
        return true;
    }

    public static string DecodePath(string encodedPath)
        => Encoding.UTF8.GetString(Convert.FromBase64String(encodedPath));

    public static string? DecodeKind(string encodedKind)
        => encodedKind == "-"
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(encodedKind));

    public static string WriteEntry(string path, string encodedKind)
        => $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(path))}\t{encodedKind}";
}

#pragma warning restore RS0051
