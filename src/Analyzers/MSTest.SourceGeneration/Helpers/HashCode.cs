// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.ComponentModel;
using System.Security.Cryptography;

namespace System;

/// <summary>
/// A polyfill type that mirrors some methods from <see cref="HashCode"/> on .7.
/// </summary>
internal struct HashCode
{
    private const uint Prime1 = 2654435761U;
    private const uint Prime2 = 2246822519U;
    private const uint Prime3 = 3266489917U;
    private const uint Prime4 = 668265263U;
    private const uint Prime5 = 374761393U;

    private static readonly uint Seed = GenerateGlobalSeed();

    private uint _v1;
    private uint _v2;
    private uint _v3;
    private uint _v4;
    private uint _queue1;
    private uint _queue2;
    private uint _queue3;
    private uint _length;

    /// <summary>
    /// Initializes the default seed.
    /// </summary>
    /// <returns>A random seed.</returns>
    private static unsafe uint GenerateGlobalSeed()
    {
        byte[] bytes = new byte[4];

        RandomNumberGenerator.Create().GetBytes(bytes);

        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// Combines a value into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the value to combine into the hash code.</typeparam>
    /// <param name="value">The value to combine into the hash code.</param>
    /// <returns>The hash code that represents the value.</returns>
    public static int Combine<T1>(T1 value)
    {
        uint hc1 = (uint)(value?.GetHashCode() ?? 0);
        uint hash = MixEmptyState();

        hash += 4;
        hash = QueueRound(hash, hc1);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines two values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2>(T1 value1, T2 value2)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hash = MixEmptyState();

        hash += 8;
        hash = QueueRound(hash, hc1);
        hash = QueueRound(hash, hc2);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines three values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hash = MixEmptyState();

        hash += 12;
        hash = QueueRound(hash, hc1);
        hash = QueueRound(hash, hc2);
        hash = QueueRound(hash, hc3);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines four values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <param name="value4">The fourth value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hc4 = (uint)(value4?.GetHashCode() ?? 0);

        Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

        v1 = Round(v1, hc1);
        v2 = Round(v2, hc2);
        v3 = Round(v3, hc3);
        v4 = Round(v4, hc4);

        uint hash = MixState(v1, v2, v3, v4);

        hash += 16;
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines five values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <param name="value4">The fourth value to combine into the hash code.</param>
    /// <param name="value5">The fifth value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
        uint hc5 = (uint)(value5?.GetHashCode() ?? 0);

        Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

        v1 = Round(v1, hc1);
        v2 = Round(v2, hc2);
        v3 = Round(v3, hc3);
        v4 = Round(v4, hc4);

        uint hash = MixState(v1, v2, v3, v4);

        hash += 20;
        hash = QueueRound(hash, hc5);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines six values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <param name="value4">The fourth value to combine into the hash code.</param>
    /// <param name="value5">The fifth value to combine into the hash code.</param>
    /// <param name="value6">The sixth value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
        uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
        uint hc6 = (uint)(value6?.GetHashCode() ?? 0);

        Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

        v1 = Round(v1, hc1);
        v2 = Round(v2, hc2);
        v3 = Round(v3, hc3);
        v4 = Round(v4, hc4);

        uint hash = MixState(v1, v2, v3, v4);

        hash += 24;
        hash = QueueRound(hash, hc5);
        hash = QueueRound(hash, hc6);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines seven values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code.</typeparam>
    /// <typeparam name="T7">The type of the seventh value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <param name="value4">The fourth value to combine into the hash code.</param>
    /// <param name="value5">The fifth value to combine into the hash code.</param>
    /// <param name="value6">The sixth value to combine into the hash code.</param>
    /// <param name="value7">The seventh value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
        uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
        uint hc6 = (uint)(value6?.GetHashCode() ?? 0);
        uint hc7 = (uint)(value7?.GetHashCode() ?? 0);

        Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

        v1 = Round(v1, hc1);
        v2 = Round(v2, hc2);
        v3 = Round(v3, hc3);
        v4 = Round(v4, hc4);

        uint hash = MixState(v1, v2, v3, v4);

        hash += 28;
        hash = QueueRound(hash, hc5);
        hash = QueueRound(hash, hc6);
        hash = QueueRound(hash, hc7);
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Combines eight values into a hash code.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to combine into the hash code.</typeparam>
    /// <typeparam name="T2">The type of the second value to combine into the hash code.</typeparam>
    /// <typeparam name="T3">The type of the third value to combine into the hash code.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to combine into the hash code.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to combine into the hash code.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to combine into the hash code.</typeparam>
    /// <typeparam name="T7">The type of the seventh value to combine into the hash code.</typeparam>
    /// <typeparam name="T8">The type of the eighth value to combine into the hash code.</typeparam>
    /// <param name="value1">The first value to combine into the hash code.</param>
    /// <param name="value2">The second value to combine into the hash code.</param>
    /// <param name="value3">The third value to combine into the hash code.</param>
    /// <param name="value4">The fourth value to combine into the hash code.</param>
    /// <param name="value5">The fifth value to combine into the hash code.</param>
    /// <param name="value6">The sixth value to combine into the hash code.</param>
    /// <param name="value7">The seventh value to combine into the hash code.</param>
    /// <param name="value8">The eighth value to combine into the hash code.</param>
    /// <returns>The hash code that represents the values.</returns>
    public static int Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
    {
        uint hc1 = (uint)(value1?.GetHashCode() ?? 0);
        uint hc2 = (uint)(value2?.GetHashCode() ?? 0);
        uint hc3 = (uint)(value3?.GetHashCode() ?? 0);
        uint hc4 = (uint)(value4?.GetHashCode() ?? 0);
        uint hc5 = (uint)(value5?.GetHashCode() ?? 0);
        uint hc6 = (uint)(value6?.GetHashCode() ?? 0);
        uint hc7 = (uint)(value7?.GetHashCode() ?? 0);
        uint hc8 = (uint)(value8?.GetHashCode() ?? 0);

        Initialize(out uint v1, out uint v2, out uint v3, out uint v4);

        v1 = Round(v1, hc1);
        v2 = Round(v2, hc2);
        v3 = Round(v3, hc3);
        v4 = Round(v4, hc4);

        v1 = Round(v1, hc5);
        v2 = Round(v2, hc6);
        v3 = Round(v3, hc7);
        v4 = Round(v4, hc8);

        uint hash = MixState(v1, v2, v3, v4);

        hash += 32;
        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <summary>
    /// Adds a single value to the current hash.
    /// </summary>
    /// <typeparam name="T">The type of the value to add into the hash code.</typeparam>
    /// <param name="value">The value to add into the hash code.</param>
    public void Add<T>(T value)
        => Add(value?.GetHashCode() ?? 0);

    /// <summary>
    /// Adds a single value to the current hash.
    /// </summary>
    /// <typeparam name="T">The type of the value to add into the hash code.</typeparam>
    /// <param name="value">The value to add into the hash code.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> instance to use.</param>
    public void Add<T>(T value, IEqualityComparer<T>? comparer)
        => Add(value is null ? 0 : (comparer?.GetHashCode(value) ?? value.GetHashCode()));

    /// <summary>
    /// Adds a span of bytes to the hash code.
    /// </summary>
    /// <param name="value">The span.</param>
    public void AddBytes(ReadOnlySpan<byte> value)
    {
        ref byte pos = ref MemoryMarshal.GetReference(value);
        ref byte end = ref Unsafe.Add(ref pos, value.Length);

        while ((nint)Unsafe.ByteOffset(ref pos, ref end) >= sizeof(int))
        {
            Add(Unsafe.ReadUnaligned<int>(ref pos));
            pos = ref Unsafe.Add(ref pos, sizeof(int));
        }

        while (Unsafe.IsAddressLessThan(ref pos, ref end))
        {
            Add((int)pos);
            pos = ref Unsafe.Add(ref pos, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Initialize(out uint v1, out uint v2, out uint v3, out uint v4)
    {
        v1 = Seed + Prime1 + Prime2;
        v2 = Seed + Prime2;
        v3 = Seed;
        v4 = Seed - Prime1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Round(uint hash, uint input)
        => RotateLeft(hash + (input * Prime2), 13) * Prime1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint QueueRound(uint hash, uint queuedValue)
        => RotateLeft(hash + (queuedValue * Prime3), 17) * Prime4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixState(uint v1, uint v2, uint v3, uint v4)
        => RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixEmptyState()
        => Seed + Prime5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MixFinal(uint hash)
    {
        hash ^= hash >> 15;
        hash *= Prime2;
        hash ^= hash >> 13;
        hash *= Prime3;
        hash ^= hash >> 16;

        return hash;
    }

    private void Add(int value)
    {
        uint val = (uint)value;
        uint previousLength = _length++;
        uint position = previousLength % 4;

        if (position == 0)
        {
            _queue1 = val;
        }
        else if (position == 1)
        {
            _queue2 = val;
        }
        else if (position == 2)
        {
            _queue3 = val;
        }
        else
        {
            if (previousLength == 3)
            {
                Initialize(out _v1, out _v2, out _v3, out _v4);
            }

            _v1 = Round(_v1, _queue1);
            _v2 = Round(_v2, _queue2);
            _v3 = Round(_v3, _queue3);
            _v4 = Round(_v4, val);
        }
    }

    /// <summary>
    /// Gets the resulting hashcode from the current instance.
    /// </summary>
    /// <returns>The resulting hashcode from the current instance.</returns>
    public readonly int ToHashCode()
    {
        uint length = _length;
        uint position = length % 4;
        uint hash = length < 4 ? MixEmptyState() : MixState(_v1, _v2, _v3, _v4);

        hash += length * 4;

        if (position > 0)
        {
            hash = QueueRound(hash, _queue1);

            if (position > 1)
            {
                hash = QueueRound(hash, _queue2);

                if (position > 2)
                {
                    hash = QueueRound(hash, _queue3);
                }
            }
        }

        hash = MixFinal(hash);

        return (int)hash;
    }

    /// <inheritdoc/>
    [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override readonly int GetHashCode()
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        => throw new NotSupportedException();

    /// <inheritdoc/>
    [Obsolete("HashCode is a mutable struct and should not be compared with other HashCodes.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    public override readonly bool Equals(object? obj)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
        => throw new NotSupportedException();

    /// <summary>
    /// Rotates the specified value left by the specified number of bits.
    /// Similar in behavior to the x86 instruction ROL.
    /// </summary>
    /// <param name="value">The value to rotate.</param>
    /// <param name="offset">The number of bits to rotate by.
    /// Any value outside the range [0..31] is treated as congruent mod 32.</param>
    /// <returns>The rotated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int offset)
        => (value << offset) | (value >> (32 - offset));
}
