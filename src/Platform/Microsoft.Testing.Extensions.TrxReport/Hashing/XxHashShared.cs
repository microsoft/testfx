// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable RS0030 // Do not use banned APIs - Debug is okay here. RoslynDebug isn't yet available in PlatformServices which links this file.

namespace System.IO.Hashing;

/// <summary>Shared implementation of the XXH3 hash algorithm for 64-bit in XxHash3 and <see cref="TestFx.Hashing.XxHash128"/> version.</summary>
#if NET
[SkipLocalsInit]
#endif
internal static unsafe partial class XxHashShared
{
    public const int StripeLengthBytes = 64;
    public const int SecretLengthBytes = 192;
    public const int SecretSizeMin = 136;
    public const int SecretLastAccStartBytes = 7;
    public const int SecretConsumeRateBytes = 8;
    public const int SecretMergeAccsStartBytes = 11;
    public const int NumStripesPerBlock = (SecretLengthBytes - StripeLengthBytes) / SecretConsumeRateBytes;
    public const int AccumulatorCount = StripeLengthBytes / sizeof(ulong);
    public const int MidSizeMaxBytes = 240;
    public const int InternalBufferStripes = InternalBufferLengthBytes / StripeLengthBytes;
    public const int InternalBufferLengthBytes = 256;

    // Cast of DefaultSecret byte[] => ulong[] (See above for the correspondence)
#pragma warning disable SA1310 // Field names should not contain underscore
    public const ulong DefaultSecretUInt64_0 = 0xBE4BA423396CFEB8;
    public const ulong DefaultSecretUInt64_1 = 0x1CAD21F72C81017C;
    public const ulong DefaultSecretUInt64_2 = 0xDB979083E96DD4DE;
    public const ulong DefaultSecretUInt64_3 = 0x1F67B3B7A4A44072;
    public const ulong DefaultSecretUInt64_4 = 0x78E5C0CC4EE679CB;
    public const ulong DefaultSecretUInt64_5 = 0x2172FFCC7DD05A82;
    public const ulong DefaultSecretUInt64_6 = 0x8E2443F7744608B8;
    public const ulong DefaultSecretUInt64_7 = 0x4C263A81E69035E0;
    public const ulong DefaultSecretUInt64_8 = 0xCB00C391BB52283C;
    public const ulong DefaultSecretUInt64_9 = 0xA32E531B8B65D088;
    public const ulong DefaultSecretUInt64_10 = 0x4EF90DA297486471;
    public const ulong DefaultSecretUInt64_11 = 0xD8ACDEA946EF1938;
    public const ulong DefaultSecretUInt64_12 = 0x3F349CE33F76FAA8;
    public const ulong DefaultSecretUInt64_13 = 0x1D4F0BC7C7BBDCF9;
    public const ulong DefaultSecretUInt64_14 = 0x3159B4CD4BE0518A;
    public const ulong DefaultSecretUInt64_15 = 0x647378D9C97E9FC8;

    // Cast of DefaultSecret offset by 3 bytes, byte[] => ulong[]
    public const ulong DefaultSecret3UInt64_0 = 0x81017CBE4BA42339;
    public const ulong DefaultSecret3UInt64_1 = 0x6DD4DE1CAD21F72C;
    public const ulong DefaultSecret3UInt64_2 = 0xA44072DB979083E9;
    public const ulong DefaultSecret3UInt64_3 = 0xE679CB1F67B3B7A4;
    public const ulong DefaultSecret3UInt64_4 = 0xD05A8278E5C0CC4E;
    public const ulong DefaultSecret3UInt64_5 = 0x4608B82172FFCC7D;
    public const ulong DefaultSecret3UInt64_6 = 0x9035E08E2443F774;
    public const ulong DefaultSecret3UInt64_7 = 0x52283C4C263A81E6;
    public const ulong DefaultSecret3UInt64_8 = 0x65D088CB00C391BB;
    public const ulong DefaultSecret3UInt64_9 = 0x486471A32E531B8B;
    public const ulong DefaultSecret3UInt64_10 = 0xEF19384EF90DA297;
    public const ulong DefaultSecret3UInt64_11 = 0x76FAA8D8ACDEA946;
    public const ulong DefaultSecret3UInt64_12 = 0xBBDCF93F349CE33F;
    public const ulong DefaultSecret3UInt64_13 = 0xE0518A1D4F0BC7C7;

    public const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    public const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    public const ulong Prime64_3 = 0x165667B19E3779F9UL;
    public const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    public const ulong Prime64_5 = 0x27D4EB2F165667C5UL;

