// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Enumeration for inheritance behavior, that can be used with both the <see cref="ClassInitializeAttribute"/> class
/// and <see cref="ClassCleanupAttribute"/> class.
/// Defines the behavior of the ClassInitialize and ClassCleanup methods of base classes.
/// The type of the enumeration must match.
/// </summary>
public enum InheritanceBehavior
{
    /// <summary>
    /// None.
    /// </summary>
    None,

    /// <summary>
    /// Before each derived class.
    /// </summary>
    BeforeEachDerivedClass,
}
