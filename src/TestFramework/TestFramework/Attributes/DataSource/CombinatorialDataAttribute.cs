// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Provides every possible combination of values for the parameters of a test method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CombinatorialDataAttribute : Attribute, ITestDataSource
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

        return CombinatorialDataGenerator.Generate(values);
    }

    /// <inheritdoc />
    public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
        => TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, data);
}
