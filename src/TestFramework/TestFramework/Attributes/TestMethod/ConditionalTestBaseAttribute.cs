// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to ignore a test class or a test method, based on a condition and using an optional message.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not cause derived classes to be ignored.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public abstract class ConditionalTestBaseAttribute : Attribute
{
    /// <summary>
    /// Gets the ignore message (in case <see cref="ShouldIgnore"/> returns <see langword="true"/>) indicating
    /// the reason for ignoring the test method or test class.
    /// </summary>
    public abstract string? ConditionalIgnoreMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should be ignored.
    /// </summary>
    public abstract bool ShouldIgnore { get; }
}
