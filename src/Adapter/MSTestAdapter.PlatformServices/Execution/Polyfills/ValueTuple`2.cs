// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System;

internal static class HashHelpers
{
    public static int Combine(int h1, int h2)
    {
        uint num = (uint)((h1 << 5) | (int)((uint)h1 >> 27));
        return (int)((num + (uint)h1) ^ (uint)h2);
    }

    internal static int CombineHashCodes(int h1, int h2)
        => Combine(Combine(RandomSeed, h1), h2);

    public static readonly int RandomSeed = Guid.NewGuid().GetHashCode();
}

internal interface ITupleInternal
{
    int GetHashCode(IEqualityComparer comparer);

    int Size { get; }

    string ToStringEnd();
}

[StructLayout(LayoutKind.Auto)]
internal struct ValueTuple<T1, T2> : IEquatable<ValueTuple<T1, T2>>, IStructuralEquatable, IStructuralComparable, IComparable, IComparable<ValueTuple<T1, T2>>, ITupleInternal
{
    /// <summary>
    /// The current <see cref="ValueTuple{T1, T2}"/> instance's first component.
    /// </summary>
    public T1 Item1;

    /// <summary>
    /// The current <see cref="ValueTuple{T1, T2}"/> instance's second component.
    /// </summary>
    public T2 Item2;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueTuple{T1, T2}"/> struct.
    /// </summary>
    /// <param name="item1">The value of the tuple's first component.</param>
    /// <param name="item2">The value of the tuple's second component.</param>
    public ValueTuple(T1 item1, T2 item2)
    {
        Item1 = item1;
        Item2 = item2;
    }

    /// <summary>
    /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
    ///
    /// <remarks>
    /// The <paramref name="obj"/> parameter is considered to be equal to the current instance under the following conditions:
    /// <list type="bullet">
    ///     <item><description>It is a <see cref="ValueTuple{T1, T2}"/> value type.</description></item>
    ///     <item><description>Its components are of the same types as those of the current instance.</description></item>
    ///     <item><description>Its components are equal to those of the current instance. Equality is determined by the default object equality comparer for each component.</description></item>
    /// </list>
    /// </remarks>
    public override readonly bool Equals([NotNullWhen(true)] object? obj)
        => obj is ValueTuple<T1, T2> tuple && Equals(tuple);

    /// <summary>
    /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified <see cref="ValueTuple{T1, T2}"/>.
    /// </summary>
    /// <param name="other">The tuple to compare with this instance.</param>
    /// <returns><see langword="true"/> if the current instance is equal to the specified tuple; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// The <paramref name="other"/> parameter is considered to be equal to the current instance if each of its fields
    /// are equal to that of the current instance, using the default comparer for that field's type.
    /// </remarks>
    public readonly bool Equals((T1, T2) other)
        => EqualityComparer<T1>.Default.Equals(Item1, other.Item1) && EqualityComparer<T2>.Default.Equals(Item2, other.Item2);

    /// <summary>
    /// Returns a value that indicates whether the current <see cref="ValueTuple{T1, T2}"/> instance is equal to a specified object based on a specified comparison method.
    /// </summary>
    /// <param name="other">The object to compare with this instance.</param>
    /// <param name="comparer">An object that defines the method to use to evaluate whether the two objects are equal.</param>
    /// <returns><see langword="true"/> if the current instance is equal to the specified object; otherwise, <see langword="false"/>.</returns>
    ///
    /// <remarks>
    /// This member is an explicit interface member implementation. It can be used only when the
    ///  <see cref="ValueTuple{T1, T2}"/> instance is cast to an <see cref="IStructuralEquatable"/> interface.
    ///
    /// The <see cref="IEqualityComparer.Equals"/> implementation is called only if <c>other</c> is not <see langword="null"/>,
    ///  and if it can be successfully cast (in C#) or converted (in Visual Basic) to a <see cref="ValueTuple{T1, T2}"/>
    ///  whose components are of the same types as those of the current instance. The IStructuralEquatable.Equals(Object, IEqualityComparer) method
    ///  first passes the <see cref="Item1"/> values of the <see cref="ValueTuple{T1, T2}"/> objects to be compared to the
    ///  <see cref="IEqualityComparer.Equals"/> implementation. If this method call returns <see langword="true"/>, the method is
    ///  called again and passed the <see cref="Item2"/> values of the two <see cref="ValueTuple{T1, T2}"/> instances.
    /// </remarks>
    readonly bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
        other is ValueTuple<T1, T2> vt &&
        comparer.Equals(Item1, vt.Item1) &&
        comparer.Equals(Item2, vt.Item2);

    readonly int IComparable.CompareTo(object other)
        => other is null
            ? 1
            : other is ValueTuple<T1, T2> objTuple
                ? CompareTo(objTuple)
                : throw new ArgumentException("Argument must be of type ValueTuple<T1, T2>.", nameof(other));

    /// <summary>Compares this instance to a specified instance and returns an indication of their relative values.</summary>
    /// <param name="other">An instance to compare.</param>
    /// <returns>
    /// A signed number indicating the relative values of this instance and <paramref name="other"/>.
    /// Returns less than zero if this instance is less than <paramref name="other"/>, zero if this
    /// instance is equal to <paramref name="other"/>, and greater than zero if this instance is greater
    /// than <paramref name="other"/>.
    /// </returns>
    public readonly int CompareTo((T1, T2) other)
    {
        int c = Comparer<T1>.Default.Compare(Item1, other.Item1);
        return c != 0 ? c : Comparer<T2>.Default.Compare(Item2, other.Item2);
    }

    readonly int IStructuralComparable.CompareTo(object other, IComparer comparer)
    {
        if (other == null)
        {
            return 1;
        }

        if (other is not ValueTuple<T1, T2> objTuple)
        {
            throw new ArgumentException("Argument must be of type ValueTuple<T1, T2>.", nameof(other));
        }

        int c = comparer.Compare(Item1, objTuple.Item1);
        return c != 0 ? c : comparer.Compare(Item2, objTuple.Item2);
    }

    /// <summary>
    /// Returns the hash code for the current <see cref="ValueTuple{T1, T2}"/> instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override readonly int GetHashCode()
        => HashHelpers.CombineHashCodes(EqualityComparer<T1>.Default.GetHashCode(Item1), EqualityComparer<T2>.Default.GetHashCode(Item2));

    readonly int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        => GetHashCodeCore(comparer);

    private readonly int GetHashCodeCore(IEqualityComparer comparer)
        => HashHelpers.CombineHashCodes(comparer.GetHashCode(Item1), comparer.GetHashCode(Item2));

    readonly int ITupleInternal.GetHashCode(IEqualityComparer comparer)
        => GetHashCodeCore(comparer);

    /// <summary>
    /// Returns a string that represents the value of this <see cref="ValueTuple{T1, T2}"/> instance.
    /// </summary>
    /// <returns>The string representation of this <see cref="ValueTuple{T1, T2}"/> instance.</returns>
    /// <remarks>
    /// The string returned by this method takes the form <c>(Item1, Item2)</c>,
    /// where <c>Item1</c> and <c>Item2</c> represent the values of the <see cref="Item1"/>
    /// and <see cref="Item2"/> fields. If either field value is <see langword="null"/>,
    /// it is represented as <see cref="string.Empty"/>.
    /// </remarks>
    public override readonly string ToString()
        => "(" + Item1?.ToString() + ", " + Item2?.ToString() + ")";

    readonly string ITupleInternal.ToStringEnd()
        => Item1?.ToString() + ", " + Item2?.ToString() + ")";

    readonly int ITupleInternal.Size => 2;
}
#endif
