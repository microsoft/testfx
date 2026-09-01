// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Provides every possible combination of values for the parameters of a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CombinatorialDataAttribute : Attribute, ITestDataSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialDataAttribute"/> class.
    /// </summary>
    public CombinatorialDataAttribute()
    {
    }

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
        for (int i = 0; i < parameters.Length; i++)
        {
            values[i] = CombinatorialValuesUtilities.GetValuesFor(parameters[i]).ToArray();
        }

        return GenerateRows(values);
    }

    /// <inheritdoc />
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data);

    private static IEnumerable<object?[]> GenerateRows(object?[][] values)
    {
        if (values.Any(static dimension => dimension.Length == 0))
        {
            yield break;
        }

        int[] indices = new int[values.Length];
        while (true)
        {
            object?[] row = new object?[values.Length];
            for (int i = 0; i < row.Length; i++)
            {
                row[i] = values[i][indices[i]];
            }

            yield return row;

            int dimension = indices.Length - 1;
            while (dimension >= 0 && ++indices[dimension] == values[dimension].Length)
            {
                indices[dimension] = 0;
                dimension--;
            }

            if (dimension < 0)
            {
                yield break;
            }
        }
    }
}
