// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// CSS Project Structure URI.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CssProjectStructureAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CssProjectStructureAttribute"/> class for CSS Project Structure URI.
    /// </summary>
    /// <param name="cssProjectStructure">The CSS Project Structure URI.</param>
    public CssProjectStructureAttribute(string? cssProjectStructure)
    {
        CssProjectStructure = cssProjectStructure;
    }

    /// <summary>
    /// Gets the CSS Project Structure URI.
    /// </summary>
    public string? CssProjectStructure { get; }
}
