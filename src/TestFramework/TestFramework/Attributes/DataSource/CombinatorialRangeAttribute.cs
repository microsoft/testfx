// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies a range of values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class CombinatorialRangeAttribute : Attribute, ICombinatorialValuesProvider
{
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

        Values = new object[count];
        for (int i = 0; i < count; i++)
        {
            Values[i] = from + i;
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

        int count = ((to - from) / step) + 1;
        Values = new object[count];
        for (int i = 0; i < count; i++)
        {
            Values[i] = from + (i * step);
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

        Values = new object[count];
        for (uint i = 0; i < count; i++)
        {
            Values[i] = from + i;
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

        var values = new List<uint>();
        if (from < to)
        {
            for (uint i = from; i <= to; i += step)
            {
                values.Add(i);
            }
        }
        else
        {
            for (uint i = from; i >= to && i <= from; i -= step)
            {
                values.Add(i);
            }
        }

        Values = values.Cast<object>().ToArray();
    }

    /// <summary>
    /// Gets the values that should be passed to this parameter.
    /// </summary>
    public object[] Values { get; }

    /// <inheritdoc />
    public object[] GetValues(ParameterInfo parameter) => Values;
}
