// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class AppxManifestInfo
{
    // The alphabet Windows uses to encode the publisher hash (Douglas Crockford's base32: the digits
    // and lowercase letters with i, l, o and u removed). Must not be reordered.
    private const string PublisherHashAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    /// <summary>
    /// Computes the 13-character publisher id (the suffix of the package family name) for
    /// <paramref name="publisher"/>, using the public Windows algorithm: base32-encode the first 8
    /// bytes of the SHA-256 hash of the UTF-16LE publisher string.
    /// </summary>
    /// <param name="publisher">The manifest's <c>Identity/@Publisher</c> value.</param>
    /// <returns>The 13-character publisher id.</returns>
    internal static string ComputePublisherId(string publisher)
    {
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));

        // Take the first 8 bytes (64 bits) and encode them big-endian as 13 base32 characters. 64 bits
        // is not a multiple of 5, so the final character carries the 4 leftover bits padded with a 0.
        var builder = new StringBuilder(13);
        int buffer = 0;
        int bitsInBuffer = 0;
        for (int i = 0; i < 8; i++)
        {
            buffer = (buffer << 8) | hash[i];
            bitsInBuffer += 8;
            // Stryker disable once Equality: delaying an exactly full five-bit group until the next byte preserves the same bit stream.
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                // Stryker disable once Bitwise: buffer is nonnegative because only its low four bits are retained between input bytes.
                builder.Append(PublisherHashAlphabet[(buffer >> bitsInBuffer) & 0x1F]);
                // Stryker disable once all: consumed high bits cannot affect any later five-bit group selected from the low-order buffer.
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        // Eight input bytes leave exactly four bits after all complete five-bit groups.
        builder.Append(PublisherHashAlphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);

        return builder.ToString();
    }
}
