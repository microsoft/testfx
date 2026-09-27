// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Specifies a range of values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CombinatorialRangeAttribute : Attribute, ICombinatorialValuesProvider
{
    private readonly object[] _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialRangeAttribute"/> class.
    /// </summary>
    /// <param name="from">The value at the beginning of the range.</param>
    /// <param name="count">The positive number of consecutive values to include.</param>
    public CombinatorialRangeAttribute(int from, int count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if ((long)from + count - 1 > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), FrameworkMessages.CombinatorialRangeExceedsInt32);
        }

        _values = new object[count];
        for (int i = 0; i < count; i++)
        {
            _values[i] = from + i;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialRangeAttribute"/> class.
    /// </summary>
    /// <param name="from">The value at the beginning of the range.</param>
    /// <param name="to">The inclusive value at the end of the range.</param>
    /// <param name="step">The non-zero amount by which each value changes.</param>
    public CombinatorialRangeAttribute(int from, int to, int step)
    {
        if (step > 0)
        {
            if (to < from)
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }
        }
        else if (step < 0)
        {
            if (to > from)
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        long valueCount = (((long)to - from) / step) + 1;
        if (valueCount > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(to), FrameworkMessages.CombinatorialRangeTooManyValues);
        }

        int count = (int)valueCount;
        _values = new object[count];
        for (int i = 0; i < count; i++)
        {
            _values[i] = checked((int)(from + ((long)i * step)));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialRangeAttribute"/> class.
    /// </summary>
    /// <param name="from">The value at the beginning of the range.</param>
    /// <param name="count">The positive number of consecutive values to include.</param>
    [CLSCompliant(false)]
    public CombinatorialRangeAttribute(uint from, uint count)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if ((ulong)from + count - 1 > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), FrameworkMessages.CombinatorialRangeExceedsUInt32);
        }

        if (count > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(count), FrameworkMessages.CombinatorialRangeTooManyValues);
        }

        int valueCount = (int)count;
        _values = new object[valueCount];
        for (int i = 0; i < valueCount; i++)
        {
            _values[i] = from + (uint)i;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialRangeAttribute"/> class.
    /// </summary>
    /// <param name="from">The value at the beginning of the range.</param>
    /// <param name="to">The inclusive value at the end of the range.</param>
    /// <param name="step">The positive amount by which each value changes.</param>
    [CLSCompliant(false)]
    public CombinatorialRangeAttribute(uint from, uint to, uint step)
    {
        if (step == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        bool ascending = from < to;
        ulong difference = ascending ? (ulong)to - from : (ulong)from - to;
        ulong count = (difference / step) + 1;
        if (count > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(to), FrameworkMessages.CombinatorialRangeTooManyValues);
        }

        _values = new object[(int)count];
        for (int i = 0; i < _values.Length; i++)
        {
            ulong offset = (ulong)i * step;
            _values[i] = (uint)(ascending ? from + offset : from - offset);
        }
    }

    /// <inheritdoc />
    public object[] GetValues(ParameterInfo parameter) => _values;
}
