// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if false
using System.Numerics;

using Microsoft.CodeAnalysis;

namespace System.Buffers.Binary;

[Embedded]
internal static class BinaryPrimitives
{
    /// <summary>
    /// Reverses a primitive value by performing an endianness swap of the specified <see cref="uint" /> value.
    /// </summary>
    /// <param name="value">The value to reverse.</param>
    /// <returns>The reversed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReverseEndianness(uint value)
        // This takes advantage of the fact that the JIT can detect
        // ROL32 / ROR32 patterns and output the correct intrinsic.
        //
        // Input: value = [ ww xx yy zz ]
        //
        // First line generates : [ ww xx yy zz ]
        //                      & [ 00 FF 00 FF ]
        //                      = [ 00 xx 00 zz ]
        //             ROR32(8) = [ zz 00 xx 00 ]
        //
        // Second line generates: [ ww xx yy zz ]
        //                      & [ FF 00 FF 00 ]
        //                      = [ ww 00 yy 00 ]
        //             ROL32(8) = [ 00 yy 00 ww ]
        //
        //                (sum) = [ zz yy xx ww ]
        //
        // Testing shows that throughput increases if the AND
        // is performed before the ROL / ROR.
        => BitOperations.RotateRight(value & 0x00FF00FFu, 8) // xx zz
            + BitOperations.RotateLeft(value & 0xFF00FF00u, 8); // ww yy

    /// <summary>
    /// Reverses a primitive value by performing an endianness swap of the specified <see cref="ulong" /> value.
    /// </summary>
    /// <param name="value">The value to reverse.</param>
    /// <returns>The reversed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReverseEndianness(ulong value) =>
        // Operations on 32-bit values have higher throughput than
        // operations on 64-bit values, so decompose.
        ((ulong)ReverseEndianness((uint)value) << 32)
            + ReverseEndianness((uint)(value >> 32));
}
#endif
