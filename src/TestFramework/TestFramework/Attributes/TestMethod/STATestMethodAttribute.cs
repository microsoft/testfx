// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The test class attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class STATestMethodAttribute : TestMethodAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="STATestMethodAttribute"/> class.
    /// </summary>
    public STATestMethodAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="STATestMethodAttribute"/> class.
    /// </summary>
    /// <param name="displayName">
    /// Display name for the test.
    /// </param>
    public STATestMethodAttribute(string? displayName)
        : base(displayName)
    {
    }
}
