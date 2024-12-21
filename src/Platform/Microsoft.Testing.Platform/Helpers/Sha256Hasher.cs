// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Platform.Helpers;

// Took from https://github.com/dotnet/sdk/blob/ad3148adcb71e606c54a4b454138947aa74ea22b/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs#L4
[ExcludeFromCodeCoverage]
internal static class Sha256Hasher
{
    private static string Hash(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
#if NETCOREAPP
        byte[] hash = SHA256.HashData(bytes);
#else
        using var hasher = SHA256.Create();
        byte[] hash = hasher.ComputeHash(bytes);
#endif

#if NETCOREAPP
        return Convert.ToHexString(hash).ToLowerInvariant();
#else
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
#endif
    }

    public static string HashWithNormalizedCasing(string text)
        => Hash(text.ToUpperInvariant());
}
