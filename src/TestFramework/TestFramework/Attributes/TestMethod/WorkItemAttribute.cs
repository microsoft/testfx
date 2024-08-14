// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// WorkItem attribute; used to specify a work item associated with this test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class WorkItemAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkItemAttribute"/> class for the WorkItem Attribute.
    /// </summary>
    /// <param name="id">The Id to a work item.</param>
    public WorkItemAttribute(int id) => Id = id;

    /// <summary>
    /// Gets the Id to a work item associated.
    /// </summary>
    public int Id { get; }
}
