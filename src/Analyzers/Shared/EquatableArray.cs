// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace MSTest.Analyzers.Shared;

/// <summary>
/// Value-equatable wrapper around <see cref="ImmutableArray{T}"/> so incremental generation
/// can cache results between runs.
/// </summary>
/// <typeparam name="T">Element type stored in the underlying <see cref="ImmutableArray{T}"/>.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
        => _array = array;

    public ImmutableArray<T> Items => AsImmutableArray();

    public int Count => Length;

    public int Length => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => AsImmutableArray()[index];

    public ImmutableArray<T> AsImmutableArray()
        => _array.IsDefault ? ImmutableArray<T>.Empty : _array;

    public ImmutableArray<T>.Enumerator GetEnumerator() => AsImmutableArray().GetEnumerator();

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        ImmutableArray<T> left = AsImmutableArray();
        ImmutableArray<T> right = other.AsImmutableArray();
        for (int i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in AsImmutableArray())
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }
}

internal static class EquatableArrayExtensions
{
    private static readonly Func<object?, bool> NotNullTest = x => x is not null;

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
        => new(source.ToImmutableArray());

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
        => source.Where((Func<T?, bool>)NotNullTest)!;
}
