// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to ignore a test class or a test method, with an optional message.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not cause derived classes to be ignored.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class IgnoreAttribute : ConditionBaseAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreAttribute"/> class with an empty message.
    /// </summary>
    public IgnoreAttribute()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreAttribute"/> class.
    /// </summary>
    /// <param name="message">
    /// Message specifies reason for ignoring.
    /// </param>
    public IgnoreAttribute(string? message)
        : base(ConditionMode.Include)
        => IgnoreMessage = message;

    /// <inheritdoc />
    public override string? IgnoreMessage { get; set; }

    /// <inheritdoc />
    public override bool IsConditionMet => false;

    /// <inheritdoc />
    public override string GroupName => "Ignore";
}
