// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The class cleanup attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ClassCleanupAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
    /// </summary>
    public ClassCleanupAttribute()
        : this(InheritanceBehavior.None, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
    /// </summary>
    /// <param name="inheritanceBehavior">
    /// Specifies the ClassCleanup Inheritance Behavior.
    /// </param>
    public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior)
        : this(inheritanceBehavior, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
    /// </summary>
    /// <param name="cleanupBehavior">
    /// Specifies the class clean-up behavior.
    /// To capture output of class clean-up method in logs
    /// <paramref name="cleanupBehavior"/> must be set to <see cref="ClassCleanupBehavior.EndOfClass"/>.
    /// </param>
    public ClassCleanupAttribute(ClassCleanupBehavior cleanupBehavior)
        : this(InheritanceBehavior.None, cleanupBehavior)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
    /// </summary>
    /// <param name="inheritanceBehavior">
    /// Specifies the ClassCleanup Inheritance Behavior.
    /// </param>
    /// <param name="cleanupBehavior">
    /// Specifies the class clean-up behavior.
    /// To capture output of class clean-up method in logs
    /// <paramref name="cleanupBehavior"/> must be set to <see cref="ClassCleanupBehavior.EndOfClass"/>.
    /// </param>
    public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior, ClassCleanupBehavior cleanupBehavior)
        : this(inheritanceBehavior, new ClassCleanupBehavior?(cleanupBehavior))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
    /// </summary>
    /// <param name="inheritanceBehavior">
    /// Specifies the ClassCleanup Inheritance Behavior.
    /// </param>
    /// <param name="cleanupBehavior">
    /// Specifies the class clean-up behavior.
    /// To capture output of class clean-up method in logs
    /// <paramref name="cleanupBehavior"/> must be set to <see cref="ClassCleanupBehavior.EndOfClass"/>.
    /// </param>
    private ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior, ClassCleanupBehavior? cleanupBehavior)
    {
        InheritanceBehavior = inheritanceBehavior;
        CleanupBehavior = cleanupBehavior;
    }

    /// <summary>
    /// Gets the Inheritance Behavior.
    /// </summary>
    public InheritanceBehavior InheritanceBehavior { get; }

    /// <summary>
    /// Gets when to run class cleanup methods.
    /// </summary>
    public ClassCleanupBehavior? CleanupBehavior { get; }
}
