// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class TestDataSourceUtilities
{
    /// <summary>
    /// Recursively resolve collections of objects to a proper string representation.
    /// </summary>
    /// <param name="data">The method arguments.</param>
    /// <param name="testIdGenerationStrategy">The strategy for creating the test ID.</param>
    /// <returns>The humanized representation of the data.</returns>
    public static string? GetHumanizedArguments(object? data, TestIdGenerationStrategy testIdGenerationStrategy)
    {
        // To avoid breaking changes, we will return the string representation of the arguments if the testIdGenerationStrategy
        // is not set to FullyQualified. This is the logic that was present in the previous implementation.
        if (testIdGenerationStrategy != TestIdGenerationStrategy.FullyQualified)
        {
            return data?.ToString();
        }

        if (data is null)
        {
            return "null";
        }

        if (!data.GetType().IsArray)
        {
            return data switch
            {
                string s => $"\"{s}\"",
                char c => $"'{c}'",
                _ => data.ToString(),
            };
        }

        // We need to box the object here so that we can support value types
        IEnumerable<object> boxedObjectEnumerable = ((IEnumerable)data).Cast<object>();
        IEnumerable<string?> elementStrings = boxedObjectEnumerable.Select(x => GetHumanizedArguments(x, testIdGenerationStrategy));
        return $"[{string.Join(",", elementStrings)}]";
    }
}
