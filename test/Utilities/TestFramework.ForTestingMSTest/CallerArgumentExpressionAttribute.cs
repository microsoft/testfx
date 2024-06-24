// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET5_0 && !NET6_0
namespace System.Runtime.CompilerServices;

/// <summary>
/// Allows capturing of the expressions passed to a method.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class CallerArgumentExpressionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.
    /// </summary>
    /// <param name="parameterName">The name of the targeted parameter.</param>
    public CallerArgumentExpressionAttribute(string parameterName) => ParameterName = parameterName;

    /// <summary>
    /// Gets the target parameter name of the <c>CallerArgumentExpression</c>.
    /// </summary>
    /// <returns>
    /// The name of the targeted parameter of the <c>CallerArgumentExpression</c>.
    /// </returns>
    public string ParameterName { get; }
}
#endif
