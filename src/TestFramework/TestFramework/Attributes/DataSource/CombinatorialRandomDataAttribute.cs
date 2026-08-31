// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

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
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} must be positive.", nameof(Count)));
        }

        if (Minimum > Maximum)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} must not be greater than {1}.", nameof(Minimum), nameof(Maximum)));
        }

        int maxPossibleValues = Maximum - Minimum + 1;
        if (Count > maxPossibleValues)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} requests more unique random values than the range between {1} and {2} contains.",
                    nameof(Count),
                    nameof(Minimum),
                    nameof(Maximum)));
        }

        Random random = Seed != NoSeed ? new Random(Seed) : new Random();
        var collisionChecker = new HashSet<int>();
        object[] values = new object[Count];
        int collisionCount = 0;
        int index = 0;
        while (collisionChecker.Count < Count)
        {
            int value = random.Next(Minimum, Maximum + 1);
            if (collisionChecker.Add(value))
            {
                values[index++] = value;
            }
            else
            {
                collisionCount++;
            }

            if (collisionCount > collisionChecker.Count * 5 && collisionCount > 1000)
            {
                throw new InvalidOperationException("Too many collisions occurred while generating unique random values.");
            }
        }

        return values;
    }
}
