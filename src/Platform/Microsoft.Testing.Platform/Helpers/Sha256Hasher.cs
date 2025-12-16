// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Platform.Helpers;

// Took from https://github.com/dotnet/sdk/blob/ad3148adcb71e606c54a4b454138947aa74ea22b/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs#L4
// With modifications to handle PlatformNotSupportedException on some platforms.
[ExcludeFromCodeCoverage]
internal static class Sha256Hasher
{
    public static string HashWithNormalizedCasing(string text)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text.ToUpperInvariant());
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexStringLower(hash);
        }
        catch (PlatformNotSupportedException)
        {
            // SHA256 is not supported on WASM WASI and similar platforms.
            // Fall back to a simple non-cryptographic hash for telemetry purposes.
            return ComputeNonCryptographicHash(text.ToUpperInvariant());
        }
    }

    private static string ComputeNonCryptographicHash(string text)
    {
        // Use a simple deterministic hash for platforms without SHA256 support.
        // This is sufficient for telemetry correlation purposes.
        int hash = text.GetHashCode();
        byte[] hashBytes = BitConverter.GetBytes(hash);

        // Expand to 32 bytes (SHA256 size) for consistency by repeating the pattern
        byte[] expandedHash = new byte[32];
        for (int i = 0; i < expandedHash.Length; i++)
        {
            expandedHash[i] = hashBytes[i % hashBytes.Length];
        }

        return Convert.ToHexStringLower(expandedHash);
    }
}
