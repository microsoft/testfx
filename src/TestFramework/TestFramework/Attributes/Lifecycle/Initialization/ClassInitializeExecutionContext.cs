// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides the information needed for executing class initialize.
/// This type is passed as a parameter to <see cref="ClassInitializeAttribute.ExecuteAsync(ClassInitializeExecutionContext)"/>.
/// </summary>
public readonly struct ClassInitializeExecutionContext
{
    internal ClassInitializeExecutionContext(Func<Task> classInitializeExecutorGetter)
        => ClassInitializeExecutorGetter = classInitializeExecutorGetter;

    /// <summary>
    /// Gets the <see cref="Func{Task}"/> that returns the <see cref="Task"/> that executes the ClassInitialize method.
    /// </summary>
    public Func<Task> ClassInitializeExecutorGetter { get; }
}
