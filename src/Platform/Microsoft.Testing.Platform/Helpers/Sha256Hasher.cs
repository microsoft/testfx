// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Platform.Helpers;

// Took from https://github.com/dotnet/sdk/blob/ad3148adcb71e606c54a4b454138947aa74ea22b/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs#L4
[ExcludeFromCodeCoverage]
internal static class Sha256Hasher
{
    public static string HashWithNormalizedCasing(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text.ToUpperInvariant());
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
