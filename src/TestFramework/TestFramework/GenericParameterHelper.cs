// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This class is designed to help user doing unit testing for types which uses generic types.
/// GenericParameterHelper satisfies some common generic type constraints
/// such as:
/// 1. public default constructor
/// 2. implements common interface: IComparable, IEnumerable.
/// </summary>
[SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "Compat reasons.")]

// This next suppression could mask a problem, since Equals and CompareTo may not agree!
[SuppressMessage("Microsoft.Design", "CA1036:OverrideMethodsOnComparableTypes", Justification = "Compat reasons.")]
[SuppressMessage("Design", "CA1010:Generic interface should also be implemented", Justification = "Part of the public API")]

// GenericParameterHelper in full CLR version also implements ICloneable, but we don't have ICloneable in core CLR
public class GenericParameterHelper : IComparable, IEnumerable
{
    private static readonly Random Randomizer = new();
    private List<object>? _ienumerableStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericParameterHelper"/> class that
    /// satisfies the 'newable' constraint in C# generics.
    /// </summary>
    /// <remarks>
    /// This constructor initializes the Data property to a random value.
    /// </remarks>
    public GenericParameterHelper()
    {
        Data = Randomizer.Next();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericParameterHelper"/> class that
    /// initializes the Data property to a user-supplied value.
    /// </summary>
    /// <param name="data">Any integer value.</param>
    public GenericParameterHelper(int data)
    {
        Data = data;
    }

    /// <summary>
    /// Gets or sets the Data.
    /// </summary>
    public int Data { get; set; }

    /// <summary>
    /// Do the value comparison for two GenericParameterHelper object.
    /// </summary>
    /// <param name="obj">object to do comparison with.</param>
    /// <returns>true if obj has the same value as 'this' GenericParameterHelper object.
    /// false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        var other = obj as GenericParameterHelper;

        return Data == other?.Data;
    }

    /// <summary>
    /// Returns a hashcode for this object.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => Data.GetHashCode();

    /// <summary>
    /// Compares the data of the two <see cref="GenericParameterHelper"/> objects.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>
    /// A signed number indicating the relative values of this instance and value.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the object passed in is not an instance of <see cref="GenericParameterHelper"/>.
    /// </exception>
    public int CompareTo(object? obj) => obj is GenericParameterHelper gpf
            ? Data.CompareTo(gpf.Data)
            : throw new NotSupportedException("GenericParameterHelper object is designed to compare objects of GenericParameterHelper type only.");

    /// <summary>
    /// Returns an IEnumerator object whose length is derived from
    /// the Data property.
    /// </summary>
    /// <returns>The IEnumerator object.</returns>
    public IEnumerator GetEnumerator()
    {
        int size = Data % 10;
        if (_ienumerableStore == null)
        {
            _ienumerableStore = new List<object>(size);

            for (int i = 0; i < size; i++)
            {
                _ienumerableStore.Add(new object());
            }
        }

        return _ienumerableStore.GetEnumerator();
    }

    /// <summary>
    /// Returns a GenericParameterHelper object that is equal to
    /// the current object.
    /// </summary>
    /// <returns>The cloned object.</returns>
    public object Clone()
    {
        GenericParameterHelper clone = new() { Data = Data };
        return clone;
    }
}
