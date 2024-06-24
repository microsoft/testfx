// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// CSS Iteration URI.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CssIterationAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CssIterationAttribute"/> class for CSS Iteration URI.
    /// </summary>
    /// <param name="cssIteration">The CSS Iteration URI.</param>
    public CssIterationAttribute(string? cssIteration)
    {
        CssIteration = cssIteration;
    }

    /// <summary>
    /// Gets the CSS Iteration URI.
    /// </summary>
    public string? CssIteration { get; }
}
