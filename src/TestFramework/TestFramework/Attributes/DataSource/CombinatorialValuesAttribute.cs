// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Specifies the values for a parameter on a combinatorial test method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
[CLSCompliant(false)]
public sealed class CombinatorialValuesAttribute : Attribute, ICombinatorialValuesProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CombinatorialValuesAttribute"/> class.
    /// </summary>
    /// <param name="values">The values to pass to this parameter.</param>
    public CombinatorialValuesAttribute(params object?[]? values)
    {
        // A single null attribute argument is bound as a null params array. Treat it as one null value.
        Values = values ?? [null];
    }

    /// <summary>
    /// Gets the values that should be passed to this parameter.
    /// </summary>
    public object?[] Values { get; }

    /// <inheritdoc />
    public object?[] GetValues(ParameterInfo parameter) => Values;
}
