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
#if NETCOREAPP
        byte[] hash = SHA256.HashData(bytes);
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);
#endif

#if NET9_0_OR_GREATER
        return Convert.ToHexStringLower(hash);
#else
        return ToHexStringLower(hash);
#endif
    }

#if !NET9_0_OR_GREATER && NETCOREAPP
    private static string ToHexStringLower(byte[] bytes)
        => string.Create(bytes.Length * 2, bytes, static (chars, args) => EncodeToUtf16(args, chars));

    private static void EncodeToUtf16(byte[] source, Span<char> destination)
    {
        ApplicationStateGuard.Ensure(destination.Length >= (source.Length * 2));

        for (int pos = 0; pos < source.Length; pos++)
        {
            ToCharsBuffer(source[pos], destination, pos * 2);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToCharsBuffer(byte value, Span<char> buffer, int startingIndex = 0)
    {
#pragma warning disable IDE0004 // Remove Unnecessary Cast
#pragma warning disable IDE0047 // Remove unnecessary parentheses
        uint difference = (((uint)value & 0xF0U) << 4) + ((uint)value & 0x0FU) - 0x8989U;
        uint packedResult = ((((uint)(-(int)difference) & 0x7070U) >> 4) + difference + 0xB9B9U) | (uint)0x2020U;

        buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
        buffer[startingIndex] = (char)(packedResult >> 8);
#pragma warning restore IDE0047 // Remove unnecessary parentheses
#pragma warning restore IDE0004 // Remove Unnecessary Cast
    }
#elif !NETCOREAPP
    private static string ToHexStringLower(byte[] bytes)
    {
        char[] chars = new char[bytes.Length * 2];

        string hexAlphabet = "0123456789abcdef";
        int charIndex = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[charIndex++] = hexAlphabet[b >> 4];
            chars[charIndex++] = hexAlphabet[b & 0xF];
        }

        return new string(chars);
    }
#endif
}
