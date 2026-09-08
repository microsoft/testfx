// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Provides values for a parameter on a combinatorial test method.
/// </summary>
public interface ICombinatorialValuesProvider
{
    /// <summary>
    /// Gets the values that should be passed to the parameter.
    /// </summary>
    /// <param name="parameter">The parameter to get values for.</param>
    /// <returns>An array of values.</returns>
    object?[] GetValues(ParameterInfo parameter);
}
