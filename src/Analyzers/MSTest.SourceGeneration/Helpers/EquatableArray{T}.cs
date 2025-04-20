// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using System.Collections.Immutable;

namespace MSTest.SourceGeneration.Helpers;

// Copy from https://github.com/Sergio0694/ComputeSharp/blob/120aff270539996ef9fc52fe46561d12da0b89d4/src/ComputeSharp.SourceGeneration/Helpers/EquatableArray%7BT%7D.cs

/// <summary>
/// An immutable, equatable array. This is equivalent to <see cref="ImmutableArray{T}"/> but with value equality support.
/// </summary>
/// <typeparam name="T">The type of values in the array.</typeparam>
/// <param name="array">The input <see cref="ImmutableArray{T}"/> to wrap.</param>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    /// <summary>
    /// The underlying <typeparamref name="T"/> array.
    /// </summary>
    private readonly T[]? _array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);

    /// <summary>
    /// Gets a reference to an item at a specified position within the array.
    /// </summary>
    /// <param name="index">The index of the item to retrieve a reference to.</param>
    /// <returns>A reference to an item at a specified position within the array.</returns>
    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref AsImmutableArray().ItemRef(index);
    }

    /// <summary>
    /// Gets a value indicating whether the current array is empty.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AsImmutableArray().IsEmpty;
    }

    /// <summary>
    /// Gets a value indicating whether the current array is default or empty.
    /// </summary>
    public bool IsDefaultOrEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AsImmutableArray().IsDefaultOrEmpty;
    }

    /// <summary>
    /// Gets the length of the current array.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AsImmutableArray().Length;
    }

    /// <inheritdoc/>
    public bool Equals(EquatableArray<T> array)
        => AsSpan().SequenceEqual(array.AsSpan());

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> array && Equals(this, array);

    /// <inheritdoc/>
    public override unsafe int GetHashCode()
    {
        if (_array is not T[] array)
        {
            return 0;
        }

        HashCode hashCode = default;

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<T> span = array;
            ref T r0 = ref MemoryMarshal.GetReference(span);
            ref byte r1 = ref Unsafe.As<T, byte>(ref r0);

            fixed (byte* p = &r1)
            {
                ReadOnlySpan<byte> bytes = new(p, span.Length);

                hashCode.AddBytes(bytes);
            }
        }
        else
        {
            foreach (T item in array)
            {
                hashCode.Add(item);
            }
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Gets an <see cref="ImmutableArray{T}"/> instance from the current <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <returns>The <see cref="ImmutableArray{T}"/> from the current <see cref="EquatableArray{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> AsImmutableArray()
        => Unsafe.As<T[]?, ImmutableArray<T>>(ref Unsafe.AsRef(in _array));

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <param name="array">The input <see cref="ImmutableArray{T}"/> instance.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.</returns>
    public static EquatableArray<T> FromImmutableArray(ImmutableArray<T> array)
        => new(array);

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> wrapping the current items.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> wrapping the current items.</returns>
    public ReadOnlySpan<T> AsSpan()
        => AsImmutableArray().AsSpan();

    /// <summary>
    /// Copies the contents of this <see cref="EquatableArray{T}"/> instance. to a mutable array.
    /// </summary>
    /// <returns>The newly instantiated array.</returns>
    public T[] ToArray()
        => [.. AsImmutableArray()];

    /// <summary>
    /// Gets an <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.</returns>
    public ImmutableArray<T>.Enumerator GetEnumerator()
        => AsImmutableArray().GetEnumerator();

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)AsImmutableArray()).GetEnumerator();

    /// <summary>
    /// Implicitly converts an <see cref="ImmutableArray{T}"/> to <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <returns>An <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.</returns>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => FromImmutableArray(array);

    /// <summary>
    /// Implicitly converts an <see cref="EquatableArray{T}"/> to <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}"/> instance from a given <see cref="EquatableArray{T}"/>.</returns>
    public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.AsImmutableArray();

    /// <summary>
    /// Checks whether two <see cref="EquatableArray{T}"/> values are the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are equal.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>
    /// Checks whether two <see cref="EquatableArray{T}"/> values are not the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are not equal.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
