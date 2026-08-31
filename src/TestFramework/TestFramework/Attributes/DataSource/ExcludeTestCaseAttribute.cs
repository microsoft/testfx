// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Suppresses generation of a specific test case from a combinatorial test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
[CLSCompliant(false)]
public class ExcludeTestCaseAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExcludeTestCaseAttribute"/> class.
    /// </summary>
    /// <param name="arguments">The values that match the test case to exclude.</param>
    public ExcludeTestCaseAttribute(params object?[]? arguments)
        => Arguments = arguments ?? [null];

    /// <summary>
    /// Gets the values that match the test case to exclude.
    /// </summary>
    public object?[] Arguments { get; }

    internal static ExcludeTestCaseAttribute[] GetExclusions(MethodInfo testMethod)
    {
        if (testMethod is null)
        {
            throw new ArgumentNullException(nameof(testMethod));
        }

        int parameterCount = testMethod.GetParameters().Length;
        ExcludeTestCaseAttribute[] exclusions = testMethod.GetCustomAttributes<ExcludeTestCaseAttribute>(true).ToArray();
        foreach (ExcludeTestCaseAttribute exclusion in exclusions)
        {
            if (exclusion.Arguments.Length != parameterCount)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialExcludeArgumentCountMismatch, nameof(ExcludeTestCaseAttribute)),
                    nameof(testMethod));
            }
        }

        return exclusions;
    }

    internal static CombinatorialIndexPredicate? CreateIndexMatcher(object?[][] candidateValues, ExcludeTestCaseAttribute[] exclusions)
    {
        if (candidateValues is null)
        {
            throw new ArgumentNullException(nameof(candidateValues));
        }

        if (exclusions is null)
        {
            throw new ArgumentNullException(nameof(exclusions));
        }

        if (exclusions.Length == 0)
        {
            return null;
        }

        var indexedExclusions = new List<IndexedExclusion>();
        foreach (ExcludeTestCaseAttribute exclusion in exclusions)
        {
            var indexedExclusion = IndexedExclusion.Create(candidateValues, exclusion);
            if (indexedExclusion is not null)
            {
                indexedExclusions.Add(indexedExclusion);
            }
        }

        CombinatorialIndexPredicate predicate = testCase =>
        {
            foreach (IndexedExclusion exclusion in indexedExclusions)
            {
                if (exclusion.Matches(testCase))
                {
                    return false;
                }
            }

            return true;
        };

        return indexedExclusions.Count == 0 ? null : predicate;
    }

    private static bool IsAny(object? argument) => argument is Type type && type == typeof(AnyDataValue);

    private sealed class IndexedExclusion
    {
        private readonly bool[]?[] _matchingValueIndices;

        private IndexedExclusion(bool[]?[] matchingValueIndices)
            => _matchingValueIndices = matchingValueIndices;

        internal static IndexedExclusion? Create(object?[][] candidateValues, ExcludeTestCaseAttribute exclusion)
        {
            if (candidateValues.Length != exclusion.Arguments.Length)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialArrayLengthMismatch, nameof(exclusion.Arguments)),
                    nameof(candidateValues));
            }

            bool[]?[] matchingValueIndices = new bool[]?[candidateValues.Length];
            for (int parameterIndex = 0; parameterIndex < candidateValues.Length; parameterIndex++)
            {
                object? excludedValue = exclusion.Arguments[parameterIndex];
                if (IsAny(excludedValue))
                {
                    continue;
                }

                bool[] matches = new bool[candidateValues[parameterIndex].Length];
                bool anyMatches = false;
                for (int valueIndex = 0; valueIndex < candidateValues[parameterIndex].Length; valueIndex++)
                {
                    if (EqualityComparer<object?>.Default.Equals(candidateValues[parameterIndex][valueIndex], excludedValue))
                    {
                        matches[valueIndex] = true;
                        anyMatches = true;
                    }
                }

                if (!anyMatches)
                {
                    return null;
                }

                matchingValueIndices[parameterIndex] = matches;
            }

            return new IndexedExclusion(matchingValueIndices);
        }

        internal bool Matches(int[] testCase)
        {
            if (_matchingValueIndices.Length != testCase.Length)
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialArrayLengthMismatch, nameof(_matchingValueIndices)),
                    nameof(testCase));
            }

            for (int parameterIndex = 0; parameterIndex < testCase.Length; parameterIndex++)
            {
                bool[]? matches = _matchingValueIndices[parameterIndex];
                if (matches is not null && !matches[testCase[parameterIndex]])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
