// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specification for when to run class cleanup methods.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
public class ClassCleanupExecutionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupExecutionAttribute"/> class.
    /// </summary>
    public ClassCleanupExecutionAttribute()
        : this(ClassCleanupBehavior.EndOfClass)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupExecutionAttribute"/> class.
    /// </summary>
    /// <param name="cleanupBehavior">
    /// Specifies the class clean-up behavior.
    /// To capture output of class clean-up method in logs
    /// <paramref name="cleanupBehavior"/> must be set to <see cref="ClassCleanupBehavior.EndOfClass"/>.
    /// </param>
    public ClassCleanupExecutionAttribute(ClassCleanupBehavior cleanupBehavior)
    {
        CleanupBehavior = cleanupBehavior;
    }

    /// <summary>
    /// Gets when to run class cleanup methods.
    /// </summary>
    public ClassCleanupBehavior CleanupBehavior { get; }
}
