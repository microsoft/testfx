// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Attribute for data driven test where data can be specified in-line.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class DataTestMethodAttribute : TestMethodAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataTestMethodAttribute"/> class.
    /// </summary>
    public DataTestMethodAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataTestMethodAttribute"/> class.
    /// </summary>
    /// <param name="displayName">
    /// Display name for the test.
    /// </param>
    public DataTestMethodAttribute(string? displayName)
        : base(displayName)
    {
    }
}
