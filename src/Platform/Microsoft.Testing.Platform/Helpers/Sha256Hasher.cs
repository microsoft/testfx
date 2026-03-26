// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Platform.Helpers;

// Took from https://github.com/dotnet/sdk/blob/ad3148adcb71e606c54a4b454138947aa74ea22b/src/Cli/Microsoft.DotNet.Cli.Utils/Sha256Hasher.cs#L4
// With modifications to handle PlatformNotSupportedException on some platforms.
[ExcludeFromCodeCoverage]
internal static class Sha256Hasher
{
    // https://github.com/dotnet/runtime/issues/99126
    [UnsupportedOSPlatform("wasi")]
    public static string HashWithNormalizedCasing(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text.ToUpperInvariant());
#if NET9_0_OR_GREATER
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
#elif NETCOREAPP
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
#endif
    }
}
