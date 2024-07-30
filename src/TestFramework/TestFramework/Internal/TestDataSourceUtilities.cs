// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

internal static class TestDataSourceUtilities
{
    public static string? ComputeDefaultDisplayName(MethodInfo methodInfo, object?[]? data, string? testMethodDisplayName,
        TestIdGenerationStrategy testIdGenerationStrategy)
    {
        if (data is null)
        {
            return null;
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();

        // We want to force call to `data.AsEnumerable()` to ensure that objects are casted to strings (using ToString())
        // so that null do appear as "null". If you remove the call, and do string.Join(",", new object[] { null, "a" }),
        // you will get empty string while with the call you will get "null,a".
        IEnumerable<object?> displayData = parameters.Length == 1 && parameters[0].ParameterType == typeof(object[])
            ? [data.AsEnumerable()]
            : data.AsEnumerable();

        string methodDisplayName = testIdGenerationStrategy == TestIdGenerationStrategy.FullyQualified ? testMethodDisplayName ?? methodInfo.Name : methodInfo.Name;

        return string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.DataDrivenResultDisplayName,
            methodDisplayName,
            string.Join(",", displayData.Select(x => GetHumanizedArguments(x, testIdGenerationStrategy))));
    }

    /// <summary>
    /// Recursively resolve collections of objects to a proper string representation.
    /// </summary>
    /// <param name="data">The method arguments.</param>
    /// <param name="testIdGenerationStrategy">The strategy for creating the test ID.</param>
    /// <returns>The humanized representation of the data.</returns>
    private static string? GetHumanizedArguments(object? data, TestIdGenerationStrategy testIdGenerationStrategy)
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
