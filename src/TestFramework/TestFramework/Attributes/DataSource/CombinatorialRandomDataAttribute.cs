// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Specifies randomly generated integer values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class CombinatorialRandomDataAttribute : Attribute, ICombinatorialValuesProvider
{
    /// <summary>
    /// Specifies that <see cref="Random"/> should choose its own seed.
    /// </summary>
    public const int NoSeed = 0;

    private readonly Lazy<object[]> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialRandomDataAttribute"/> class.
    /// </summary>
    public CombinatorialRandomDataAttribute()
        => _values = new(GenerateValues);

    /// <summary>
    /// Gets or sets the positive number of values to generate.
    /// </summary>
    public int Count { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum value, inclusive.
    /// </summary>
    public int Minimum { get; set; }

    /// <summary>
    /// Gets or sets the maximum value, inclusive.
    /// </summary>
    public int Maximum { get; set; } = int.MaxValue - 1;

    /// <summary>
    /// Gets or sets the seed used for random number generation.
    /// </summary>
    public int Seed { get; set; } = NoSeed;

    /// <summary>
    /// Gets the generated values.
    /// </summary>
    public object[] Values => _values.Value;

    /// <inheritdoc />
    public object[] GetValues(ParameterInfo parameter) => Values;

    private object[] GenerateValues()
    {
        if (Count < 1)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialRandomCountMustBePositive, nameof(Count)));
        }

        if (Minimum > Maximum)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialRandomMinimumExceedsMaximum, nameof(Minimum), nameof(Maximum)));
        }

        long maxPossibleValues = (long)Maximum - Minimum + 1;
        if (Count > maxPossibleValues)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.CombinatorialRandomCountExceedsRange,
                    nameof(Count),
                    nameof(Minimum),
                    nameof(Maximum)));
        }

        Random random = Seed != NoSeed ? new Random(Seed) : new Random();
        var selectedOffsets = new HashSet<long>();
        object[] values = new object[Count];
        byte[] randomBytes = new byte[sizeof(uint)];
        int index = 0;
        for (long maximumOffset = maxPossibleValues - Count; maximumOffset < maxPossibleValues; maximumOffset++)
        {
            long candidateOffset = NextInt64(random, maximumOffset + 1, randomBytes);
            long selectedOffset;
            if (selectedOffsets.Add(candidateOffset))
            {
                selectedOffset = candidateOffset;
            }
            else
            {
                selectedOffsets.Add(maximumOffset);
                selectedOffset = maximumOffset;
            }

            values[index++] = checked((int)(Minimum + selectedOffset));
        }

        return values;
    }

    private static long NextInt64(Random random, long maximumExclusive, byte[] randomBytes)
    {
        const ulong uintRange = (ulong)uint.MaxValue + 1;
        ulong range = (ulong)maximumExclusive;
        ulong rejectionLimit = uintRange - (uintRange % range);
        uint sample;
        do
        {
            random.NextBytes(randomBytes);
            sample = (uint)(randomBytes[0]
                | (randomBytes[1] << 8)
                | (randomBytes[2] << 16)
                | (randomBytes[3] << 24));
        }
        while (sample >= rejectionLimit);

        return (long)(sample % range);
    }
}