    public const uint Prime32_1 = 0x9E3779B1U;
    public const uint Prime32_2 = 0x85EBCA77U;
    public const uint Prime32_3 = 0xC2B2AE3DU;
    public const uint Prime32_4 = 0x27D4EB2FU;
    public const uint Prime32_5 = 0x165667B1U;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>Gets the default secret for when no seed is provided.</summary>
    /// <remarks>This is the same as a custom secret derived from a seed of 0.</remarks>
    public static
#if NET
        ReadOnlySpan<byte> DefaultSecret =>
#else
        readonly byte[] DefaultSecret =
#endif
    [
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, // DefaultSecretUInt64_0
        0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c, // DefaultSecretUInt64_1
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, // DefaultSecretUInt64_2
        0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f, // DefaultSecretUInt64_3
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, // DefaultSecretUInt64_4
        0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21, // DefaultSecretUInt64_5
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, // DefaultSecretUInt64_6
        0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c, // DefaultSecretUInt64_7
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, // DefaultSecretUInt64_8
        0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3, // DefaultSecretUInt64_9
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, // DefaultSecretUInt64_10
        0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8, // DefaultSecretUInt64_11
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, // DefaultSecretUInt64_12
        0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d, // DefaultSecretUInt64_13
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, // DefaultSecretUInt64_14
        0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64, // DefaultSecretUInt64_15
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, // DefaultSecretUInt64_16
        0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb, // DefaultSecretUInt64_17
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, // DefaultSecretUInt64_18
        0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e, // DefaultSecretUInt64_19
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, // DefaultSecretUInt64_20
        0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce, // DefaultSecretUInt64_21
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, // DefaultSecretUInt64_22
        0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e, // DefaultSecretUInt64_23
    ];

#if DEBUG && NET // TODO
    static XxHashShared()
    {
        // Make sure DefaultSecret is the custom secret derived from a seed of 0.
        byte* secret = stackalloc byte[SecretLengthBytes];
        DeriveSecretFromSeed(secret, 0);

        Debug.Assert(DefaultSecret.Length == SecretLengthBytes, "DefaultSecret.Length was expected to be equal to SecretLengthBytes");
        for (int i = 0; i < DefaultSecret.Length; i++)
        {
            Debug.Assert(DefaultSecret[i] == secret[i], "DefaultSecret was expected to be equal to secret.");
        }

        // Validate some relationships.
        Debug.Assert(InternalBufferLengthBytes % StripeLengthBytes == 0, "InternalBufferLengthBytes % StripeLengthBytes was expected to be zero.");

        ReadOnlySpan<ulong> defaultSecretUInt64 = MemoryMarshal.Cast<byte, ulong>(DefaultSecret);
        Debug.Assert(ReadLE64(defaultSecretUInt64[0]) == DefaultSecretUInt64_0, "defaultSecretUInt64[0] was expected to be equals to DefaultSecretUInt64_0");
        Debug.Assert(ReadLE64(defaultSecretUInt64[1]) == DefaultSecretUInt64_1, "defaultSecretUInt64[1] was expected to be equals to DefaultSecretUInt64_1");
        Debug.Assert(ReadLE64(defaultSecretUInt64[2]) == DefaultSecretUInt64_2, "defaultSecretUInt64[2] was expected to be equals to DefaultSecretUInt64_2");
        Debug.Assert(ReadLE64(defaultSecretUInt64[3]) == DefaultSecretUInt64_3, "defaultSecretUInt64[3] was expected to be equals to DefaultSecretUInt64_3");
        Debug.Assert(ReadLE64(defaultSecretUInt64[4]) == DefaultSecretUInt64_4, "defaultSecretUInt64[4] was expected to be equals to DefaultSecretUInt64_4");
        Debug.Assert(ReadLE64(defaultSecretUInt64[5]) == DefaultSecretUInt64_5, "defaultSecretUInt64[5] was expected to be equals to DefaultSecretUInt64_5");
        Debug.Assert(ReadLE64(defaultSecretUInt64[6]) == DefaultSecretUInt64_6, "defaultSecretUInt64[6] was expected to be equals to DefaultSecretUInt64_6");
        Debug.Assert(ReadLE64(defaultSecretUInt64[7]) == DefaultSecretUInt64_7, "defaultSecretUInt64[7] was expected to be equals to DefaultSecretUInt64_7");
        Debug.Assert(ReadLE64(defaultSecretUInt64[8]) == DefaultSecretUInt64_8, "defaultSecretUInt64[8] was expected to be equals to DefaultSecretUInt64_8");
        Debug.Assert(ReadLE64(defaultSecretUInt64[9]) == DefaultSecretUInt64_9, "defaultSecretUInt64[9] was expected to be equals to DefaultSecretUInt64_9");
        Debug.Assert(ReadLE64(defaultSecretUInt64[10]) == DefaultSecretUInt64_10, "defaultSecretUInt64[10] was expected to be equals to DefaultSecretUInt64_10");
        Debug.Assert(ReadLE64(defaultSecretUInt64[11]) == DefaultSecretUInt64_11, "defaultSecretUInt64[11] was expected to be equals to DefaultSecretUInt64_11");
        Debug.Assert(ReadLE64(defaultSecretUInt64[12]) == DefaultSecretUInt64_12, "defaultSecretUInt64[12] was expected to be equals to DefaultSecretUInt64_12");
        Debug.Assert(ReadLE64(defaultSecretUInt64[13]) == DefaultSecretUInt64_13, "defaultSecretUInt64[13] was expected to be equals to DefaultSecretUInt64_13");
        Debug.Assert(ReadLE64(defaultSecretUInt64[14]) == DefaultSecretUInt64_14, "defaultSecretUInt64[14] was expected to be equals to DefaultSecretUInt64_14");
        Debug.Assert(ReadLE64(defaultSecretUInt64[15]) == DefaultSecretUInt64_15, "defaultSecretUInt64[15] was expected to be equals to DefaultSecretUInt64_15");

        ReadOnlySpan<ulong> defaultSecret3UInt64 = MemoryMarshal.Cast<byte, ulong>(DefaultSecret.Slice(3));
        Debug.Assert(ReadLE64(defaultSecret3UInt64[0]) == DefaultSecret3UInt64_0, "defaultSecret3UInt64[0] was expected to be equals to DefaultSecret3UInt64_0");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[1]) == DefaultSecret3UInt64_1, "defaultSecret3UInt64[1] was expected to be equals to DefaultSecret3UInt64_1");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[2]) == DefaultSecret3UInt64_2, "defaultSecret3UInt64[2] was expected to be equals to DefaultSecret3UInt64_2");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[3]) == DefaultSecret3UInt64_3, "defaultSecret3UInt64[3] was expected to be equals to DefaultSecret3UInt64_3");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[4]) == DefaultSecret3UInt64_4, "defaultSecret3UInt64[4] was expected to be equals to DefaultSecret3UInt64_4");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[5]) == DefaultSecret3UInt64_5, "defaultSecret3UInt64[5] was expected to be equals to DefaultSecret3UInt64_5");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[6]) == DefaultSecret3UInt64_6, "defaultSecret3UInt64[6] was expected to be equals to DefaultSecret3UInt64_6");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[7]) == DefaultSecret3UInt64_7, "defaultSecret3UInt64[7] was expected to be equals to DefaultSecret3UInt64_7");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[8]) == DefaultSecret3UInt64_8, "defaultSecret3UInt64[8] was expected to be equals to DefaultSecret3UInt64_8");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[9]) == DefaultSecret3UInt64_9, "defaultSecret3UInt64[9] was expected to be equals to DefaultSecret3UInt64_9");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[10]) == DefaultSecret3UInt64_10, "defaultSecret3UInt64[10] was expected to be equals to DefaultSecret3UInt64_10");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[11]) == DefaultSecret3UInt64_11, "defaultSecret3UInt64[11] was expected to be equals to DefaultSecret3UInt64_11");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[12]) == DefaultSecret3UInt64_12, "defaultSecret3UInt64[12] was expected to be equals to DefaultSecret3UInt64_12");
        Debug.Assert(ReadLE64(defaultSecret3UInt64[13]) == DefaultSecret3UInt64_13, "defaultSecret3UInt64[13] was expected to be equals to DefaultSecret3UInt64_13");

        static ulong ReadLE64(ulong data) => BitConverter.IsLittleEndian ? data : System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(data);
    }
#endif

    [StructLayout(LayoutKind.Auto)]
    public struct State
    {
        /// <summary>The accumulators. Length is <see cref="AccumulatorCount"/>.</summary>
        internal fixed ulong Accumulators[AccumulatorCount];

        /// <summary>Used to store a custom secret generated from a seed. Length is <see cref="SecretLengthBytes"/>.</summary>
        internal fixed byte Secret[SecretLengthBytes];

        /// <summary>The internal buffer. Length is <see cref="InternalBufferLengthBytes"/>.</summary>
        internal fixed byte Buffer[InternalBufferLengthBytes];

        /// <summary>The amount of memory in <see cref="Buffer"/>.</summary>
        internal uint BufferedCount;

        /// <summary>Number of stripes processed in the current block.</summary>
        internal ulong StripesProcessedInCurrentBlock;

        /// <summary>Total length hashed.</summary>
        internal ulong TotalLength;

        /// <summary>The seed employed (possibly 0).</summary>
        internal ulong Seed;
    }
}
