﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Test Owner.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OwnerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerAttribute"/> class.
    /// </summary>
    /// <param name="owner">
    /// The owner.
    /// </param>
    public OwnerAttribute(string? owner)
    {
        Owner = owner;
    }

    /// <summary>
    /// Gets the owner.
    /// </summary>
    public string? Owner { get; }
}
