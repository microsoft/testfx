// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Description of the test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DescriptionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DescriptionAttribute"/> class to describe a test.
    /// </summary>
    /// <param name="description">The description.</param>
    public DescriptionAttribute(string? description)
    {
        Description = description;
    }

    /// <summary>
    /// Gets the description of a test.
    /// </summary>
    public string? Description { get; }
}
