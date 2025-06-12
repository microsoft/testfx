﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute doesn't currently provide any different functionality compared to <see cref="TestMethodAttribute"/>. It's only
/// present for backward compatibility. Using <see cref="TestMethodAttribute"/> is recommended, even for parameterized tests.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class DataTestMethodAttribute : TestMethodAttribute
{
    private protected override bool UseAsync => GetType() == typeof(DataTestMethodAttribute);

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
