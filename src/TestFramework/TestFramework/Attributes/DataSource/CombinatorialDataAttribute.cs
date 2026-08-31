// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides every possible combination of values for the parameters of a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CombinatorialDataAttribute : Attribute, ITestDataSource
{
    /// <inheritdoc />
    public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
    {
        if (methodInfo is null)
        {
            throw new ArgumentNullException(nameof(methodInfo));
        }

        ParameterInfo[] parameters = methodInfo.GetParameters();
        if (parameters.Length == 0)
        {
            return [];
        }

        object?[][] values = new object?[parameters.Length][];
        var valueSources = new ICombinatorialValuesProvider?[parameters.Length];
        int[] dimensionSizes = new int[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            values[i] = CombinatorialValuesUtilities.GetValuesFor(parameters[i], out valueSources[i]).ToArray();
            dimensionSizes[i] = values[i].Length;
        }

        ExcludeTestCaseAttribute[] exclusions = ExcludeTestCaseAttribute.GetExclusions(methodInfo);
        CombinatorialIndexPredicate? isTestCaseAllowed = ExcludeTestCaseAttribute.CreateIndexMatcher(values, exclusions);
        int[][] testCases = CombinatorialTestCaseGenerator.GenerateCombinations(dimensionSizes, isTestCaseAllowed);
        return testCases.Select(indices =>
            indices.Select((valueIndex, parameterIndex) =>
                CombinatorialValuesUtilities.GetValueForTestCase(parameters[parameterIndex], valueSources[parameterIndex], values[parameterIndex], valueIndex))
                .ToArray());
    }

    /// <inheritdoc />
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data);
}
