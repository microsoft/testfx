// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Priority attribute; used to specify the priority of a unit test.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PriorityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityAttribute"/> class.
    /// </summary>
    /// <param name="priority">
    /// The priority.
    /// </param>
    public PriorityAttribute(int priority)
    {
        Priority = priority;
    }

    /// <summary>
    /// Gets the priority.
    /// </summary>
    public int Priority { get; }
}
