// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to conditionally control whether a test class or a test method will run or be ignored, based on a condition and using an optional message.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public abstract class ConditionBaseAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionBaseAttribute"/> class.
    /// </summary>
    /// <param name="mode">The condition mode.</param>
    protected ConditionBaseAttribute(ConditionMode mode)
        => Mode = mode;

    /// <summary>
    /// Gets the condition mode.
    /// </summary>
    public ConditionMode Mode { get; }

    /// <summary>
    /// Gets or sets the ignore message (in case <see cref="ShouldRun"/> returns <see langword="false"/>) indicating
    /// the reason for ignoring the test method or test class.
    /// </summary>
    public virtual string? IgnoreMessage { get; set; }

    /// <summary>
    /// Gets the group name for this attribute. This is relevant when multiple
    /// attributes that inherit <see cref="ConditionBaseAttribute"/> are present.
    /// The ShouldRun values of attributes in the same group are "OR"ed together.
    /// While the value from different groups is "AND"ed together.
    /// In other words, a test will be ignored if any group has all its <see cref="ShouldRun"/> values as false.
    /// </summary>
    /// <remarks>
    /// Usually, you can use <see langword="nameof"/> to return the group name.
    /// </remarks>
    public abstract string GroupName { get; }

    /// <summary>
    /// Gets a value indicating whether the test method or test class should be ignored.
    /// </summary>
    public abstract bool ShouldRun { get; }
}
