// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// An enumeration used with <see cref="ConditionBaseAttribute"/> to control whether the condition is reversed.
/// </summary>
public enum ConditionMode
{
    /// <summary>
    /// Runs only when the condition is met (the default).
    /// </summary>
    Include,

    /// <summary>
    /// Ignores the test when the condition is met (i.e, reverse the condition).
    /// </summary>
    Exclude,
}
